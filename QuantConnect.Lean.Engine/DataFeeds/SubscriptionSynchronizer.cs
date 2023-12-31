﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Logging;
using QuantConnect.Securities;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Provides the ability to synchronize subscriptions into time slices
    /// </summary>
    public class SubscriptionSynchronizer : ISubscriptionSynchronizer, ITimeProvider
    {
        private readonly UniverseSelection _universeSelection;
        private TimeSliceFactory _timeSliceFactory;
        private ITimeProvider _timeProvider;
        private ManualTimeProvider _frontierTimeProvider;
        private readonly Dictionary<string, BaseData> _lastBaseDataDictionary = new();

        /// <summary>
        /// Event fired when a <see cref="Subscription"/> is finished
        /// </summary>
        public event EventHandler<Subscription> SubscriptionFinished;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionSynchronizer"/> class
        /// </summary>
        /// <param name="universeSelection">The universe selection instance used to handle universe
        /// selection subscription output</param>
        /// <returns>A time slice for the specified frontier time</returns>
        public SubscriptionSynchronizer(UniverseSelection universeSelection)
        {
            _universeSelection = universeSelection;
        }

        /// <summary>
        /// Sets the time provider. If already set will throw.
        /// </summary>
        /// <param name="timeProvider">The time provider, used to obtain the current frontier UTC value</param>
        public void SetTimeProvider(ITimeProvider timeProvider)
        {
            if (_timeProvider != null)
            {
                throw new Exception("SubscriptionSynchronizer.SetTimeProvider(): can only be called once");
            }
            _timeProvider = timeProvider;
            _frontierTimeProvider = new ManualTimeProvider(_timeProvider.GetUtcNow());
        }

        /// <summary>
        /// Sets the <see cref="TimeSliceFactory"/> instance to use
        /// </summary>
        /// <param name="timeSliceFactory">Used to create the new <see cref="TimeSlice"/></param>
        public void SetTimeSliceFactory(TimeSliceFactory timeSliceFactory)
        {
            if (_timeSliceFactory != null)
            {
                throw new Exception("SubscriptionSynchronizer.SetTimeSliceFactory(): can only be called once");
            }
            _timeSliceFactory = timeSliceFactory;
        }

        /// <summary>
        /// 优化期权链检测次数，每天一次
        /// hetao
        /// </summary>
        private DateTime _lastCheckUniverseSelectionDate = DateTime.MinValue;

        /// <summary>
        /// Syncs the specified subscriptions. The frontier time used for synchronization is
        /// managed internally and dependent upon previous synchronization operations.
        /// </summary>
        /// <param name="subscriptions">The subscriptions to sync</param>
        public TimeSlice Sync(IEnumerable<Subscription> subscriptions)
        {
            var delayedSubscriptionFinished = false;
            var changes = SecurityChanges.None;
            var data = new List<DataFeedPacket>(512);
            // NOTE: Tight coupling in UniverseSelection.ApplyUniverseSelection
            var universeData = new Dictionary<Universe, BaseDataCollection>();
            var universeDataForTimeSliceCreate = new Dictionary<Universe, BaseDataCollection>();

            _frontierTimeProvider.SetCurrentTimeUtc(_timeProvider.GetUtcNow());
            var frontierUtc = _frontierTimeProvider.GetUtcNow();
            //Log.Trace(frontierUtc.ToString("MM/dd/yyyy hh:mm:ss.fff") + "---------------frontierUtc----Sync-------");
            SecurityChanges newChanges;
            do
            {
                newChanges = SecurityChanges.None;
                foreach (var subscription in subscriptions)
                {
                    if (subscription.endOfStream)
                    {
                        OnSubscriptionFinished(subscription);
                        continue;
                    }

                    // prime if needed
                    if (subscription.current == null)
                    {
                        if (!subscription.MoveNext())
                        {
                            OnSubscriptionFinished(subscription);
                            continue;
                        }
                    }

                    DataFeedPacket packet = null;

                    //此时获取不到数据，进行回填数据，以保证期权链完整
                    //writer：lh
                    if (subscription.current == null)
                    {
                        var security = (Security)subscription.Security;
                        if (security.exchange.ExchangeOpen)
                        {
                            if (_lastBaseDataDictionary.TryGetValue(
                                subscription.Security.Symbol.ID.ToString(), out var lastBaseData))
                            {
                                // for performance, lets be selfish about creating a new instance
                                packet = new DataFeedPacket(
                                    subscription.Security,
                                    subscription.Configuration,
                                    subscription.RemovedFromUniverse
                                );
                                lastBaseData.Time = frontierUtc.ConvertFromUtc(security.Exchange.TimeZone);
                                lastBaseData.IsFillForward = true;
                                packet.Add(lastBaseData);
                            }
                        }
                    }

                    while (subscription.current != null && subscription.current.emitTimeUtc <= frontierUtc)
                    {
                        if (packet == null)
                        {
                            // for performance, lets be selfish about creating a new instance
                            packet = new DataFeedPacket(
                                subscription.Security,
                                subscription.Configuration,
                                subscription.RemovedFromUniverse
                            );
                        }
                        _lastBaseDataDictionary[subscription.current.data.symbol.id.ToString()] = subscription.current.data;
                        packet.Add(subscription.current.data);

                        if (!subscription.MoveNext())
                        {
                            delayedSubscriptionFinished = true;
                            break;
                        }
                    }

                    if (packet?.Count > 0)
                    {
                        // we have new universe data to select based on, store the subscription data until the end
                        if (!subscription.isUniverseSelectionSubscription)
                        {
                            data.Add(packet);
                        }
                        else
                        {
                            // assume that if the first item is a base data collection then the enumerator handled the aggregation,
                            // otherwise, load all the the data into a new collection instance
                            var packetBaseDataCollection = packet.Data[0] as BaseDataCollection;
                            var packetData = packetBaseDataCollection == null
                                ? packet.Data
                                : packetBaseDataCollection.Data;

                            if (universeData.TryGetValue(subscription.Universes.Single(), out var collection))
                            {
                                collection.Data.AddRange(packetData);
                            }
                            else
                            {
                                if (packetBaseDataCollection is OptionChainUniverseDataCollection optionChain)
                                {
                                    collection = new OptionChainUniverseDataCollection(
                                        frontierUtc, 
                                        subscription.Configuration.Symbol, 
                                        packetData, 
                                        optionChain.Underlying);
                                }
                                else if (packetBaseDataCollection is FuturesChainUniverseDataCollection)
                                {
                                    collection = new FuturesChainUniverseDataCollection(
                                        frontierUtc, 
                                        subscription.Configuration.Symbol, 
                                        packetData);
                                }
                                else
                                {
                                    collection = new BaseDataCollection(
                                        frontierUtc, subscription.Configuration.Symbol, packetData);
                                }

                                universeData[subscription.Universes.Single()] = collection;
                            }
                        }
                    }

                    if (subscription.isUniverseSelectionSubscription
                        && subscription.Universes.Single().DisposeRequested
                        || delayedSubscriptionFinished)
                    {
                        delayedSubscriptionFinished = false;
                        // we need to do this after all usages of subscription.Universes
                        OnSubscriptionFinished(subscription);
                    }
                }

                foreach (var (universe, baseDataCollection) in universeData)
                {
                    universeDataForTimeSliceCreate[universe] = baseDataCollection;

                    if (_universeSelection.Algorithm.LiveMode)
                    {
                        newChanges += _universeSelection.ApplyUniverseSelection(
                            universe,
                            frontierUtc,
                            baseDataCollection
                        );
                    }
                    else
                    {
                        ////优化期权链过滤次数,每天一次 hetao
                        if (_lastCheckUniverseSelectionDate != frontierUtc.Date)
                        {
                            newChanges += _universeSelection.ApplyUniverseSelection(
                                universe,
                                frontierUtc,
                                baseDataCollection
                            );
                        }
                    }
                }
                universeData.Clear();

                changes += newChanges;
            }
            while (newChanges != SecurityChanges.None
                || _universeSelection.AddPendingCurrencyDataFeeds(frontierUtc));


            var timeSlice = _timeSliceFactory.Create(frontierUtc, data, changes, universeDataForTimeSliceCreate);

            //优化期权链过滤次数
            //hetao
            if (timeSlice.Slice.OptionChains.Any())
            {
                _lastCheckUniverseSelectionDate = frontierUtc.Date;
            }

            return timeSlice;
        }

        /// <summary>
        /// Event invocator for the <see cref="SubscriptionFinished"/> event
        /// </summary>
        protected virtual void OnSubscriptionFinished(Subscription subscription)
        {
            var handler = SubscriptionFinished;
            if (handler != null) handler(this, subscription);
        }

        /// <summary>
        /// Returns the current UTC frontier time
        /// </summary>
        public DateTime GetUtcNow()
        {
            return _frontierTimeProvider.GetUtcNow();
        }
    }
}