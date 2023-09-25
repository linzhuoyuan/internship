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
using QuantConnect.Util;
using QuantConnect.Securities.Option;

namespace QuantConnect.Securities.Future
{
    public class DeribitFutureMarginModel : SecurityMarginModel
    {
        private decimal _btc_perpetual_leverage = 100;
        private decimal _eth_perpetual_leverage = 50;
        private List<Order> _tempOrders = new List<Order>();

        public static bool IsPerpetual(string symbol)
        {
            if(symbol.Contains("PERPETUAL") && !symbol.Contains("/"))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="requiredFreeBuyingPowerPercent"></param>
        public DeribitFutureMarginModel(decimal requiredFreeBuyingPowerPercent = 0)
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
            if(IsPerpetual(security.Symbol.Value))
            {
                if(security.Symbol.Value.Contains("BTC"))
                {
                    return _btc_perpetual_leverage;
                }
                else if (security.Symbol.Value.Contains("ETH"))
                {
                    return _eth_perpetual_leverage;
                }
            }

            return 1;
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
            throw new InvalidOperationException("Futures are leveraged products and different leverage cannot be set by user");
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

            /*
            var quoteCurrency = parameters.Order.Symbol.Value.Contains("BTC") ? "BTC" : "ETH";

            var orders1 = parameters.Portfolio.Transactions.GetOpenOrders(x=>x.Id < parameters.Order.Id &&
                   x.SecurityType == SecurityType.Future && x.Symbol.Value.Contains(quoteCurrency));

            var s1 = SummerInitialMargin(parameters.Portfolio, parameters.Security, orders1);


            var orders2 = new List<Order>(orders1);
            orders2.Add(parameters.Order);
            var s2 = SummerInitialMargin(parameters.Portfolio, parameters.Security, orders2);

            var m = s2 - s1;

            return m;
            */ 

            var orderValue = parameters.Order.AbsoluteQuantity * GetInitialMarginRequirement(parameters.Security, parameters.Order.AbsoluteQuantity);
            return orderValue + Math.Abs(feesInAccountCurrency);
        }

        /// <summary>
        /// Gets the margin currently alloted to the specified holding  维持保证金 = 持仓成本*维持保证金比例
        /// </summary>
        /// <param name="security">The security to compute maintenance margin for</param>
        /// <returns>The maintenance margin required for the </returns>
        protected override decimal GetMaintenanceMargin(Security security)
        {
            return security.Holdings.AbsoluteQuantity * GetMaintenanceMarginRequirement(security);
        }

        /// <summary>
        /// The percentage of an order's absolute cost that must be held in free cash in order to place the order
        /// </summary>
        protected override decimal GetInitialMarginRequirement(Security security)
        {

            throw new Exception("Deribit Future GetInitialMarginRequirement");
        }

        /// <summary>
        /// The percentage of the holding's absolute cost that must be held in free cash in order to avoid a margin call
        /// </summary>
        public override decimal GetMaintenanceMarginRequirement(Security security)
        {
            if (security.Symbol.Value.Contains("/")) return 0;
            
            decimal rate = 0;
            if (IsPerpetual(security.Symbol.Value))
            {
                if (security.Symbol.Value.Contains("BTC"))
                {
                    var btcNumber = security.Holdings.AbsoluteQuantity / security.Cache.MarkPrice;
                    rate = (decimal)0.00575 + (btcNumber / 100) * (decimal)0.005;
                }
                else if (security.Symbol.Value.Contains("ETH"))
                {
                    var ethNumber = security.Holdings.AbsoluteQuantity / security.Cache.MarkPrice;
                    rate = (decimal)0.00575 + (ethNumber / 5000) * (decimal)0.01;
                }
            }
            return rate;
        }


        public static decimal GetInitialMarginRequirement(Security security, decimal quantity)
        {
            if (security.Symbol.Value.Contains("/")) return 0;

            decimal rate = 0;
            try
            {
                if (IsPerpetual(security.Symbol.Value))
                {
                    if (security.Symbol.Value.Contains("BTC"))
                    {
                        var btcNumber = quantity / security.Cache.MarkPrice;
                        rate = (decimal)0.01 + (btcNumber / 100) * (decimal)0.005;
                    }
                    else if (security.Symbol.Value.Contains("ETH"))
                    {
                        var ethNumber = quantity / security.Cache.MarkPrice;
                        rate = (decimal)0.02 + (ethNumber / 5000) * (decimal)0.01;
                    }
                }
            }
            catch (DivideByZeroException ex)
            {
                Log.Error($"GetInitialMarginRequirement: Mark price is {security.Cache.MarkPrice}");
            }
            
            return rate;
        }

        public static decimal GetMaintenanceMarginRequirement(Security security, decimal quantity)
        {
            if (security.Symbol.Value.Contains("/")) return 0;

            decimal rate = 0;
            if (IsPerpetual(security.Symbol.Value))
            {
                if (security.Symbol.Value.Contains("BTC"))
                {
                    var btcNumber = quantity / security.Cache.MarkPrice;
                    rate = (decimal)0.00575 + (btcNumber / 100) * (decimal)0.005;
                }
                else if (security.Symbol.Value.Contains("ETH"))
                {
                    var ethNumber = quantity / security.Cache.MarkPrice;
                    rate = (decimal)0.01 + (ethNumber / 5000) * (decimal)0.01;
                }
            }
            return rate;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="portfolio"></param>
        /// <param name="security"></param>
        /// <param name="direction"></param>
        /// <param name="offset"></param>
        /// <param name="orderId"></param>
        /// <returns></returns>
        protected override decimal GetMarginRemaining(SecurityPortfolioManager portfolio, Security security, OrderDirection direction, OrderOffset offset = OrderOffset.None, long orderId = long.MaxValue)
        {
            decimal result = 0;
            var quoteCurrency = security.Symbol.Value.Contains("BTC") ? "BTC" : "ETH";

            var total = portfolio.GetTotalPortfolioValueForCurrency(quoteCurrency);
            result =portfolio.GetMarginRemainingForCurrency(quoteCurrency, total,orderId);
            return result;
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


            decimal freeMargin = 0;
            decimal initialMarginRequiredForOrder = 0;

            //When order only reduces or closes a security position, capital is always sufficient 持仓和订单方向相反，且持仓大于订单，资金总是充足的
            //if (parameters.Security.Holdings.Quantity * parameters.Order.Quantity < 0 && Math.Abs(parameters.Security.Holdings.Quantity) >= Math.Abs(parameters.Order.Quantity))
            //{
            //    return new HasSufficientBuyingPowerForOrderResult(true);
            //}

            //反向
            if (parameters.Security.Holdings.Quantity * parameters.Order.Quantity < 0)
            {
                var orders = parameters.Portfolio.Transactions.GetOpenOrders(x => x.symbol==parameters.Order.Symbol && x.Direction == parameters.Order.Direction).ToList();

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

        //======================================================================================================================================
        /// <summary>
        /// 使用方法：markPrice截取，markPrice以下的按照降序 markPrice以上的按照升序
        /// </summary>
        /// <param name="list"></param>
        /// <param name="asc"></param>
        /// <param name="markPrice"></param>
        /// <returns></returns>
        public static List<Order> OrderSort(List<Order> list, bool asc,decimal markPrice)
        {
            int flag = asc ? 1 : -1;
            list.Sort((x, y) => flag * x.Price.CompareTo(y.Price));
            if (asc)
            {
                return list.Where(x => x.Price > markPrice).ToList();
            }
            else
            {
                return list.Where(x => x.Price < markPrice).ToList();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="quantityLast"></param>
        /// <param name="priceLast"></param>
        /// <param name="priceNext"></param>
        /// <returns></returns>
        public static decimal CalculateProfit(decimal quantityLast, decimal priceLast, decimal priceNext)
        {
            decimal profit = 0;
            profit = ((quantityLast / priceLast) - (quantityLast / priceNext));
            Log.Trace($"profit= (({quantityLast}/{priceLast})-({quantityLast}/{priceNext})) ={profit}");
            return profit;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="price"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        public static decimal InitMariginHolding(decimal price,decimal quantity)
        {
            Log.Trace($"InitMariginHolding=((1 + 0.005 * {quantity} / {price}) / 100 )* ({quantity} / {price})");
            return ((1 + (decimal)0.005 * quantity / price) / 100 )* (quantity / price);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="priceLast"></param>
        /// <param name="quantityLast"></param>
        /// <param name="priceNext"></param>
        /// <param name="quantityNext"></param>
        /// <param name="initialMarginLast"></param>
        /// <returns></returns>
        public static decimal InitialMarginNextOrder(decimal priceLast, decimal quantityLast, decimal priceNext, decimal quantityNext, decimal initialMarginLast)
        {
            //计算盈亏
            var pnl = CalculateProfit(quantityLast, priceLast, priceNext);
            //计算持有保证金
            var initialMarginNow = InitMariginHolding(priceNext, (quantityLast + quantityNext));
            //计算初始保证金
            var initialMarginNext = initialMarginNow - pnl - initialMarginLast;
            Log.Trace($"initialMarginNext({initialMarginNext}) = initialMarginNow({initialMarginNow}) - pnl({pnl}) - initialMarginLast({initialMarginLast})");
            if (initialMarginNext < 0)
                return 0;

            return initialMarginNext;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        public static decimal GetOrderPrice(Order order)
        {
            switch (order.Type)
            {
                case OrderType.Market:
                case OrderType.StopMarket:
                case OrderType.MarketOnClose:
                case OrderType.MarketOnOpen:
                    return order.Price;
                case OrderType.Limit:
                    var limitOrder = order as LimitOrder;
                    return limitOrder.LimitPrice;
                case OrderType.StopLimit:
                    var stopLimitOrder = order as StopLimitOrder;
                    return stopLimitOrder.LimitPrice;
                default:
                    return order.Price;
            }
        }


        public static decimal SummerInitialMargin(SecurityPortfolioManager portfolio,Security security,List<Order> orders)
        {
            decimal sum = 0;
            decimal markPrice = security.Cache.MarkPrice;
            decimal temp_Price = markPrice;
            decimal temp_Quantity = 0;
            decimal hold_Quantity = 0;


            if (security.Holdings.Invested)
            {
                temp_Price = markPrice;
                temp_Quantity = security.Holdings.Quantity;
                hold_Quantity = security.Holdings.Quantity;
            }

            var temp_InitialMargin = InitMariginHolding(temp_Price, temp_Quantity);
            var holding_InitialMargin = temp_InitialMargin;
            sum += holding_InitialMargin;
            Log.Trace($"Price:{temp_Price} Quantity{temp_Quantity} holding_InitialMargin({holding_InitialMargin})");
            bool asc = true;
            //var orders = portfolio.Transactions.GetOpenOrders();
            var orderDown  = OrderSort(orders, !asc, markPrice); //desc sort
            var orderUp = OrderSort(orders, asc, markPrice); //asc sort

            foreach (var item in orderUp)
            {
                var quantityNext = item.Quantity;
                var priceNext = GetOrderPrice(item);
                temp_InitialMargin = InitialMarginNextOrder(temp_Price, temp_Quantity, priceNext, quantityNext, temp_InitialMargin);
                Log.Trace($"Price:{priceNext} Quantity{quantityNext} holding_InitialMargin({holding_InitialMargin})");
                temp_Price = priceNext;
                temp_Quantity += quantityNext;
                sum += temp_InitialMargin;
            }

            temp_Price = markPrice;
            temp_Quantity = hold_Quantity;


            foreach (var item in orderDown)
            {
                var quantityNext = item.Quantity;
                var priceNext = GetOrderPrice(item);
                temp_InitialMargin = InitialMarginNextOrder(temp_Price, temp_Quantity, priceNext, quantityNext, temp_InitialMargin);
                Log.Trace($"Price:{priceNext} Quantity{quantityNext} holding_InitialMargin({holding_InitialMargin})");
                temp_Price = priceNext;
                temp_Quantity += quantityNext;
                sum += temp_InitialMargin;
            }

            System.Diagnostics.Debug.WriteLine($"SummerInitialMargin sum ={sum}(BTC)");
            var result = sum * security.Cache.MarkPrice;
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="portfolio"></param>
        /// <param name="quoteCurrency"></param>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public static decimal GetFutureTotalInitialMargin(SecurityPortfolioManager portfolio, string quoteCurrency, long orderId = long.MaxValue)
        {
            decimal result = 0;

            var security = portfolio.Securities
                .Where(x => x.Key.Value.Contains("PERPETUAL") 
                                                           && x.Key.Value.Contains(quoteCurrency) 
                                                           && !x.Key.Value.Contains("/"))
                .Select(x => x.Value)
                .FirstOrDefault();
            if (security != null)
            {
                result = GetFutureInitialMargin(portfolio, security);
            }
            return result;
        }


        public static decimal GetFutureInitialMargin(SecurityPortfolioManager portfolio,Security security,int orderId = int.MaxValue)
        {
            //同一合约的订单
            decimal margin = 0;
            var orders = portfolio.Transactions.GetOpenOrders(x => x.symbol ==security.symbol);

            if (security.Holdings.Invested)
            {
                decimal holdingMargin = security.Holdings.AbsoluteQuantity * GetInitialMarginRequirement(security, security.Holdings.AbsoluteQuantity);
                margin += holdingMargin;


                decimal reverseQuantity = 0;
                foreach (var order in orders)
                {
                    if (order is StopLimitOrder)
                    {
                        var stopOrder = order as StopLimitOrder;
                        if (!stopOrder.StopTriggered)
                        {
                            continue;
                        }
                    }

                    if (order is StopMarketOrder)
                    {
                        if (order.brokerId.Count < 2)
                        {
                            continue;
                        }
                    }
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
                        decimal orderMargin = order.AbsoluteQuantity * GetInitialMarginRequirement(security, order.AbsoluteQuantity);
                        margin += orderMargin;
                    }
                }

                //如果反向挂单比持仓多 算多出的保证金
                if (Math.Abs(security.Holdings.Quantity) < Math.Abs(reverseQuantity))
                {
                    decimal quantity = Math.Abs(reverseQuantity) - Math.Abs(security.Holdings.Quantity);
                    decimal orderMargin = quantity * GetInitialMarginRequirement(security, quantity);
                    margin += orderMargin;
                }

            }
            else //没有持仓 只按公式计算
            {
                foreach (var order in orders)
                {
                    if (order is StopLimitOrder)
                    {
                        var stopOrdr = order as StopLimitOrder;
                        if (!stopOrdr.StopTriggered)
                        {
                            continue;
                        }
                    }

                    if (order is StopMarketOrder)
                    {
                        if (order.brokerId.Count < 2)
                        {
                            continue;
                        }
                    }

                    decimal orderMargin = order.AbsoluteQuantity * GetInitialMarginRequirement(security, order.AbsoluteQuantity);
                    margin += orderMargin;
                }
            }

            return margin;
        }
    }
}
