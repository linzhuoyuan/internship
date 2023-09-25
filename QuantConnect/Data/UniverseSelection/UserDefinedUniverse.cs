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
*/

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using QuantConnect.Interfaces;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Data.UniverseSelection
{
    /// <summary>
    /// Represents the universe defined by the user's algorithm. This is
    /// the default universe where manually added securities live by
    /// market/security type. They can also be manually generated and
    /// can be configured to fire on certain interval and will always
    /// return the internal list of symbols.
    /// </summary>
    public class UserDefinedUniverse : Universe, INotifyCollectionChanged, ITimeTriggeredUniverse
    {
        private readonly TimeSpan _interval;
        private readonly HashSet<SubscriptionDataConfig> _subscriptionDataConfigs = new();
        private readonly HashSet<Symbol> _symbols = new();
        // `UniverseSelection.RemoveSecurityFromUniverse()` will query us at `GetSubscriptionRequests()` to get the `SubscriptionDataConfig` and remove it from the DF
        // and we need to return the config even after the call to `Remove()`
        private readonly HashSet<SubscriptionDataConfig> _pendingRemovedConfigs = new();
        private readonly UniverseSettings _universeSettings;
        private readonly Func<DateTime, IEnumerable<Symbol>> _selector;

        /// <summary>
        /// Event fired when a symbol is added or removed from this universe
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Gets the interval of this user defined universe
        /// </summary>
        public TimeSpan Interval
        {
            get { return _interval; }
        }

        /// <summary>
        /// Gets the settings used for subscriptons added for this universe
        /// </summary>
        public override UniverseSettings UniverseSettings
        {
            get { return _universeSettings; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserDefinedUniverse"/> class
        /// </summary>
        /// <param name="configuration">The configuration used to resolve the data for universe selection</param>
        /// <param name="universeSettings">The settings used for new subscriptions generated by this universe</param>
        /// <param name="securityInitializer">Initializes securities when they're added to the universe</param>
        /// <param name="interval">The interval at which selection should be performed</param>
        /// <param name="symbols">The initial set of symbols in this universe</param>
        [Obsolete("This constructor is obsolete because SecurityInitializer is obsolete and will not be used.")]
        public UserDefinedUniverse(SubscriptionDataConfig configuration, UniverseSettings universeSettings, ISecurityInitializer securityInitializer, TimeSpan interval, IEnumerable<Symbol> symbols)
            : base(configuration, securityInitializer)
        {
            _interval = interval;
            _symbols = symbols.ToHashSet();
            _universeSettings = universeSettings;
            _selector = time => _subscriptionDataConfigs.Select(x => x.Symbol).Union(_symbols);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserDefinedUniverse"/> class
        /// </summary>
        /// <param name="configuration">The configuration used to resolve the data for universe selection</param>
        /// <param name="universeSettings">The settings used for new subscriptions generated by this universe</param>
        /// <param name="securityInitializer">Initializes securities when they're added to the universe</param>
        /// <param name="interval">The interval at which selection should be performed</param>
        /// <param name="selector">Universe selection function invoked for each time returned via GetTriggerTimes.
        /// The function parameter is a DateTime in the time zone of configuration.ExchangeTimeZone</param>
        [Obsolete("This constructor is obsolete because SecurityInitializer is obsolete and will not be used.")]
        public UserDefinedUniverse(SubscriptionDataConfig configuration, UniverseSettings universeSettings, ISecurityInitializer securityInitializer, TimeSpan interval, Func<DateTime,IEnumerable<string>> selector)
            : base(configuration, securityInitializer)
        {
            _interval = interval;
            _universeSettings = universeSettings;
            _selector = time =>
            {
                var selectSymbolsResult = selector(time.ConvertFromUtc(Configuration.ExchangeTimeZone));
                // if we received an unchaged then short circuit the symbol creation and return it directly
                if (ReferenceEquals(selectSymbolsResult, Unchanged)) return Unchanged;
                return selectSymbolsResult.Select(sym => Symbol.Create(sym, Configuration.SecurityType, Configuration.Market));
            };
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="UserDefinedUniverse"/> class
        /// </summary>
        /// <param name="configuration">The configuration used to resolve the data for universe selection</param>
        /// <param name="universeSettings">The settings used for new subscriptions generated by this universe</param>
        /// <param name="interval">The interval at which selection should be performed</param>
        /// <param name="symbols">The initial set of symbols in this universe</param>
        public UserDefinedUniverse(SubscriptionDataConfig configuration, UniverseSettings universeSettings, TimeSpan interval, IEnumerable<Symbol> symbols)
            : base(configuration)
        {
            _interval = interval;
            _symbols = symbols.ToHashSet();
            _universeSettings = universeSettings;
            // the selector Func will be the union of the provided symbols and the added symbols or subscriptions data configurations
            _selector = time => _subscriptionDataConfigs.Select(x => x.Symbol).Union(_symbols);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserDefinedUniverse"/> class
        /// </summary>
        /// <param name="configuration">The configuration used to resolve the data for universe selection</param>
        /// <param name="universeSettings">The settings used for new subscriptions generated by this universe</param>
        /// <param name="interval">The interval at which selection should be performed</param>
        /// <param name="selector">Universe selection function invoked for each time returned via GetTriggerTimes.
        /// The function parameter is a DateTime in the time zone of configuration.ExchangeTimeZone</param>
        public UserDefinedUniverse(SubscriptionDataConfig configuration, UniverseSettings universeSettings, TimeSpan interval, Func<DateTime, IEnumerable<string>> selector)
            : base(configuration)
        {
            _interval = interval;
            _universeSettings = universeSettings;
            _selector = time =>
            {
                var selectSymbolsResult = selector(time.ConvertFromUtc(Configuration.ExchangeTimeZone));
                // if we received an unchaged then short circuit the symbol creation and return it directly
                if (ReferenceEquals(selectSymbolsResult, Unchanged)) return Unchanged;
                return selectSymbolsResult.Select(sym => Symbol.Create(sym, Configuration.SecurityType, Configuration.Market));
            };
        }

        /// <summary>
        /// Creates a user defined universe symbol
        /// </summary>
        /// <param name="securityType">The security</param>
        /// <param name="market">The market</param>
        /// <returns>A symbol for user defined universe of the specified security type and market</returns>
        public static Symbol CreateSymbol(SecurityType securityType, string market)
        {
            var ticker = string.Format("qc-universe-userdefined-{0}-{1}", market.ToLower(), securityType);
            SecurityIdentifier sid;
            switch (securityType)
            {
                case SecurityType.Base:
                    sid = SecurityIdentifier.GenerateBase(ticker, market);
                    break;

                case SecurityType.Equity:
                    sid = SecurityIdentifier.GenerateEquity(SecurityIdentifier.DefaultDate, ticker, market);
                    break;

                case SecurityType.Option:
                    var underlying = SecurityIdentifier.GenerateEquity(SecurityIdentifier.DefaultDate, ticker, market);
                    sid = SecurityIdentifier.GenerateOption(SecurityIdentifier.DefaultDate, underlying, market, 0, 0, 0);
                    break;

                case SecurityType.Forex:
                    sid = SecurityIdentifier.GenerateForex(ticker, market);
                    break;

                case SecurityType.Cfd:
                    sid = SecurityIdentifier.GenerateCfd(ticker, market);
                    break;

                case SecurityType.Future:
                    sid = SecurityIdentifier.GenerateFuture(SecurityIdentifier.DefaultDate, ticker, market);
                    break;

                case SecurityType.Crypto:
                    sid = SecurityIdentifier.GenerateCrypto(ticker, market);
                    break;

                case SecurityType.Commodity:
                default:
                    throw new NotImplementedException("The specified security type is not implemented yet: " + securityType);
            }

            return new Symbol(sid, ticker);
        }

        /// <summary>
        /// Adds the specified <see cref="Symbol"/> to this universe
        /// </summary>
        /// <param name="symbol">The symbol to be added to this universe</param>
        /// <returns>True if the symbol was added, false if it was already present</returns>
        public bool Add(Symbol symbol)
        {
            if (_symbols.Add(symbol))
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, symbol));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds the specified <see cref="SubscriptionDataConfig"/> to this universe
        /// </summary>
        /// <param name="subscriptionDataConfig">The subscription data configuration to be added to this universe</param>
        /// <returns>True if the subscriptionDataConfig was added, false if it was already present</returns>
        public bool Add(SubscriptionDataConfig subscriptionDataConfig)
        {
            if (_subscriptionDataConfigs.Add(subscriptionDataConfig))
            {
                var tickType = AblSymbolDatabase.GetSecurityDataFeed(subscriptionDataConfig.symbol);
                if (subscriptionDataConfig.TickType == tickType)
                {
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, subscriptionDataConfig.Symbol));
                }                
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes the specified <see cref="Symbol"/> from this universe
        /// </summary>
        /// <param name="symbol">The symbol to be removed</param>
        /// <returns>True if the symbol was removed, false if the symbol was not present</returns>
        public bool Remove(Symbol symbol)
        {
            if (RemoveAndKeepTrack(symbol))
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, symbol));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the symbols defined by the user for this universe
        /// </summary>
        /// <param name="utcTime">The curren utc time</param>
        /// <param name="data">The symbols to remain in the universe</param>
        /// <returns>The data that passes the filter</returns>
        public override IEnumerable<Symbol> SelectSymbols(DateTime utcTime, BaseDataCollection data)
        {
            return _selector(utcTime);
        }

        /// <summary>
        /// Returns an enumerator that defines when this user defined universe will be invoked
        /// </summary>
        /// <returns>An enumerator of DateTime that defines when this universe will be invoked</returns>
        public virtual IEnumerable<DateTime> GetTriggerTimes(DateTime startTimeUtc, DateTime endTimeUtc, MarketHoursDatabase marketHoursDatabase)
        {
            var exchangeHours = marketHoursDatabase.GetExchangeHours(Configuration);
            var localStartTime = startTimeUtc.ConvertFromUtc(exchangeHours.TimeZone);
            var localEndTime = endTimeUtc.ConvertFromUtc(exchangeHours.TimeZone);

            var first = true;
            foreach (var dateTime in LinqExtensions.Range(localStartTime, localEndTime, dt => dt + Interval))
            {
                if (first)
                {
                    yield return dateTime;
                    first = false;
                }
                else if (exchangeHours.IsOpen(dateTime, dateTime + Interval, Configuration.ExtendedMarketHours))
                {
                    yield return dateTime;
                }
            }
        }

        /// <summary>
        /// Event invocator for the <see cref="CollectionChanged"/> event
        /// </summary>
        /// <param name="e">The notify collection changed event arguments</param>
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            var handler = CollectionChanged;
            if (handler != null) handler(this, e);
        }

        /// <summary>
        /// Gets the subscription requests to be added for the specified security
        /// </summary>
        /// <param name="security">The security to get subscriptions for</param>
        /// <param name="currentTimeUtc">The current time in utc. This is the frontier time of the algorithm</param>
        /// <param name="maximumEndTimeUtc">The max end time</param>
        /// <param name="subscriptionService">Instance which implements <see cref="ISubscriptionDataConfigService"/> interface</param>
        /// <returns>All subscriptions required by this security</returns>
        public override IEnumerable<SubscriptionRequest> GetSubscriptionRequests(Security security,
            DateTime currentTimeUtc,
            DateTime maximumEndTimeUtc,
            ISubscriptionDataConfigService subscriptionService)
        {
            var result = _subscriptionDataConfigs.Where(x => x.Symbol == security.Symbol).ToList();
            if (!result.Any())
            {
                result = _pendingRemovedConfigs.Where(x => x.Symbol == security.Symbol).ToList();
                if (result.Any())
                {
                    _pendingRemovedConfigs.RemoveWhere(x => x.Symbol == security.Symbol);
                }
                else
                {
                    result = base.GetSubscriptionRequests(security, currentTimeUtc, maximumEndTimeUtc, subscriptionService).Select(x => x.Configuration).ToList();
                    // we create subscription data configs ourselves, add the configs
                    _subscriptionDataConfigs.UnionWith(result);
                }
            }
            return result.Select(config => new SubscriptionRequest(isUniverseSubscription: false,
                                                                   universe: this,
                                                                   security: security,
                                                                   configuration: config,
                                                                   startTimeUtc: currentTimeUtc,
                                                                   endTimeUtc: maximumEndTimeUtc));
        }

        /// <summary>
        /// Tries to remove the specified security from the universe.
        /// </summary>
        /// <param name="utcTime">The current utc time</param>
        /// <param name="security">The security to be removed</param>
        /// <returns>True if the security was successfully removed, false if
        /// we're not allowed to remove or if the security didn't exist</returns>
        internal override bool RemoveMember(DateTime utcTime, Security security)
        {
            if (base.RemoveMember(utcTime, security))
            {
                RemoveAndKeepTrack(security.Symbol);
                return true;
            }
            return false;
        }

        private bool RemoveAndKeepTrack(Symbol symbol)
        {
            var toBeRemoved = _subscriptionDataConfigs.Where(x => x.Symbol == symbol).ToList();
            var removedSymbol = _symbols.Remove(symbol);

            if (removedSymbol || toBeRemoved.Any())
            {
                _subscriptionDataConfigs.RemoveWhere(x => x.Symbol == symbol);
                _pendingRemovedConfigs.UnionWith(toBeRemoved);
                return true;
            }

            return false;
        }
    }
}
