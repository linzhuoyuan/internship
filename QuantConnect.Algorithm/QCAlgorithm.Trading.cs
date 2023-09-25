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
using System.Linq;
using NodaTime;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Securities.Option;
using QuantConnect.Util;

namespace QuantConnect.Algorithm
{
    public partial class QCAlgorithm
    {
        private int _maxOrders = 10000;
        private bool _isMarketOnOpenOrderWarningSent = false;
        private bool _isMarkPriceReady = false;
        private PriorityQueue<SubmitOrderRequest, DateTime> _twapOrdersQueue;
        private HashSet<Symbol> _twapTradingSymbols;
        private object _twapLock;
        /// <summary>
        /// Transaction Manager - Process transaction fills and order management.
        /// </summary>
        public SecurityTransactionManager Transactions { get; set; }

        public void InitTwap(double twapMaxOpenOrderSeconds, int maxOrdersPerHalfSecond, int twapOrderQueueInitCapacity = 100, int maxNumberOfSymbols = 20)
        {
            _twapOrdersQueue = new PriorityQueue<SubmitOrderRequest, DateTime>(twapOrderQueueInitCapacity);
            _twapTradingSymbols = new HashSet<Symbol>(maxNumberOfSymbols);
            _twapLock = new object();
            Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromSeconds(0.5)), () =>
            {
                lock (_twapLock)
                {
                    foreach (var order in _twapTradingSymbols.SelectMany(s => Transactions.GetOpenOrders(s)))
                    {
                        if (order.CreatedTime < UtcTime.AddSeconds(-twapMaxOpenOrderSeconds))
                        {
                            Transactions.CancelOrder(order.Id);
                        }
                    } 

                    var i = 0;
                    while (_twapOrdersQueue.TryPeek(out _, out var time) && time <= UtcTime &&
                           i < maxOrdersPerHalfSecond)
                    {
                        var orderRequest = _twapOrdersQueue.Dequeue();
                        switch (orderRequest.OrderType)
                        {
                            case OrderType.Limit:
                                var limitPrice = orderRequest.LimitPrice == 0
                                    ? orderRequest.Quantity > 0
                                        ? Securities[orderRequest.Symbol].AskPrice
                                        : Securities[orderRequest.Symbol].BidPrice
                                    : orderRequest.LimitPrice;
                                LimitOrder(orderRequest.Symbol, orderRequest.Quantity, limitPrice, orderRequest.Tag);
                                break;
                            case OrderType.Market:
                                MarketOrder(orderRequest.Symbol, orderRequest.Quantity, tag: orderRequest.Tag);
                                break;
                            default:
                                throw new ArgumentException($"{orderRequest.OrderType} not supported for TWAP!!!");
                        }

                        i++;
                    }
                }
            });
            Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromSeconds(111.11)), () =>
            {
                lock (_twapLock)
                {
                    if (_twapTradingSymbols.Count >= Math.Floor(0.9 * maxNumberOfSymbols))
                    {
                        _twapTradingSymbols.EnsureCapacity(_twapTradingSymbols.Count + maxNumberOfSymbols);
                    }
                    if (_twapOrdersQueue.Count >= Math.Floor(0.9 * twapOrderQueueInitCapacity))
                    {
                        _twapOrdersQueue.EnsureCapacity(_twapOrdersQueue.Count + twapOrderQueueInitCapacity);
                    }
                }
            });
        }

        /// <summary>
        /// Place twap orders, only market and limit orders are allowed. Please NOTE that if you placed twap order or a symbol, you should not be placing other orders for the same symbol
        /// </summary>
        protected bool Twap(Symbol symbol, decimal quantityPerOrder, double intervalSeconds, double totalOrders,
            OrderType orderType, out string message, decimal limitPrice = 0, decimal stopPrice = 0, string tag = "")
        {
            if (_twapTradingSymbols is null)
            {
                throw new Exception(
                    $"Have to call InitTwap(double twapMaxOpenOrderTime, int twapOrderQueueInitCapacity = 100, int maxNumberOfSymbols = 20) in OnWarmUpFinished before using Twap order!!!");
            }

            lock (_twapLock)
            {
                if (_twapTradingSymbols.Add(symbol))
                {
                    var orderTime = UtcTime;
                    for (var i = 0; i < totalOrders; i++)
                    {
                        var order = new SubmitOrderRequest(orderType, symbol.SecurityType, symbol, quantityPerOrder,
                            stopPrice, limitPrice, UtcTime, tag);
                        _twapOrdersQueue.Enqueue(order, orderTime);
                        orderTime = orderTime.AddSeconds(intervalSeconds);
                    }

                    Schedule.On(DateRules.On(orderTime.Year, orderTime.Month, orderTime.Day),
                        TimeRules.At(orderTime.AddSeconds(0.5).TimeOfDay, DateTimeZone.Utc),
                        () =>
                        {
                            lock (_twapLock)
                            {
                                _twapTradingSymbols.Remove(symbol);
                            }
                        });
                    message =
                        $"placed {totalOrders} TWAP orders for {symbol.Value + "-" + symbol.SecurityType}, interval is {intervalSeconds} seconds, volume per order is {quantityPerOrder}, last order will be placed at {orderTime} UTC";
                    return true;
                }

                message = $"There is a twap for {symbol} in progress!";
                return false;
            }
        }

        protected bool IsTwapTrading(Symbol symbol)
        {
            lock (_twapLock)
            {
                return _twapTradingSymbols.Contains(symbol);
            }
        }


        /// <summary>
        /// Buy Stock (Alias of Order)
        /// </summary>
        /// <param name="symbol">string Symbol of the asset to trade</param>
        /// <param name="quantity">int Quantity of the asset to trade</param>
        /// <param name="idCallback"></param>
        /// <seealso cref="Buy(Symbol, double)"/>
        public IEnumerable<OrderTicket> Buy(Symbol symbol, int quantity, Action<long> idCallback = null)
        {
            return Order(symbol, (decimal)Math.Abs(quantity), idCallback);
        }

        /// <summary>
        /// Buy Stock (Alias of Order)
        /// </summary>
        /// <param name="symbol">string Symbol of the asset to trade</param>
        /// <param name="quantity">double Quantity of the asset to trade</param>
        /// <param name="idCallback"></param>
        /// <seealso cref="Buy(Symbol, decimal)"/>
        public IEnumerable<OrderTicket> Buy(Symbol symbol, double quantity, Action<long> idCallback = null)
        {
            return Order(symbol, (decimal)Math.Abs(quantity), idCallback);
        }

        /// <summary>
        /// Buy Stock (Alias of Order)
        /// </summary>
        /// <param name="symbol">string Symbol of the asset to trade</param>
        /// <param name="quantity">decimal Quantity of the asset to trade</param>
        /// <param name="idCallback"></param>
        /// <seealso cref="Order(Symbol, int)"/>
        public IEnumerable<OrderTicket> Buy(Symbol symbol, decimal quantity, Action<long> idCallback = null)
        {
            return Order(symbol, Math.Abs(quantity), idCallback);
        }

        /// <summary>
        /// Buy Stock (Alias of Order)
        /// </summary>
        /// <param name="symbol">string Symbol of the asset to trade</param>
        /// <param name="quantity">float Quantity of the asset to trade</param>
        /// <seealso cref="Buy(Symbol, decimal)"/>
        public IEnumerable<OrderTicket> Buy(Symbol symbol, float quantity, Action<long> idCallback = null)
        {
            return Order(symbol, (decimal)Math.Abs(quantity), idCallback);
        }

        /// <summary>
        /// Sell stock (alias of Order)
        /// </summary>
        /// <param name="symbol">string Symbol of the asset to trade</param>
        /// <param name="quantity">int Quantity of the asset to trade</param>
        /// <seealso cref="Sell(Symbol, decimal)"/>
        public IEnumerable<OrderTicket> Sell(Symbol symbol, int quantity, Action<long> idCallback = null)
        {
            return Order(symbol, (decimal)Math.Abs(quantity) * -1, idCallback);
        }

        /// <summary>
        /// Sell stock (alias of Order)
        /// </summary>
        /// <param name="symbol">String symbol to sell</param>
        /// <param name="quantity">Quantity to order</param>
        /// <returns>int Order Id.</returns>
        public IEnumerable<OrderTicket> Sell(Symbol symbol, double quantity, Action<long> idCallback = null)
        {
            return Order(symbol, (decimal)Math.Abs(quantity) * -1, idCallback);
        }

        /// <summary>
        /// Sell stock (alias of Order)
        /// </summary>
        /// <param name="symbol">String symbol</param>
        /// <param name="quantity">Quantity to sell</param>
        /// <returns>int order id</returns>
        public IEnumerable<OrderTicket> Sell(Symbol symbol, float quantity, Action<long> idCallback = null)
        {
            return Order(symbol, (decimal)Math.Abs(quantity) * -1m, idCallback);
        }

        /// <summary>
        /// Sell stock (alias of Order)
        /// </summary>
        /// <param name="symbol">String symbol to sell</param>
        /// <param name="quantity">Quantity to sell</param>
        /// <returns>Int Order Id.</returns>
        public IEnumerable<OrderTicket> Sell(Symbol symbol, decimal quantity, Action<long> idCallback = null)
        {
            return Order(symbol, Math.Abs(quantity) * -1, idCallback);
        }

        /// <summary>
        /// Issue an order/trade for asset: Alias wrapper for Order(string, int);
        /// </summary>
        /// <seealso cref="Order(Symbol, decimal)"/>
        public IEnumerable<OrderTicket> Order(Symbol symbol, double quantity, Action<long> idCallback = null)
        {
            return Order(symbol, (decimal)quantity, idCallback);
        }

        /// <summary>
        /// Issue an order/trade for asset
        /// </summary>
        /// <remarks></remarks>
        public IEnumerable<OrderTicket> Order(Symbol symbol, int quantity, Action<long> idCallback = null)
        {
            return MarketOrder(symbol, (decimal)quantity, idCallback: idCallback);
        }

        /// <summary>
        /// Issue an order/trade for asset
        /// </summary>
        /// <remarks></remarks>
        public IEnumerable<OrderTicket> Order(Symbol symbol, decimal quantity, Action<long> idCallback = null)
        {
            return MarketOrder(symbol, quantity, idCallback: idCallback);
        }

        /// <summary>
        /// Wrapper for market order method: submit a new order for quantity of symbol using type order.
        /// </summary>
        /// <param name="symbol">Symbol of the MarketType Required.</param>
        /// <param name="quantity">Number of shares to request.</param>
        /// <param name="asynchronous">Send the order asynchronously (false). Otherwise we'll block until it fills</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <seealso cref="MarketOrder(Symbol, decimal, bool, string)"/>
        public IEnumerable<OrderTicket> Order(Symbol symbol, decimal quantity, bool asynchronous = false, string tag = "", Action<long> idCallback = null)
        {
            return MarketOrder(symbol, quantity, asynchronous, tag, idCallback: idCallback);
        }

        /// <summary>
        /// Market order implementation: Send a market order and wait for it to be filled.
        /// </summary>
        /// <param name="symbol">Symbol of the MarketType Required.</param>
        /// <param name="quantity">Number of shares to request.</param>
        /// <param name="asynchronous">Send the order asynchronously (false). Otherwise we'll block until it fills</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <returns>int Order id</returns>
        public IEnumerable<OrderTicket> MarketOrder(Symbol symbol, int quantity, bool asynchronous = false, string tag = "", Action<long> idCallback = null)
        {
            return MarketOrder(symbol, (decimal)quantity, asynchronous, tag);
        }

        /// <summary>
        /// Market order implementation: Send a market order and wait for it to be filled.
        /// </summary>
        /// <param name="symbol">Symbol of the MarketType Required.</param>
        /// <param name="quantity">Number of shares to request.</param>
        /// <param name="asynchronous">Send the order asynchronously (false). Otherwise we'll block until it fills</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <returns>int Order id</returns>
        public IEnumerable<OrderTicket> MarketOrder(Symbol symbol, double quantity, bool asynchronous = false, string tag = "", Action<long> idCallback = null)
        {
            return MarketOrder(symbol, (decimal)quantity, asynchronous, tag);
        }

        /// <summary>
        /// MarketOrder is MarketOrder which is Commented out,
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="quantity"></param>
        /// <param name="asynchronous"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public IEnumerable<OrderTicket> MarketOrder(Symbol symbol, decimal quantity, bool asynchronous = false, string tag = "", Action<long> idCallback = null)
        {
            var orders = new List<OrderTicket>();

            //symbol belong to Europe and America 
            if (!SupportOffset.IsSupportOffset(symbol))
            {
                var order = MarketOrderPriv(symbol, quantity, OrderOffset.None, asynchronous, tag, idCallback);
                orders.Add(order);
                return orders;
            }

            //backtest mode
            if (!LiveMode)
            {
                var order = MarketOrderPriv(symbol, quantity, OrderOffset.None, asynchronous, tag, idCallback);
                orders.Add(order);
                return orders;
            }

            //配置不支持自动开平
            if (!AutoOpenClose)
            {
                var order = MarketOrderPriv(symbol, quantity, OrderOffset.None, asynchronous, tag, idCallback);
                orders.Add(order);
                return orders;
            }

            // chinese symbol ,offset==none,not open or close
            var security = Securities[symbol];
            //buy : first close short holding, second open buy order
            if (quantity > 0)
            {
                var value = security.ShortHoldings.GetOpenQuantity();
                value = Math.Abs(value);
                var closeValue = Math.Min(value, quantity);
                if (closeValue > 0)
                {
                    var order1 = CloseBuy(symbol, closeValue, asynchronous, tag);
                    orders.Add(order1);
                }

                if (value < quantity)
                {
                    var order2 = OpenBuy(symbol, quantity - value, asynchronous, tag);
                    orders.Add(order2);
                }
                return orders;
            }

            //sell : first close long holding, second open sell order
            if (quantity < 0)
            {
                quantity = Math.Abs(quantity);
                var value = security.LongHoldings.GetOpenQuantity();
                var closeValue = Math.Min(value, quantity);

                if (closeValue > 0)
                {
                    var closeOrder = CloseSell(symbol, closeValue, asynchronous, tag);
                    orders.Add(closeOrder);
                }
                if (value < quantity)
                {
                    var openOrder = OpenSell(symbol, quantity - value, asynchronous, tag);
                    orders.Add(openOrder);
                }
                return orders;
            }
            return orders;
        }

        /// <summary>
        /// Market order implementation: Send a market order and wait for it to be filled. replace public MarketOrder which be commented
        /// </summary>
        /// <param name="symbol">Symbol of the MarketType Required.</param>
        /// <param name="quantity">Number of shares to request.</param>
        /// <param name="offset">open or close for chinese order</param>
        /// <param name="asynchronous">Send the order asynchronously (false). Otherwise we'll block until it fills</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <param name="idCallback"></param>
        /// <returns>int Order id</returns>
        private OrderTicket MarketOrderPriv(
            Symbol symbol,
            decimal quantity,
            OrderOffset offset = OrderOffset.None,
            bool asynchronous = false,
            string tag = "",
            Action<long> idCallback = null)
        {
            var security = Securities[symbol];

            // check the exchange is open before sending a market order, if it's not open
            // then convert it into a market on open order
            if (!security.Exchange.ExchangeOpen)
            {
                var mooTicket = MarketOnOpenOrder(security.Symbol, quantity, offset, tag, idCallback);
                if (!_isMarketOnOpenOrderWarningSent)
                {
                    var anyNonDailySubscriptions = security.Subscriptions.Any(x => x.Resolution != Resolution.Daily);
                    if (mooTicket.SubmitRequest.Response.IsSuccess && !anyNonDailySubscriptions)
                    {
                        Debug("Warning: all market orders sent using daily data, or market orders sent after hours are automatically converted into MarketOnOpen orders.");
                        _isMarketOnOpenOrderWarningSent = true;
                    }
                }
                return mooTicket;
            }

            var request = CreateSubmitOrderRequest(OrderType.Market, security, quantity, tag, DefaultOrderProperties?.Clone(), offset);
            request.IdCallback = idCallback;

            // If warming up, do not submit
            if (IsWarmingUp)
            {
                return OrderTicket.InvalidWarmingUp(Transactions, request);
            }

            //Initialize the Market order parameters:
            var preOrderCheckResponse = PreOrderChecks(request);
            if (preOrderCheckResponse.IsError)
            {
                return OrderTicket.InvalidSubmitRequest(Transactions, request, preOrderCheckResponse);
            }

            //Add the order and create a new order Id.
            var ticket = Transactions.AddOrder(request);

            // Wait for the order event to process, only if the exchange is open
            if (!asynchronous)
            {
                Transactions.WaitForOrder(ticket.OrderId);
            }

            return ticket;
        }

        /// <summary>
        /// Market on open order implementation: Send a market order when the exchange opens
        /// </summary>
        /// <param name="symbol">The symbol to be ordered</param>
        /// <param name="quantity">The number of shares to required</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <returns>The order ID</returns>
        public OrderTicket MarketOnOpenOrder(Symbol symbol, double quantity, OrderOffset offset = OrderOffset.None, string tag = "", Action<long> idCallback = null)
        {
            return MarketOnOpenOrder(symbol, (decimal)quantity, offset, tag, idCallback);
        }

        /// <summary>
        /// Market on open order implementation: Send a market order when the exchange opens
        /// </summary>
        /// <param name="symbol">The symbol to be ordered</param>
        /// <param name="quantity">The number of shares to required</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <returns>The order ID</returns>
        public OrderTicket MarketOnOpenOrder(Symbol symbol, int quantity, OrderOffset offset = OrderOffset.None, string tag = "", Action<long> idCallback = null)
        {
            return MarketOnOpenOrder(symbol, (decimal)quantity, offset, tag, idCallback);
        }

        /// <summary>
        /// Market on open order implementation: Send a market order when the exchange opens
        /// </summary>
        /// <param name="symbol">The symbol to be ordered</param>
        /// <param name="quantity">The number of shares to required</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <param name="idCallback"></param>
        /// <returns>The order ID</returns>
        public OrderTicket MarketOnOpenOrder(Symbol symbol, decimal quantity, OrderOffset offset = OrderOffset.None, string tag = "", Action<long> idCallback = null)
        {
            var security = Securities[symbol];
            var request = CreateSubmitOrderRequest(OrderType.MarketOnOpen, security, quantity, tag, DefaultOrderProperties?.Clone(), offset);
            request.IdCallback = idCallback;

            var response = PreOrderChecks(request);
            if (response.IsError)
            {
                return OrderTicket.InvalidSubmitRequest(Transactions, request, response);
            }

            return Transactions.AddOrder(request);
        }

        /// <summary>
        /// Market on close order implementation: Send a market order when the exchange closes
        /// </summary>
        /// <param name="symbol">The symbol to be ordered</param>
        /// <param name="quantity">The number of shares to required</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <returns>The order ID</returns>
        public OrderTicket MarketOnCloseOrder(Symbol symbol, int quantity, string tag = "", OrderOffset offset = OrderOffset.None)
        {
            return MarketOnCloseOrder(symbol, (decimal)quantity, tag, offset);
        }

        /// <summary>
        /// Market on close order implementation: Send a market order when the exchange closes
        /// </summary>
        /// <param name="symbol">The symbol to be ordered</param>
        /// <param name="quantity">The number of shares to required</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <returns>The order ID</returns>
        public OrderTicket MarketOnCloseOrder(Symbol symbol, double quantity, string tag = "", OrderOffset offset = OrderOffset.None)
        {
            return MarketOnCloseOrder(symbol, (decimal)quantity, tag, offset);
        }

        /// <summary>
        /// Market on close order implementation: Send a market order when the exchange closes
        /// </summary>
        /// <param name="symbol">The symbol to be ordered</param>
        /// <param name="quantity">The number of shares to required</param>
        /// <param name="tag">Place a custom order property or tag (e.g. indicator data).</param>
        /// <returns>The order ID</returns>
        public OrderTicket MarketOnCloseOrder(Symbol symbol, decimal quantity, string tag = "", OrderOffset offset = OrderOffset.None)
        {
            var security = Securities[symbol];
            var request = CreateSubmitOrderRequest(OrderType.MarketOnClose, security, quantity, tag, DefaultOrderProperties?.Clone(), offset);
            var response = PreOrderChecks(request);
            if (response.IsError)
            {
                return OrderTicket.InvalidSubmitRequest(Transactions, request, response);
            }

            return Transactions.AddOrder(request);
        }

        /// <summary>
        /// Send a limit order to the transaction handler:
        /// </summary>
        /// <param name="symbol">String symbol for the asset</param>
        /// <param name="quantity">Quantity of shares for limit order</param>
        /// <param name="limitPrice">Limit price to fill this order</param>
        /// <param name="tag">String tag for the order (optional)</param>
        /// <param name="limitPriceAdvance"></param>
        /// <param name="advance"></param>
        /// <returns>Order id</returns>
        public IEnumerable<OrderTicket> LimitOrder(Symbol symbol, int quantity, decimal limitPrice, string tag = "", decimal limitPriceAdvance = 0m, string advance = "", Action<long> idCallback = null)
        {
            return LimitOrder(symbol, (decimal)quantity, limitPrice, tag, limitPriceAdvance, advance, idCallback);
        }

        /// <summary>
        /// Send a limit order to the transaction handler:
        /// </summary>
        /// <param name="symbol">String symbol for the asset</param>
        /// <param name="quantity">Quantity of shares for limit order</param>
        /// <param name="limitPrice">Limit price to fill this order</param>
        /// <param name="tag">String tag for the order (optional)</param>
        /// <param name="limitPriceAdvance"></param>
        /// <param name="advance"></param>
        /// <returns>Order id</returns>
        public IEnumerable<OrderTicket> LimitOrder(Symbol symbol, double quantity, decimal limitPrice, string tag = "", decimal limitPriceAdvance = 0m, string advance = "", Action<long> idCallback = null)
        {
            return LimitOrder(symbol, (decimal)quantity, limitPrice, tag, limitPriceAdvance, advance, idCallback);
        }

        /*
        /// <summary>
        /// Send a limit order to the transaction handler:
        /// </summary>
        /// <param name="symbol">String symbol for the asset</param>
        /// <param name="quantity">Quantity of shares for limit order</param>
        /// <param name="limitPrice">Limit price to fill this order</param>
        /// <param name="offset">open or close for chinese order</param>
        /// <param name="tag">String tag for the order (optional)</param>
        /// <returns>Order id</returns>
        public OrderTicket LimitOrder(Symbol symbol, decimal quantity, decimal limitPrice, OrderOffset offset = OrderOffset.None, string tag = "")
        {
            var security = Securities[symbol];
            var request = CreateSubmitOrderRequest(OrderType.Limit, security, quantity, tag, limitPrice: limitPrice, properties: DefaultOrderProperties?.Clone(),offset:offset);
            var response = PreOrderChecks(request);
            if (response.IsError)
            {
                return OrderTicket.InvalidSubmitRequest(Transactions, request, response);
            }

            return Transactions.AddOrder(request);
        }
        */
        /// <summary>
        /// Send a limit order to the transaction handler:
        /// </summary>
        /// <param name="symbol">String symbol for the asset</param>
        /// <param name="quantity">Quantity of shares for limit order</param>
        /// <param name="limitPrice">Limit price to fill this order</param>
        /// <param name="tag">String tag for the order (optional)</param>
        /// <param name="limitPriceAdvance"></param>
        /// <param name="advance"></param>
        /// <returns>Order id</returns>
        public IEnumerable<OrderTicket> LimitOrder(
            Symbol symbol,
            decimal quantity,
            decimal limitPrice,
            string tag = "",
            decimal limitPriceAdvance = 0m,
            string advance = "",
            Action<long> idCallback = null)
        {
            var orders = new List<OrderTicket>();

            //symbol beyong to Europe and America 
            //if (!symbol.IsChinaMarket())
            if (!SupportOffset.IsSupportOffset(symbol))
            {
                var order = LimitOrderPriv(
                    symbol,
                    quantity,
                    limitPrice,
                    OrderOffset.None,
                    tag,
                    limitPriceAdvance,
                    advance,
                    idCallback);
                orders.Add(order);
                return orders;
            }

            //backtest mode
            if (!LiveMode)
            {
                var order = LimitOrderPriv(
                    symbol,
                    quantity,
                    limitPrice,
                    OrderOffset.None,
                    tag,
                    limitPriceAdvance,
                    advance,
                    idCallback);
                orders.Add(order);
                return orders;
            }

            //配置不支持自动开平
            if (!AutoOpenClose)
            {
                var order = LimitOrderPriv(
                    symbol,
                    quantity,
                    limitPrice,
                    OrderOffset.None,
                    tag,
                    limitPriceAdvance,
                    advance,
                    idCallback);
                orders.Add(order);
                return orders;
            }


            // chinese symbol ,offset==none,not open or close
            var security = Securities[symbol];
            //buy : first close short holding, second open buy order
            if (quantity > 0)
            {
                var value = security.ShortHoldings.GetOpenQuantity();
                value = Math.Abs(value);
                var closeValue = Math.Min(value, quantity);
                if (closeValue > 0)
                {
                    var order1 = CloseBuyLimit(symbol, closeValue, limitPrice, tag);
                    orders.Add(order1);
                }

                if (value < quantity)
                {
                    var order2 = OpenBuyLimit(symbol, quantity - value, limitPrice, tag);
                    orders.Add(order2);
                }
                return orders;
            }

            //sell : first close long holding, second open sell order
            if (quantity < 0)
            {
                quantity = Math.Abs(quantity);
                var value = security.LongHoldings.GetOpenQuantity();
                var closeValue = Math.Min(value, quantity);
                if (closeValue > 0)
                {
                    var order1 = CloseSellLimit(symbol, closeValue, limitPrice, tag);
                    orders.Add(order1);
                }
                if (value < quantity)
                {
                    var order2 = OpenSellLimit(symbol, quantity - value, limitPrice, tag);
                    orders.Add(order2);
                }
                return orders;
            }
            return orders;
        }

        /// <summary>
        /// Send a limit order to the transaction handler:  replace public LimitOrder whitch be commented
        /// </summary>
        /// <param name="symbol">String symbol for the asset</param>
        /// <param name="quantity">Quantity of shares for limit order</param>
        /// <param name="limitPrice">Limit price to fill this order</param>
        /// <param name="offset">open or close for chinese order</param>
        /// <param name="tag">String tag for the order (optional)</param>
        /// <param name="limitPriceAdvance"></param>
        /// <param name="advance"></param>
        /// <param name="idCallback"></param>
        /// <returns>Order id</returns>
        private OrderTicket LimitOrderPriv(
            Symbol symbol,
            decimal quantity,
            decimal limitPrice,
            OrderOffset offset = OrderOffset.None,
            string tag = "",
            decimal limitPriceAdvance = 0m,
            string advance = "",
            Action<long> idCallback = null)
        {
            var security = Securities[symbol];
            var request = CreateSubmitOrderRequest(OrderType.Limit, security, quantity, tag, limitPrice: limitPrice, properties: DefaultOrderProperties?.Clone(), offset: offset, limitPriceAdvance: limitPriceAdvance, advance: advance);
            request.IdCallback = idCallback;
            var response = PreOrderChecks(request);
            if (response.IsError)
            {
                return OrderTicket.InvalidSubmitRequest(Transactions, request, response);
            }

            return Transactions.AddOrder(request);
        }
        /// <summary>
        /// Create a stop market order and return the newly created order id; or negative if the order is invalid
        /// </summary>
        /// <param name="symbol">String symbol for the asset we're trading</param>
        /// <param name="quantity">Quantity to be traded</param>
        /// <param name="stopPrice">Price to fill the stop order</param>
        /// <param name="tag">Optional string data tag for the order</param>
        /// <returns>Int orderId for the new order.</returns>
        public OrderTicket StopMarketOrder(Symbol symbol, int quantity, decimal stopPrice, string tag = "", OrderOffset offset = OrderOffset.None)
        {
            return StopMarketOrder(symbol, (decimal)quantity, stopPrice, tag, offset);
        }

        /// <summary>
        /// Create a stop market order and return the newly created order id; or negative if the order is invalid
        /// </summary>
        /// <param name="symbol">String symbol for the asset we're trading</param>
        /// <param name="quantity">Quantity to be traded</param>
        /// <param name="stopPrice">Price to fill the stop order</param>
        /// <param name="tag">Optional string data tag for the order</param>
        /// <returns>Int orderId for the new order.</returns>
        public OrderTicket StopMarketOrder(Symbol symbol, double quantity, decimal stopPrice, string tag = "", OrderOffset offset = OrderOffset.None)
        {
            return StopMarketOrder(symbol, (decimal)quantity, stopPrice, tag, offset);
        }

        /// <summary>
        /// Create a stop market order and return the newly created order id; or negative if the order is invalid
        /// </summary>
        /// <param name="symbol">String symbol for the asset we're trading</param>
        /// <param name="quantity">Quantity to be traded</param>
        /// <param name="stopPrice">Price to fill the stop order</param>
        /// <param name="tag">Optional string data tag for the order</param>
        /// <returns>Int orderId for the new order.</returns>
        public OrderTicket StopMarketOrder(Symbol symbol, decimal quantity, decimal stopPrice, string tag = "", OrderOffset offset = OrderOffset.None)
        {
            var security = Securities[symbol];
            var request = CreateSubmitOrderRequest(OrderType.StopMarket, security, quantity, tag, stopPrice: stopPrice, properties: DefaultOrderProperties?.Clone(), offset: offset);
            var response = PreOrderChecks(request);
            if (response.IsError)
            {
                return OrderTicket.InvalidSubmitRequest(Transactions, request, response);
            }

            return Transactions.AddOrder(request);
        }



        /// <summary>
        /// Send a stop limit order to the transaction handler:
        /// </summary>
        /// <param name="symbol">String symbol for the asset</param>
        /// <param name="quantity">Quantity of shares for limit order</param>
        /// <param name="stopPrice">Stop price for this order</param>
        /// <param name="limitPrice">Limit price to fill this order</param>
        /// <param name="tag">String tag for the order (optional)</param>
        /// <returns>Order id</returns>
        public OrderTicket StopLimitOrder(Symbol symbol, int quantity, decimal stopPrice, decimal limitPrice, OrderOffset offset = OrderOffset.None, string tag = "", StopPriceTriggerType stopPriceTriggerType = StopPriceTriggerType.LastPrice)
        {
            return StopLimitOrder(symbol, (decimal)quantity, stopPrice, limitPrice, offset, tag, stopPriceTriggerType);
        }

        /// <summary>
        /// Send a stop limit order to the transaction handler:
        /// </summary>
        /// <param name="symbol">String symbol for the asset</param>
        /// <param name="quantity">Quantity of shares for limit order</param>
        /// <param name="stopPrice">Stop price for this order</param>
        /// <param name="limitPrice">Limit price to fill this order</param>
        /// <param name="tag">String tag for the order (optional)</param>
        /// <returns>Order id</returns>
        public OrderTicket StopLimitOrder(Symbol symbol, double quantity, decimal stopPrice, decimal limitPrice, OrderOffset offset = OrderOffset.None, string tag = "", StopPriceTriggerType stopPriceTriggerType = StopPriceTriggerType.LastPrice)
        {
            return StopLimitOrder(symbol, (decimal)quantity, stopPrice, limitPrice, offset, tag, stopPriceTriggerType);
        }


        /// <summary>
        /// Send a stop limit order to the transaction handler:
        /// </summary>
        /// <param name="symbol">String symbol for the asset</param>
        /// <param name="quantity">Quantity of shares for limit order</param>
        /// <param name="stopPrice">Stop price for this order</param>
        /// <param name="limitPrice">Limit price to fill this order</param>
        /// <param name="tag">String tag for the order (optional)</param>
        /// <returns>Order id</returns>
        public OrderTicket StopLimitOrder(Symbol symbol, decimal quantity, decimal stopPrice, decimal limitPrice, OrderOffset offset = OrderOffset.None, string tag = "", StopPriceTriggerType stopPriceTriggerType = StopPriceTriggerType.LastPrice)
        {
            var security = Securities[symbol];
            var request = CreateSubmitOrderRequest(OrderType.StopLimit, security, quantity, tag, stopPrice: stopPrice, limitPrice: limitPrice, properties: DefaultOrderProperties?.Clone(), offset: offset);
            var response = PreOrderChecks(request);
            if (response.IsError)
            {
                return OrderTicket.InvalidSubmitRequest(Transactions, request, response);
            }

            //Add the order and create a new order Id.
            return Transactions.AddOrder(request);
        }

        /// <summary>
        /// Send an exercise order to the transaction handler
        /// </summary>
        /// <param name="optionSymbol">String symbol for the option position</param>
        /// <param name="quantity">Quantity of options contracts</param>
        /// <param name="asynchronous">Send the order asynchronously (false). Otherwise we'll block until it fills</param>
        /// <param name="tag">String tag for the order (optional)</param>
        public OrderTicket ExerciseOption(Symbol optionSymbol, int quantity, bool asynchronous = false, string tag = "")
        {
            var option = (Option)Securities[optionSymbol];

            var request = CreateSubmitOrderRequest(OrderType.OptionExercise, option, quantity, tag, DefaultOrderProperties?.Clone());

            // If warming up, do not submit
            if (IsWarmingUp)
            {
                return OrderTicket.InvalidWarmingUp(Transactions, request);
            }

            //Initialize the exercise order parameters
            var preOrderCheckResponse = PreOrderChecks(request);
            if (preOrderCheckResponse.IsError)
            {
                return OrderTicket.InvalidSubmitRequest(Transactions, request, preOrderCheckResponse);
            }

            //Add the order and create a new order Id.
            var ticket = Transactions.AddOrder(request);

            // Wait for the order event to process, only if the exchange is open
            if (!asynchronous)
            {
                Transactions.WaitForOrder(ticket.OrderId);
            }

            return ticket;
        }

        // Support for option strategies trading

        /// <summary>
        /// Buy Option Strategy (Alias of Order)
        /// </summary>
        /// <param name="strategy">Specification of the strategy to trade</param>
        /// <param name="quantity">Quantity of the strategy to trade</param>
        /// <returns>Sequence of order ids</returns>
        public IEnumerable<OrderTicket> Buy(OptionStrategy strategy, int quantity)
        {
            return Order(strategy, Math.Abs(quantity));
        }

        /// <summary>
        /// Sell Option Strategy (alias of Order)
        /// </summary>
        /// <param name="strategy">Specification of the strategy to trade</param>
        /// <param name="quantity">Quantity of the strategy to trade</param>
        /// <returns>Sequence of order ids</returns>
        public IEnumerable<OrderTicket> Sell(OptionStrategy strategy, int quantity)
        {
            return Order(strategy, Math.Abs(quantity) * -1);
        }

        /// <summary>
        ///  Issue an order/trade for buying/selling an option strategy
        /// </summary>
        /// <param name="strategy">Specification of the strategy to trade</param>
        /// <param name="quantity">Quantity of the strategy to trade</param>
        /// <returns>Sequence of order ids</returns>
        public IEnumerable<OrderTicket> Order(OptionStrategy strategy, int quantity)
        {
            return GenerateOrders(strategy, quantity);
        }

        private IEnumerable<OrderTicket> GenerateOrders(OptionStrategy strategy, int strategyQuantity)
        {
            var orders = new List<OrderTicket>();

            // setting up the tag text for all orders of one strategy
            var strategyTag = strategy.Name + " (" + strategyQuantity.ToString() + ")";

            // walking through all option legs and issuing orders
            if (strategy.OptionLegs != null)
            {
                foreach (var optionLeg in strategy.OptionLegs)
                {
                    var optionSeq = Securities.Where(kv => kv.Key.Underlying == strategy.Underlying &&
                                                            kv.Key.ID.OptionRight == optionLeg.Right &&
                                                            kv.Key.ID.Date == optionLeg.Expiration &&
                                                            kv.Key.ID.StrikePrice == optionLeg.Strike);

                    if (optionSeq.Count() != 1)
                    {
                        var error = string.Format("Couldn't find the option contract in algorithm securities list. Underlying: {0}, option {1}, strike {2}, expiration: {3}",
                                strategy.Underlying.ToString(), optionLeg.Right.ToString(), optionLeg.Strike.ToString(), optionLeg.Expiration.ToString());
                        throw new InvalidOperationException(error);
                    }

                    var option = optionSeq.First().Key;

                    switch (optionLeg.OrderType)
                    {
                        case OrderType.Market:
                            var marketOrder = MarketOrder(option, optionLeg.Quantity * strategyQuantity, tag: strategyTag);
                            orders.AddRange(marketOrder);
                            break;
                        case OrderType.Limit:
                            var limitOrder = LimitOrder(option, optionLeg.Quantity * strategyQuantity, optionLeg.OrderPrice, tag: strategyTag);
                            orders.AddRange(limitOrder);
                            break;
                        default:
                            throw new InvalidOperationException("Order type is not supported in option strategy: " + optionLeg.OrderType.ToString());
                    }
                }
            }

            // walking through all underlying legs and issuing orders
            if (strategy.UnderlyingLegs != null)
            {
                foreach (var underlyingLeg in strategy.UnderlyingLegs)
                {
                    if (!Securities.ContainsKey(strategy.Underlying))
                    {
                        var error = string.Format("Couldn't find the option contract underlying in algorithm securities list. Underlying: {0}", strategy.Underlying.ToString());
                        throw new InvalidOperationException(error);
                    }

                    switch (underlyingLeg.OrderType)
                    {
                        case OrderType.Market:
                            var marketOrder = MarketOrder(strategy.Underlying, underlyingLeg.Quantity * strategyQuantity, tag: strategyTag);
                            orders.AddRange(marketOrder);
                            break;
                        case OrderType.Limit:
                            var limitOrder = LimitOrder(strategy.Underlying, underlyingLeg.Quantity * strategyQuantity, underlyingLeg.OrderPrice, tag: strategyTag);
                            orders.AddRange(limitOrder);
                            break;
                        default:
                            throw new InvalidOperationException("Order type is not supported in option strategy: " + underlyingLeg.OrderType.ToString());
                    }
                }
            }
            return orders;
        }



        /// <summary>
        /// Perform pre-order checks to ensure we have sufficient capital,
        /// the market is open, and we haven't exceeded maximum realistic orders per day.
        /// </summary>
        /// <returns>OrderResponse. If no error, order request is submitted.</returns>
        private OrderResponse PreOrderChecks(SubmitOrderRequest request)
        {
            var response = PreOrderChecksImpl(request);
            if (response.IsError)
            {
                Error(response.ErrorMessage);
                if (!_liveMode)
                {
                    throw new Exception($"Invalid Order!!! message: {response.ErrorMessage}, order: {request}");
                }
            }
            return response;
        }

        /// <summary>
        /// Perform pre-order checks to ensure we have sufficient capital,
        /// the market is open, and we haven't exceeded maximum realistic orders per day.
        /// </summary>
        /// <returns>OrderResponse. If no error, order request is submitted.</returns>
        private OrderResponse PreOrderChecksImpl(SubmitOrderRequest request)
        {
            if (IsWarmingUp)
            {
                return OrderResponse.WarmingUp(request);
            }


            //Most order methods use security objects; so this isn't really used.
            // todo: Left here for now but should review
            if (!Securities.TryGetValue(request.Symbol, out var security))
            {
                return OrderResponse.Error(request, OrderResponseErrorCode.MissingSecurity, "You haven't requested " + request.Symbol.ToString() + " data. Add this with AddSecurity() in the Initialize() Method.");
            }

            //Ordering 0 is useless.
            if (request.Quantity == 0)
            {
                return OrderResponse.ZeroQuantity(request);
            }

            if (Math.Abs(request.Quantity) < security.SymbolProperties.LotSize)
            {
                return OrderResponse.Error(request, OrderResponseErrorCode.OrderQuantityLessThanLoteSize, $"Unable to {request.OrderRequestType.ToString().ToLower()} order with id {request.OrderId} which quantity ({Math.Abs(request.Quantity)}) is less than lot size ({security.SymbolProperties.LotSize}).");
            }

            if (!security.IsTradable)
            {
                return OrderResponse.Error(request, OrderResponseErrorCode.NonTradableSecurity, "The security with symbol '" + request.Symbol.ToString() + "' is marked as non-tradable.");
            }

            var price = security.Price;

            //Check the exchange is open before sending a market on close orders
            if (request.OrderType == OrderType.MarketOnClose && !security.Exchange.ExchangeOpen)
            {
                return OrderResponse.Error(request, OrderResponseErrorCode.ExchangeNotOpen, request.OrderType + " order and exchange not open.");
            }

            //Check the exchange is open before sending a exercise orders
            if (request.OrderType == OrderType.OptionExercise && !security.Exchange.ExchangeOpen)
            {
                return OrderResponse.Error(request, OrderResponseErrorCode.ExchangeNotOpen, request.OrderType + " order and exchange not open.");
            }

            if (price == 0 && request.Symbol.ID.Market != Market.Deribit)
            {
                return OrderResponse.Error(request, OrderResponseErrorCode.SecurityPriceZero, request.Symbol.ToString() + ": asset price is $0. If using custom data make sure you've set the 'Value' property.");
            }

            if (request.Symbol.ID.Market == Market.Deribit &&
               (security.Type == SecurityType.Option || security.Type == SecurityType.Future) &&
                security.Cache.MarkPrice == 0)
            {
                //return OrderResponse.Error(request, OrderResponseErrorCode.SecurityPriceZero, request.Symbol.ToString() + ": asset mark price is $0. If using custom data make sure you've set the 'Value' property.");
            }

            // check quote currency existence/conversion rate on all orders
            var quoteCurrency = security.QuoteCurrency.Symbol;
            if (!Portfolio.CashBook.TryGetValue(quoteCurrency, out var quoteCash))
            {
                return OrderResponse.Error(request, OrderResponseErrorCode.QuoteCurrencyRequired, request.Symbol.Value + ": requires " + quoteCurrency + " in the cashbook to trade.");
            }
            if (security.QuoteCurrency.ConversionRate == 0m)
            {
                return OrderResponse.Error(request, OrderResponseErrorCode.ConversionRateZero, request.Symbol.Value + ": requires " + quoteCurrency + " to have a non-zero conversion rate. This can be caused by lack of data.");
            }

            // need to also check base currency existence/conversion rate on forex orders
            if (security.Type == SecurityType.Forex || security.Type == SecurityType.Crypto)
            {
                var baseCurrency = ((IBaseCurrencySymbol)security).BaseCurrencySymbol;
                if (!Portfolio.CashBook.TryGetValue(baseCurrency, out var baseCash))
                {
                    return OrderResponse.Error(request, OrderResponseErrorCode.ForexBaseAndQuoteCurrenciesRequired, request.Symbol.Value + ": requires " + baseCurrency + " and " + quoteCurrency + " in the cashbook to trade.");
                }
                if (baseCash.ConversionRate == 0m)
                {
                    return OrderResponse.Error(request, OrderResponseErrorCode.ForexConversionRateZero, request.Symbol.Value + ": requires " + baseCurrency + " and " + quoteCurrency + " to have non-zero conversion rates. This can be caused by lack of data.");
                }
            }

            //Make sure the security has some data:
            if (!security.HasData)
            {
                return OrderResponse.Error(request, OrderResponseErrorCode.SecurityHasNoData, "There is no data for this symbol yet, please check the security.HasData flag to ensure there is at least one data point.");
            }

            // We've already processed too many orders: max 10k
            if (!LiveMode && Transactions.OrdersCount > _maxOrders)
            {
                Status = AlgorithmStatus.Stopped;
                return OrderResponse.Error(request, OrderResponseErrorCode.ExceededMaximumOrders, string.Format("You have exceeded maximum number of orders ({0}), for unlimited orders upgrade your account.", _maxOrders));
            }

            if (request.OrderType == OrderType.OptionExercise)
            {
                if (security.Type != SecurityType.Option)
                    return OrderResponse.Error(request, OrderResponseErrorCode.NonExercisableSecurity, "The security with symbol '" + request.Symbol.ToString() + "' is not exercisable.");

                if (security.Holdings.IsShort)
                    return OrderResponse.Error(request, OrderResponseErrorCode.UnsupportedRequestType, "The security with symbol '" + request.Symbol.ToString() + "' has a short option position. Only long option positions are exercisable.");

                if (request.Quantity > security.Holdings.Quantity)
                    return OrderResponse.Error(request, OrderResponseErrorCode.UnsupportedRequestType, "Cannot exercise more contracts of '" + request.Symbol.ToString() + "' than is currently available in the portfolio. ");

                if (request.Quantity <= 0.0m)
                    OrderResponse.ZeroQuantity(request);
            }

            if (request.OrderType == OrderType.MarketOnClose)
            {
                var nextMarketClose = security.Exchange.Hours.GetNextMarketClose(security.LocalTime, false);
                // must be submitted with at least 10 minutes in trading day, add buffer allow order submission
                var latestSubmissionTime = nextMarketClose.Subtract(Orders.MarketOnCloseOrder.DefaultSubmissionTimeBuffer);
                if (!security.Exchange.ExchangeOpen || Time > latestSubmissionTime)
                {
                    // tell the user we require a 16 minute buffer, on minute data in live a user will receive the 3:44->3:45 bar at 3:45,
                    // this is already too late to submit one of these orders, so make the user do it at the 3:43->3:44 bar so it's submitted
                    // to the brokerage before 3:45.
                    return OrderResponse.Error(request, OrderResponseErrorCode.MarketOnCloseOrderTooLate, "MarketOnClose orders must be placed with at least a 16 minute buffer before market close.");
                }
            }

            // passes all initial order checks
            return OrderResponse.Success(request);
        }

        /// <summary>
        /// Liquidate all holdings and cancel open orders. Called at the end of day for tick-strategies.
        /// </summary>
        /// <param name="symbolToLiquidate">Symbols we wish to liquidate</param>
        /// <param name="tag">Custom tag to know who is calling this.</param>
        /// <returns>Array of order ids for liquidated symbols</returns>
        /// <seealso cref="MarketOrder(QuantConnect.Symbol,decimal,bool,string)"/>
        public List<long> Liquidate(Symbol symbolToLiquidate = null, string tag = "Liquidated")
        {
            var orderIdList = new List<long>();
            if (!Settings.LiquidateEnabled)
            {
                Debug("Liquidate() is currently disabled by settings. To re-enable please set 'Settings.LiquidateEnabled' to true");
                return orderIdList;
            }

            IEnumerable<Symbol> toLiquidate;
            if (symbolToLiquidate != null)
            {
                toLiquidate = Securities.ContainsKey(symbolToLiquidate)
                    ? new[] { symbolToLiquidate } : Enumerable.Empty<Symbol>();
            }
            else
            {
                toLiquidate = Securities.Keys.OrderBy(x => x.Value);
            }


            foreach (var symbol in toLiquidate)
            {
                // get open orders
                var orders = Transactions.GetOpenOrders(symbol);

                // get quantity in portfolio

                var security = Securities[symbol];
                var quantity = security.Holdings.Quantity + security.LongHoldings.Quantity + security.ShortHoldings.Quantity;

                // if there is only one open market order that would close the position, do nothing
                if (orders.Count == 1 && quantity != 0 && orders[0].Quantity == -quantity && orders[0].Type == OrderType.Market)
                    continue;

                // cancel all open orders
                var marketOrdersQuantity = 0m;
                foreach (var order in orders)
                {
                    if (order.Type == OrderType.Market)
                    {
                        // pending market order
                        var ticket = Transactions.GetOrderTicket(order.Id);
                        if (ticket != null)
                        {
                            // get remaining quantity
                            marketOrdersQuantity += ticket.Quantity - ticket.QuantityFilled;
                        }
                    }
                    else
                    {
                        Transactions.CancelOrder(order.Id, tag);
                    }
                }

                // Liquidate at market price
                if (quantity != 0)
                {
                    // calculate quantity for closing market order
                    //??? 进一步解决
                    var ticket = Order(symbol, -quantity - marketOrdersQuantity, tag: tag).First<OrderTicket>();
                    if (ticket.Status == OrderStatus.Filled)
                    {
                        orderIdList.Add(ticket.OrderId);
                    }
                }
            }

            return orderIdList;
        }

        /// <summary>
        /// Maximum number of orders for the algorithm
        /// </summary>
        /// <param name="max"></param>
        public void SetMaximumOrders(int max)
        {
            if (!_locked)
            {
                _maxOrders = max;
            }
        }

        /// <summary>
        /// Alias for SetHoldings to avoid the M-decimal errors.
        /// </summary>
        /// <param name="symbol">string symbol we wish to hold</param>
        /// <param name="percentage">double percentage of holdings desired</param>
        /// <param name="liquidateExistingHoldings">liquidate existing holdings if neccessary to hold this stock</param>
        /// <seealso cref="MarketOrder(QuantConnect.Symbol,decimal,bool,string)"/>
        public void SetHoldings(Symbol symbol, double percentage, bool liquidateExistingHoldings = false)
        {
            SetHoldings(symbol, (decimal)percentage, liquidateExistingHoldings);
        }

        /// <summary>
        /// Alias for SetHoldings to avoid the M-decimal errors.
        /// </summary>
        /// <param name="symbol">string symbol we wish to hold</param>
        /// <param name="percentage">float percentage of holdings desired</param>
        /// <param name="liquidateExistingHoldings">bool liquidate existing holdings if neccessary to hold this stock</param>
        /// <param name="tag">Tag the order with a short string.</param>
        /// <seealso cref="MarketOrder(QuantConnect.Symbol,decimal,bool,string)"/>
        public void SetHoldings(Symbol symbol, float percentage, bool liquidateExistingHoldings = false, string tag = "")
        {
            SetHoldings(symbol, (decimal)percentage, liquidateExistingHoldings, tag);
        }

        /// <summary>
        /// Alias for SetHoldings to avoid the M-decimal errors.
        /// </summary>
        /// <param name="symbol">string symbol we wish to hold</param>
        /// <param name="percentage">float percentage of holdings desired</param>
        /// <param name="liquidateExistingHoldings">bool liquidate existing holdings if neccessary to hold this stock</param>
        /// <param name="tag">Tag the order with a short string.</param>
        /// <seealso cref="MarketOrder(QuantConnect.Symbol,decimal,bool,string)"/>
        public void SetHoldings(Symbol symbol, int percentage, bool liquidateExistingHoldings = false, string tag = "")
        {
            SetHoldings(symbol, (decimal)percentage, liquidateExistingHoldings, tag);
        }

        /// <summary>
        /// Automatically place a market order which will set the holdings to between 100% or -100% of *PORTFOLIO VALUE*.
        /// E.g. SetHoldings("AAPL", 0.1); SetHoldings("IBM", -0.2); -> Sets portfolio as long 10% APPL and short 20% IBM
        /// E.g. SetHoldings("AAPL", 2); -> Sets apple to 2x leveraged with all our cash.
        /// If the market is closed, place a market on open order.
        /// </summary>
        /// <param name="symbol">Symbol indexer</param>
        /// <param name="percentage">decimal fraction of portfolio to set stock</param>
        /// <param name="liquidateExistingHoldings">bool flag to clean all existing holdings before setting new faction.</param>
        /// <param name="tag">Tag the order with a short string.</param>
        /// <seealso cref="MarketOrder(QuantConnect.Symbol,decimal,bool,string)"/>
        public void SetHoldings(
            Symbol symbol,
            decimal percentage,
            bool liquidateExistingHoldings = false,
            string tag = "")
        {
            //Initialize Requirements:
            if (!Securities.TryGetValue(symbol, out var security))
            {
                Error($"{symbol} not found in portfolio. Request this data when initializing the algorithm.");
                return;
            }

            //If they triggered a liquidate
            if (liquidateExistingHoldings)
            {
                foreach (var kvp in Portfolio)
                {
                    var holdingSymbol = kvp.Key;
                    var holdings = kvp.Value;
                    if (holdingSymbol != symbol && holdings.AbsoluteQuantity > 0)
                    {
                        //Go through all existing holdings [synchronously], market order the inverse quantity:
                        var liquidationQuantity = CalculateOrderQuantity(holdingSymbol, 0m);
                        Order(holdingSymbol, liquidationQuantity, false, tag);
                    }
                }
            }

            //Calculate total unfilled quantity for open market orders
            var marketOrdersQuantity =
                (from order in Transactions.GetOpenOrders(symbol)
                 where order.Type == OrderType.Market
                 select Transactions.GetOrderTicket(order.Id)
                 into ticket
                 where ticket != null
                 select ticket.Quantity - ticket.QuantityFilled).Sum();

            //Only place trade if we've got > 1 share to order.
            var quantity = CalculateOrderQuantity(symbol, percentage) - marketOrdersQuantity;
            if (Math.Abs(quantity) > 0)
            {
                //Check whether the exchange is open to send a market order. If not, send a market on open order instead
                if (security.Exchange.ExchangeOpen)
                {
                    MarketOrder(symbol, quantity, false, tag);
                }
                else
                {
                    //???
                    MarketOnOpenOrder(symbol, quantity, OrderOffset.None, tag);
                }
            }
        }

        /// <summary>
        /// Calculate the order quantity to achieve target-percent holdings.
        /// </summary>
        /// <param name="symbol">Security object we're asking for</param>
        /// <param name="target">Target percentag holdings</param>
        /// <returns>Order quantity to achieve this percentage</returns>
        public decimal CalculateOrderQuantity(Symbol symbol, double target)
        {
            return CalculateOrderQuantity(symbol, (decimal)target);
        }

        /// <summary>
        /// Calculate the order quantity to achieve target-percent holdings.
        /// </summary>
        /// <param name="symbol">Security object we're asking for</param>
        /// <param name="target">Target percentage holdings, this is an unlevered value, so
        /// if you have 2x leverage and request 100% holdings, it will utilize half of the
        /// available margin</param>
        /// <returns>Order quantity to achieve this percentage</returns>
        public decimal CalculateOrderQuantity(Symbol symbol, decimal target)
        {
            var percent = PortfolioTarget.Percent(this, symbol, target, true);

            if (percent == null)
            {
                return 0;
            }
            return percent.Quantity;
        }

        /// <summary>
        /// Obsolete implementation of Order method accepting a OrderType. This was deprecated since it
        /// was impossible to generate other orders via this method. Any calls to this method will always default to a Market Order.
        /// </summary>
        /// <param name="symbol">Symbol we want to purchase</param>
        /// <param name="quantity">Quantity to buy, + is long, - short.</param>
        /// <param name="type">Order Type</param>
        /// <param name="asynchronous">Don't wait for the response, just submit order and move on.</param>
        /// <param name="tag">Custom data for this order</param>
        /// <param name="idCallback"></param>
        /// <returns>Integer Order ID.</returns>
        [Obsolete("This Order method has been made obsolete, use Order(string, int, bool, string) method instead. Calls to the obsolete method will only generate market orders.")]
        public IEnumerable<OrderTicket> Order(Symbol symbol, int quantity, OrderType type, bool asynchronous = false, string tag = "", Action<long> idCallback = null)
        {
            return Order(symbol, quantity, asynchronous, tag, idCallback);
        }

        /// <summary>
        /// Obsolete method for placing orders.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="quantity"></param>
        /// <param name="type"></param>
        /// <param name="idCallback"></param>
        [Obsolete("This Order method has been made obsolete, use the specialized Order helper methods instead. Calls to the obsolete method will only generate market orders.")]
        public IEnumerable<OrderTicket> Order(Symbol symbol, decimal quantity, OrderType type, Action<long> idCallback = null)
        {
            return Order(symbol, quantity, idCallback);
        }

        /// <summary>
        /// Obsolete method for placing orders.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="quantity"></param>
        /// <param name="type"></param>
        /// <param name="idCallback"></param>
        [Obsolete("This Order method has been made obsolete, use the specialized Order helper methods instead. Calls to the obsolete method will only generate market orders.")]
        public IEnumerable<OrderTicket> Order(Symbol symbol, int quantity, OrderType type, Action<long> idCallback = null)
        {
            return Order(symbol, (decimal)quantity, idCallback);
        }

        /// <summary>
        /// Determines if the exchange for the specified symbol is open at the current time.
        /// </summary>
        /// <param name="symbol">The symbol</param>
        /// <returns>True if the exchange is considered open at the current time, false otherwise</returns>
        public bool IsMarketOpen(Symbol symbol)
        {
            var exchangeHours = MarketHoursDatabase
                .FromDataFolder()
                .GetExchangeHours(symbol.ID.Market, symbol, symbol.SecurityType);

            var time = UtcTime.ConvertFromUtc(exchangeHours.TimeZone);

            return exchangeHours.IsOpen(time, false);
        }

        private SubmitOrderRequest CreateSubmitOrderRequest(OrderType orderType, Security security, decimal quantity, string tag, IOrderProperties properties, OrderOffset offset = OrderOffset.None, decimal stopPrice = 0m, decimal limitPrice = 0m, decimal limitPriceAdvance = 0m, string advance = "", StopPriceTriggerType stopPriceTriggerType = StopPriceTriggerType.LastPrice)
        {
            return new SubmitOrderRequest(orderType, security.Type, security.Symbol, quantity, stopPrice, limitPrice, UtcTime, tag, properties, offset, limitPriceAdvance, advance, stopPriceTriggerType);
        }

        //================================================for command of chinese order==============================================================
        public OrderTicket OpenBuy(Symbol symbol, decimal quantity, bool asynchronous = false, string tag = "", Action<long> idCallback = null)
        {
            return MarketOrderPriv(symbol, Math.Abs(quantity), OrderOffset.Open, asynchronous, tag, idCallback);
        }

        public OrderTicket CloseBuy(Symbol symbol, decimal quantity, bool asynchronous = false, string tag = "", Action<long> idCallback = null)
        {
            var order = MarketOrderPriv(symbol, Math.Abs(quantity), OrderOffset.Close, asynchronous, tag, idCallback);
            var security = Securities[symbol];
            security.ShortHoldings.SetHoldingOrder(order);
            return order;
        }

        public OrderTicket OpenSell(Symbol symbol, decimal quantity, bool asynchronous = false, string tag = "", Action<long> idCallback = null)
        {
            return MarketOrderPriv(symbol, Math.Abs(quantity) * -1, OrderOffset.Open, asynchronous, tag, idCallback: idCallback);
        }

        public OrderTicket CloseSell(Symbol symbol, decimal quantity, bool asynchronous = false, string tag = "", Action<long> idCallback = null)
        {
            var order = MarketOrderPriv(symbol, Math.Abs(quantity) * -1, OrderOffset.Close, asynchronous, tag, idCallback: idCallback);
            var security = Securities[symbol];
            security.LongHoldings.SetHoldingOrder(order);
            return order;
        }

        public OrderTicket OpenBuyLimit(Symbol symbol, decimal quantity, decimal limitPrice, string tag = "", Action<long> idCallback = null)
        {
            return LimitOrderPriv(symbol, Math.Abs(quantity), limitPrice, OrderOffset.Open, tag, idCallback: idCallback);
        }

        public OrderTicket CloseBuyLimit(Symbol symbol, decimal quantity, decimal limitPrice, string tag = "", Action<long> idCallback = null)
        {
            var order = LimitOrderPriv(symbol, Math.Abs(quantity), limitPrice, OrderOffset.Close, tag, idCallback: idCallback);
            var security = Securities[symbol];
            security.ShortHoldings.SetHoldingOrder(order);
            return order;
        }

        public OrderTicket OpenSellLimit(Symbol symbol, decimal quantity, decimal limitPrice, string tag = "", Action<long> idCallback = null)
        {
            return LimitOrderPriv(symbol, Math.Abs(quantity) * -1, limitPrice, OrderOffset.Open, tag, idCallback: idCallback);
        }

        public OrderTicket CloseSellLimit(Symbol symbol, decimal quantity, decimal limitPrice, string tag = "", Action<long> idCallback = null)
        {
            var order = LimitOrderPriv(symbol, Math.Abs(quantity) * -1, limitPrice, OrderOffset.Close, tag, idCallback: idCallback);
            var security = Securities[symbol];
            security.LongHoldings.SetHoldingOrder(order);
            return order;
        }

        public bool ConversionRateReady()
        {
            foreach (var s in Portfolio.Securities)
            {
                if (s.Value.QuoteCurrency.Symbol != "USD" && s.Value.QuoteCurrency.ConversionRate == 0)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="btc_or_eth"></param>
        /// <returns>btc:eth</returns>
        public string GetBTCETHPortfolio()
        {
            decimal btc_value = 0;
            decimal eth_value = 0;

            decimal btc_cb_value = 0;
            decimal eth_cb_value = 0;
            decimal btc_ucb_value = 0;
            decimal eth_ucb_value = 0;


            if (Portfolio.CashBook.ContainsKey("BTC"))
            {
                btc_cb_value = Portfolio.CashBook["BTC"].Amount;
            }
            if (Portfolio.CashBook.ContainsKey("ETH"))
            {
                eth_cb_value = Portfolio.CashBook["ETH"].Amount;
            }

            if (Portfolio.UnsettledCashBook.ContainsKey("BTC"))
            {
                btc_ucb_value = Portfolio.UnsettledCashBook["BTC"].Amount;
            }
            if (Portfolio.UnsettledCashBook.ContainsKey("ETH"))
            {
                eth_ucb_value = Portfolio.UnsettledCashBook["ETH"].Amount;
            }

            decimal btc_option_holdingvalue = 0;
            decimal eth_option_holdingvalue = 0;
            decimal btc_future_holdingvalue = 0;
            decimal eth_future_holdingvalue = 0;
            foreach (var kvp in Portfolio.Securities)
            {
                var position = kvp.Value;
                var securityType = position.Type;
                if (securityType == SecurityType.Option)
                {
                    if (position.Symbol.Underlying.Value == "BTCUSD")
                    {
                        btc_option_holdingvalue += position.Holdings.HoldingsValue / position.QuoteCurrency.ConversionRate;
                    }
                    if (position.Symbol.Underlying.Value == "ETHUSD")
                    {
                        eth_option_holdingvalue += position.Holdings.HoldingsValue / position.QuoteCurrency.ConversionRate;
                    }
                }

                if (securityType == SecurityType.Future)
                {
                    if (position.Symbol.Value.Contains("BTCUSD") || position.Symbol.Value.Contains("BTC-USD"))
                    {
                        btc_future_holdingvalue += position.Holdings.UnrealizedProfit / position.QuoteCurrency.ConversionRate;
                        btc_future_holdingvalue += position.LongHoldings.UnrealizedProfit / position.QuoteCurrency.ConversionRate;
                        btc_future_holdingvalue += position.ShortHoldings.UnrealizedProfit / position.QuoteCurrency.ConversionRate;
                    }
                    if (position.Symbol.Value.Contains("ETHUSD") || position.Symbol.Value.Contains("ETH-USD"))
                    {
                        eth_future_holdingvalue += position.Holdings.UnrealizedProfit / position.QuoteCurrency.ConversionRate;
                        btc_future_holdingvalue += position.LongHoldings.UnrealizedProfit / position.QuoteCurrency.ConversionRate;
                        btc_future_holdingvalue += position.ShortHoldings.UnrealizedProfit / position.QuoteCurrency.ConversionRate;
                    }
                }
            }
            btc_value += btc_cb_value + btc_ucb_value + btc_option_holdingvalue + btc_future_holdingvalue;
            eth_value += eth_cb_value + eth_ucb_value + eth_option_holdingvalue + eth_future_holdingvalue;
            return btc_value.ToString(CultureInfo.InvariantCulture) + ":" + eth_value.ToString(CultureInfo.InvariantCulture);
        }

        public decimal GetCurrencyPortfolioForDeribit(string cash)
        {

            decimal cb_value = 0;
            decimal ucb_value = 0;

            if (Portfolio.CashBook.ContainsKey(cash))
            {
                cb_value = Portfolio.CashBook[cash].Amount;
            }
            if (Portfolio.UnsettledCashBook.ContainsKey(cash))
            {
                ucb_value = Portfolio.UnsettledCashBook[cash].Amount;
            }

            decimal option_holdingvalue = 0;
            decimal future_holdingvalue = 0;
            foreach (var kvp in Portfolio.Securities)
            {
                var position = kvp.Value;
                var securityType = position.Type;
                if (securityType == SecurityType.Option)
                {
                    if (position.Symbol.Underlying.Value.Contains(cash))
                    {
                        option_holdingvalue += position.Holdings.HoldingsValue / position.QuoteCurrency.ConversionRate;
                    }
                }

                if (securityType == SecurityType.Future)
                {
                    if (position.Symbol.Value.Contains(cash))
                    {
                        future_holdingvalue += position.Holdings.UnrealizedProfit / position.QuoteCurrency.ConversionRate;
                        future_holdingvalue += position.LongHoldings.UnrealizedProfit / position.QuoteCurrency.ConversionRate;
                        future_holdingvalue += position.ShortHoldings.UnrealizedProfit / position.QuoteCurrency.ConversionRate;
                    }
                }
            }
            var value = cb_value + ucb_value + option_holdingvalue + future_holdingvalue;
            return value;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool MarkPriceReady()
        {
            if (_isMarkPriceReady == false)
            {
                var cryptos = Securities.Where(x => x.Key.Value == "BTCUSD" || x.Key.Value == "ETHUSD").Select(x => x.Value).ToList();
                foreach (var s in cryptos)
                {
                    if (s.Price == 0)
                    {
                        Log($"MarkPriceReady. {s.Symbol.Value} not ready");
                        return false;
                    }
                }

                var symbols = Portfolio.Transactions.GetOpenOrders().Select(x => x.Symbol).ToList();
                var securities = Securities.Where(x => x.Value.Holdings.Invested || symbols.Contains(x.Key)).Select(x => x.Value).ToList();

                foreach (var s in securities)
                {
                    if (s.Type == SecurityType.Option && !s.Symbol.Value.Contains("?") && s.Cache.MarkPrice == 0)
                    {
                        Log($"MarkPriceReady. {s.Symbol.Value} not ready");
                        return false;
                    }
                    if (s.Type == SecurityType.Future && !s.Symbol.Value.Contains("/") && s.Cache.MarkPrice == 0)
                    {
                        Log($"MarkPriceReady. {s.Symbol.Value} not ready");
                        return false;
                    }
                }
                _isMarkPriceReady = true;
            }
            return true;
        }
    }
}
