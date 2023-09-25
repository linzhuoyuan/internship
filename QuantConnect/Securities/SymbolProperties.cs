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

namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents common properties for a specific security, uniquely identified by market, symbol and security type
    /// </summary>
    public class SymbolProperties
    {
        /// <summary>
        /// The description of the security
        /// </summary>
        public string Description
        {
            get;
            set;
        }

        /// <summary>
        /// The quote currency of the security
        /// </summary>
        public string QuoteCurrency
        {
            get;
            set;
        }

        /// 分红调整改动：设置合约乘数属性为外部可设置； writter:lh
        /// <summary>
        /// The contract multiplier for the security
        /// </summary>
        public decimal ContractMultiplier
        {
            get;
            set;
        }

        /// <summary>
        /// The minimum price variation (tick size) for the security
        /// </summary>
        public decimal MinimumPriceVariation
        {
            get;
            set;
        }

        /// <summary>
        /// The lot size (lot size of the order) for the security
        /// </summary>
        public decimal LotSize
        {
            get;
            set;
        }

        /// <summary>
        /// 最小下单市值
        /// </summary>
        public decimal MinNotional
        {
            get;
            set;
        }

        /// <summary>
        /// Creates an instance of the <see cref="SymbolProperties"/> class
        /// </summary>
        public SymbolProperties(
            string description, string quoteCurrency, decimal contractMultiplier, decimal minimumPriceVariation, decimal lotSize, decimal minNotional = 1)
        {
            Description = description;
            QuoteCurrency = quoteCurrency;
            ContractMultiplier = contractMultiplier;
            MinimumPriceVariation = minimumPriceVariation;
            LotSize = lotSize;
            if(LotSize <= 0)
            {
                throw new Exception("SymbolProperties LotSize can not be less than or equal to 0");
            }

            MinNotional = minNotional;
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetProperties(SymbolProperties p)
        {
            Description = p.Description;
            QuoteCurrency = p.QuoteCurrency;
            ContractMultiplier = p.ContractMultiplier;
            MinimumPriceVariation = p.MinimumPriceVariation;
            LotSize = p.LotSize;
            if (LotSize <= 0)
            {
                throw new Exception("SymbolProperties LotSize can not be less than or equal to 0");
            }

            MinNotional = p.MinNotional;
        }

        public bool Equel(SymbolProperties p)
        {
            if (this.QuoteCurrency != p.QuoteCurrency || 
                this.ContractMultiplier != p.ContractMultiplier ||
                this.MinimumPriceVariation != p.MinimumPriceVariation ||
                this.LotSize != p.LotSize ||
                this.MinNotional != p.MinNotional)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets a default instance of the <see cref="SymbolProperties"/> class for the specified <paramref name="quoteCurrency"/>
        /// </summary>
        /// <param name="quoteCurrency">The quote currency of the symbol</param>
        /// <returns>A default instance of the<see cref="SymbolProperties"/> class</returns>
        public static SymbolProperties GetDefault(string quoteCurrency)
        {
            return new SymbolProperties("", quoteCurrency.LazyToUpper(), 1, 0.01m, 1);
        }
    }
}
