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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Moq;
using NUnit.Framework;
using QuantConnect.Brokerages;
using QuantConnect.Brokerages.Deribit;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities;
using TheOne.Deribit;

namespace QuantConnect.Tests.Brokerages.Deribit
{
    //永续合约测试用例
    [TestFixture]
    public class DeribitBrokerageBtcPerpetualTests
    {
        private readonly List<Order> _orders = new List<Order>();
        private DeribitBrokerage _deribitBrokerage;
        private const decimal buyQuantity = 100m;
        private TickBaseIdGen _idGen = new TickBaseIdGen();

        /// <summary>
        /// Gets the symbol to be traded, must be shortable
        /// </summary>
        protected Symbol FutureSymbol => Symbol.CreateFuture(
            "BTC-PERPETUAL",
            Market.Deribit,
            new DateTime(2030, 12, 31),
            "BTC-PERPETUAL"
        );

        protected void Wait(int timeout, Func<bool> state)
        {
            var StartTime = Environment.TickCount;
            do
            {
                if (Environment.TickCount > StartTime + timeout)
                {
                    throw new Exception("Websockets connection timeout.");
                }
                Thread.Sleep(1);
            }
            while (!state());
        }


        internal static Security CreateFuture(Symbol symbol)
        {
            return new Security(
                SecurityExchangeHours.AlwaysOpen(TimeZones.Utc),
                new SubscriptionDataConfig(
                    typeof(TradeBar),
                    symbol,
                    Resolution.Tick,
                    TimeZones.Utc,
                    TimeZones.Utc,
                    false,
                    false,
                    false
                ),
                new Cash(Currencies.USD, 0, 1m),
                SymbolProperties.GetDefault(Currencies.USD),
                ErrorCurrencyConverter.Instance
            );
        }

        [SetUp]
        public void InitializeBrokerage()
        {
            var securities = new SecurityManager(new TimeKeeper(DateTime.UtcNow, new[] { TimeZones.Utc }));
            securities.Add(FutureSymbol, CreateFuture(FutureSymbol));
            var transactions = new SecurityTransactionManager(null, securities);
            transactions.SetOrderProcessor(new BrokerageTransactionHandler());

            var algorithm = new Mock<IAlgorithm>();
            algorithm.Setup(a => a.Transactions).Returns(transactions);
            algorithm.Setup(a => a.BrokerageModel).Returns(new DeribitBrokerageModel(AccountType.Margin));
            algorithm.Setup(a => a.Portfolio).Returns(new SecurityPortfolioManager(securities, transactions));
            algorithm.Setup(a => a.OptionChainProvider).Returns(new LiveDeribitOptionChainProvider());
            algorithm.Setup(a => a.FutureChainProvider).Returns(new LiveDeribitFutureChainProvider());

          

            var priceProvider = new Mock<IPriceProvider>();
            priceProvider.Setup(a => a.GetLastPrice(It.IsAny<Symbol>())).Returns(10000m);

            _deribitBrokerage = new DeribitBrokerage(Config.Get("deribit-url"),
                Config.Get("deribit-rest"),
                Config.Get("deribit-api-key"),
                Config.Get("deribit-api-secret"),
                algorithm.Object,
                priceProvider.Object);
            _deribitBrokerage.Connect();
        }

        [TearDown]
        public void Teardown()
        {
            try
            {
                return;
                // give the tear down a header so we can easily find it in the logs
                Log.Trace("-----");
                Log.Trace("DeribitBrokerageTests.Teardown(): Starting teardown...");
                Log.Trace("-----");

                var canceledResetEvent = new ManualResetEvent(false);
                var filledResetEvent = new ManualResetEvent(false);
                _deribitBrokerage.OrderStatusChanged += (sender, orderEvent) =>
                {
                    if (orderEvent.Status == OrderStatus.Filled)
                    {
                        filledResetEvent.Set();
                    }
                    if (orderEvent.Status == OrderStatus.Canceled)
                    {
                        canceledResetEvent.Set();
                    }
                };

                // cancel all open orders

                Log.Trace("DeribitBrokerageTests.Teardown(): Canceling open orders...");

                var orders = _deribitBrokerage.GetOpenOrders();
                foreach (var order in orders)
                {
                    _deribitBrokerage.CancelOrder(order);
                    canceledResetEvent.WaitOne(3000);
                    canceledResetEvent.Reset();
                }

                Log.Trace("DeribitBrokerageTests.Teardown(): Liquidating open positions...");

               // liquidate all positions
               //var holdings = _deribitBrokerage.GetAccountHoldings();
               // foreach (var holding in holdings.Where(x => x.Quantity != 0))
               // {
               //     var liquidate = new MarketOrder(holding.Symbol, (decimal)-holding.Quantity, DateTime.UtcNow);
               //     _deribitBrokerage.PlaceOrder(liquidate);
               //     filledResetEvent.WaitOne(3000);
               //     filledResetEvent.Reset();
               // }

                var openOrdersText = _deribitBrokerage.GetOpenOrders().Select(x => x.Symbol.ToString() + " " + x.Quantity);
                Log.Trace("deribitBrokerageTests.Teardown(): Open orders: " + string.Join(", ", openOrdersText));
                Assert.AreEqual(0, openOrdersText.Count(), "Failed to verify that there are zero open orders.");

                //var holdingsText = _deribitBrokerage.GetAccountHoldings().Where(x => x.Quantity != 0).Select(x => x.Symbol.ToString() + " " + x.Quantity);
                //Log.Trace("deribitBrokerageTests.Teardown(): Account holdings: " + string.Join(", ", holdingsText));
                //Assert.AreEqual(0, holdingsText.Count(), "Failed to verify that there are zero account holdings.");

                _orders.Clear();
            }
            finally
            {
                _deribitBrokerage?.Dispose();
            }
        }

        [Test]
        public void PlacedOrderHasNewBrokerageOrderID()
        {
            var deribit = _deribitBrokerage;
            var id = 0;
            var order = new MarketOrder(FutureSymbol, buyQuantity, DateTime.UtcNow) { Id = ++id };
            _orders.Add(order);
            deribit.PlaceOrder(order);

            var brokerageID = order.BrokerId.Single();
            Assert.AreNotEqual(0, brokerageID);

            order = new MarketOrder(FutureSymbol, buyQuantity, DateTime.UtcNow) { Id = ++id };
            _orders.Add(order);
            deribit.PlaceOrder(order);

            Assert.AreNotEqual(brokerageID, order.BrokerId.Single());
        }

        [Test]
        public void ClientBuyMarketOrder()
        {
            bool orderFilled = false;
            var manualResetEvent = new ManualResetEvent(false);
            var deribit = _deribitBrokerage;

            deribit.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderEvent.Status == OrderStatus.Filled)
                {
                    orderFilled = true;
                    manualResetEvent.Set();
                }
            };

            var order = new MarketOrder(FutureSymbol, buyQuantity, DateTime.UtcNow) {Id = _idGen.Next()};
            _orders.Add(order);
            var result = deribit.PlaceOrder(order);
            Assert.IsTrue(result, "place order failed");

            manualResetEvent.WaitOne(2500);
            var orderFromderibit = AssertOrderOpened(orderFilled, deribit, order);
            Assert.AreEqual(OrderDirection.Buy, orderFromderibit.Direction);
        }

        [Test]
        public void ClientSellMarketOrder()
        {
            bool orderFilled = false;
            var manualResetEvent = new ManualResetEvent(false);

            var deribit = _deribitBrokerage;

            deribit.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderEvent.Status == OrderStatus.Filled)
                {
                    orderFilled = true;
                    manualResetEvent.Set();
                }
            };

            // sell a single share
            var order = new MarketOrder(FutureSymbol, -buyQuantity, DateTime.UtcNow) { Id = _idGen.Next() };
            _orders.Add(order);
            var result =deribit.PlaceOrder(order);
            Assert.IsTrue(result, "place order failed");

            manualResetEvent.WaitOne(2500);

            var orderFromderibit = AssertOrderOpened(orderFilled, deribit, order);
            Assert.AreEqual(OrderDirection.Sell, orderFromderibit.Direction);
        }

        [Test, Description("测试限价单能否正常成交")]
        public void ClientPlacesLimitOrder()
        {
            var manualResetEvent = new ManualResetEvent(false);
            var deribit = _deribitBrokerage;
            int id = 0;
            var tick = GetTickPrice(FutureSymbol);
            var order = new LimitOrder(FutureSymbol, buyQuantity, tick.AskPrice, DateTime.UtcNow, null) { Id = _idGen.Next() };
            
            deribit.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (order.Id == orderEvent.OrderId)
                {
                    order.Status = orderEvent.Status;
                    if (orderEvent.Status == OrderStatus.Filled)
                    {
                        manualResetEvent.Set();
                    }
                }
            };

            var result = deribit.PlaceOrder(order);
            Assert.IsTrue(result, "place order failed");
            manualResetEvent.WaitOne(5000);

            Assert.AreEqual(order.Status, OrderStatus.Filled);
        }


        [Test, Description("测试StopMarketOrder是否下单正常")]
        public void ClientPlacesStopMarketOrder()
        {
            bool orderFilled = false;
            var manualResetEvent = new ManualResetEvent(false);
            var deribit = _deribitBrokerage;

            var tick = GetTickPrice(FutureSymbol);
            decimal delta = 100;
            var order = new StopMarketOrder(FutureSymbol, buyQuantity, tick.AskPrice + delta, DateTime.UtcNow) { Id = _idGen.Next() };

            // if we can't get a price then make the delta huge
            deribit.OrderStatusChanged += (sender, args) =>
            {
                if (order.Id == args.OrderId)
                {
                    order.Status = args.Status;
                    manualResetEvent.Set();
                }
            };

            var result = deribit.PlaceOrder(order);
            Assert.IsTrue(result, "place order failed");
            manualResetEvent.WaitOne(3000);

            DeribitMessages.Order mOrder;
            var ret = deribit.RestApi.GetOrderState(order.BrokerId[0], out mOrder);
            Assert.IsTrue(ret, "GetOrderState failed");

            Assert.AreEqual(mOrder.order_type, "stop_market");
        }

        [Test, Description("测试StopLimitOrder是否下单正常")]
        public void ClientPlacesStopLimitOrder()
        {
            bool orderFilled = false;
            var manualResetEvent = new ManualResetEvent(false);
            var deribit = _deribitBrokerage;

            var tick = GetTickPrice(FutureSymbol);
            int id = 0;
            decimal delta = 100;
            var order = new StopLimitOrder(FutureSymbol, buyQuantity, tick.AskPrice + delta, tick.AskPrice - delta, DateTime.UtcNow) { Id = _idGen.Next() };

            // if we can't get a price then make the delta huge
            deribit.OrderStatusChanged += (sender, args) =>
            {
                if (order.Id == args.OrderId)
                {
                    order.Status = args.Status;
                    manualResetEvent.Set();
                }
            };

            var result = deribit.PlaceOrder(order);
            Assert.IsTrue(result,"place order failed");
            manualResetEvent.WaitOne(3000);
            

            DeribitMessages.Order mOrder;
            var ret = deribit.RestApi.GetOrderState(order.BrokerId[0], out mOrder);
            Assert.IsTrue(ret, "GetOrderState failed");


            Assert.AreEqual(mOrder.order_type, "stop_limit");
        }

        [Test, Description("通过变动价位测试改单是否正确")]
        public void ClientUpdatesLimitOrder()
        {
            int id = 0;
            var deribit = _deribitBrokerage;

            bool filled = false;
            deribit.OrderStatusChanged += (sender, args) =>
            {
                if (args.Status == OrderStatus.Filled)
                {
                    filled = true;
                }

                if (args.Status == OrderStatus.Invalid)
                {
                    System.Console.WriteLine($"Order Invaild");
                }
            };

            var tick = GetTickPrice(FutureSymbol);
            var price1 = tick.AskPrice + 10m;
            var order = new LimitOrder(FutureSymbol, -buyQuantity, price1, DateTime.UtcNow) { Id = ++id };
            _orders.Add(order);
            deribit.PlaceOrder(order);

            Thread.Sleep(1000);
            order.LimitPrice = price1 + 10m;
            deribit.UpdateOrder(order);

            DeribitMessages.Order mOrder;
            var ret = deribit.RestApi.GetOrderState(order.BrokerId[0], out mOrder);
            Assert.IsTrue(ret, "GetOrderState failed");

            Assert.Equals(order.LimitPrice, mOrder.price);
        }

        [Test, Description("能否正常撤销限价单")]
        public void ClientCancelsLimitOrder()
        {
            var orderedResetEvent = new ManualResetEvent(false);
            var canceledResetEvent = new ManualResetEvent(false);

            var deribit = _deribitBrokerage;
            // try to sell a single share at a ridiculous price, we'll cancel this later
            var tick = GetTickPrice(FutureSymbol);
            var order = new LimitOrder(FutureSymbol, buyQuantity, tick.AskPrice - 100m, DateTime.UtcNow, null) { Id = _idGen.Next() }; ;

            deribit.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (order.Id == orderEvent.OrderId)
                {
                    order.Status = orderEvent.Status;
                    if (orderEvent.Status == OrderStatus.Submitted)
                    {
                        orderedResetEvent.Set();
                    }
                    if (orderEvent.Status == OrderStatus.Canceled)
                    {
                        canceledResetEvent.Set();
                    }
                }
            };


            var ret =deribit.PlaceOrder(order);
            Assert.IsTrue(ret,"place order failed");
            orderedResetEvent.WaitOneAssertFail(5000, "Limit order failed to be submitted.");

            Thread.Sleep(1000);

            ret = deribit.CancelOrder(order);
            Assert.IsTrue(ret, "cancel order failed");
            canceledResetEvent.WaitOneAssertFail(2500, "Canceled event did not fire.");

            Assert.AreEqual(order.Status, OrderStatus.Canceled);
        }


        [Test, Description("能否正常撤销市价条件单")]
        public void ClientCancelsStopMarketOrder()
        {
            var orderedResetEvent = new ManualResetEvent(false);
            var canceledResetEvent = new ManualResetEvent(false);

            var deribit = _deribitBrokerage;
            // try to sell a single share at a ridiculous price, we'll cancel this later
            var tick = GetTickPrice(FutureSymbol);
            var order = new StopMarketOrder(FutureSymbol, buyQuantity, tick.AskPrice + 100m, DateTime.UtcNow, null) { Id = _idGen.Next() }; ;

            deribit.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (order.Id == orderEvent.OrderId)
                {
                    order.Status = orderEvent.Status;
                    if (orderEvent.Status == OrderStatus.Submitted)
                    {
                        orderedResetEvent.Set();
                    }
                    if (orderEvent.Status == OrderStatus.Canceled)
                    {
                        canceledResetEvent.Set();
                    }
                }
            };


            var ret = deribit.PlaceOrder(order);
            Assert.IsTrue(ret, "place order failed");
            orderedResetEvent.WaitOneAssertFail(5000, "Limit order failed to be submitted.");

            Thread.Sleep(1000);

            ret = deribit.CancelOrder(order);
            Assert.IsTrue(ret, "cancel order failed");
            canceledResetEvent.WaitOneAssertFail(2500, "Canceled event did not fire.");

            Assert.AreEqual(order.Status, OrderStatus.Canceled);
        }

        [Test, Description("能否正常撤销限价条件单")]
        public void ClientCancelsStopLimittOrder()
        {
            var orderedResetEvent = new ManualResetEvent(false);
            var canceledResetEvent = new ManualResetEvent(false);

            var deribit = _deribitBrokerage;
            // try to sell a single share at a ridiculous price, we'll cancel this later
            var tick = GetTickPrice(FutureSymbol);
            var order = new StopLimitOrder(FutureSymbol, buyQuantity, tick.AskPrice + 100m, tick.AskPrice + 100m, DateTime.UtcNow, null) { Id = _idGen.Next() }; ;

            deribit.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (order.Id == orderEvent.OrderId)
                {
                    order.Status = orderEvent.Status;
                    if (orderEvent.Status == OrderStatus.Submitted)
                    {
                        orderedResetEvent.Set();
                    }
                    if (orderEvent.Status == OrderStatus.Canceled)
                    {
                        canceledResetEvent.Set();
                    }
                }
            };


            var ret = deribit.PlaceOrder(order);
            Assert.IsTrue(ret, "place order failed");
            orderedResetEvent.WaitOneAssertFail(5000, "Limit order failed to be submitted.");

            Thread.Sleep(1000);

            ret = deribit.CancelOrder(order);
            Assert.IsTrue(ret, "cancel order failed");
            canceledResetEvent.WaitOneAssertFail(2500, "Canceled event did not fire.");

            Assert.AreEqual(order.Status, OrderStatus.Canceled);
        }

        [Test]
        public void ClientFiresSingleOrderFilledEvent()
        {
            var deribit = _deribitBrokerage;

            var order = new MarketOrder(FutureSymbol, buyQuantity, new DateTime()) { Id = _idGen.Next() };
            _orders.Add(order);

            int orderFilledEventCount = 0;
            var orderFilledResetEvent = new ManualResetEvent(false);
            deribit.OrderStatusChanged += (sender, fill) =>
            {
                if (fill.Status == OrderStatus.Filled)
                {
                    orderFilledEventCount++;
                    orderFilledResetEvent.Set();
                }

                // mimic what the transaction handler would do
                order.Status = fill.Status;
            };

            deribit.PlaceOrder(order);

            orderFilledResetEvent.WaitOneAssertFail(2500, "Didnt fire order filled event");

            // wait a little to see if we get multiple fill events
            Thread.Sleep(2000);

            Assert.AreEqual(1, orderFilledEventCount);
        }


        private static Order AssertOrderOpened(bool orderFilled, DeribitBrokerage deribit, Order order)
        {
            // if the order didn't fill check for it as an open order
            if (!orderFilled)
            {
                // find the right order and return it
                foreach (var openOrder in deribit.GetOpenOrders())
                {
                    if (openOrder.BrokerId.Any(id => order.BrokerId.Any(x => x == id)))
                    {
                        return openOrder;
                    }
                }
                Assert.Fail("The order was not filled and was unable to be located via GetOpenOrders()");
            }

            Assert.Pass("The order was successfully filled!");
            return null;
        }
        /// <summary>
        /// Handles the Tick
        /// </summary>
        /// <returns>The new  ID</returns>
        private Tick GetTickPrice(Symbol symbol)
        {
            var tick = _deribitBrokerage.GetTick(symbol);
            return tick;
        }

    }
}