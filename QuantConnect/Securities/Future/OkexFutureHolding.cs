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
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace QuantConnect.Securities.Future
{
    /// <summary>
    /// Future holdings implementation of the base securities class
    /// </summary>
    /// <seealso cref="SecurityHolding"/>
    class OkexFutureHolding : SecurityHolding
    {

        private readonly Security _security;
        private readonly ICurrencyConverter _currencyConverter;


        /// <summary>
        /// Future Holding Class constructor
        /// </summary>
        /// <param name="security">The future security being held</param>
        /// <param name="currencyConverter">A currency converter instance</param>
        public OkexFutureHolding(Security security, ICurrencyConverter currencyConverter)
            : base(security, currencyConverter)
        {
            _security = security;
            _currencyConverter = currencyConverter;
        }

        /// <summary>
        /// 功能性函数
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static bool IsSwap(string symbol)
        {
            if (symbol.Contains("SWAP")) return true;
            return false;
        }

        /// <summary>
        /// 功能性函数
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static bool IsUSDTMarign(string symbol)
        {
            if (symbol.Contains("USDT")) return true;
            return false;
        }

        public static bool IsFutureUSD(string symbol)
        {
            if(!IsSwap(symbol) && IsUSDTMarign(symbol))
            {
                return true;
            }
            return false;
        }

        public static bool IsFutureUSDT(string symbol)
        {
            if (IsSwap(symbol) && !IsUSDTMarign(symbol))
            {
                return true;
            }
            return false;
        }

        public static bool IsSwapUSD(string symbol)
        {
            if (IsSwap(symbol) && IsUSDTMarign(symbol))
            {
                return true;
            }
            return false;
        }

        public static bool IsSwapUSDT(string symbol)
        {
            if (!IsSwap(symbol) && !IsUSDTMarign(symbol))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Acquisition cost of the security total holdings in units of the account's currency.
        /// </summary>
        public override decimal HoldingsCost
        {
            get
            {
                if (Quantity == 0)
                {
                    return 0;
                }

                if(IsFutureUSD(this.Symbol.Value))
                {
                    var coin = this.Symbol.Value.Substring(0, 3);
                    if(OkexFutureMarginModel.OkexFutureContractValForUSD.ContainsKey(coin))
                    {
                        var contract_val = OkexFutureMarginModel.OkexFutureContractValForUSD[coin];
                        // contract_val replace AveragePrice 面值替换均价
                        return contract_val * Quantity * _security.QuoteCurrency.ConversionRate * _security.SymbolProperties.ContractMultiplier;
                    }
                    
                } else if(IsFutureUSDT(this.Symbol.Value))
                {
                    return 0;
                }
                else if (IsSwapUSD(this.Symbol.Value))
                {
                    return 0;
                }
                else if (IsSwapUSDT(this.Symbol.Value))
                {
                    return 0;
                }                    

                return 0;
            }
        }

        /// <summary>
        /// Market value of our holdings in units of the account's currency.
        /// </summary>
        public override decimal HoldingsValue
        {
            get
            {
                if (Quantity == 0)
                {
                    return 0;
                }

                if (IsFutureUSD(this.Symbol.Value))
                {
                    var coin = this.Symbol.Value.Substring(0, 3);
                    if (OkexFutureMarginModel.OkexFutureContractValForUSD.ContainsKey(coin))
                    {
                        var contract_val = OkexFutureMarginModel.OkexFutureContractValForUSD[coin];
                        //收益=投资总额(100 美元的整数)*比特币的涨跌幅*固定的杠杆倍数。 持仓市值 = 本金 + 收益
                        var change = (Price - AveragePrice) / AveragePrice;
                        var leverage = _security.MarginModel.GetLeverage(_security);
                        return contract_val * Quantity * (1 + change * leverage) * _security.QuoteCurrency.ConversionRate * _security.SymbolProperties.ContractMultiplier;
                    }

                }
                else if (IsFutureUSDT(this.Symbol.Value))
                {
                    return 0;
                }
                else if (IsSwapUSD(this.Symbol.Value))
                {
                    return 0;
                }
                else if (IsSwapUSDT(this.Symbol.Value))
                {
                    return 0;
                }

                return 0;
            }
        }

        ///// <summary>
        ///// Unrealized profit of this security when absolute quantity held is more than zero in units of the account's currency.
        ///// </summary>
        public override decimal UnrealizedProfit
        {
            get { return TotalCloseProfit(); }
        }

        /// <summary>
        /// Profit if we closed the holdings right now including the approximate fees in units of the account's currency.
        /// </summary>
        /// <remarks>Does not use the transaction model for market fills but should.</remarks>
        public override decimal TotalCloseProfit()
        {
            if (Quantity == 0)
            {
                return 0;
            }

            // this is in the account currency
            var marketOrder = new MarketOrder(_security.Symbol, -Quantity, _security.LocalTime.ConvertToUtc(_security.Exchange.TimeZone));

            var orderFee = _security.FeeModel.GetOrderFee(
                new OrderFeeParameters(_security, marketOrder)).Value;
            var feesInAccountCurrency = _currencyConverter.
                ConvertToAccountCurrency(orderFee).Amount;

            var price = marketOrder.Direction == OrderDirection.Sell ? _security.BidPrice : _security.AskPrice;


            if (IsFutureUSD(this.Symbol.Value))
            {
                var coin = this.Symbol.Value.Substring(0, 3);
                if (OkexFutureMarginModel.OkexFutureContractValForUSD.ContainsKey(coin))
                {
                    var contract_val = OkexFutureMarginModel.OkexFutureContractValForUSD[coin];
                    //收益=投资总额(100 美元的整数)*比特币的涨跌幅*固定的杠杆倍数。
                    var change = (Price - AveragePrice) / AveragePrice;
                    var leverage = _security.MarginModel.GetLeverage(_security);
                    return contract_val * Quantity * change * leverage * _security.QuoteCurrency.ConversionRate * _security.SymbolProperties.ContractMultiplier
                        -feesInAccountCurrency;
                }

            }
            else if (IsFutureUSDT(this.Symbol.Value))
            {
                return 0;
            }
            else if (IsSwapUSD(this.Symbol.Value))
            {
                return 0;
            }
            else if (IsSwapUSDT(this.Symbol.Value))
            {
                return 0;
            }

            return 0;
        }

    }
}
