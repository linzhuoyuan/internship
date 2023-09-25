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
    /// Represents a simple, constant margining model by specifying the percentages of required margin.
    /// </summary>
    public class MomOptionMarginModel : SecurityMarginModel
    {

        // initial margin
        //private const decimal OptionMarginRequirement = 1;
        private const decimal OptionMarginParameter1 = 0.12m;
        private const decimal OptionMarginParameter2 = 0.07m;


        /// <summary>
        /// Gets the total margin required to execute the specified order in units of the account currency including fees
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the order</param>
        /// <returns>The total margin in terms of the currency quoted in the order</returns>
        protected override decimal GetInitialMarginRequiredForOrder(
            InitialMarginRequiredForOrderParameters parameters)
        {
            var fees = parameters.Security.FeeModel.GetOrderFee(
              new OrderFeeParameters(parameters.Security,
                  parameters.Order)).Value;
            var feesInAccountCurrency = parameters.CurrencyConverter.
                ConvertToAccountCurrency(fees).Amount;

            var value = parameters.Order.GetValue(parameters.Security);
            var isLong = parameters.Order.Direction == OrderDirection.Buy && parameters.Order.Offset == OrderOffset.Open ? true : false;
            var orderValue = isLong ? value : GetInitialMarginRequirement(parameters.Security);

            return orderValue + Math.Sign(orderValue) * feesInAccountCurrency;
        }

        /// <summary>
        /// Gets the margin cash available for a trade 剩余保证金
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">The security to be traded</param>
        /// <param name="direction">The direction of the trade</param>
        /// <param name="offset">The offset of the trade</param>
        /// <param name="orderId">The orderId of the trade</param>
        /// <returns>The margin available for the trade</returns>
        protected override decimal GetMarginRemaining(SecurityPortfolioManager portfolio, Security security, OrderDirection direction,
            OrderOffset offset = OrderOffset.None, long orderId = 0)
        {
            return portfolio.MarginRemaining;
        }

        /// <summary>
        /// Gets the margin currently alloted to the specified holding
        /// </summary>
        /// <param name="security">The security to compute maintenance margin for</param>
        /// <returns>The maintenance margin required for the security</returns>
        protected override decimal GetMaintenanceMargin(Security security)
        {
            //只有义务仓才会算维持保证金
            if (!security.Holdings.Invested)
                return 0m;
            var option = (Option)security;
            var underlying = option.Underlying;
            if (option.Close == 0m ||
                option.StrikePrice == 0m ||
                option.Underlying == null ||
                underlying.Close == 0m)
            {
                return 0m;
            }
            var amountOTM = option.Right == OptionRight.Call
                ? Math.Max(0, option.StrikePrice - underlying.Close)
                : Math.Max(0, underlying.Close - option.StrikePrice);
            var optionRight = option.Symbol.ID.OptionRight;
            if (optionRight == OptionRight.Call)
            {
                //认购期权义务仓维持保证金＝[合约结算价+Max（12%×合约标的收盘价- 认购期权虚值，7%×合约标的收盘价）]×合约单位
                return security.Holdings.AbsoluteQuantity * (option.Close + Math.Max(OptionMarginParameter1 * underlying.Close - amountOTM, OptionMarginParameter2 * underlying.Close)) * option.SymbolProperties.ContractMultiplier;
            }
            //认沽期权义务仓维持保证金＝Min[合约结算价 + Max（12 %×合标的收盘价 - 认沽期权虚值，7 %×行权价格），行权价格]×合约单位
            return security.Holdings.AbsoluteQuantity * Math.Min(option.Close + Math.Max(OptionMarginParameter1 * underlying.Close - amountOTM, OptionMarginParameter2 * option.StrikePrice), option.StrikePrice) * option.SymbolProperties.ContractMultiplier;

        }

        /// <summary>
        /// 卖出开仓初始保证金计算
        /// <param name="security">Option security</param>
        /// <returns>The initial margin required for the security</returns>
        protected override decimal GetInitialMarginRequirement(Security security)
        {
            var option = (Option)security;
            var underlying = option.Underlying;
            if (option.Close == 0m ||
                option.StrikePrice == 0m ||
                option.Underlying == null ||
                underlying.Close == 0m)
            {
                return 0m;
            }
            var amountOTM = option.Right == OptionRight.Call
                ? Math.Max(0, option.StrikePrice - underlying.Close)
                : Math.Max(0, underlying.Close - option.StrikePrice);
            var optionRight = option.Symbol.ID.OptionRight;
            if (optionRight == OptionRight.Call)
            {
                //认购期权义务仓开仓保证金＝[合约前结算价+Max（12%×合约标的前收盘价- 认购期权虚值，7%×合约标的前收盘价）] ×合约单位
                return (option.Price + Math.Max(OptionMarginParameter1 * underlying.Price - amountOTM, OptionMarginParameter2 * underlying.Price)) * option.SymbolProperties.ContractMultiplier;
            }
            //认沽期权义务仓开仓保证金＝Min[合约前结算价 + Max（12 %×合约标的前收盘价 - 认沽期权虚值，7 %×行权价格），行权价格] ×合约单位
            return Math.Min(option.Price + Math.Max(OptionMarginParameter1 * underlying.Price - amountOTM, OptionMarginParameter2 * option.StrikePrice), option.StrikePrice) * option.SymbolProperties.ContractMultiplier;
        }
    }
}