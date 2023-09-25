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
using Newtonsoft.Json;
using QuantConnect.Logging;
using QuantConnect.Securities.Option;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Defines a single option contract at a specific expiration and strike price
    /// </summary>

    [JsonConverter(typeof(OptionContractJsonConverter))]
    public class OptionContract
    {
        private ResetLazy<OptionPriceModelResult> _optionPriceModelResult = 
            new(() => new OptionPriceModelResult(0m, new Greeks()));
        private decimal _pricingVolatility;
        internal readonly Symbol symbol;
        internal readonly Symbol underlyingSymbol;
        internal decimal lastPrice;
        internal decimal volume;
        internal decimal bidPrice;
        internal decimal bidSize;
        internal decimal askPrice;
        internal decimal askSize;
        internal decimal underlyingLastPrice;
        internal DateTime time;
        internal decimal openInterest;
        internal DateTime expiry = DateTime.MinValue;

        /// <summary>
        /// Gets the option contract's symbol
        /// </summary>
        public Symbol Symbol => symbol;

        /// <summary>
        /// Gets the underlying security's symbol
        /// </summary>
        public Symbol UnderlyingSymbol => underlyingSymbol;

        /// <summary>
        /// Gets the strike price
        /// </summary>
        public decimal Strike => Symbol.ID.StrikePrice;

        /// <summary>
        /// Gets the expiration date
        /// </summary>
        public DateTime Expiry  
        {
            get => expiry == DateTime.MinValue ? Symbol.ID.Date : expiry;
            set => expiry = value;
        }

        /// <summary>
        /// Gets the right being purchased (call [right to buy] or put [right to sell])
        /// </summary>
        public OptionRight Right => Symbol.ID.OptionRight;

        /// <summary>
        /// Gets the theoretical price of this option contract as computed by the <see cref="IOptionPriceModel"/>
        /// </summary>
        [JsonIgnore]
        public decimal TheoreticalPrice => _optionPriceModelResult.Value.TheoreticalPrice;

        /// <summary>
        /// Gets the implied volatility of the option contract as computed by the <see cref="IOptionPriceModel"/>
        /// </summary>
        [JsonIgnore]
        public decimal ImpliedVolatility => _optionPriceModelResult.Value.ImpliedVolatility;

        /// <summary>
        /// Gets the greeks for this contract
        /// </summary>
        [JsonIgnore]
        public Greeks Greeks => _optionPriceModelResult.Value.Greeks;

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
        [JsonIgnore]
        public decimal OpenInterest
        {
            get => openInterest;
            set => openInterest = value;
        }

        /// <summary>
        /// Gets the open interest
        /// </summary>
        [JsonIgnore]
        public decimal PricingVolatility
        {
            get => _pricingVolatility;
            set
            {
                if (_pricingVolatility != value)
                {
                    _optionPriceModelResult.Reset();
                    _pricingVolatility = value;
                }
            }
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
        /// Gets the last price the underlying security traded at
        /// </summary>
        public decimal UnderlyingLastPrice
        {
            get => underlyingLastPrice;
            set => underlyingLastPrice = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionContract"/> class
        /// </summary>
        /// <param name="symbol">The option contract symbol</param>
        /// <param name="underlyingSymbol">The symbol of the underlying security</param>
        public OptionContract(Symbol symbol, Symbol underlyingSymbol)
        {
            this.symbol = symbol;
            this.underlyingSymbol = underlyingSymbol;
        }

        /// <summary>
        /// Sets the option price model evaluator function to be used for this contract
        /// </summary>
        /// <param name="optionPriceModelEvaluator">Function delegate used to evaluate the option price model</param>
        internal void SetOptionPriceModel(Func<OptionPriceModelResult> optionPriceModelEvaluator)
        {
            _optionPriceModelResult = new ResetLazy<OptionPriceModelResult>(optionPriceModelEvaluator);
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
