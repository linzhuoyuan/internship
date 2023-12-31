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
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Util;

namespace QuantConnect.Securities
{

    /// <summary>
    /// Provides a base class for all buying power models
    /// </summary>
    public class BuyingPowerModel : IBuyingPowerModel
    {
        private decimal _initialMarginRequirement;
        private decimal _maintenanceMarginRequirement;
        /// <summary>
        /// The percentage used to determine the required unused buying power for the account.
        /// </summary>
        protected decimal RequiredFreeBuyingPowerPercent;

        /// <summary>
        /// Initializes a new instance of the <see cref="BuyingPowerModel"/> with no leverage (1x)
        /// </summary>
        public BuyingPowerModel()
            : this(1m)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BuyingPowerModel"/>
        /// </summary>
        /// <param name="initialMarginRequirement">The percentage of an order's absolute cost
        /// that must be held in free cash in order to place the order</param>
        /// <param name="maintenanceMarginRequirement">The percentage of the holding's absolute
        /// cost that must be held in free cash in order to avoid a margin call</param>
        /// <param name="requiredFreeBuyingPowerPercent">The percentage used to determine the required
        /// unused buying power for the account.</param>
        public BuyingPowerModel(
            decimal initialMarginRequirement,
            decimal maintenanceMarginRequirement,
            decimal requiredFreeBuyingPowerPercent
            )
        {
            if (initialMarginRequirement < 0 || initialMarginRequirement > 1)
            {
                throw new ArgumentException("Initial margin requirement must be between 0 and 1");
            }

            if (maintenanceMarginRequirement < 0 || maintenanceMarginRequirement > 1)
            {
                throw new ArgumentException("Maintenance margin requirement must be between 0 and 1");
            }

            if (requiredFreeBuyingPowerPercent < 0 || requiredFreeBuyingPowerPercent > 1)
            {
                throw new ArgumentException("Free Buying Power Percent requirement must be between 0 and 1");
            }

            _initialMarginRequirement = initialMarginRequirement;
            _maintenanceMarginRequirement = maintenanceMarginRequirement;
            RequiredFreeBuyingPowerPercent = requiredFreeBuyingPowerPercent;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BuyingPowerModel"/>
        /// </summary>
        /// <param name="leverage">The leverage</param>
        /// <param name="requiredFreeBuyingPowerPercent">The percentage used to determine the required
        /// unused buying power for the account.</param>
        public BuyingPowerModel(decimal leverage, decimal requiredFreeBuyingPowerPercent = 0)
        {
            if (leverage < 1)
            {
                throw new ArgumentException("Leverage must be greater than or equal to 1.");
            }

            if (requiredFreeBuyingPowerPercent < 0 || requiredFreeBuyingPowerPercent > 1)
            {
                throw new ArgumentException("Free Buying Power Percent requirement must be between 0 and 1");
            }

            _initialMarginRequirement = 1 / leverage;
            _maintenanceMarginRequirement = 1 / leverage;
            RequiredFreeBuyingPowerPercent = requiredFreeBuyingPowerPercent;
        }

        /// <summary>
        /// Gets the current leverage of the security
        /// </summary>
        /// <param name="security">The security to get leverage for</param>
        /// <returns>The current leverage in the security</returns>
        public virtual decimal GetLeverage(Security security)
        {
            return 1 / GetMaintenanceMarginRequirement(security);
        }

        /// <summary>
        /// Sets the leverage for the applicable securities, i.e, equities
        /// </summary>
        /// <remarks>
        /// This is added to maintain backwards compatibility with the old margin/leverage system
        /// </remarks>
        /// <param name="security"></param>
        /// <param name="leverage">The new leverage</param>
        public virtual void SetLeverage(Security security, decimal leverage)
        {
            if (leverage < 1)
            {
                throw new ArgumentException("Leverage must be greater than or equal to 1.");
            }

            var margin = 1 / leverage;
            _initialMarginRequirement = margin;
            _maintenanceMarginRequirement = margin;
        }

        /// <summary>
        /// Gets the total margin required to execute the specified order in units of the account currency including fees
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the order</param>
        /// <returns>The total margin in terms of the currency quoted in the order</returns>
        protected virtual decimal GetInitialMarginRequiredForOrder(
            InitialMarginRequiredForOrderParameters parameters)
        {
            //Get the order value from the non-abstract order classes (MarketOrder, LimitOrder, StopMarketOrder)
            //Market order is approximated from the current security price and set in the MarketOrder Method in QCAlgorithm.

            var fees = parameters.Security.FeeModel.GetOrderFee(
                new OrderFeeParameters(parameters.Security,
                    parameters.Order)).Value;
            var feesInAccountCurrency = parameters.CurrencyConverter.
                ConvertToAccountCurrency(fees).Amount;

            var orderValue = parameters.Order.GetValue(parameters.Security)
                * GetInitialMarginRequirement(parameters.Security);
            return orderValue + Math.Sign(orderValue) * feesInAccountCurrency;
        }

        /// <summary>
        /// Gets the margin currently allotted to the specified holding  维持保证金 = 持仓成本*维持保证金比例
        /// </summary>
        /// <param name="security">The security to compute maintenance margin for</param>
        /// <returns>The maintenance margin required for the </returns>
        protected virtual decimal GetMaintenanceMargin(Security security)
        {
            //return security.Holdings.AbsoluteHoldingsCost * GetMaintenanceMarginRequirement(security);
            return (security.Holdings.AbsoluteHoldingsCost + security.LongHoldings.AbsoluteHoldingsCost + security.ShortHoldings.AbsoluteHoldingsCost) * GetMaintenanceMarginRequirement(security);
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
        protected virtual decimal GetMarginRemaining(
            SecurityPortfolioManager portfolio,
            Security security,
            OrderDirection direction,
            OrderOffset offset = OrderOffset.None,
            long orderId = 0)
        {
            //总资产=结算账户所有剩余资金 + 未结算账户所有剩余资金 + (证券+期权)的持仓市值 +（期货+cfd)盈亏
            var totalPortfolioValue = portfolio.TotalPortfolioValue;
            //剩余保证金（包含持仓资产）= 总资产 - 未结算账单 - 总维持保证金
            var result = portfolio.GetMarginRemaining(totalPortfolioValue);

            if (offset == OrderOffset.None)
            {
                if (direction != OrderDirection.Hold)
                {
                    var holdings = security.Holdings;
                    //If the order is in the same direction as holdings, our remaining cash is our cash
                    //In the opposite direction, our remaining cash is 2 x current value of assets + our cash
                    if (holdings.IsLong)
                    {
                        switch (direction)
                        {
                            case OrderDirection.Sell:
                                result +=
                                    // portion of margin to close the existing position 维持保证金
                                    GetMaintenanceMargin(security) +
                                    // portion of margin to open the new position  初始保证金
                                    security.Holdings.AbsoluteHoldingsValue * GetInitialMarginRequirement(security);
                                break;
                        }
                    }
                    else if (holdings.IsShort)
                    {
                        switch (direction)
                        {
                            case OrderDirection.Buy:
                                result +=
                                    // portion of margin to close the existing position
                                    GetMaintenanceMargin(security) +
                                    // portion of margin to open the new position
                                    security.Holdings.AbsoluteHoldingsValue * GetInitialMarginRequirement(security);
                                break;
                        }
                    }
                }
                //结果 = 剩余保证金（包含持仓资产） - 总资产*预留比例
                result -= totalPortfolioValue * RequiredFreeBuyingPowerPercent;
            }
            return result < 0 ? 0 : result;
        }



        ///// <summary>
        ///// Gets the margin cash available for a trade 剩余保证金
        ///// </summary>
        ///// <param name="portfolio">The algorithm's portfolio</param>
        ///// <param name="security">The security to be traded</param>
        ///// <param name="direction">The direction of the trade</param>
        ///// <param name="offset">The offset of the trade</param>
        ///// <returns>The margin available for the trade</returns>
        //protected virtual decimal GetMarginRemaining(SecurityPortfolioManager portfolio, Security security, OrderDirection direction, OrderOffset offset = OrderOffset.None)
        //{
        //    var totalPortfolioValue = portfolio.GetTotalPortfolioValueForCurrency(security.QuoteCurrency.Symbol);
        //    var result = portfolio.GetMarginRemainingForCurrency(security.QuoteCurrency.Symbol, totalPortfolioValue);
        //    return result;
        //}

        /// <summary>
        /// The percentage of an order's absolute cost that must be held in free cash in order to place the order
        /// </summary>
        protected virtual decimal GetInitialMarginRequirement(Security security)
        {
            return _initialMarginRequirement;
        }

        /// <summary>
        /// The percentage of the holding's absolute cost that must be held in free cash in order to avoid a margin call
        /// </summary>
        public virtual decimal GetMaintenanceMarginRequirement(Security security)
        {
            return _maintenanceMarginRequirement;
        }

        /// <summary>
        /// Check if there is sufficient buying power to execute this order.
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the order</param>
        /// <returns>Returns buying power information for an order</returns>
        public virtual HasSufficientBuyingPowerForOrderResult HasSufficientBuyingPowerForOrder(HasSufficientBuyingPowerForOrderParameters parameters)
        {
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
                var option = (Option.Option)parameters.Security;
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
            //if (SupportOffset.IsSupportOffset(parameters.Security.Symbol) && parameters.Portfolio.Transactions.LiveMode)
            if (parameters.Order.Offset != OrderOffset.None && parameters.Portfolio.Transactions.LiveMode)
            {
                // When order only reduces or closes a security position, capital is always sufficient 持仓和订单方向相反，且持仓大于订单，资金总是充足的
                // 平仓卖出
                if ((parameters.Order.Quantity < 0 && parameters.Order.Offset == OrderOffset.Close) &&
                    Math.Abs(parameters.Security.LongHoldings.Quantity) >= Math.Abs(parameters.Order.Quantity))
                {
                    return new HasSufficientBuyingPowerForOrderResult(true);
                }
                // 平仓买入
                if ((parameters.Order.Quantity > 0 && parameters.Order.Offset == OrderOffset.Close) &&
                    Math.Abs(parameters.Security.ShortHoldings.Quantity) >= Math.Abs(parameters.Order.Quantity))
                {
                    return new HasSufficientBuyingPowerForOrderResult(true);
                }

                freeMargin = GetMarginRemaining(parameters.Portfolio, parameters.Security, parameters.Order.Direction, parameters.Order.Offset);
                initialMarginRequiredForOrder = GetInitialMarginRequiredForOrder(
                    new InitialMarginRequiredForOrderParameters(parameters.Portfolio.CashBook,
                        parameters.Security,
                        parameters.Order));
            }
            else
            {
                // When order only reduces or closes a security position, capital is always sufficient 持仓和订单方向相反，且持仓大于订单，资金总是充足的
                if (parameters.Security.Holdings.Quantity * parameters.Order.Quantity < 0 && Math.Abs(parameters.Security.Holdings.Quantity) >= Math.Abs(parameters.Order.Quantity))
                {
                    return new HasSufficientBuyingPowerForOrderResult(true);
                }

                freeMargin = GetMarginRemaining(parameters.Portfolio, parameters.Security, parameters.Order.Direction);
                initialMarginRequiredForOrder = GetInitialMarginRequiredForOrder(
                    new InitialMarginRequiredForOrderParameters(parameters.Portfolio.CashBook,
                        parameters.Security,
                        parameters.Order));
            }

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

            return new HasSufficientBuyingPowerForOrderResult(true);
        }

        /// <summary>
        /// Get the maximum market order quantity to obtain a position with a given value in account currency.
        /// Will not take into account buying power.
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the target percentage holdings</param>
        /// <returns>Returns the maximum allowed market order quantity and if zero, also the reason</returns>
        public virtual GetMaximumOrderQuantityForTargetValueResult GetMaximumOrderQuantityForTargetValue(GetMaximumOrderQuantityForTargetValueParameters parameters)
        {
            // this is expensive so lets fetch it once
            var totalPortfolioValue = parameters.Portfolio.TotalPortfolioValue;

            // adjust target portfolio value to comply with required Free Buying Power Percent
            var targetPortfolioValue =
                parameters.Target * (totalPortfolioValue - totalPortfolioValue * RequiredFreeBuyingPowerPercent);

            // if targeting zero, simply return the negative of the quantity
            if (targetPortfolioValue == 0)
            {
                return new GetMaximumOrderQuantityForTargetValueResult(-parameters.Security.Holdings.Quantity, string.Empty, false);
            }

            var currentHoldingsValue = parameters.Security.Holdings.HoldingsValue;

            // remove directionality, we'll work in the land of absolutes
            var targetOrderValue = Math.Abs(targetPortfolioValue - currentHoldingsValue);
            var direction = targetPortfolioValue > currentHoldingsValue ? OrderDirection.Buy : OrderDirection.Sell;

            // determine the unit price in terms of the account currency
            var utcTime = parameters.Security.LocalTime.ConvertToUtc(parameters.Security.Exchange.TimeZone);
            var unitPrice = new MarketOrder(parameters.Security.Symbol, 1, utcTime).GetValue(parameters.Security);
            if (unitPrice == 0)
            {
                var reason = $"The price of the {parameters.Security.Symbol.Value} security is zero because it does not have any market " +
                    "data yet. When the security price is set this security will be ready for trading.";
                return new GetMaximumOrderQuantityForTargetValueResult(0, reason);
            }

            // calculate the total margin available
            var marginRemaining = GetMarginRemaining(parameters.Portfolio, parameters.Security, direction);
            if (marginRemaining <= 0)
            {
                var reason = "The portfolio does not have enough margin available.";
                return new GetMaximumOrderQuantityForTargetValueResult(0, reason);
            }

            // continue iterating while we do not have enough margin for the order
            decimal orderValue = 0;
            decimal orderFees = 0;
            // compute the initial order quantity
            var orderQuantity = targetOrderValue / unitPrice;

            // rounding off Order Quantity to the nearest multiple of Lot Size
            orderQuantity -= orderQuantity % parameters.Security.SymbolProperties.LotSize;
            if (orderQuantity == 0)
            {
                var reason = $"The order quantity is less than the lot size of {parameters.Security.SymbolProperties.LotSize} " +
                    "and has been rounded to zero.";
                return new GetMaximumOrderQuantityForTargetValueResult(0, reason, false);
            }

            var loopCount = 0;
            // Just in case...
            var lastOrderQuantity = 0m;
            do
            {
                // Each loop will reduce the order quantity based on the difference between orderValue and targetOrderValue
                if (orderValue > targetOrderValue)
                {
                    var currentOrderValuePerUnit = orderValue / orderQuantity;
                    var amountOfOrdersToRemove = (orderValue - targetOrderValue) / currentOrderValuePerUnit;
                    if (amountOfOrdersToRemove < parameters.Security.SymbolProperties.LotSize)
                    {
                        // we will always substract at leat 1 LotSize
                        amountOfOrdersToRemove = parameters.Security.SymbolProperties.LotSize;
                    }

                    orderQuantity -= amountOfOrdersToRemove;
                    orderQuantity -= orderQuantity % parameters.Security.SymbolProperties.LotSize;
                }

                if (orderQuantity <= 0)
                {
                    var reason = $"The order quantity is less than the lot size of {parameters.Security.SymbolProperties.LotSize} " +
                        $"and has been rounded to zero.Target order value {targetOrderValue}. Order fees " +
                        $"{orderFees}. Order quantity {orderQuantity}.";
                    return new GetMaximumOrderQuantityForTargetValueResult(0, reason);
                }

                // generate the order
                var order = new MarketOrder(parameters.Security.Symbol, orderQuantity, utcTime);

                var fees = parameters.Security.FeeModel
                    .GetOrderFee(new OrderFeeParameters(parameters.Security, order))
                    .Value;
                orderFees = parameters.Portfolio.CashBook.ConvertToAccountCurrency(fees).Amount;

                // The TPV, take out the fees(unscaled) => yields available value for trading(less fees)
                // then scale that by the target -- finally remove currentHoldingsValue to get targetOrderValue
                targetOrderValue = Math.Abs(
                    (totalPortfolioValue - orderFees - totalPortfolioValue * RequiredFreeBuyingPowerPercent)
                    * parameters.Target - currentHoldingsValue
                );

                // After the first loop we need to recalculate order quantity since now we have fees included
                if (loopCount == 0)
                {
                    // re compute the initial order quantity
                    orderQuantity = targetOrderValue / unitPrice;
                    orderQuantity -= orderQuantity % parameters.Security.SymbolProperties.LotSize;
                }
                else
                {
                    // Start safe check after first loop
                    if (lastOrderQuantity == orderQuantity)
                    {
                        var message = "GetMaximumOrderQuantityForTargetValue failed to converge to target order value " +
                            $"{targetOrderValue}. Current order value is {orderValue}. Order quantity {orderQuantity}. " +
                            $"Lot size is {parameters.Security.SymbolProperties.LotSize}. Order fees {orderFees}. Security symbol " +
                            $"{parameters.Security.Symbol}";
                        throw new Exception(message);
                    }

                    lastOrderQuantity = orderQuantity;
                }

                orderValue = orderQuantity * unitPrice;
                loopCount++;
                // we always have to loop at least twice
            }
            while (loopCount < 2 || orderValue > targetOrderValue);

            // add directionality back in
            return new GetMaximumOrderQuantityForTargetValueResult((direction == OrderDirection.Sell ? -1 : 1) * orderQuantity);
        }

        /// <summary>
        /// Gets the amount of buying power reserved to maintain the specified position 维持保证金 = 持仓成本*维持保证金比例
        /// </summary>
        /// <param name="parameters">A parameters object containing the security</param>
        /// <returns>The reserved buying power in account currency</returns>
        public virtual ReservedBuyingPowerForPosition GetReservedBuyingPowerForPosition(ReservedBuyingPowerForPositionParameters parameters)
        {
            var maintenanceMargin = GetMaintenanceMargin(parameters.Security);
            return parameters.ResultInAccountCurrency(maintenanceMargin);
        }

        /// <summary>
        /// Gets the buying power available for a trade
        /// </summary>
        /// <param name="parameters">A parameters object containing the algorithm's potrfolio, security, and order direction</param>
        /// <returns>The buying power available for the trade</returns>
        public virtual BuyingPower GetBuyingPower(BuyingPowerParameters parameters)
        {
            var marginRemaining = GetMarginRemaining(parameters.Portfolio, parameters.Security, parameters.Direction, parameters.Offset, parameters.OrderId);
            return parameters.ResultInAccountCurrency(marginRemaining);
        }


        /////////////////////////////////////////////////////////额外扩展//////////////////////////////////////////////////////////
        /// <summary>
        /// 
        /// </summary>
        /// <param name="portfolio"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public virtual decimal GetFrozenAmount(SecurityPortfolioManager portfolio, Func<Order, bool> filter = null)
        {
            decimal sum = 0;
            var orders = portfolio.Transactions.GetOpenOrders(filter);

            foreach (var order in orders)
            {
                // 计算每个单子的初始保证金
                var rate = (order.AbsoluteQuantity - Math.Abs(order.FillQuantity)) / order.AbsoluteQuantity;
                var security = portfolio.Securities[order.Symbol];
                var orderValue = order.GetValue(security)
               * GetInitialMarginRequirement(security) * rate;
                sum += orderValue;
            }
            return sum;
        }
    }
}