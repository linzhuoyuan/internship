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
using System.Globalization;
using System.IO;
using System.Linq;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.Configuration;
using QuantConnect.Securities.Future;

namespace QuantConnect.Securities.Option
{
    public class DeribitOptionMarginModel : SecurityMarginModel
    {
        private readonly decimal _leverage = 1;
        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="requiredFreeBuyingPowerPercent"></param>
        public DeribitOptionMarginModel(decimal requiredFreeBuyingPowerPercent = 0)
        {
            RequiredFreeBuyingPowerPercent = requiredFreeBuyingPowerPercent;
        }

        /// <summary>
        /// Deribit global leverage rule
        /// </summary>
        /// <param name="security"></param>
        /// <returns></returns>
        public override decimal GetLeverage(Security security)
        {
            return _leverage;
        }

        /// <summary>
        /// Sets the leverage for the applicable securities, i.e, futures
        /// </summary>
        /// <remarks>
        /// This is added to maintain backwards compatibility with the old margin/leverage system
        /// </remarks>
        /// <param name="security"></param>
        /// <param name="leverage">The new leverage</param>
        public override void SetLeverage(Security security, decimal leverage)
        {
            // Futures are leveraged products and different leverage cannot be set by user.
            throw new InvalidOperationException("Option are leveraged products and different leverage cannot be set by user");
        }

        /// <summary>
        /// Gets the margin cash available for a trade
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">The security to be traded</param>
        /// <param name="direction">The direction of the trade</param>
        /// <param name="offset">The offset of the trade</param>
        /// <param name="orderId">The orderId of the trade</param>
        /// <returns>The margin available for the trade</returns>
        protected override decimal GetMarginRemaining(SecurityPortfolioManager portfolio, Security security, OrderDirection direction, OrderOffset offset = OrderOffset.None, long orderId = long.MaxValue)
        {
            decimal result = 0;
            var quoteCurrency = security.QuoteCurrency.Symbol;

            var total = portfolio.GetTotalPortfolioValueForCurrency(quoteCurrency);
            result = portfolio.GetMarginRemainingForCurrency(quoteCurrency, total, orderId);
            return result;
        }

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

            //var orderValue = parameters.Order.GetValue(parameters.Security)
            //    * GetInitialMarginRequirement(parameters.Security);
            bool isOrder = true;
            var unitValue = GetUnitInitialMargin(parameters.Security, parameters.Order.Quantity, isOrder);
            var orderValue = unitValue * parameters.Order.Quantity;

            //Log.Trace($"GetInitialMarginRequiredForOrder {parameters.Security.Symbol.Value} {parameters.Order.Quantity} {orderValue}");
            return orderValue + Math.Sign(orderValue) * feesInAccountCurrency;
        }


        /// <summary>
        /// Gets the margin currently alloted to the specified holding  维持保证金 = 持仓成本*维持保证金比例
        /// deribit 维持保证金 = size*GetMaintenanceMarginRequirement
        /// </summary>
        /// <param name="security">The security to compute maintenance margin for</param>
        /// <returns>The maintenance margin required for the </returns>
        protected override decimal GetMaintenanceMargin(Security security)
        {
            //return security.Holdings.AbsoluteHoldingsCost * GetMaintenanceMarginRequirement(security);
            var unitValue = GetUnitMaintenanceMargin(security, security.Holdings.Quantity);
            return unitValue * security.Holdings.AbsoluteQuantity;
        }

        /// <summary>
        /// The percentage of an order's absolute cost that must be held in free cash in order to place the order
        /// </summary>
        protected override decimal GetInitialMarginRequirement(Security security)
        {
            //return GetInitialMarginRequirement(security, security.Holdings.HoldingsValue);
            throw new InvalidOperationException("deribit GetInitialMarginRequirement");
        }

        /// <summary>
        /// The percentage of the holding's absolute cost that must be held in free cash in order to avoid a margin call
        /// </summary>
        public override decimal GetMaintenanceMarginRequirement(Security security)
        {
            //return GetMaintenanceMarginRequirement(security, security.Holdings.HoldingsValue);
            //throw new InvalidOperationException("deribit GetMaintenanceMarginRequirement");
            if (security.Holdings.Quantity > 0)
            {
                return 1;
            }
            var unit = GetUnitMaintenanceMargin(security, security.Holdings.Quantity);
            var result = unit / (security.QuoteCurrency.ConversionRate * security.SymbolProperties.ContractMultiplier);
            result /= security.Cache.MarkPrice;
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="security"></param>
        /// <param name="quantity"></param>
        /// <param name="isOrder">order or holding</param>
        /// <returns></returns>
        public decimal GetUnitInitialMargin(Security security, decimal quantity, bool isOrder)
        {
            if (security.Symbol.Value.Contains("?")) return 0;
            if (quantity > 0)
            {
                if (isOrder)
                {
                    return security.Cache.MarkPrice * security.QuoteCurrency.ConversionRate * security.SymbolProperties.ContractMultiplier;
                }
                else
                {
                    return 0;
                }
            }

            decimal result = 0;
            var option = (Option)security;
            var underlying = option.Underlying;

            if (option.Right == OptionRight.Call)
            {
                //Initial margin (BTC): Maximum (0.15 - Out of the Money Amount/Underlying MarkPrice, 0.1) + Mark Price of the option
                var outOfMoney = Math.Max(option.StrikePrice - underlying.Price, 0);
                result = Math.Max((decimal)0.15 - outOfMoney / underlying.Price, (decimal)0.1) + security.Cache.MarkPrice;
            }
            else
            {
                //Initial margin (BTC): Maximum (Maximum (0.15 - Out of the Money Amount/Underlying MarkPrice, 0.1 )+ markprice_option, Maintenance Margin)
                var outOfMoney = Math.Max(underlying.Price - option.StrikePrice, 0);
                var v1 = Math.Max((decimal)0.15 - outOfMoney / underlying.Price, (decimal)0.1) + security.Cache.MarkPrice;
                var v2 = GetUnitMaintenanceMargin(security, quantity) / (security.QuoteCurrency.ConversionRate * security.SymbolProperties.ContractMultiplier);
                result = Math.Max(v1, v2);//这需要填 security.Holdings.HoldingsValue 还是 orderValue
            }

            return result * security.QuoteCurrency.ConversionRate * security.SymbolProperties.ContractMultiplier;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="security"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        public decimal GetUnitMaintenanceMargin(Security security, decimal quantity)
        {
            if (security.Symbol.Value.Contains("?")) return 0;
            if (quantity > 0)
                return 0;

            decimal result = 0;
            var option = (Option)security;
            if (option.Right == OptionRight.Call)
            {
                // Maintenance margin(BTC): 0.075 + mark price of the option
                result = (decimal)0.075 + security.Cache.MarkPrice;
            }
            else
            {
                //Maintenance margin (BTC):Maximum (0.075, 0.075 * markprice_option) + mark_price_option
                result = Math.Max((decimal)0.075, (decimal)0.075 * security.Cache.MarkPrice) + security.Cache.MarkPrice;
            }
            return result * security.QuoteCurrency.ConversionRate * security.SymbolProperties.ContractMultiplier;
        }

        /// <summary>
        /// Check if there is sufficient buying power to execute this order.
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the order</param>
        /// <returns>Returns buying power information for an order</returns>
        public override HasSufficientBuyingPowerForOrderResult HasSufficientBuyingPowerForOrder(HasSufficientBuyingPowerForOrderParameters parameters)
        {
            return new HasSufficientBuyingPowerForOrderResult(true);
            // short circuit the div 0 case  除数不能为0
            if (parameters.Order.Quantity == 0)
            {
                return new HasSufficientBuyingPowerForOrderResult(true);
            }

            //没有查到订单返回错误
            var ticket = parameters.Portfolio.Transactions.GetOrderTicket(parameters.Order.Id);
            if (ticket == null)
            {
                var reason = $"Null order ticket for id: {parameters.Order.Id}";
                Log.Error($"SecurityMarginModel.HasSufficientBuyingPowerForOrder(): {reason}");
                return new HasSufficientBuyingPowerForOrderResult(false, reason);
            }

            //期权执行
            if (parameters.Order.Type == OrderType.OptionExercise)
            {
                // for option assignment and exercise orders we look into the requirements to process the underlying security transaction
                var option = (Option)parameters.Security;
                var underlying = option.Underlying;

                if (option.IsAutoExercised(underlying.Close) && option.ExerciseSettlement == SettlementType.PhysicalDelivery)
                {
                    var quantity = option.GetExerciseQuantity(parameters.Order.Quantity);

                    var newOrder = new LimitOrder
                    {
                        Id = parameters.Order.Id,
                        Time = parameters.Order.Time,
                        LimitPrice = option.StrikePrice,
                        Symbol = underlying.Symbol,
                        Quantity = option.Symbol.ID.OptionRight == OptionRight.Call ? quantity : -quantity
                    };

                    // we continue with this call for underlying
                    return underlying.BuyingPowerModel.HasSufficientBuyingPowerForOrder(
                        new HasSufficientBuyingPowerForOrderParameters(parameters.Portfolio, underlying, newOrder));
                }

                return new HasSufficientBuyingPowerForOrderResult(true);
            }

            decimal freeMargin = 0;
            decimal initialMarginRequiredForOrder = 0;

            //// When order only reduces or closes a security position, capital is always sufficient 持仓和订单方向相反，且持仓大于订单，资金总是充足的
            //if (parameters.Security.Holdings.Quantity * parameters.Order.Quantity < 0 && Math.Abs(parameters.Security.Holdings.Quantity) >= Math.Abs(parameters.Order.Quantity))
            //{
            //    return new HasSufficientBuyingPowerForOrderResult(true);
            //}

            //反向
            if (parameters.Security.Holdings.Quantity * parameters.Order.Quantity < 0)
            {
                var orders = parameters.Portfolio.Transactions.GetOpenOrders(x => x.symbol == parameters.Order.Symbol && x.Direction == parameters.Order.Direction).ToList();

                var totalQuantity = orders.Sum(o => o.Quantity);
                if (Math.Abs(parameters.Security.Holdings.Quantity) >= Math.Abs(totalQuantity))
                {
                    return new HasSufficientBuyingPowerForOrderResult(true);
                }
            }

            freeMargin = GetMarginRemaining(parameters.Portfolio, parameters.Security, parameters.Order.Direction,
                parameters.Order.Offset, parameters.Order.Id);
            /*
            initialMarginRequiredForOrder = GetInitialMarginRequiredForOrder(
                new InitialMarginRequiredForOrderParameters(parameters.Portfolio.CashBook,
                    parameters.Security,
                    parameters.Order,
                    parameters.Portfolio));


            // pro-rate the initial margin required for order based on how much has already been filled
            var percentUnfilled = (Math.Abs(parameters.Order.Quantity) - Math.Abs(ticket.QuantityFilled)) / Math.Abs(parameters.Order.Quantity);
            var initialMarginRequiredForRemainderOfOrder = percentUnfilled * initialMarginRequiredForOrder;

            // 此订单的初始保证金 > 剩余保证金
            if (Math.Abs(initialMarginRequiredForRemainderOfOrder) > freeMargin)
            {
                var reason = $"Id: {parameters.Order.Id}, " +
                    $"Initial Margin: {initialMarginRequiredForRemainderOfOrder.Normalize()}, " +
                    $"Free Margin: {freeMargin.Normalize()}";

                Log.Error($"SecurityMarginModel.HasSufficientBuyingPowerForOrder(): {reason}");
                return new HasSufficientBuyingPowerForOrderResult(false, reason);
            }
            */
            if (freeMargin < 0)
            {
                var reason = $"Id: {parameters.Order.Id}, " +
                             $"Free Margin: {freeMargin.Normalize()}";

                Log.Error($"SecurityMarginModel.HasSufficientBuyingPowerForOrder(): {reason}");
                return new HasSufficientBuyingPowerForOrderResult(false, reason);
            }
            return new HasSufficientBuyingPowerForOrderResult(true);
        }


        /////////////////////////////////////////////////////////额外扩展//////////////////////////////////////////////////////////
        /// <summary>
        /// 
        /// </summary>
        /// <param name="portfolio"></param>
        /// <param name="quoteCurrency"></param>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public static decimal GetOptionTotalInitialMargin(SecurityPortfolioManager portfolio, string quoteCurrency, long orderId = long.MaxValue)
        {
            decimal marigin = 0;
            foreach (var x in portfolio.Securities.Listed())
            {
                var security = x.Value;
                var key = x.Key;
                if (security.Type == SecurityType.Option && key.value.Contains(quoteCurrency) && security.holdings.Invested)
                {
                    marigin += GetOptionInitialMargin(portfolio,security);
                }
            }

            return marigin;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="portfolio"></param>
        /// <param name="security"></param>
        /// <returns></returns>
        public static decimal GetOptionInitialMargin(SecurityPortfolioManager portfolio, Security security, int orderId = int.MaxValue)
        {
            decimal marigin = 0;
            //挂单保证金 + 持仓保证金
            var orders = portfolio.Transactions.GetOpenOrders(x => x.Symbol == security.Symbol);

            if (security.Holdings.Invested)
            {
                decimal holdingMarigin = security.Holdings.AbsoluteQuantity * ((DeribitOptionMarginModel)security.BuyingPowerModel).GetUnitInitialMargin(security, security.Holdings.Quantity, false);
                marigin += holdingMarigin;

                decimal reverseQuantity = 0;
                foreach (var order in orders)
                {
                    //if (orderId != int.MaxValue && orderId == order.id)
                    //{
                    //    continue;
                    //}
                    //反向单
                    if (security.Holdings.Quantity * order.Quantity < 0)
                    {
                        reverseQuantity += order.Quantity;
                    }
                    else//同向单 按照公式计算
                    {
                        decimal orderMarigin = order.AbsoluteQuantity * ((DeribitOptionMarginModel)security.BuyingPowerModel).GetUnitInitialMargin(security, order.Quantity, true);
                        marigin += orderMarigin;
                    }
                }

                //如果反向挂单比持仓多 算多出的保证金
                if (Math.Abs(security.Holdings.Quantity) < Math.Abs(reverseQuantity))
                {
                    decimal quantity = (Math.Abs(reverseQuantity) - Math.Abs(security.Holdings.Quantity)) * Math.Sign(security.Holdings.Quantity) *(-1);

                    quantity = Math.Sign(security.Holdings.Quantity) * (-1) * quantity;
                    decimal orderMarigin = quantity * ((DeribitOptionMarginModel)security.BuyingPowerModel).GetUnitInitialMargin(security, quantity, true);
                    marigin += orderMarigin;
                }
            }
            else //没有持仓 只按公式计算
            {
                foreach (var order in orders)
                {
                    decimal orderMarigin = order.AbsoluteQuantity * ((DeribitOptionMarginModel)security.BuyingPowerModel).GetUnitInitialMargin(security, order.Quantity, true);
                    marigin += orderMarigin;
                }
            }

            return marigin;
        }
    }
}
