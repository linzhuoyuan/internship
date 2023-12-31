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
using System.Diagnostics;
using System.Threading;
using NodaTime;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Lean.Engine.Results;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Implementation of the <see cref="ISynchronizer"/> interface which provides the mechanism to stream data to the algorithm
    /// </summary>
    public class Synchronizer : ISynchronizer, IDataFeedTimeProvider
    {
        protected DateTimeZone dateTimeZone;

        /// <summary>
        /// The algorithm instance
        /// </summary>
        protected IAlgorithm Algorithm;

        /// <summary>
        /// The subscription manager
        /// </summary>
        protected IDataFeedSubscriptionManager SubscriptionManager;

        /// <summary>
        /// The subscription synchronizer
        /// </summary>
        protected SubscriptionSynchronizer SubscriptionSynchronizer;

        /// <summary>
        /// The time slice factory
        /// </summary>
        protected TimeSliceFactory TimeSliceFactory;

        /// <summary>
        /// Continuous UTC time provider
        /// </summary>
        public ITimeProvider TimeProvider { get; protected set; }

        /// <summary>
        /// Time provider which returns current UTC frontier time
        /// </summary>
        public ITimeProvider FrontierTimeProvider => SubscriptionSynchronizer;

        /// <summary>
        /// For security matrix of option price add by pang
        /// </summary>
        public IResultHandler ResultHandler { get; set; }

        /// <summary>
        /// Initializes the instance of the Synchronizer class
        /// </summary>
        public virtual void Initialize(
            IAlgorithm algorithm,
            IDataFeedSubscriptionManager dataFeedSubscriptionManager)
        {
            SubscriptionManager = dataFeedSubscriptionManager;
            Algorithm = algorithm;
            SubscriptionSynchronizer = new SubscriptionSynchronizer(
                SubscriptionManager.UniverseSelection);
        }

        /// <summary>
        /// Returns an enumerable which provides the data to stream to the algorithm
        /// </summary>
        public virtual IEnumerable<TimeSlice> StreamData(CancellationToken cancellationToken)
        {
            PostInitialize();

            // GetTimeProvider() will call GetInitialFrontierTime() which
            // will consume added subscriptions so we need to do this after initialization
            TimeProvider = GetTimeProvider();
            SubscriptionSynchronizer.SetTimeProvider(TimeProvider);

            var previousEmitTime = DateTime.MaxValue;
            var sw = new Stopwatch();
            while (!cancellationToken.IsCancellationRequested)
            {
                sw.Restart();
                TimeSlice timeSlice;
                try
                {
                    timeSlice = SubscriptionSynchronizer.Sync(SubscriptionManager.DataFeedSubscriptions);
                }
                catch (Exception err)
                {
                    Log.Error(err);
                    // notify the algorithm about the error, so it can be reported to the user
                    Algorithm.RunTimeError = err;
                    Algorithm.Status = AlgorithmStatus.RuntimeError;
                    break;
                }

                // check for cancellation
                if (cancellationToken.IsCancellationRequested) break;

                // SubscriptionFrontierTimeProvider will return twice the same time if there are no more subscriptions or if Subscription.Current is null
                if (timeSlice.Time != previousEmitTime)
                {
                    previousEmitTime = timeSlice.Time;
                    sw.Stop();
                    timeSlice.Slice.CreateElapsed = sw.Elapsed;
                    yield return timeSlice;
                }
                else if (timeSlice.SecurityChanges == SecurityChanges.None)
                {
                    // there's no more data to pull off, we're done (frontier is max value and no security changes)
                    break;
                }
            }

            Log.Trace("Synchronizer.GetEnumerator(): Exited thread.");
        }

        /// <summary>
        /// Performs additional initialization steps after algorithm initialization
        /// </summary>
        protected virtual void PostInitialize()
        {
            SubscriptionSynchronizer.SubscriptionFinished += (sender, subscription) =>
            {
                SubscriptionManager.RemoveSubscription(subscription.Configuration);
                Log.Debug("Synchronizer.SubscriptionFinished(): Finished subscription:" +
                    $"{subscription.Configuration} at {FrontierTimeProvider.GetUtcNow()} UTC");
            };

            // this is set after the algorithm initializes
            dateTimeZone = Algorithm.TimeZone;
            TimeSliceFactory = new TimeSliceFactory(dateTimeZone);
            TimeSliceFactory.ResultHandler = ResultHandler;
            SubscriptionSynchronizer.SetTimeSliceFactory(TimeSliceFactory);
        }

        /// <summary>
        /// Gets the <see cref="ITimeProvider"/> to use. By default this will load the
        /// <see cref="RealTimeProvider"/> for live mode, else <see cref="SubscriptionFrontierTimeProvider"/>
        /// </summary>
        /// <returns>The <see cref="ITimeProvider"/> to use</returns>
        protected virtual ITimeProvider GetTimeProvider()
        {
            return new SubscriptionFrontierTimeProvider(GetInitialFrontierTime(), SubscriptionManager);
        }

        private DateTime GetInitialFrontierTime()
        {
            var frontier = DateTime.MaxValue;
            foreach (var subscription in SubscriptionManager.DataFeedSubscriptions)
            {
                var current = subscription.Current;
                if (current == null)
                {
                    continue;
                }

                // we need to initialize both the frontier time and the offset provider, in order to do
                // this we'll first convert the current.EndTime to UTC time, this will allow us to correctly
                // determine the offset in ticks using the OffsetProvider, we can then use this to recompute
                // the UTC time. This seems odd, but is necessary given Noda time's lenient mapping, the
                // OffsetProvider exists to give forward marching mapping

                // compute the initial frontier time
                if (current.EmitTimeUtc < frontier)
                {
                    frontier = current.EmitTimeUtc;
                }
            }

            if (frontier == DateTime.MaxValue)
            {
                frontier = Algorithm.StartDate.ConvertToUtc(dateTimeZone);
            }

            //Log.Trace($"testtheone:-->GetInitialFrontierTime frontier:{frontier.ToString("MM/dd/yyyy hh:mm:ss.fff tt")}");
            return frontier;
        }
    }
}
