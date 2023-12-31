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
using Newtonsoft.Json.Serialization;

namespace QuantConnect
{
    /// <summary>
    /// Markets Collection: Soon to be expanded to a collection of items specifying the market hour, timezones and country codes.
    /// </summary>
    public static class Market
    {
        // the upper bound (non-inclusive) for market identifiers
        private const int MaxMarketIdentifier = 1000;

        private static readonly object _lock = new();
        private static readonly Dictionary<string, int> Markets = new();
        private static readonly Dictionary<int, string> ReverseMarkets = new();
        private static readonly IEnumerable<Tuple<string, int>> HardcodedMarkets = new List<Tuple<string, int>>
        {
            Tuple.Create("empty", 0),
            Tuple.Create(USA, 1),
            Tuple.Create(FXCM, 2),
            Tuple.Create(Oanda, 3),
            Tuple.Create(Dukascopy, 4),
            Tuple.Create(Bitfinex, 5),

            Tuple.Create(Globex, 6),
            Tuple.Create(NYMEX, 7),
            Tuple.Create(CBOT, 8),
            Tuple.Create(ICE, 9),
            Tuple.Create(CBOE, 10),
            Tuple.Create(NSE, 11),

            Tuple.Create(GDAX, 12),
            Tuple.Create(Kraken, 13),
            Tuple.Create(Bittrex, 14),
            Tuple.Create(Bithumb, 15),
            Tuple.Create(Binance, 16),
            Tuple.Create(Poloniex, 17),
            Tuple.Create(Coinone, 18),
            Tuple.Create(HitBTC, 19),
            Tuple.Create(OkCoin, 20),
            Tuple.Create(Bitstamp, 21),
            Tuple.Create(SSE, 22),
            Tuple.Create(SZSE, 23),
            Tuple.Create(Deribit, 24),
            Tuple.Create(Okex, 25),
            Tuple.Create(CFFEX, 26),
            Tuple.Create(FTX, 27),
            Tuple.Create(MEXC, 28),
            Tuple.Create(DYDX, 29),
            Tuple.Create(Uniswap, 30),
            Tuple.Create(HKG, 31),
            Tuple.Create(HKA, 32),
            Tuple.Create(OneInch, 33)
        };

        static Market()
        {
            // initialize our maps
            foreach (var market in HardcodedMarkets)
            {
                Markets[market.Item1] = market.Item2;
                ReverseMarkets[market.Item2] = market.Item1;
            }
        }
        
        /// <summary>
        /// USA Market
        /// </summary>
        public const string USA = "usa";

        /// <summary>
        /// Oanda Market
        /// </summary>
        public const string Oanda = "oanda";

        /// <summary>
        /// FXCM Market Hours
        /// </summary>
        public const string FXCM = "fxcm";

        /// <summary>
        /// Dukascopy Market
        /// </summary>
        public const string Dukascopy = "dukascopy";

        /// <summary>
        /// Bitfinex market
        /// </summary>
        public const string Bitfinex = "bitfinex";

        // Futures exchanges

        /// <summary>
        /// CME Globex
        /// </summary>
        public const string Globex = "cmeglobex";

        /// <summary>
        /// NYMEX
        /// </summary>
        public const string NYMEX = "nymex";

        /// <summary>
        /// CBOT
        /// </summary>
        public const string CBOT = "cbot";

        /// <summary>
        /// ICE
        /// </summary>
        public const string ICE = "ice";

        /// <summary>
        /// CBOE
        /// </summary>
        public const string CBOE = "cboe";

        /// <summary>
        /// NSE
        /// </summary>
        public const string NSE = "nse";

        /// <summary>
        /// GDAX
        /// </summary>
        public const string GDAX = "gdax";

        /// <summary>
        /// Kraken
        /// </summary>
        public const string Kraken = "kraken";

        /// <summary>
        /// Bitstamp
        /// </summary>
        public const string Bitstamp = "bitstamp";

        /// <summary>
        /// OkCoin
        /// </summary>
        public const string OkCoin = "okcoin";

        /// <summary>
        /// Bithumb
        /// </summary>
        public const string Bithumb = "bithumb";

        /// <summary>
        /// Binance
        /// </summary>
        public const string Binance = "binance";

        /// <summary>
        /// Poloniex
        /// </summary>
        public const string Poloniex = "poloniex";

        /// <summary>
        /// Coinone
        /// </summary>
        public const string Coinone = "coinone";

        /// <summary>
        /// HitBTC
        /// </summary>
        public const string HitBTC = "hitbtc";

        /// <summary>
        /// Bittrex
        /// </summary>
        public const string Bittrex = "bittrex";

        /// <summary>
        /// ShangHai Market
        /// </summary>
        public const string SSE = "sse";
		
		/// <summary>
        /// Shenzhen Market
        /// </summary>
        public const string SZSE = "szse";

        /// <summary>
        /// Beijing Market
        /// </summary>
        public const string BSE= "bse";

        public const string Deribit = "deribit";

        public const string Okex = "okex";

        /// <summary>
        /// 
        /// </summary>
        public const string CFFEX = "cffex";

        public const string FTX = "ftx";

        public const string MEXC = "mexc";

        public const string DYDX = "dydx";

        public const string Uniswap = "uniswap";

        public const string HKG = "hkg";

        public const string HKA = "hka";
        public const string OneInch = "oneinch";

        /// <summary>
        /// Adds the specified market to the map of available markets with the specified identifier.
        /// </summary>
        /// <param name="market">The market string to add</param>
        /// <param name="identifier">The identifier for the market, this value must be positive and less than 1000</param>
        public static void Add(string market, int identifier)
        {
            if (identifier >= MaxMarketIdentifier)
            {
                var message = string.Format("The market identifier is limited to positive values less than {0}.", MaxMarketIdentifier);
                throw new ArgumentOutOfRangeException("identifier", message);
            }

            market = market.ToLower();

            // we lock since we don't want multiple threads getting these two dictionaries out of sync
            lock (_lock)
            {
                if (Markets.TryGetValue(market, out var marketIdentifier) && identifier != marketIdentifier)
                {
                    throw new ArgumentException("Attempted to add an already added market with a different identifier. Market: " + market);
                }

                if (ReverseMarkets.TryGetValue(identifier, out var existingMarket))
                {
                    throw new ArgumentException("Attempted to add a market identifier that is already in use. New Market: " + market + " Existing Market: " + existingMarket);
                }

                // update our maps
                Markets[market] = identifier;
                ReverseMarkets[identifier] = market;
            }
        }

        /// <summary>
        /// Gets the market code for the specified market. Returns <c>null</c> if the market is not found
        /// </summary>
        /// <param name="market">The market to check for (case sensitive)</param>
        /// <returns>The internal code used for the market. Corresponds to the value used when calling <see cref="Add"/></returns>
        public static int? Encode(string market)
        {
            lock (_lock)
            {
                return !Markets.TryGetValue(market, out var code) ? (int?) null : code;
            }
        }

        /// <summary>
        /// Gets the market string for the specified market code.
        /// </summary>
        /// <param name="code">The market code to be decoded</param>
        /// <returns>The string representation of the market, or null if not found</returns>
        public static string Decode(int code)
        {
            lock (_lock)
            {
                return !ReverseMarkets.TryGetValue(code, out var market) ? null : market;
            }
        }

        /// <summary>
        /// IsChinaMarket
        /// </summary>
        /// <param name="market"></param>
        /// <returns></returns>
        public static bool IsChinaMarket(string market)
        {
            switch (market)
            {
                case BSE:
                case SSE:
                case SZSE:
                case CFFEX:
                    return true;
                default: return false;
            }
        }
        /// <summary>
        /// IsChinaMarket
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static bool IsChinaMarket(Symbol symbol)
        {
            return IsChinaMarket(symbol.ID.Market);
        }
    }
}