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
using System.Net;
using QuantConnect.Interfaces;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using QuantConnect.Configuration;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// An implementation of <see cref="IFutureChainProvider"/> that fetches the list of contracts
    /// from the Options Clearing Corporation (OCC) website
    /// </summary>
    public class LiveDeribitFutureChainProvider : IFutureChainProvider
    {
        /// <summary>
        /// Static constructor for the <see cref="LiveOptionChainProvider"/> class
        /// </summary>

        /// <summary>
        /// Gets the list of option contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The underlying symbol</param>
        /// <param name="date">The date for which to request the option chain (only used in backtesting)</param>
        /// <returns>The list of option contracts</returns>
        public IEnumerable<Symbol> GetFutureContractList(Symbol symbol, DateTime date)
        {
            if (symbol.SecurityType != SecurityType.Crypto)
            {
                throw new NotSupportedException($"LiveFutureChainProvider.GetFutureContractList(): SecurityType.Crypto is expected but was {symbol.SecurityType}");
            }

            return FindFutureContracts(symbol.Value);
        }

        /// <summary>
        /// Retrieve the list of option contracts for an underlying symbol from the OCC website
        /// </summary>
        private static IEnumerable<Symbol> FindFutureContracts(string underlyingSymbol)
        {
            var underlying = underlyingSymbol.ToUpper().Contains("BTC") ? "BTC" : "ETH";
            var symbols = new List<Symbol>();

            using (var client = new WebClient())
            {
                //连续合约
                var ss = GetContracts(client, underlying, "false");
                symbols.AddRange(ss);
            }
            return symbols;
        }

        private static IEnumerable<Symbol> GetContracts(WebClient client, string underlyingSymbol, string expired)
        {
            var symbols = new List<Symbol>();
            var env = Config.Get("deribit-rest", "");
            string url = "";
            if (env.Contains("test"))
            {
                url = "https://test.deribit.com/api/v2/public/get_instruments?currency=" + underlyingSymbol + "&expired=" + expired + "&kind=future";
            }
            else
            {
                url = "https://www.deribit.com/api/v2/public/get_instruments?currency=" + underlyingSymbol + "&expired=" + expired + "&kind=future";
            }
            
            // get result of option infomation 
            var result = JObject.Parse(client.DownloadString(url));
            var futureList = (JArray)result["result"];
            // parse the lines, creating the Lean option symbols
            foreach (JObject option in futureList)
            {

                var expiryDate = Time.UnixTimeStampToDateTime(((double)option["expiration_timestamp"]) / 1000);
                var alias = (string)option["instrument_name"];
                if(expired =="false" && alias.Contains("PERPETUAL"))
                {
                    expiryDate = DateTime.Now.AddYears(1);
                }
                var future = Symbol.CreateFuture(alias, Market.Deribit, expiryDate, alias);
                symbols.Add(future);
            }

            return symbols;
        }
    }
}
