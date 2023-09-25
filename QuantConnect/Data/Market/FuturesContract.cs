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

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Defines a single futures contract at a specific expiration
    /// </summary>
    public class FuturesContract
    {
        internal readonly Symbol symbol;
        internal readonly Symbol underlyingSymbol;
        internal DateTime time;
        internal decimal openInterest;
        internal decimal lastPrice;
        internal decimal volume;
        internal decimal bidPrice;
        internal decimal bidSize;
        internal decimal askPrice;
        internal decimal askSize;

        /// <summary>
        /// Gets the futures contract's symbol
        /// </summary>
        public Symbol Symbol => symbol;

        /// <summary>
        /// Gets the underlying security's symbol
        /// </summary>
        public Symbol UnderlyingSymbol => underlyingSymbol;

        /// <summary>
        /// Gets the expiration date
        /// </summary>
        public DateTime Expiry => Symbol.ID.Date;

        /// <summary>
        /// Gets the local date time this contract's data was last updated
        /// </summary>
        public DateTime Time
        {
            get => time;
            set => time = value;
        }

        /// <summary>
        /// Gets the open interest
        /// </summary>
        public decimal OpenInterest
        {
            get => openInterest;
            set => openInterest = value;
        }

        /// <summary>
        /// Gets the last price this contract traded at
        /// </summary>
        public decimal LastPrice
        {
            get => lastPrice;
            set => lastPrice = value;
        }

        /// <summary>
        /// Gets the last volume this contract traded at
        /// </summary>
        public decimal Volume
        {
            get => volume;
            set => volume = value;
        }

        /// <summary>
        /// Gets the current bid price
        /// </summary>
        public decimal BidPrice
        {
            get => bidPrice;
            set => bidPrice = value;
        }

        /// <summary>
        /// Get the current bid size
        /// </summary>
        public decimal BidSize
        {
            get => bidSize;
            set => bidSize = value;
        }

        /// <summary>
        /// Gets the ask price
        /// </summary>
        public decimal AskPrice
        {
            get => askPrice;
            set => askPrice = value;
        }

        /// <summary>
        /// Gets the current ask size
        /// </summary>
        public decimal AskSize
        {
            get => askSize;
            set => askSize = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FuturesContract"/> class
        /// </summary>
        /// <param name="symbol">The futures contract symbol</param>
        /// <param name="underlyingSymbol">The symbol of the underlying security</param>
        public FuturesContract(Symbol symbol, Symbol underlyingSymbol)
        {
            this.symbol = symbol;
            this.underlyingSymbol = underlyingSymbol;
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        public override string ToString() => symbol.value;
    }
}
