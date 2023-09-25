/*
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
using System.Linq;
using Newtonsoft.Json;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents a holding of a currency in cash.
    /// </summary>
    public class Cash
    {
        private readonly object _locker = new object();
        internal decimal conversionRate;
        internal bool isBaseCurrency;
        internal bool invertRealTimePrice;
        internal decimal amount;
        internal decimal frozenAmount;
        internal readonly string symbol;
        internal Security conversionRateSecurity;
        internal readonly string currencySymbol;

        /// <summary>
        /// Event fired when this instance is updated
        /// <see cref="AddAmount"/>, <see cref="SetAmount"/>, <see cref="Update"/>
        /// </summary>
        public event EventHandler<CashEventArgs> Updated;

        /// <summary>
        /// Gets the symbol of the security required to provide conversion rates.
        /// If this cash represents the account currency, then <see cref="QuantConnect.Symbol.Empty"/>
        /// is returned
        /// </summary>
        public Symbol SecuritySymbol => conversionRateSecurity?.symbol ?? QuantConnect.Symbol.Empty;

        /// <summary>
        /// Gets the security used to apply conversion rates.
        /// If this cash represents the account currency, then null is returned.
        /// </summary>
        [JsonIgnore]
        public Security ConversionRateSecurity
        {
            get => conversionRateSecurity;
            set => conversionRateSecurity = value;
        }

        /// <summary>
        /// Gets the symbol used to represent this cash
        /// </summary>
        public string Symbol => symbol;

        /// <summary>
        /// Gets or sets the amount of cash held
        /// </summary>
        public decimal Amount
        {
            get => amount;
            private set => amount = value;
        }


        /// <summary>
        /// Gets or sets the amount of cash frozen
        /// </summary>
        public decimal FrozenAmount
        {
            get => frozenAmount;
            private set => frozenAmount = value;
        }

        /// <summary>
        /// Gets the conversion rate into account currency
        /// </summary>
        public decimal ConversionRate
        {
            get
            {
                return conversionRate;
            }
            internal set
            {
                conversionRate = value;
                OnUpdate();
            }
        }

        /// <summary>
        /// The symbol of the currency, such as $
        /// </summary>
        public string CurrencySymbol => currencySymbol;

        /// <summary>
        /// Gets the value of this cash in the account currency
        /// </summary>
        public decimal ValueInAccountCurrency => amount * conversionRate;

        /// <summary>
        /// Gets the value of this cash in the account currency
        /// </summary>
        public decimal FrozenValueInAccountCurrency => frozenAmount * conversionRate;

        /// <summary>
        /// Initializes a new instance of the <see cref="Cash"/> class
        /// </summary>
        /// <param name="symbol">The symbol used to represent this cash</param>
        /// <param name="amount">The amount of this currency held</param>
        /// <param name="conversionRate">
        /// The initial conversion rate of this currency into the <see cref="AccountCurrency"/>
        /// </param>
        /// <param name="currencySymbol"></param>
        public Cash(string symbol, decimal amount, decimal conversionRate, string currencySymbol="")
        {
            if (string.IsNullOrEmpty(symbol))
            {
                throw new ArgumentException("Cash symbols cannot be null or empty.");
            }
            this.amount = amount;
            ConversionRate = conversionRate;
            this.symbol = symbol.LazyToUpper();
            this.currencySymbol = currencySymbol.IsNullOrEmpty()? Currencies.GetCurrencySymbol(Symbol): currencySymbol;
        }

        /// <summary>
        /// Updates this cash object with the specified data
        /// </summary>
        /// <param name="data">The new data for this cash object</param>
        public void Update(BaseData data)
        {
            if (isBaseCurrency) return;

            var rate = data.value;
            if (invertRealTimePrice)
            {
                rate = 1 / rate;
            }
            conversionRate = rate;
            OnUpdate();
        }

        /// <summary>
        /// Adds the specified amount of currency to this Cash instance and returns the new total.
        /// This operation is thread-safe
        /// </summary>
        /// <param name="amount">The amount of currency to be added</param>
        /// <returns>The amount of currency directly after the addition</returns>
        public decimal AddAmount(decimal amount)
        {
            lock (_locker)
            {
                this.amount += amount;
            }
            OnUpdate();
            return this.amount;
        }

        /// <summary>
        /// Sets the Quantity to the specified amount
        /// </summary>
        /// <param name="amount">The amount to set the quantity to</param>
        public void SetAmount(decimal amount)
        {
            lock (_locker)
            {
                this.amount = amount;
            }
            OnUpdate();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="frozenAmount"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public decimal AddFrozenAmount(decimal frozenAmount, decimal amount = 0)
        {
            lock (_locker)
            {
                this.amount += amount;
                this.frozenAmount += frozenAmount;
            }
            OnUpdate();
            return this.amount;
        }


        /// <summary>
        /// Sets the Quantity to the specified amount
        /// </summary>
        /// <param name="amount">The amount to set the quantity to</param>
        public void SetFrozenAmount(decimal amount)
        {
            lock (_locker)
            {
                frozenAmount = amount;
            }
            OnUpdate();
        }

        /// <summary>
        /// Ensures that we have a data feed to convert this currency into the base currency.
        /// This will add a <see cref="SubscriptionDataConfig"/> and create a <see cref="Security"/> at the lowest resolution if one is not found.
        /// </summary>
        /// <param name="securities">The security manager</param>
        /// <param name="subscriptions">The subscription manager used for searching and adding subscriptions</param>
        /// <param name="marketMap">The market map that decides which market the new security should be in</param>
        /// <param name="changes">Will be used to consume <see cref="SecurityChanges.AddedSecurities"/></param>
        /// <param name="securityService">Will be used to create required new <see cref="Security"/></param>
        /// <param name="accountCurrency"></param>
        /// <returns>Returns the added <see cref="SubscriptionDataConfig"/>, otherwise null</returns>
        public SubscriptionDataConfig EnsureCurrencyDataFeed(
            SecurityManager securities,
            SubscriptionManager subscriptions,
            IReadOnlyDictionary<SecurityType, string> marketMap,
            SecurityChanges changes,
            ISecurityService securityService,
            string accountCurrency)
        {
            // this gets called every time we add securities using universe selection,
            // so must of the time we've already resolved the value and don't need to again
            if (conversionRateSecurity != null)
            {
                return null;
            }

            if (symbol == accountCurrency)
            {
                conversionRateSecurity = null;
                isBaseCurrency = true;
                ConversionRate = 1.0m;
                return null;
            }

            // we require a security that converts this into the base currency
            var normal = symbol + accountCurrency;
            var invert = accountCurrency + Symbol;
            // TODO: should add in symbol convert and have uniformed symbol format
            var ftxNormal = symbol + '_' + accountCurrency;
            var securitiesToSearch = securities
                .Select(kvp => kvp.Value)
                .Concat(changes.AddedSecurities)
                .Where(s => s.Type is SecurityType.Forex or SecurityType.Cfd or SecurityType.Crypto);

            foreach (var security in securitiesToSearch)
            {
                if (security.symbol.value == normal || security.Symbol.Value == ftxNormal)
                {
                    conversionRateSecurity = security;
                    return null;
                }
                if (security.Symbol.Value == invert)
                {
                    conversionRateSecurity = security;
                    invertRealTimePrice = true;
                    return null;
                }
            }
            // if we've made it here we didn't find a security, so we'll need to add one

            // Create a SecurityType to Market mapping with the markets from SecurityManager members
            var markets = securities
                .Select(x => x.Key)
                .GroupBy(x => x.SecurityType)
                .ToDictionary(
                    x => x.Key,
                    y => y.First().id.Market);
            if (markets.ContainsKey(SecurityType.Cfd) && !markets.ContainsKey(SecurityType.Forex))
            {
                markets.Add(SecurityType.Forex, markets[SecurityType.Cfd]);
            }
            if (markets.ContainsKey(SecurityType.Forex) && !markets.ContainsKey(SecurityType.Cfd))
            {
                markets.Add(SecurityType.Cfd, markets[SecurityType.Forex]);
            }

            var potentials = Currencies.CurrencyPairs
                .Select(fx => CreateSymbol(marketMap, fx, markets, SecurityType.Forex))
                .Concat(Currencies.CfdCurrencyPairs.Select(cfd => CreateSymbol(marketMap, cfd, markets, SecurityType.Cfd)))
                .Concat(Currencies.CryptoCurrencyPairs.Select(crypto => CreateSymbol(marketMap, crypto, markets, SecurityType.Crypto)));


            var minimumResolution = subscriptions.Subscriptions.Select(x => x.Resolution).DefaultIfEmpty(Resolution.Minute).Min();

            foreach (var item in potentials)
            {
                if (item.value == normal || item.value == invert)
                {
                    invertRealTimePrice = item.value == invert;
                    var securityType = item.id.SecurityType;

                    // use the first subscription defined in the subscription manager
                    var (objectType, tickType) = subscriptions
                        .LookupSubscriptionConfigDataTypes(securityType, minimumResolution, false)
                        .First();

                    // set this as an internal feed so that the data doesn't get sent into the algorithm's OnData events
                    var config = subscriptions.SubscriptionDataConfigService.Add(
                        item,
                        minimumResolution,
                        fillForward: true,
                        extendedMarketHours: false,
                        isInternalFeed: true,
                        subscriptionDataTypes: new List<Tuple<Type, TickType>>
                        {
                            new(objectType, tickType)
                        })
                        .First();

                    var security = securityService.CreateSecurity(
                        item,
                        config,
                        addToSymbolCache: false);

                    conversionRateSecurity = security;
                    securities.Add(config.symbol, security);
                    Log.Trace("Cash.EnsureCurrencyDataFeed(): Adding " + item.value + " for cash " + symbol + " currency feed");
                    return config;
                }
            }

            //======================================================================================================================================
            var forexEntries = GetAvailableSymbolPropertiesDatabaseEntries(SecurityType.Forex, marketMap, markets);
            var cfdEntries = GetAvailableSymbolPropertiesDatabaseEntries(SecurityType.Cfd, marketMap, markets);
            var cryptoEntries = GetAvailableSymbolPropertiesDatabaseEntries(SecurityType.Crypto, marketMap, markets);

            var potentialEntries = forexEntries
                .Concat(cfdEntries)
                .Concat(cryptoEntries)
                .ToList();

            if (!potentialEntries.Any(x =>
                Symbol == x.Key.Symbol.Substring(0, x.Key.Symbol.Length - x.Value.QuoteCurrency.Length) ||
                Symbol == x.Value.QuoteCurrency))
            {
                // currency not found in any tradeable pair
                Log.Error($"No tradeable pair was found for currency {Symbol}, conversion rate to account currency ({accountCurrency}) will be set to zero.");
                ConversionRateSecurity = null;
                ConversionRate = 0m;
                return null;
            }

            potentials = potentialEntries
                .Select(x => QuantConnect.Symbol.Create(x.Key.Symbol, x.Key.SecurityType, x.Key.Market));

            minimumResolution = subscriptions.Subscriptions.Select(x => x.Resolution).DefaultIfEmpty(Resolution.Minute).Min();

            foreach (var item in potentials)
            {
                if (item.value == normal || item.value == invert)
                {
                    invertRealTimePrice = item.value == invert;
                    var securityType = item.id.SecurityType;

                    // use the first subscription defined in the subscription manager
                    var (objectType, tickType) = subscriptions
                        .LookupSubscriptionConfigDataTypes(securityType, minimumResolution, false)
                        .First();

                    // set this as an internal feed so that the data doesn't get sent into the algorithm's OnData events
                    var config = subscriptions.SubscriptionDataConfigService.Add(
                            item,
                            minimumResolution,
                            fillForward: true,
                            extendedMarketHours: false,
                            isInternalFeed: true,
                            subscriptionDataTypes: new List<Tuple<Type, TickType>>
                            {
                                new Tuple<Type, TickType>(objectType, tickType)
                            })
                        .First();

                    var security = securityService.CreateSecurity(
                        item,
                        config,
                        addToSymbolCache: false);

                    conversionRateSecurity = security;
                    securities.Add(config.symbol, security);
                    Log.Trace("Cash.EnsureCurrencyDataFeed(): Adding " + item.value + " for cash " + symbol + " currency feed");
                    return config;
                }
            }
            //==========================================================================================================================

            // if this still hasn't been set then it's an error condition
            throw new ArgumentException(string.Format("In order to maintain cash in {0} you are required to add a subscription for Forex pair {0}{1} or {1}{0}", symbol, accountCurrency));
        }

        /// <summary>
        /// Returns a <see cref="string"/> that represents the current <see cref="Cash"/>.
        /// </summary>
        /// <returns>A <see cref="string"/> that represents the current <see cref="Cash"/>.</returns>
        public override string ToString()
        {
            // round the conversion rate for output
            var rate = ConversionRate;
            rate = rate < 1000 ? rate.RoundToSignificantDigits(5) : Math.Round(rate, 4);
            return $"{Symbol}: {CurrencySymbol}{Amount,15:0.00} @ {rate,10:0.0000####} = ${Math.Round(ValueInAccountCurrency, 4)},{FrozenAmount,15:0.00} @ {rate,10:0.0000####} = ${Math.Round(FrozenValueInAccountCurrency, 4)}";
        }

        private static Symbol CreateSymbol(
            IReadOnlyDictionary<SecurityType, string> marketMap, 
            string crypto, 
            Dictionary<SecurityType, string> markets, 
            SecurityType securityType)
        {
            if (!markets.TryGetValue(securityType, out var market))
            {
                market = marketMap[securityType];
            }

            return QuantConnect.Symbol.Create(crypto, securityType, market);
        }

        private static IEnumerable<KeyValuePair<SecurityDatabaseKey, SymbolProperties>> GetAvailableSymbolPropertiesDatabaseEntries(
            SecurityType securityType,
            IReadOnlyDictionary<SecurityType, string> marketMap,
            IReadOnlyDictionary<SecurityType, string> markets
        )
        {
            var marketJoin = new HashSet<string>();
            {
                if (marketMap.TryGetValue(securityType, out var market))
                {
                    marketJoin.Add(market);
                }
                if (markets.TryGetValue(securityType, out market))
                {
                    marketJoin.Add(market);
                }
            }

            return marketJoin.SelectMany(market => SymbolPropertiesDatabase.FromDataFolder()
                .GetSymbolPropertiesList(market, securityType));
        }

        private void OnUpdate()
        {
            Updated?.Invoke(this, new CashEventArgs(Symbol));
        }
    }
}