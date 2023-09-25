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
    /// An implementation of <see cref="IOptionChainProvider"/> that fetches the list of contracts
    /// from the Options Clearing Corporation (OCC) website
    /// </summary>
    public class LiveDeribitOptionChainProvider : IOptionChainProvider
    {
        /// <summary>
        /// Static constructor for the <see cref="LiveOptionChainProvider"/> class
        /// </summary>
        static LiveDeribitOptionChainProvider()
        {
            // The OCC website now requires at least TLS 1.1 for API requests.
            // NET 4.5.2 and below does not enable these more secure protocols by default, so we add them in here
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        }

        /// <summary>
        /// Gets the list of option contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The underlying symbol</param>
        /// <param name="date">The date for which to request the option chain (only used in backtesting)</param>
        /// <returns>The list of option contracts</returns>
        public IEnumerable<Symbol> GetOptionContractList(Symbol symbol, DateTime date)
        {
            if (symbol.SecurityType != SecurityType.Crypto)
            {
                throw new NotSupportedException($"LiveOptionChainProvider.GetOptionContractList(): SecurityType.Crypto is expected but was {symbol.SecurityType}");
            }

            return FindOptionContracts(symbol.Value);
        }

        /// <summary>
        /// Retrieve the list of option contracts for an underlying symbol from the OCC website
        /// </summary>
        private static IEnumerable<Symbol> FindOptionContracts(string underlyingSymbol)
        {            
            var symbols = new List<Symbol>();

            using (var client = new WebClient())
            {
                // derbit restful api
                var env = Config.Get("deribit-rest", "");
                string url = "";
                if (env.Contains("test"))
                {
                    url = underlyingSymbol.ToUpper().Contains("BTC")
                        ? "https://test.deribit.com/api/v2/public/get_instruments?currency=BTC&expired=false&kind=option"
                        : "https://test.deribit.com/api/v2/public/get_instruments?currency=ETH&expired=false&kind=option";
                }
                else
                {
                    url = underlyingSymbol.ToUpper().Contains("BTC")
                        ? "https://www.deribit.com/api/v2/public/get_instruments?currency=BTC&expired=false&kind=option"
                        : "https://www.deribit.com/api/v2/public/get_instruments?currency=ETH&expired=false&kind=option";
                }

                // get result of option infomation 
                var result = JObject.Parse(client.DownloadString(url));
                var optionList = (JArray)result["result"];
                // parse the lines, creating the Lean option symbols
                foreach (JObject option in optionList)
                {

                    var expiryDate = Time.UnixTimeStampToDateTime(((double)option["expiration_timestamp"])/1000);
                    var strike = (decimal)option["strike"];
                    var alias = (string)option["instrument_name"];

                    if ((string)option["option_type"] == "call")
                    {
                        symbols.Add(Symbol.CreateOption(underlyingSymbol, Market.Deribit, OptionStyle.European, OptionRight.Call, strike, expiryDate, alias));
                    }

                    if ((string)option["option_type"] == "put")
                    {
                        symbols.Add(Symbol.CreateOption(underlyingSymbol, Market.Deribit, OptionStyle.European, OptionRight.Put, strike, expiryDate, alias));
                    }
                }
            }

            return symbols;
        }
    }
}
