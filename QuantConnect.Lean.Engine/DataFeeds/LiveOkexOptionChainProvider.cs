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
using System.Linq;
using System.Net;
using QuantConnect.Interfaces;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using QuantConnect.Configuration;
using RestSharp;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// An implementation of <see cref="IOptionChainProvider"/> that fetches the list of contracts
    /// from the Options Clearing Corporation (OCC) website
    /// </summary>
    public class LiveOkexOptionChainProvider : IOptionChainProvider
    {
        /// <summary>
        /// Static constructor for the <see cref="LiveOptionChainProvider"/> class
        /// </summary>
        static LiveOkexOptionChainProvider()
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

            return FindOptionContracts(symbol);
        }
        public static string HmacSHA256(string infoStr, string secret)
        {
            byte[] sha256Data = Encoding.UTF8.GetBytes(infoStr);
            byte[] secretData = Encoding.UTF8.GetBytes(secret);
            using (var hmacsha256 = new HMACSHA256(secretData))
            {
                byte[] buffer = hmacsha256.ComputeHash(sha256Data);
                return Convert.ToBase64String(buffer);
            }
        }

        /// <summary>
        /// Retrieve the list of option contracts for an underlying symbol from the OCC website
        /// </summary>
        private static IEnumerable<Symbol> FindOptionContracts(Symbol underlyingSymbol)
        {
            var symbols = new List<Symbol>();
            var restURL = Config.Get("okex-rest", "https://www.okex.com/");

            RestClient client = new RestClient(restURL);
            string method = "GET";
            var path = "api/option/v3/instruments/BTC-USD";
            Uri uri = new Uri($"{restURL}{path}");
            RestRequest request = new RestRequest(path, Method.Get);
            request.AddHeader("Accept", "application/json");
            request.AddHeader("OK-ACCESS-KEY", Config.Get("okex-api-key"));
            var timeAdjSeconds = Config.Get("okex-time-adj-second", "0");
            var now = DateTime.Now.AddSeconds(int.Parse(timeAdjSeconds));
            var timeStamp = TimeZoneInfo.ConvertTimeToUtc(now).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var requestUrl = uri.PathAndQuery;
            string sign = HmacSHA256($"{timeStamp}{method}{requestUrl}", Config.Get("okex-api-secret"));
            request.AddHeader("OK-ACCESS-SIGN", sign);
            request.AddHeader("OK-ACCESS-TIMESTAMP", timeStamp.ToString());
            request.AddHeader("OK-ACCESS-PASSPHRASE", Config.Get("okex-api-passPhase"));
            RestResponse response = client.ExecuteAsync(request).Result;
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"Okex.GetInstruments: request failed: [{(int)response.StatusCode}] {response.StatusDescription}, Content: {response.Content}, ErrorMessage: {response.ErrorMessage}");
            }
            if (response.Content[0] == '{')
            {
                var result = JObject.Parse(response.Content);
                throw new Exception($"Okex.GetInstruments: request failed: [{result["errorCode"].ToString()}] ErrorMessage:{result.GetValue("error_message").ToString()}");
            }
            // get result of option infomation 
            var optionList = JArray.Parse(response.Content);
            // parse the lines, creating the Lean option symbols
            foreach (JObject option in optionList)
            {  
                string timeStr = option["delivery"].ToString();
                var expiryDate = DateTime.ParseExact(timeStr, @"yyyy/M/d H:mm:ss", CultureInfo.InvariantCulture);
                var strike = decimal.Parse((string)option["strike"]);
                string alias = (string)option["instrument_id"];
                string option_type = (string)option["option_type"];
                if (option_type == "C")
                { 
                    symbols.Add(Symbol.CreateOption(underlyingSymbol, Market.Okex, OptionStyle.European, OptionRight.Call, strike, expiryDate, alias));
                }
                else if (option_type == "P")
                { 
                    symbols.Add(Symbol.CreateOption(underlyingSymbol, Market.Okex, OptionStyle.European, OptionRight.Put, strike, expiryDate, alias));
                }
                else
                {
                        throw new Exception($"Invalid option type: {option_type}");
                }

            }
            return symbols;
        }
    }
}
