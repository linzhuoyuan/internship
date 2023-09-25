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
using QuantConnect.Interfaces;
using QuantConnect.Logging;

namespace QuantConnect.Brokerages.Deribit
{
    /// <summary>
    /// Provides the mapping between Lean symbols and Deribit symbols.
    /// </summary>
    public class DeribitSymbolMapper : ISymbolMapper
    {
        /// The list of known Deribit currencies.
        /// </summary>
        private readonly IAlgorithm _algorithm;
        private static Dictionary<string,Symbol> _indexSymbol =new Dictionary<string, Symbol>();
        private Dictionary<string, Symbol> _btcContracts = null;
        private Dictionary<string, Symbol> _ethContracts = null;
        private Dictionary<string, Symbol> _futures = new Dictionary<string, Symbol>();

        private static readonly HashSet<string> KnownCurrencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "USD"
        };

        public IOptionChainProvider OptionChainProvider { get; set; }
        public IFutureChainProvider FutureChainProvider { get; set; }

        public DeribitSymbolMapper()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="algorithm"></param>
        public DeribitSymbolMapper(IAlgorithm algorithm)
        {
            _algorithm = algorithm;
            OptionChainProvider = _algorithm.OptionChainProvider;
            FutureChainProvider = _algorithm.FutureChainProvider;
        }

        /// <summary>
        /// Converts a Lean symbol instance to an Deribit symbol
        /// </summary>
        /// <param name="symbol">A Lean symbol instance</param>
        /// <returns>The Deribit symbol</returns>
        public string GetBrokerageSymbol(Symbol symbol)
        {
            if (symbol == null || string.IsNullOrWhiteSpace(symbol.Value) || symbol.Value =="?")
                throw new ArgumentException("Invalid symbol: " + (symbol == null ? "null" : symbol.ToString()));

            string brokerageSymbol;
            if (symbol.SecurityType == SecurityType.Crypto)
                brokerageSymbol = ConvertLeanSymbolToDeribitIndexSymbol(symbol.Value);
            else
            {
                brokerageSymbol = symbol.Value;
            }
            return brokerageSymbol;
        }

        /// <summary>
        /// Converts an Deribit symbol to a Lean symbol instance
        /// </summary>
        /// <param name="brokerageSymbol">The Deribit symbol</param>
        /// <param name="securityType">The security type</param>
        /// <param name="market">The market</param>
        /// <param name="expirationDate">Expiration date of the security(if applicable)</param>
        /// <param name="strike">The strike of the security (if applicable)</param>
        /// <param name="optionRight">The option right of the security (if applicable)</param>
        /// <returns>A new Lean Symbol instance</returns>
        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market, DateTime expirationDate = default(DateTime), decimal strike = 0, OptionRight optionRight = 0)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
                throw new ArgumentException($"Invalid Deribit symbol: {brokerageSymbol}");

            if (market != Market.Deribit)
                throw new ArgumentException($"Invalid market: {market}");

            if (securityType == SecurityType.Crypto)
                return GetIndexSymbol(brokerageSymbol);
            else if (securityType == SecurityType.Future)
                return GetFutureSymbol(brokerageSymbol);
            else if (securityType == SecurityType.Option)
                return GetOptionSymbol(brokerageSymbol);
            else
                throw new ArgumentException($"Invalid security type: {securityType}");
        }


        public Symbol GetIndexSymbol(string brokerageSymbol)
        {
            if (_indexSymbol.ContainsKey(brokerageSymbol))
            {
                return _indexSymbol[brokerageSymbol];
            }
            var symbol = Symbol.Create(ConvertDeribitIndexSymbolToLeanSymbol(brokerageSymbol), SecurityType.Crypto, Market.Deribit);
            _indexSymbol[brokerageSymbol] = symbol;
            return symbol;
        }
        public Symbol GetUnderlyingSymbol(String brokerageSymbol)
        {
            if (brokerageSymbol.StartsWith("BTC"))
            {
                return Symbol.Create("btcusd", SecurityType.Crypto, Market.Deribit);
            }
            else if (brokerageSymbol.StartsWith("ETH"))
            {
                return Symbol.Create("ethusd", SecurityType.Crypto, Market.Deribit);
            }
            else
            {
                throw new ArgumentException($"Invalid symbol: {brokerageSymbol}");
            }
        }

        /// <summary>
        /// Converts an Deribit symbol to a Lean symbol instance
        /// </summary>
        /// <param name="brokerageSymbol">The Deribit symbol</param>
        /// <returns>A new Lean Symbol instance</returns>
        public Symbol GetLeanSymbol(string brokerageSymbol)
        {
            var securityType = GetBrokerageSecurityType(brokerageSymbol);
            return GetLeanSymbol(brokerageSymbol, securityType, Market.Deribit);
        }

        /// <summary>
        /// Returns the security type for an Deribit symbol
        /// </summary>
        /// <param name="brokerageSymbol">The Deribit symbol</param>
        /// <returns>The security type</returns>
        public SecurityType GetBrokerageSecurityType(string brokerageSymbol)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
                throw new ArgumentException($"Invalid Deribit symbol: {brokerageSymbol}");
            if(brokerageSymbol.Length > 7)
            {
                if(brokerageSymbol.EndsWith("P") || brokerageSymbol.EndsWith("C"))
                {
                    return SecurityType.Option;
                }
                else
                {
                    return SecurityType.Future;
                }
            }
            else
            {
                return SecurityType.Crypto;
            }
        }

        /// <summary>
        /// Returns the security type for a Lean symbol
        /// </summary>
        /// <param name="leanSymbol">The Lean symbol</param>
        /// <returns>The security type</returns>
        public SecurityType GetLeanSecurityType(string leanSymbol)
        {
            if (string.IsNullOrWhiteSpace(leanSymbol))
                throw new ArgumentException($"Invalid Lean symbol: {leanSymbol}");
            return GetBrokerageSecurityType(ConvertLeanSymbolToDeribitIndexSymbol(leanSymbol));
        }

        /// <summary>
        /// Checks if the currency is supported by Deribit
        /// </summary>
        /// <returns>True if Deribit supports the currency</returns>
        public bool IsKnownFiatCurrency(string currency)
        {
            if (string.IsNullOrWhiteSpace(currency))
                return false;

            return KnownCurrencies.Contains(currency);
        }

        /// <summary>
        /// Converts an Deribit symbol to a Lean symbol string
        /// </summary>
        public static string ConvertDeribitIndexSymbolToLeanSymbol(string deribitSymbol)
        {
            if (string.IsNullOrWhiteSpace(deribitSymbol))
                throw new ArgumentException($"Invalid Deribit symbol: {deribitSymbol}");

            // return as it is due to Deribit has similar Symbol format
            return deribitSymbol.Replace("_","").ToUpper();
        }

        /// <summary>
        /// Converts a Lean symbol string to an Deribit symbol
        /// </summary>
        public static string ConvertLeanSymbolToDeribitIndexSymbol(string leanSymbol)
        {
            if (string.IsNullOrWhiteSpace(leanSymbol))
                throw new ArgumentException($"Invalid Lean symbol: {leanSymbol}");

            leanSymbol = leanSymbol.Insert(2, "-");
            return leanSymbol.ToUpper();
        }

        public Symbol GetOptionSymbol(string optionSymbol)
        {
            if (_btcContracts == null)
            {
                _btcContracts = new Dictionary<string, Symbol>();
                var btcSymbol = Symbol.Create("btcusd", SecurityType.Crypto, Market.Deribit);
                var contracts = OptionChainProvider.GetOptionContractList(btcSymbol, new DateTime());
                foreach (Symbol s in contracts)
                {
                    _btcContracts.Add(s.Value, s);
                }
            }
            if (_ethContracts == null)
            {
                _ethContracts = new Dictionary<string, Symbol>();
                var btcSymbol = Symbol.Create("ethusd", SecurityType.Crypto, Market.Deribit);
                var contracts = OptionChainProvider.GetOptionContractList(btcSymbol, new DateTime());
                foreach (Symbol s in contracts)
                {
                    _ethContracts.Add(s.Value, s);
                }
            }
            if (optionSymbol.StartsWith("BTC"))
            {
                if (!_btcContracts.ContainsKey(optionSymbol))
                {
                    var result = ParseSymbolFromName(optionSymbol);
                    if (result == null)
                    {
                        Log.Error($"{optionSymbol}在OptionList中不存在，不处理");
                    }

                    _btcContracts.Add(optionSymbol, result);
                    return result;
                }
                return (Symbol)_btcContracts[optionSymbol];
            }
            else if (optionSymbol.StartsWith("ETH"))
            {
                if (!_ethContracts.ContainsKey(optionSymbol))
                {
                    var result = ParseSymbolFromName(optionSymbol);
                    if (result == null)
                    {
                        Log.Error($"{optionSymbol}在OptionList中不存在，不处理");
                    }

                    _ethContracts.Add(optionSymbol, result);
                    return result;
                }
                return (Symbol)_ethContracts[optionSymbol];
            }
            else
            {
                Log.Error($"{optionSymbol}非BTC&ETH期权，不处理");
                return null;
            }
        }


        public Symbol GetFutureSymbol(string futureSymbol)
        {
            if (_futures.ContainsKey(futureSymbol))
            {
                return (Symbol)_futures[futureSymbol];
            }
            var futures = FutureChainProvider.GetFutureContractList(GetUnderlyingSymbol(futureSymbol), new DateTime());
            foreach (Symbol s in futures)
            {
                _futures.Add(s.Value, s);
            }

            if (!_futures.ContainsKey(futureSymbol))
            {
                var result = ParseSymbolFromName(futureSymbol);
                if (result == null)
                {
                    Log.Error($"{futureSymbol}在FutureList中不存在，不处理");
                }
                _futures.Add(futureSymbol, result);
                return result;
            }
            return (Symbol)_futures[futureSymbol];
        }


        public static Symbol ParseSymbolFromName(string name)
        {
            name = name.ToUpper();
            if (name.EndsWith("-C") || name.EndsWith("-P"))
            {
                return ParseOptionSymbolFromName(name);
            }
            else
            {
                if (name.Contains("-"))
                {
                    return ParseFutureSymbolFromName(name);
                }
                else
                {
                    return Symbol.Create(ConvertDeribitIndexSymbolToLeanSymbol(name), SecurityType.Crypto, Market.Deribit);
                }
            }

            return null;
        }

        public static Symbol ParseFutureSymbolFromName(string name)
        {
            //BTC-25SEP20 BTC-PERPETUAL
            var arr = name.Split('-');
            var underlying = arr[0].ToUpper();
            if (name.Contains("PERPETUAL"))
            {
                return Symbol.CreateFuture(name, Market.Deribit, DateTime.Now.AddYears(1), name);
            }
            else
            {
                var date = arr[1];
                var y = Convert.ToInt32("20" + date.Substring(date.Length - 2));
                var ms = date.Substring(date.Length - 2 - 3, 3);
                var m = _mons[ms];
                var d = Convert.ToInt32(date.Substring(0, date.Length - 2 - 3));
                var expiryDate = new DateTime(y, m, d);

                return Symbol.CreateFuture(name, Market.Deribit, expiryDate, name);
            }

            return null;
        }

        public static Symbol ParseOptionSymbolFromName(string name)
        {
            //BTC-31JUL20-9500-C
            var arr = name.Split('-');
            var underlying = arr[0].ToUpper() + "USD";
            var strike = Convert.ToDecimal(arr[2]);
            var alias = name;
            var date = arr[1];


            var y = Convert.ToInt32("20" + date.Substring(date.Length - 2));
            var ms = date.Substring(date.Length - 2 - 3, 3);
            var m = _mons[ms];
            var d = Convert.ToInt32(date.Substring(0, date.Length - 2 - 3));
            var expiryDate = new DateTime(y, m, d);

            if (name.EndsWith("-C"))
            {
                return Symbol.CreateOption(underlying, Market.Deribit, OptionStyle.European, OptionRight.Call, strike, expiryDate, alias);
            }

            if (name.EndsWith("-P"))
            {
                return Symbol.CreateOption(underlying, Market.Deribit, OptionStyle.European, OptionRight.Put, strike, expiryDate, alias);
            }

            return null;
        }

        static Dictionary<string, int> _mons = new Dictionary<string, int>()
        {
            {"JAN",1},
            {"FEB",2},
            {"MAR",3},
            {"APR",4},
            {"MAY",5},
            {"JUN",6},
            {"JUL",7},
            {"AUG",8},
            {"SEP",9},
            {"OCT",10},
            {"NOV",11},
            {"DEC",12}
        };

    }
}
