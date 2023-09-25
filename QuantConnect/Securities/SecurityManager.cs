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
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using QuantConnect.Data;
using QuantConnect.Interfaces;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Enumerable security management class for grouping security objects into an array and providing any common properties.
    /// </summary>
    /// <remarks>Implements IDictionary for the index searching of securities by symbol</remarks>
    public class SecurityManager : IDictionary<Symbol, Security>, INotifyCollectionChanged
    {
        /// <summary>
        /// Event fired when a security is added or removed from this collection
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        private readonly ITimeKeeper _timeKeeper;

        //Internal dictionary implementation:
        /// <summary>
        /// 上市并且有过交易的
        /// </summary>
        private readonly ConcurrentDictView<Symbol, Security> _tradedAndListed;
        /// <summary>
        /// 上市的
        /// </summary>
        private readonly ConcurrentDictionary<Symbol, Security> _listed;
        /// <summary>
        /// 全部的
        /// </summary>
        private readonly ConcurrentDictionary<Symbol, Security> _all;

        private SecurityService _securityService;

        /// <summary>
        /// Gets the most recent time this manager was updated
        /// </summary>
        public DateTime UtcTime => _timeKeeper.UtcTime;

        /// <summary>
        /// Initialise the algorithm security manager with two empty dictionaries
        /// </summary>
        /// <param name="timeKeeper"></param>
        public SecurityManager(ITimeKeeper timeKeeper)
        {
            _timeKeeper = timeKeeper;
            _tradedAndListed = new ConcurrentDictView<Symbol, Security>();
            _listed = new ConcurrentDictionary<Symbol, Security>();
            _all = new ConcurrentDictionary<Symbol, Security>();
        }

        /// <summary>
        /// Add a new security with this symbol to the collection.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        /// <param name="symbol">symbol for security we're trading</param>
        /// <param name="security">security object</param>
        /// <param name="notifyChanged"></param>
        /// <seealso cref="Add(Security)"/>
        public void Add(Symbol symbol, Security security, bool notifyChanged)
        {
            if (_all.TryAdd(symbol, security))
            {
                _tradedAndListed.TryAdd(symbol, security);
                _listed.TryAdd(symbol, security);
                security.SetLocalTimeKeeper(_timeKeeper.GetLocalTimeKeeper(security.exchange.TimeZone));
                if (notifyChanged)
                {
                    OnCollectionChanged(
                        new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, security));
                }
            }
        }

        public void Add(Symbol symbol, Security security)
        {
            Add(symbol, security, true);
        }

        /// <summary>
        /// Add a new security with this symbol to the collection.
        /// </summary>
        /// <param name="security">security object</param>
        public void Add(Security security)
        {
            Add(security.Symbol, security);
        }

        /// <summary>
        /// Add a symbol-security by its key value pair.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        /// <param name="pair"></param>
        public void Add(KeyValuePair<Symbol, Security> pair)
        {
            Add(pair.Key, pair.Value);
        }

        /// <summary>
        /// Clear the securities array to delete all the portfolio and asset information.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        public void Clear()
        {
            _listed.Clear();
            _tradedAndListed.Clear();
            _all.Clear();
        }

        /// <summary>
        /// Check if this collection contains this key value pair.
        /// </summary>
        /// <param name="pair">Search key-value pair</param>
        /// <remarks>IDictionary implementation</remarks>
        /// <returns>Bool true if contains this key-value pair</returns>
        public bool Contains(KeyValuePair<Symbol, Security> pair)
        {
            return _tradedAndListed.Contains(pair) || _all.Contains(pair);
        }

        /// <summary>
        /// Check if this collection contains this symbol.
        /// </summary>
        /// <param name="symbol">Symbol we're checking for.</param>
        /// <remarks>IDictionary implementation</remarks>
        /// <returns>Bool true if contains this symbol pair</returns>
        public bool ContainsKey(Symbol symbol)
        {
            return _tradedAndListed.ContainsKey(symbol) || _all.ContainsKey(symbol);
        }

        /// <summary>
        /// Copy from the internal array to an external array.
        /// </summary>
        /// <param name="array">Array we're outputting to</param>
        /// <param name="number">Starting index of array</param>
        /// <remarks>IDictionary implementation</remarks>
        public void CopyTo(KeyValuePair<Symbol, Security>[] array, int number)
        {
            //((IDictionary<Symbol, Security>)_tradedAndListed).CopyTo(array, number);
            ((IDictionary<Symbol, Security>)_all).CopyTo(array, number);
        }

        /// <summary>
        /// Count of the number of securities in the collection.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        public int Count => _all.Skip(0).Count();

        /// <summary>
        /// Flag indicating if the internal array is read only.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        public bool IsReadOnly => false;

        /// <summary>
        /// Remove a key value of of symbol-securities from the collections.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        /// <param name="pair">Key Value pair of symbol-security to remove</param>
        /// <returns>Boolean true on success</returns>
        public bool Remove(KeyValuePair<Symbol, Security> pair)
        {
            return Remove(pair.Key);
        }

        public bool Remove(Symbol symbol, bool ignoreTraded)
        {
            if (_listed.TryRemove(symbol, out var security))
            {
                security.RemoveLocalTimeKeeper();
            }

            if (ignoreTraded)
            {
                if (_tradedAndListed.TryGetValue(symbol, out security))
                {
                    if (security.holdings.TotalSaleVolume > 0)
                    {
                        return false;
                    }
                }
            }

            if (_tradedAndListed.TryRemove(symbol, out _))
            {
                //OnCollectionChanged(
                //    new NotifyCollectionChangedEventArgs(
                //        NotifyCollectionChangedAction.Remove, security));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove this symbol security: Dictionary interface implementation.
        /// </summary>
        /// <param name="symbol">Symbol we're searching for</param>
        /// <returns>true success</returns>
        public bool Remove(Symbol symbol)
        {
            return Remove(symbol, false);
        }

        /// <summary>
        /// List of the symbol-keys in the collection of securities.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        public ICollection<Symbol> Keys => _all.Select(x => x.Key).ToList();

        /// <summary>
        /// Try and get this security object with matching symbol and return true on success.
        /// </summary>
        /// <param name="symbol">String search symbol</param>
        /// <param name="security">Output Security object</param>
        /// <remarks>IDictionary implementation</remarks>
        /// <returns>True on successfully locating the security object</returns>
        public bool TryGetValue(Symbol symbol, out Security security)
        {
            return _tradedAndListed.TryGetValue(symbol, out security) || _all.TryGetValue(symbol, out security);
        }

        /// <summary>
        /// Get a list of the security objects for this collection.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        public ICollection<Security> Values => _all.Select(x => x.Value).ToList();

        /// <summary>
        /// Get the enumerator for this security collection.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        /// <returns>Enumerable key value pair</returns>
        IEnumerator<KeyValuePair<Symbol, Security>> IEnumerable<KeyValuePair<Symbol, Security>>.GetEnumerator()
        {
            return _all.GetEnumerator();
        }

        /// <summary>
        /// Get the enumerator for this securities collection.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        /// <returns>Enumerator.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        IEnumerator IEnumerable.GetEnumerator()
        {
            return _all.GetEnumerator();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<KeyValuePair<Symbol, Security>> All()
        {
            return _all;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<KeyValuePair<Symbol, Security>> ListedAndTraded()
        {
            return _tradedAndListed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<KeyValuePair<Symbol, Security>> Listed()
        {
            return _listed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<Security> SecurityListed()
        {
            return _listed.Select(n => n.Value);
        }

        /// <summary>
        /// Indexer method for the security manager to access the securities objects by their symbol.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        /// <param name="symbol">Symbol object indexer</param>
        /// <returns>Security</returns>
        public Security this[Symbol symbol]
        {
            get
            {
                if (!_tradedAndListed.TryGetValue(symbol, out var security) && !_all.TryGetValue(symbol, out security))
                {
                    throw new Exception(
                        $"This asset symbol ({symbol}) was not found in your security list. Please add this security or check it exists before using it with 'Securities.ContainsKey(\"{SymbolCache.GetTicker(symbol)}\")'");
                }
                return security;
            }
            set
            {
                if (_tradedAndListed.TryGetValue(symbol, out var existing) && existing != value)
                {
                    throw new ArgumentException("Unable to over write existing Security: " + symbol.ToString());
                }

                // no security exists for the specified symbol key, add it now
                if (existing == null)
                {
                    Add(symbol, value);
                }
            }
        }

        /// <summary>
        /// Indexer method for the security manager to access the securities objects by their symbol.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        /// <param name="ticker">string ticker symbol indexer</param>
        /// <returns>Security</returns>
        public Security this[string ticker]
        {
            get
            {
                if (!SymbolCache.TryGetSymbol(ticker, out var symbol))
                {
                    throw new Exception(string.Format("This asset symbol ({0}) was not found in your security list. Please add this security or check it exists before using it with 'Securities.ContainsKey(\"{0}\")'", ticker));
                }
                return this[symbol];
            }
            set
            {
                if (!SymbolCache.TryGetSymbol(ticker, out var symbol))
                {
                    throw new Exception(string.Format("This asset symbol ({0}) was not found in your security list. Please add this security or check it exists before using it with 'Securities.ContainsKey(\"{0}\")'", ticker));
                }
                this[symbol] = value;
            }
        }

        /// <summary>
        /// Event invocation for the <see cref="CollectionChanged"/> event
        /// </summary>
        /// <param name="changedEventArgs">Event arguments for the <see cref="CollectionChanged"/> event</param>
        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs changedEventArgs)
        {
            var handler = CollectionChanged;
            handler?.Invoke(this, changedEventArgs);
        }

        /// <summary>
        /// Sets the Security Service to be used
        /// </summary>
        public void SetSecurityService(SecurityService securityService)
        {
            _securityService = securityService;
        }

        /// <summary>
        /// Creates a new security
        /// </summary>
        /// <remarks>Following the obsolete of Security.Subscriptions,
        /// both overloads will be merged removing <see cref="SubscriptionDataConfig"/> arguments</remarks>
        public Security CreateSecurity(
            Symbol symbol,
            List<SubscriptionDataConfig> subscriptionDataConfigList,
            decimal leverage = 0,
            bool addToSymbolCache = true)
        {
            return _securityService.CreateSecurity(
                symbol, subscriptionDataConfigList, leverage, addToSymbolCache);
        }


        /// <summary>
        /// Creates a new security
        /// </summary>
        /// <remarks>Following the obsolete of Security.Subscriptions,
        /// both overloads will be merged removing <see cref="SubscriptionDataConfig"/> arguments</remarks>
        public Security CreateSecurity(
            Symbol symbol,
            SubscriptionDataConfig subscriptionDataConfig,
            decimal leverage = 0,
            bool addToSymbolCache = true)
        {
            return _securityService.CreateSecurity(
                symbol, subscriptionDataConfig, leverage, addToSymbolCache);
        }

        /// <summary>
        /// Set live mode state of the algorithm
        /// </summary>
        /// <param name="isLiveMode">True, live mode is enabled</param>
        public void SetLiveMode(bool isLiveMode)
        {
            _securityService.SetLiveMode(isLiveMode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IOrderedEnumerable<KeyValuePair<Symbol, Security>> ItemOrderBy(
            Func<KeyValuePair<Symbol, Security>, string> keySelector)
        {
            return _all.ToArray().OrderBy(keySelector);
        }
    }
}
