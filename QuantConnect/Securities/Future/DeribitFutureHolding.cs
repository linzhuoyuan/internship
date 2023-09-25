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
using QuantConnect.Configuration;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace QuantConnect.Securities.Future
{
    /// <summary>
    /// Future holdings implementation of the base securities class
    /// </summary>
    /// <seealso cref="SecurityHolding"/>
    public class DeribitFutureHolding : SecurityHolding
    {
        private readonly ICurrencyConverter _currencyConverter;
        /// <summary>
        /// Future Holding Class constructor
        /// </summary>
        /// <param name="security">The future security being held</param>
        /// <param name="currencyConverter">A currency converter instance</param>
        public DeribitFutureHolding(Security security, ICurrencyConverter currencyConverter)
            : base(security, currencyConverter)
        {
            _currencyConverter = currencyConverter;
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
                //ConversionRate == 1  ContractMultiplier == 1
                return Quantity * security.QuoteCurrency.ConversionRate * security.SymbolProperties.ContractMultiplier;
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
                //return (Quantity/ AveragePrice)* Price * security.QuoteCurrency.ConversionRate * security.SymbolProperties.ContractMultiplier;
                return Quantity * security.QuoteCurrency.ConversionRate * security.SymbolProperties.ContractMultiplier;
            }
        }

        /// <summary>
        /// Unrealized profit of this security when absolute quantity held is more than zero in units of the account's currency.
        /// </summary>
        public override decimal UnrealizedProfit
        {
            get { return TotalCloseProfit(); }
        }

        /// <summary>
        /// Profit if we closed the holdings right now including the approximate fees in units of the account's currency.
        /// https://zhuanlan.zhihu.com/p/63086586 refferance
        /// </summary>
        /// <remarks>Does not use the transaction model for market fills but should.</remarks>
        public override decimal TotalCloseProfit()
        {
            if (Quantity == 0)
            {
                return 0;
            }

            // this is in the account currency
            var marketOrder = new MarketOrder(security.Symbol, -Quantity, 
                security.LocalTime.ConvertToUtc(security.Exchange.TimeZone));

            var orderFee = security.FeeModel.GetOrderFee(
                new OrderFeeParameters(security, marketOrder)).Value;
            var feesInAccountCurrency = _currencyConverter.
                ConvertToAccountCurrency(orderFee).Amount;

            decimal price = security.Cache.MarkPrice;
            if (price == 0)
            {
                price = marketOrder.Direction == OrderDirection.Sell ? security.BidPrice : security.AskPrice;
            }

            if (price == 0)
            {
                return 0;
            }

            // Note: In Deribit, perpetual are quoted as USD-contract, so the Quantity is the total value of the contract in USD. We should convert it to 
            // BTC to get the profit in BTC and convert to USD. 
            // (https://www.binance.com/zh-TC/support/faq/3a55a23768cb416fb404f06ffedde4b2)
            return (Quantity / AveragePrice - Quantity / price) * price * security.QuoteCurrency.ConversionRate * security.SymbolProperties.ContractMultiplier - feesInAccountCurrency;
        }
    }
}
