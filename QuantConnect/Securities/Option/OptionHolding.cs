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
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// Option holdings implementation of the base securities class
    /// </summary>
    /// <seealso cref="SecurityHolding"/>
    public class OptionHolding : SecurityHolding
    {
        private readonly Security _security;
        private readonly ICurrencyConverter _currencyConverter;

        /// <summary>
        /// Option Holding Class constructor
        /// </summary>
        /// <param name="security">The option security being held</param>
        /// <param name="currencyConverter">A currency converter instance</param>
        public OptionHolding(Option security, ICurrencyConverter currencyConverter)
            : base(security, currencyConverter)
        {
            _security = security;
            _currencyConverter = currencyConverter;
        }

        /// <summary>
        /// Unrealized profit of this security when absolute quantity held is more than zero in units of the account's currency.
        /// </summary>
        public override decimal UnrealizedProfit
        {
            //多头持仓：未实现盈亏=标记价格*合约乘数*持仓张数 -开仓均价(或结算价格)*合约乘数*持仓张数；
            //空头持仓：未实现盈亏=开仓均价(或结算价格)*合约乘数*持仓张数 - 标记价格*合约乘数*持仓张数；
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
            if(_security.Symbol.ID.Market == Market.Okex || _security.Symbol.ID.Market == Market.Deribit)//只追加此条件
            {
                price = _security.Cache.MarkPrice;//标记价格
            }

            if (price == 0)
            {
                return 0;
            }

            return (price - AveragePrice) * Quantity * _security.QuoteCurrency.ConversionRate
                * _security.SymbolProperties.ContractMultiplier - feesInAccountCurrency;
        }
    }
}