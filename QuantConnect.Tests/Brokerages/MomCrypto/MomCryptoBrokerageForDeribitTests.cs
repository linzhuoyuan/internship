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
using System.Linq;
using System.Threading;
using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.Brokerages.MomCrypto;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Securities.Future;
using QuantConnect.Securities.Option;
using QuantConnect.Tests.Engine;
using QuantConnect.Tests.Engine.BrokerageTransactionHandlerTests;
using QuantConnect.Util;
using QuantConnect.Lean.Engine.TransactionHandlers;
using OrderStatus = QuantConnect.Orders.OrderStatus;
using OrderType = QuantConnect.Orders.OrderType;
using Moq;
using QuantConnect.Brokerages;
using QuantConnect.Lean.Engine.Results;

namespace QuantConnect.Tests.Brokerages.MomCrypto
{
    [TestFixture]
    public class MomCryptoBrokerageForDeribitTests
    {
        private readonly List<Order> _orders = new List<Order>();
        private MomCryptoBrokerage _brokerage;
        private const decimal buyOptionQuantity = 0.1m;
        private const decimal buyFutureQuantity = 100m;
        private TickBaseIdGen _idGen = new TickBaseIdGen();
        private IAlgorithm _algorithm;

        protected Symbol OptionSymbol => Symbol.CreateOption(
            "BTCUSD",
            Market.Deribit,
            OptionStyle.European,
            OptionRight.Call,
            16000,
            new DateTime(2020, 12, 25),
            "BTC-25DEC20-16000-C"
        );

        protected Symbol FutureSymbol => Symbol.CreateFuture(
            "BTC-PERPETUAL",
            Market.Deribit,
            new DateTime(2021, 10, 28),
            "BTC-PERPETUAL"
        );

        [SetUp]
        public void InitializeBrokerage()
        {
            var leanEngineSystemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);

            //Setup packeting, queue and controls system: These don't do much locally.
            leanEngineSystemHandlers.Initialize();

            string assemblyPath;
            var job = leanEngineSystemHandlers.JobQueue.NextJob(out assemblyPath);
            var leanEngineAlgorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance);
            var algorithm = leanEngineAlgorithmHandlers.Setup.CreateAlgorithmInstance(job, assemblyPath, 120);
            _algorithm = algorithm;

            _brokerage = new MomCryptoBrokerage(algorithm, Config.Get("momcrypto-trade-server"),
                Config.Get("momcrypto-userId"),
                Config.Get("momcrypto-passwd"),
                Config.Get("momcrypto-marketdata-server"),
                Config.Get("momcrypto-userId"),
                Config.Get("momcrypto-passwd"),
                Config.Get("momcrypto-historydata-server"),
                false);
            _brokerage.UseSyncData = false;
            _brokerage.SetInitQuery(false);
            _brokerage.Connect();

        }

        [TearDown]
        public void Teardown()
        {
            return;
            try
            { // give the tear down a header so we can easily find it in the logs
                Log.Trace("-----");
                Log.Trace("MomBrokerageTests.Teardown(): Starting teardown...");
                Log.Trace("-----");
                Thread.Sleep(5000);
                var orders = _brokerage.GetOpenOrders();
                Thread.Sleep(5000);
                var holdings = _brokerage.GetAccountHoldings();
                var holdingLen = holdings.Where(x => x.Quantity != 0).ToList().Count;
                var orderIds = new long[holdingLen];
                int i = 0;
                foreach (var holding in holdings.Where(x => x.Quantity != 0))
                {
                    var id = _idGen.Next();
                    if (id < _algorithm.Transactions.LastOrderId)
                    {
                        id = _algorithm.Transactions.GetIncrementOrderId();
                    }
                    orderIds[i] = id;
                    i++;
                }

                var canceledResetEvent = new AutoResetEvent(false);
                var filledResetEvent = new AutoResetEvent(false);
                _brokerage.OrderStatusChanged += (sender, orderEvent) =>
                {
                    if (orderEvent.Status == OrderStatus.Filled && orderIds.Where(x => x == orderEvent.OrderId).ToList().Count > 0)
                    {
                        filledResetEvent.Set();
                    }
                    if (orderEvent.Status == OrderStatus.Canceled && orders.Where(x => x.Id == orderEvent.OrderId).ToList().Count > 0)
                    {
                        canceledResetEvent.Set();
                    }
                };

                // cancel all open orders

                Log.Trace("InteractiveBrokersBrokerageTests.Teardown(): Canceling open orders...");


                foreach (var order in orders)
                {
                    _brokerage.CancelOrder(order);
                    canceledResetEvent.WaitOne(30000);
                    //canceledResetEvent.Reset();
                }

                Log.Trace("MomBrokerageTests.Teardown(): Liquidating open positions...");

                Thread.Sleep(2000);
                // liquidate all positions

                i = 0;
                foreach (var holding in holdings.Where(x => x.Quantity != 0))
                {
                    var liquidate = new MarketOrder(holding.Symbol, (int)-holding.Quantity, DateTime.UtcNow)
                    {
                        Id = orderIds[i],
                        Status = OrderStatus.New
                    };
                    i++;
                    _brokerage.PlaceOrder(liquidate);
                    filledResetEvent.WaitOne(30000);
                    //filledResetEvent.Reset();
                }

                Thread.Sleep(2000);

                var openOrdersText = _brokerage.GetOpenOrders().Select(x => x.Symbol.ToString() + " " + x.Quantity);
                Log.Trace("Teardown(): Open orders: " + string.Join(", ", openOrdersText));
                Assert.AreEqual(0, openOrdersText.Count(), "Failed to verify that there are zero open orders.");


                var holdingsText = _brokerage.GetAccountHoldings().Where(x => x.Quantity != 0).Select(x => x.Symbol.ToString() + " " + x.Quantity);
                Log.Trace("Teardown(): Account holdings: " + string.Join(", ", holdingsText));
                Assert.AreEqual(0, holdingsText.Count(), "Failed to verify that there are zero account holdings.");

                _orders.Clear();
            }
            finally
            {
                _brokerage?.Dispose();
            }
        }


        [Test, Description("测试连接状态")]
        public void ClientConnects()
        {
            Thread.Sleep(10000);
            Assert.IsTrue(_brokerage.IsConnected);
        }

        [Test, Description("测试断开后再连接标志位是否正确")]
        public void IsConnectedUpdatesCorrectly()
        {
            Thread.Sleep(10000);
            Assert.IsTrue(_brokerage.IsConnected);

            _brokerage.Disconnect();
            Assert.IsFalse(_brokerage.IsConnected);

            _brokerage.Connect();
            Thread.Sleep(10000);
            Assert.IsTrue(_brokerage.IsConnected);
        }

        [Test, Description("测试重置后是否重连")]
        public void IsConnectedAfterReset()
        {
            Thread.Sleep(10000);
            Assert.IsTrue(_brokerage.IsConnected);

            _brokerage.ResetConnection();
            Thread.Sleep(10000);
            Assert.IsTrue(_brokerage.IsConnected);
        }

        [Test, Description("测试反复断开，连接是否正确")]
        public void ConnectDisconnectLoop()
        {
            Thread.Sleep(10000);
            Assert.IsTrue(_brokerage.IsConnected);

            const int iterations = 2;
            for (var i = 0; i < iterations; i++)
            {
                _brokerage.Disconnect();
                Assert.IsFalse(_brokerage.IsConnected);
                _brokerage.Connect();
                Thread.Sleep(10000);
                Assert.IsTrue(_brokerage.IsConnected);
            }
        }

        [Test, Description("测试反复重置后是否连接")]
        public void ResetConnectionLoop()
        {
            Thread.Sleep(10000);
            Assert.IsTrue(_brokerage.IsConnected);

            const int iterations = 2;
            for (var i = 0; i < iterations; i++)
            {
                _brokerage.ResetConnection();
                Thread.Sleep(10000);
                Assert.IsTrue(_brokerage.IsConnected);
            }
        }

        [Test, Description("测试两个不同的订单有不用的 order id")]
        public void PlacedOrderHasNewBrokerageOrderID()
        {
            Thread.Sleep(10000);
            var mom = _brokerage;

            var order = new MarketOrder(FutureSymbol, buyFutureQuantity, DateTime.UtcNow) { Id = _idGen.Next(), Status = OrderStatus.New };
            _orders.Add(order);
            var ret = mom.PlaceOrder(order);
            Thread.Sleep(500);

            var brokerageID = order.BrokerId.Single();
            Assert.AreNotEqual(0, brokerageID);

            order = new MarketOrder(FutureSymbol, buyFutureQuantity, DateTime.UtcNow) { Id = _idGen.Next(), Status = OrderStatus.New };
            _orders.Add(order);
            ret = mom.PlaceOrder(order);
            Thread.Sleep(500);
            Assert.AreNotEqual(brokerageID, order.BrokerId.Single());
        }

        [Test, Description("测试买期货市价单是否发送成功")]
        public void ClientBuyFutureMarketOrder()
        {
            Thread.Sleep(10000);
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";
            bool orderFilled = false;
            var autoEvent = new AutoResetEvent(false);
            var mom = _brokerage;
            var orderId = _idGen.Next();
            var order = new MarketOrder(FutureSymbol, buyFutureQuantity, DateTime.UtcNow) { Id = orderId, Status = OrderStatus.New };

            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderEvent.OrderId == orderId)
                {
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        autoEvent.Set();
                    }

                    if (orderEvent.Status == OrderStatus.Filled)
                    {
                        orderFilled = true;
                        autoEvent.Set();
                    }
                }
            };


            _orders.Add(order);
            var ret = mom.PlaceOrder(order);

            Assert.IsTrue(ret, "PlaceOrder Failed");

            autoEvent.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            var orderFrommom = AssertOrderOpened(orderFilled, mom, order);
            Assert.AreEqual(OrderType.Market, orderFrommom.Type);
        }


        [Test, Description("测试卖期货市价单是否发送成功")]
        public void ClientSellFutureMarketOrder()
        {
            Thread.Sleep(10000);
            bool orderFilled = false;
            var manualResetEvent = new AutoResetEvent(false);

            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            var orderId = _idGen.Next();
            var order = new MarketOrder(FutureSymbol, -buyFutureQuantity, DateTime.UtcNow) { Id = orderId, Status = OrderStatus.New }; ;

            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderEvent.Status == OrderStatus.Invalid)
                {
                    placeOrderFailed = true;
                    placeOrderFailedMsg = orderEvent.Message;
                    manualResetEvent.Set();
                }

                if (orderEvent.Status == OrderStatus.Filled)
                {
                    orderFilled = true;
                    manualResetEvent.Set();
                }
            };

            // sell a single share

            _orders.Add(order);
            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            manualResetEvent.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            var orderFrommom = AssertOrderOpened(orderFilled, mom, order);
            Assert.AreEqual(OrderType.Market, orderFrommom.Type);
        }

        [Test, Description("测试买期权市价单是否发送成功")]
        public void ClientBuyOptionMarketOrder()
        {
            Thread.Sleep(10000);
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";
            bool orderFilled = false;
            var autoEvent = new AutoResetEvent(false);
            var mom = _brokerage;
            var orderId = _idGen.Next();
            var order = new MarketOrder(OptionSymbol, buyOptionQuantity, DateTime.UtcNow) { Id = orderId, Status = OrderStatus.New };

            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderEvent.OrderId == orderId)
                {
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        autoEvent.Set();
                    }

                    if (orderEvent.Status == OrderStatus.Filled)
                    {
                        orderFilled = true;
                        autoEvent.Set();
                    }
                }
            };


            _orders.Add(order);
            var ret = mom.PlaceOrder(order);

            Assert.IsTrue(ret, "PlaceOrder Failed");

            autoEvent.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            var orderFrommom = AssertOrderOpened(orderFilled, mom, order);
            Assert.AreEqual(OrderType.Market, orderFrommom.Type);
        }


        [Test, Description("测试卖期权市价单是否发送成功")]
        public void ClientSellOptionMarketOrder()
        {
            Thread.Sleep(10000);
            bool orderFilled = false;
            var manualResetEvent = new AutoResetEvent(false);

            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            var orderId = _idGen.Next();
            var order = new MarketOrder(OptionSymbol, -buyOptionQuantity, DateTime.UtcNow) { Id = orderId, Status = OrderStatus.New }; ;

            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderEvent.Status == OrderStatus.Invalid)
                {
                    placeOrderFailed = true;
                    placeOrderFailedMsg = orderEvent.Message;
                    manualResetEvent.Set();
                }

                if (orderEvent.Status == OrderStatus.Filled)
                {
                    orderFilled = true;
                    manualResetEvent.Set();
                }
            };

            // sell a single share

            _orders.Add(order);
            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            manualResetEvent.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            var orderFrommom = AssertOrderOpened(orderFilled, mom, order);
            Assert.AreEqual(OrderType.Market, orderFrommom.Type);
        }


        [Test, Description("测试买入永续期货限价单是否成功")]
        public void ClientFutureLimitOrder()
        {
            Thread.Sleep(10000);
            bool orderFilled = false;
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";
            var manualResetEvent = new AutoResetEvent(false);
            var mom = _brokerage;
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(10000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");


            decimal delta = 0.02m;
            var n = tick.LastPrice.GetDecimalNum();
            var limitPrice = tick.LastPrice * (1 - delta);
            limitPrice = limitPrice.RoundToSignificantDigits(n);
            long orderId = _idGen.Next();
            var order = new LimitOrder(FutureSymbol, buyFutureQuantity, limitPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderId == orderEvent.OrderId)
                {

                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        manualResetEvent.Set();
                    }

                    if (orderEvent.Status == OrderStatus.Filled)
                    {
                        orderFilled = true;
                        manualResetEvent.Set();
                    }
                }

            };

            _orders.Add(order);
            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            manualResetEvent.WaitOne(3000);

            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            var orderFrommom = AssertOrderOpened(orderFilled, mom, order);
            Assert.AreEqual(OrderType.Limit, orderFrommom.Type);
        }

        [Test, Description("测试买入期权限价单是否成功")]
        public void ClientOptionLimitOrder()
        {
            Thread.Sleep(10000);
            bool orderFilled = false;
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";
            var manualResetEvent = new AutoResetEvent(false);
            var mom = _brokerage;
            mom.Subscribe(null, new List<Symbol>() { OptionSymbol });
            Thread.Sleep(10000);
            var tick = mom.GetTicker(OptionSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");


            decimal delta = 0.02m;
            var n = tick.LastPrice.GetDecimalNum();
            var limitPrice = tick.LastPrice * (1 - delta);
            limitPrice = limitPrice.RoundToSignificantDigits(n);
            limitPrice = limitPrice < tick.BidPrice ? limitPrice : tick.BidPrice;
            long orderId = _idGen.Next();
            var order = new LimitOrder(OptionSymbol, buyOptionQuantity, limitPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderId == orderEvent.OrderId)
                {

                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        manualResetEvent.Set();
                    }

                    if (orderEvent.Status == OrderStatus.Filled)
                    {
                        orderFilled = true;
                        manualResetEvent.Set();
                    }
                }

            };

            _orders.Add(order);
            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            manualResetEvent.WaitOne(3000);

            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            var orderFrommom = AssertOrderOpened(orderFilled, mom, order);
            Assert.AreEqual(OrderType.Limit, orderFrommom.Type);
        }

        [Test]
        public void ClientCancelFutureLimitOrder()
        {
            Thread.Sleep(10000);

            var orderedResetEvent = new AutoResetEvent(false);
            var canceledResetEvent = new AutoResetEvent(false);

            bool orderFilled = false;
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            Thread.Sleep(2000);
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");


            decimal delta = 0.02m;
            long orderId = _idGen.Next();

            var n = tick.LastPrice.GetDecimalNum();
            var limitPrice = tick.LastPrice * (1 + delta);
            limitPrice = limitPrice.RoundToSignificantDigits(n);

            // try to sell a single share at a ridiculous price, we'll cancel this later
            var order = new LimitOrder(FutureSymbol, -buyFutureQuantity, limitPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            mom.OrderStatusChanged += (sender, orderEvent) =>
            {

                if (orderId == orderEvent.OrderId)
                {

                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderedResetEvent.Set();
                    }

                    //if (orderEvent.Status == OrderStatus.Filled)
                    //{
                    //    orderFilled = true;
                    //    orderedResetEvent.Set();
                    //}

                    if (orderEvent.Status == OrderStatus.Canceled)
                    {

                        canceledResetEvent.Set();
                    }
                }
            };


            _orders.Add(order);
            mom.PlaceOrder(order);
            orderedResetEvent.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {orderId} {placeOrderFailedMsg}");

            var orders = mom.GetOpenOrders();
            var condition = orders.Where(x => x.Id == orderId).ToList().Count == 0;
            Assert.IsFalse(condition, "下单失败 {orderId}");

            mom.CancelOrder(order);

            canceledResetEvent.WaitOneAssertFail(2500, "Canceled event did not fire.");

            var openOrders = mom.GetOpenOrders();
            var cancelledOrder = openOrders.FirstOrDefault(x => x.BrokerId.Contains(order.BrokerId[0]));
            Assert.IsNull(cancelledOrder);
        }

        [Test]
        public void ClientCancelOptionLimitOrder()
        {
            Thread.Sleep(10000);

            var orderedResetEvent = new AutoResetEvent(false);
            var canceledResetEvent = new AutoResetEvent(false);

            bool orderFilled = false;
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            Thread.Sleep(2000);
            mom.Subscribe(null, new List<Symbol>() { OptionSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(OptionSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");


            decimal delta = 0.02m;
            long orderId = _idGen.Next();

            var n = tick.LastPrice.GetDecimalNum();
            var limitPrice = tick.LastPrice * (1 + delta);
            limitPrice = limitPrice.RoundToSignificantDigits(n);
            limitPrice = limitPrice > tick.AskPrice ? limitPrice : tick.AskPrice;
            // try to sell a single share at a ridiculous price, we'll cancel this later
            var order = new LimitOrder(OptionSymbol, -buyOptionQuantity, limitPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            mom.OrderStatusChanged += (sender, orderEvent) =>
            {

                if (orderId == orderEvent.OrderId)
                {

                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderedResetEvent.Set();
                    }

                    if (orderEvent.Status == OrderStatus.Canceled)
                    {

                        canceledResetEvent.Set();
                    }
                }
            };


            _orders.Add(order);
            mom.PlaceOrder(order);
            orderedResetEvent.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            var orders = mom.GetOpenOrders();
            var condition = orders.Where(x => x.Id == orderId).ToList().Count == 0;
            Assert.IsFalse(condition, "下单失败");

            mom.CancelOrder(order);

            canceledResetEvent.WaitOneAssertFail(2500, "Canceled event did not fire.");

            var openOrders = mom.GetOpenOrders();
            var cancelledOrder = openOrders.FirstOrDefault(x => x.BrokerId.Contains(order.BrokerId[0]));
            Assert.IsNull(cancelledOrder);
        }


        [Test]
        public void ClientFiresSingleOrderFilledEvent()
        {
            Thread.Sleep(10000);
            var mom = _brokerage;
            long orderId = _idGen.Next();
            var order = new MarketOrder(FutureSymbol, buyFutureQuantity, new DateTime()) { Id = orderId, Status = OrderStatus.New };

            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            int orderFilledEventCount = 0;
            var orderFilledResetEvent = new AutoResetEvent(false);
            mom.OrderStatusChanged += (sender, fill) =>
            {
                if (orderId == fill.OrderId)
                {
                    if (fill.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = fill.Message;
                        orderFilledResetEvent.Set();
                    }

                    if (fill.Status == OrderStatus.Filled)
                    {
                        orderFilledEventCount++;
                        orderFilledResetEvent.Set();
                    }

                    // mimic what the transaction handler would do
                    order.Status = fill.Status;
                }

            };

            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            orderFilledResetEvent.WaitOneAssertFail(2500, "Didnt fire order filled event");
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            // wait a little to see if we get multiple fill events
            Thread.Sleep(2000);

            Assert.AreEqual(1, orderFilledEventCount);
        }

        [Test]
        public void GetsAccountHoldings()
        {
            Thread.Sleep(10000);

            var mom = _brokerage;
            long orderId = _idGen.Next();
            var order = new MarketOrder(FutureSymbol, buyFutureQuantity, new DateTime()) { Id = orderId, Status = OrderStatus.New };
            bool orderFilled = false;
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var positionVolume = 0m;
            var previousHoldings = mom.GetAccountHoldings();

            foreach (var holding in previousHoldings)
            {
                if (holding.Symbol.ID.Symbol == FutureSymbol.Value)
                {
                    positionVolume = holding.Quantity;
                }
            }

            // wait for order to complete before request account holdings
            var manualResetEvent = new ManualResetEvent(false);
            mom.OrderStatusChanged += (sender, fill) =>
            {
                if (orderId == fill.OrderId)
                {
                    if (fill.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = fill.Message;
                        manualResetEvent.Set();
                    }

                    if (fill.Status == OrderStatus.Filled)
                    {
                        orderFilled = true;
                        manualResetEvent.Set();
                    }
                }
            };

            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            // wait for the order to go through
            manualResetEvent.WaitOneAssertFail(3000, "Didn't receive order event");
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            Assert.IsTrue(orderFilled, "market order do not receive fill order event");

            // mom is slow to update tws
            Thread.Sleep(3000);

            var newHoldings = mom.GetAccountHoldings();

            foreach (var holding in newHoldings)
            {
                if (holding.Symbol.ID.Symbol == FutureSymbol.Value)
                {
                    Assert.IsTrue(positionVolume + buyFutureQuantity == holding.Quantity, "holding Quantity Not Equal");
                }
            }
        }

        [Test]
        public void GetsCashBalanceAfterConnect()
        {
            Thread.Sleep(10000);

            string currency = "BTC";

            var mom = _brokerage;
            var cashBalance = mom.GetCashBalance();
            Assert.IsTrue(cashBalance.Any(x => x.Currency == currency));
            foreach (var cash in cashBalance)
            {
                Console.WriteLine(cash);
                if (cash.Currency == currency)
                {
                    Assert.AreNotEqual(0m, cashBalance.Single(x => x.Currency == currency));
                }
            }
        }

        [Test]
        public void GetsCashBalanceAfterTrade()
        {
            Thread.Sleep(10000);

            string currency = "BTC";
            var mom = _brokerage;

            decimal balance = mom.GetCashBalance().Single(x => x.Currency == currency).Amount;

            // wait for our order to fill
            var manualResetEvent = new AutoResetEvent(false);
            mom.OrderStatusChanged += (sender, args) =>
            {
                if (args.Status == OrderStatus.Filled)
                {
                    manualResetEvent.Set();
                }
            };

            var order = new MarketOrder(FutureSymbol, buyFutureQuantity, DateTime.UtcNow)
            {
                Id = _idGen.Next(),
                Status = OrderStatus.New
            };
            _orders.Add(order);
            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            manualResetEvent.WaitOneAssertFail(3000, "Didn't receive account changed event");
            Thread.Sleep(1000);

            decimal balanceAfterTrade = mom.GetCashBalance().Single(x => x.Currency == currency).Amount;

            Assert.AreNotEqual(balance, balanceAfterTrade);
        }

        [Test]
        public void GetOpenOrders()
        {
            Thread.Sleep(10000);

            var mom = _brokerage;
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");

            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            decimal delta = 0.005m;
            var n = tick.LastPrice.GetDecimalNum();
            var limitPrice = tick.LastPrice * (1 - delta);
            limitPrice = limitPrice.RoundToSignificantDigits(n);
            long orderId = _idGen.Next();
            var order = new LimitOrder(FutureSymbol, buyFutureQuantity, limitPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            var orderEventFired = new AutoResetEvent(false);
            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderId == orderEvent.OrderId)
                {
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderEventFired.Set();
                    }

                    if (orderEvent.Status == OrderStatus.Filled)
                    {
                        Assert.Pass($"The order orderId:{orderId} BrokerId:{order.BrokerId[0]} was filled!");
                    }
                }
            };

            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, $"PlaceOrder Failed orderId:{orderId}");

            orderEventFired.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg} orderId:{orderId} BrokerId:{order.BrokerId[0]}  limitPrice:{limitPrice} LastPrice:{tick.LastPrice} BidPrice:{tick.BidPrice} AskPrice:{tick.AskPrice}");

            Thread.Sleep(3000);

            var openOrders = mom.GetOpenOrders();

            Assert.AreNotEqual(openOrders.Where(x => x.Id == orderId).ToList().Count, 0,$"orderId:{orderId} BrokerId:{order.BrokerId[0]}");
        }

        [Test, Description("低于当前价买入限价止损单")]
        public void BuyStopLimitOrderBelowLastPrice()
        {
            Thread.Sleep(10000);
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");

            decimal delta = 0.02m;
            var n = tick.LastPrice.GetDecimalNum();
            var limitPrice = tick.LastPrice * (1 - delta);
            limitPrice = limitPrice.RoundToSignificantDigits(n);
            long orderId = _idGen.Next();
            var order = new StopLimitOrder(FutureSymbol, buyFutureQuantity, limitPrice, limitPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            var orderEventFired = new AutoResetEvent(false);
            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderId == orderEvent.OrderId)
                {
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderEventFired.Set();
                    }
                }
            };

            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            orderEventFired.WaitOneAssertFail(3000, "Didn't receive order status event");
            Assert.IsTrue(placeOrderFailed);
        }

        [Test, Description("低于当前价卖出限价止损单")]
        public void SellStopLimitOrderBelowLastPrice()
        {
            Thread.Sleep(10000);

            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");

            decimal delta = 0.02m;
            var n = tick.LastPrice.GetDecimalNum();
            var limitPrice = tick.LastPrice * (1 - delta);
            limitPrice = limitPrice.RoundToSignificantDigits(n);
            long orderId = _idGen.Next();
            var order = new StopLimitOrder(FutureSymbol, -buyFutureQuantity, limitPrice, limitPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            var orderEventFired = new AutoResetEvent(false);
            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderId == orderEvent.OrderId)
                {
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderEventFired.Set();
                    }
                }
            };

            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            orderEventFired.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");


            Thread.Sleep(3000);

            var openOrders = mom.GetOpenOrders();

            Assert.AreNotEqual(openOrders.Where(x => x.Id == orderId).ToList().Count, 0);

        }

        [Test, Description("高于当前价买入限价止损单")]
        public void BuyStopLimitOrderHigherLastPrice()
        {
            Thread.Sleep(10000);
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");

            decimal delta = 0.02m;
            var n = tick.LastPrice.GetDecimalNum();
            var limitPrice = tick.LastPrice * (1 + delta);
            limitPrice = limitPrice.RoundToSignificantDigits(n);
            long orderId = _idGen.Next();
            var order = new StopLimitOrder(FutureSymbol, buyFutureQuantity, limitPrice, limitPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            var orderEventFired = new AutoResetEvent(false);
            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderId == orderEvent.OrderId)
                {
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderEventFired.Set();
                    }
                }
            };

            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            orderEventFired.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            Thread.Sleep(3000);

            var openOrders = mom.GetOpenOrders();

            Assert.AreNotEqual(openOrders.Where(x => x.Id == orderId).ToList().Count, 0);
        }

        [Test, Description("高于当前价卖出限价止损单")]
        public void SellStopLimitOrderHigherLastPrice()
        {
            Thread.Sleep(10000);
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");

            decimal delta = 0.05m;
            var n = tick.LastPrice.GetDecimalNum();
            var limitPrice = tick.LastPrice * (1 + delta);
            limitPrice = limitPrice.RoundToSignificantDigits(n);
            long orderId = _idGen.Next();
            var order = new StopLimitOrder(FutureSymbol, -buyFutureQuantity, limitPrice, limitPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            var orderEventFired = new AutoResetEvent(false);
            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderId == orderEvent.OrderId)
                {
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderEventFired.Set();
                    }
                }
            };

            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            orderEventFired.WaitOneAssertFail(3000, "Didn't receive order status event");
            Assert.IsTrue(placeOrderFailed);
        }



        [Test, Description("低于当前价买入市价止损单")]
        public void BuyStopMarketOrderBelowLastPrice()
        {
            Thread.Sleep(10000);
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";


            var mom = _brokerage;
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");

            decimal delta = 0.05m;
            var n = tick.LastPrice.GetDecimalNum();
            var stopPrice = tick.LastPrice * (1 - delta);
            stopPrice = stopPrice.RoundToSignificantDigits(n);
            long orderId = _idGen.Next();
            var order = new StopMarketOrder(FutureSymbol, buyFutureQuantity, stopPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            var orderEventFired = new AutoResetEvent(false);
            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderId == orderEvent.OrderId)
                {
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderEventFired.Set();
                    }

                }
            };

            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            orderEventFired.WaitOneAssertFail(3000, "Didn't receive order status event");
            Assert.IsTrue(placeOrderFailed);

        }

        [Test, Description("低于当前价卖出市价止损单")]
        public void SellStopMarketOrderBelowLastPrice()
        {
            Thread.Sleep(10000);
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");

            decimal delta = 0.02m;
            var n = tick.LastPrice.GetDecimalNum();
            var stopPrice = tick.LastPrice * (1 - delta);
            stopPrice = stopPrice.RoundToSignificantDigits(n);
            long orderId = _idGen.Next();
            var order = new StopMarketOrder(FutureSymbol, -buyFutureQuantity, stopPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            var orderEventFired = new AutoResetEvent(false);
            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderId == orderEvent.OrderId)
                {
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderEventFired.Set();
                    }
                }
            };

            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            orderEventFired.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg} orderId:{orderId} BrokerId:{order.BrokerId[0]} stopPrice:{stopPrice}");

            Thread.Sleep(3000);

            var openOrders = mom.GetOpenOrders();

            Assert.AreNotEqual(openOrders.Where(x => x.Id == orderId).ToList().Count, 0);
        }

        [Test, Description("高于当前价买入市价止损单")]
        public void BuyStopMarketOrderHigherLastPrice()
        {
            Thread.Sleep(10000);
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";


            var mom = _brokerage;
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");

            decimal delta = 0.05m;
            var n = tick.LastPrice.GetDecimalNum();
            var stopPrice = tick.LastPrice * (1 + delta);
            stopPrice = stopPrice.RoundToSignificantDigits(n);
            long orderId = _idGen.Next();
            var order = new StopMarketOrder(FutureSymbol, buyFutureQuantity, stopPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            var orderEventFired = new AutoResetEvent(false);
            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderId == orderEvent.OrderId)
                {
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderEventFired.Set();
                    }

                }
            };

            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            orderEventFired.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg} orderId:{orderId} BrokerId:{order.BrokerId[0]} stopPrice:{stopPrice}");

            Thread.Sleep(3000);

            var openOrders = mom.GetOpenOrders();

            Assert.AreNotEqual(openOrders.Where(x => x.Id == orderId).ToList().Count, 0);
        }

        [Test, Description("高于当前价卖出市价止损单")]
        public void SellStopMarketOrderHigherLastPrice()
        {
            Thread.Sleep(10000);
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");

            decimal delta = 0.05m;
            var n = tick.LastPrice.GetDecimalNum();
            var stopPrice = tick.LastPrice * (1 + delta);
            stopPrice = stopPrice.RoundToSignificantDigits(n);
            long orderId = _idGen.Next();
            var order = new StopMarketOrder(FutureSymbol, -buyFutureQuantity, stopPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            var orderEventFired = new AutoResetEvent(false);
            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderId == orderEvent.OrderId)
                {
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderEventFired.Set();
                    }
                }
            };

            var ret = mom.PlaceOrder(order);
            Assert.IsTrue(ret, "PlaceOrder Failed");

            orderEventFired.WaitOneAssertFail(3000, "Didn't receive order status event");
            Assert.IsTrue(placeOrderFailed);
        }

        [Test, Description("测试撤销所有订单")]
        public void CancelAllOpenOrder()
        {
            Thread.Sleep(10000);

            var mom = _brokerage;
            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderEvent.Status == OrderStatus.Canceled)
                {

                }
            };

            var openOrders = mom.GetOpenOrders();

            foreach (var order in openOrders)
            {
                mom.CancelOrder(order);
                Thread.Sleep(500);
            }

            Thread.Sleep(3000);
            var openOrders2 = mom.GetOpenOrders();

            Assert.AreEqual(openOrders2.Count, 0);
        }


        [Test, Description("测试平掉所有持仓")]
        public void CloseAllPosiotn()
        {
            Thread.Sleep(10000);
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;

            var holdings = mom.GetAccountHoldings();

            foreach (var holding in holdings)
            {
                var liquidate = new MarketOrder(holding.Symbol, (int)-holding.Quantity, DateTime.UtcNow)
                {
                    Id = _idGen.Next(),
                    Status = OrderStatus.New
                };
                _brokerage.PlaceOrder(liquidate);
                Thread.Sleep(1000);
            }

            Thread.Sleep(3000);
            var holdings2 = mom.GetAccountHoldings();

            Assert.AreEqual(holdings2.Count, 0);
        }


        [Test, Description("测试断开后查资金是否重连")]
        public void GetCashBalanceConnectsIfDisconnected()
        {
            Thread.Sleep(10000);

            var mom = _brokerage;
            Assert.IsTrue(mom.IsConnected);

            mom.Disconnect();
            Assert.IsFalse(mom.IsConnected);

            mom.GetCashBalance();
            Thread.Sleep(10000);
            Assert.IsTrue(mom.IsConnected);
        }

        [Test, Description("测试断开后查持仓是否重连")]
        public void GetAccountHoldingsConnectsIfDisconnected()
        {
            Thread.Sleep(10000);

            var mom = _brokerage;
            Assert.IsTrue(mom.IsConnected);

            mom.Disconnect();
            Assert.IsFalse(mom.IsConnected);

            mom.GetAccountHoldings();
            Thread.Sleep(10000);
            Assert.IsTrue(mom.IsConnected);
        }

        [Test, Description("下限价单后确认后两次撤单")]
        public void ClientCancelsLimitOrderTwince()
        {
            Thread.Sleep(10000);

            var orderedResetEvent = new AutoResetEvent(false);
            var canceledResetEvent = new AutoResetEvent(false);

            bool orderFilled = false;
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            Thread.Sleep(2000);
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");


            int cancelCount = 0;
            decimal delta = 0.02m;
            long orderId = _idGen.Next();

            var n = tick.LastPrice.GetDecimalNum();
            var limitPrice = tick.LastPrice * (1 + delta);
            limitPrice = limitPrice.RoundToSignificantDigits(n);

            // try to sell a single share at a ridiculous price, we'll cancel this later
            var order = new LimitOrder(FutureSymbol, -buyFutureQuantity, limitPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            mom.OrderStatusChanged += (sender, orderEvent) =>
            {

                if (orderId == orderEvent.OrderId)
                {

                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderedResetEvent.Set();
                    }

                    if (orderEvent.Status == OrderStatus.Canceled)
                    {
                        canceledResetEvent.Set();
                        cancelCount++;
                    }
                }
            };


            _orders.Add(order);
            mom.PlaceOrder(order);
            orderedResetEvent.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            Thread.Sleep(2000);
            var orders = mom.GetOpenOrders();
            var condition = orders.Where(x => x.Id == orderId).ToList().Count == 0;
            Assert.IsFalse(condition, "下单失败");

            order.Status = OrderStatus.CancelPending;
            mom.CancelOrder(order);
            mom.CancelOrder(order);
            canceledResetEvent.WaitOneAssertFail(3000, "Canceled event did not fire.");

            var openOrders = mom.GetOpenOrders();
            var cancelledOrder = openOrders.FirstOrDefault(x => x.BrokerId.Contains(order.BrokerId[0]));
            Assert.IsNull(cancelledOrder);
            canceledResetEvent.WaitOne(3000);
            Assert.AreEqual(cancelCount, 1);
        }


        [Test, Description("下市价单成交后撤单")]
        public void ClientCancelsMarketOrderAfterFilled()
        {
            Thread.Sleep(10000);

            var orderedResetEvent = new AutoResetEvent(false);
            var canceledResetEvent = new AutoResetEvent(false);

            bool orderFilled = false;
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            Thread.Sleep(2000);

            long orderId = _idGen.Next();
            // try to sell a single share at a ridiculous price, we'll cancel this later
            var order = new MarketOrder(FutureSymbol, -buyFutureQuantity, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            mom.OrderStatusChanged += (sender, orderEvent) =>
            {

                if (orderId == orderEvent.OrderId)
                {
                    order.Status = orderEvent.Status;
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderedResetEvent.Set();
                    }

                    if (orderEvent.Status == OrderStatus.Filled)
                    {
                        orderFilled = true;
                        orderedResetEvent.Set();
                    }

                    if (orderEvent.Status == OrderStatus.Canceled)
                    {
                        canceledResetEvent.Set();
                    }
                }
            };


            _orders.Add(order);
            mom.PlaceOrder(order);
            orderedResetEvent.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            //等待成交
            orderedResetEvent.WaitOneAssertFail(3000, "do not receive order filled");

            if (orderFilled)
            {
                mom.CancelOrder(order);
            }

            Thread.Sleep(3000);
            Assert.AreEqual(order.Status, OrderStatus.Filled);
        }

        [Test, Description("下市价单后立即撤单")]
        public void ClientCancelsMarketOrderAfterPlaceOrder()
        {
            Thread.Sleep(10000);

            var orderedResetEvent = new AutoResetEvent(false);
            var canceledResetEvent = new AutoResetEvent(false);

            bool orderFilled = false;
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            Thread.Sleep(2000);

            decimal delta = 0.05m;
            long orderId = _idGen.Next();
            // try to sell a single share at a ridiculous price, we'll cancel this later
            var order = new MarketOrder(FutureSymbol, -buyFutureQuantity, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            mom.OrderStatusChanged += (sender, orderEvent) =>
            {

                if (orderId == orderEvent.OrderId)
                {
                    order.Status = orderEvent.Status;
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderedResetEvent.Set();
                    }

                    if (orderEvent.Status == OrderStatus.Filled)
                    {
                        orderFilled = true;
                        orderedResetEvent.Set();
                    }

                    if (orderEvent.Status == OrderStatus.Canceled)
                    {
                        canceledResetEvent.Set();
                    }
                }
            };


            _orders.Add(order);
            mom.PlaceOrder(order);
            order.Status = OrderStatus.CancelPending;
            mom.CancelOrder(order);
            orderedResetEvent.WaitOneAssertFail(3000, "do not receive order ");
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            Thread.Sleep(2000);
            Assert.AreEqual(order.Status, OrderStatus.Filled);
        }

        [Test, Description("交易后状态和重启后查询订单状态是否一致")]
        public void OrderStatusTradeAfterCompareWithQuery()
        {
            bool orderSubmitted = false;
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";

            var mom = _brokerage;
            Thread.Sleep(2000);
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(5000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");

            var orderedResetEvent = new AutoResetEvent(false);

            decimal delta = 0.02m;
            var n = tick.LastPrice.GetDecimalNum();
            var limitPrice = tick.LastPrice * (1 + delta);
            limitPrice = limitPrice.RoundToSignificantDigits(n);
            long orderId = _idGen.Next();
            string brokerId="";
            // try to sell a single share at a ridiculous price, we'll cancel this later
            var order = new LimitOrder(FutureSymbol, -buyFutureQuantity, limitPrice, DateTime.UtcNow, null) { Id = orderId, Status = OrderStatus.New };

            OrderStatus status = OrderStatus.New;

            mom.OrderStatusChanged += (sender, orderEvent) =>
            {

                if (orderId == orderEvent.OrderId)
                {
                    status = orderEvent.Status;
                    if (orderEvent.Status == OrderStatus.Invalid)
                    {
                        placeOrderFailed = true;
                        placeOrderFailedMsg = orderEvent.Message;
                        orderedResetEvent.Set();
                    }
                }
            };


            _orders.Add(order);
            mom.PlaceOrder(order);
            orderedResetEvent.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            OrderStatus oldStatus = status;
            brokerId = order.BrokerId[0];
            Thread.Sleep(2000);
            mom.ClearOrderData();
            var orders = mom.GetOpenOrders();
            var o = orders.Where(x => x.BrokerId[0] == brokerId).ToList().FirstOrDefault();
            Assert.AreEqual(o.Status, oldStatus,$"BrokerId {order.BrokerId[0]}");
        }



        [Test, Description("策略下市价单")]
        public void AlgorithmMarketOrder()
        {
            Thread.Sleep(10000);

            var mom = _brokerage;
            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(10000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");

            Thread.Sleep(2000);
            var orderedResetEvent = new AutoResetEvent(false);
            var canceledResetEvent = new AutoResetEvent(false);

            bool orderFilled = false;
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";
            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderEvent.Status == OrderStatus.Invalid)
                {
                    placeOrderFailed = true;
                    placeOrderFailedMsg = orderEvent.Message;
                    orderedResetEvent.Set();
                }

                if (orderEvent.Status == OrderStatus.Filled)
                {
                    orderFilled = true;
                    orderedResetEvent.Set();
                }

                if (orderEvent.Status == OrderStatus.Canceled)
                {
                    canceledResetEvent.Set();
                }
            };

            var brokerModel = new MomCryptoBrokerageModel();

            var transactionHandler = new BrokerageTransactionHandler();
            var security = CreateFutureSecurity(FutureSymbol);
            security.Cache = new SecurityCache();
            tick.MarkPrice = tick.MarkPrice == 0 ? tick.LastPrice : tick.MarkPrice;
            security.Cache.AddData(tick);

            security.SetBuyingPowerModel(brokerModel.GetBuyingPowerModel(security));
            security.SetFeeModel(brokerModel.GetFeeModel(security));

            _algorithm.SetBrokerageModel(brokerModel);
            _algorithm.SetCash("BTC", 10000000);


            _algorithm.Securities.Add(FutureSymbol, security);
            _algorithm.Transactions.SetOrderProcessor(transactionHandler);

            var marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
            var symbolPropertiesDatabase = SymbolPropertiesDatabase.FromDataFolder();


            var securityService = new SecurityService(
                _algorithm.Portfolio.CashBook,
                marketHoursDatabase,
                symbolPropertiesDatabase,
                _algorithm);
            _algorithm.Securities.SetSecurityService(securityService);
            _algorithm.SetLiveMode(true);


            transactionHandler.Initialize(_algorithm, mom, new BacktestingResultHandler() { Algorithm = _algorithm });

            _algorithm.SetFinishedWarmingUp();

            Thread.Sleep(250);

            //开始策略
            var results = ((QCAlgorithm)_algorithm).MarketOrder(FutureSymbol, buyFutureQuantity);
            orderedResetEvent.WaitOneAssertFail(10000, "do not receive order ");
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            var result = results.First();
            var order = _algorithm.Transactions.GetOrderById(result.OrderId);

            var orderFrommom = AssertOrderOpened(orderFilled, mom, order);
        }


        [Test, Description("策略下限价单，循环撤单")]
        public void AlgorithmLimitOrderAndCancle()
        {
            Thread.Sleep(10000);

            var mom = _brokerage;

            mom.Subscribe(null, new List<Symbol>() { FutureSymbol });
            Thread.Sleep(10000);
            var tick = mom.GetTicker(FutureSymbol.Value);

            Assert.IsNotNull(tick, "can not get tick");

            decimal delta = 0.02m;
            var n = tick.LastPrice.GetDecimalNum();
            var limitPrice = tick.LastPrice * (1 - delta);
            limitPrice = limitPrice.RoundToSignificantDigits(n);

            Thread.Sleep(2000);
            var orderedResetEvent = new AutoResetEvent(false);
            var canceledResetEvent = new AutoResetEvent(false);

            bool orderFilled = false;
            bool placeOrderFailed = false;
            string placeOrderFailedMsg = "";
            mom.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (orderEvent.Status == OrderStatus.Invalid)
                {
                    placeOrderFailed = true;
                    placeOrderFailedMsg = orderEvent.Message;
                    orderedResetEvent.Set();
                }

                if (orderEvent.Status == OrderStatus.Filled)
                {
                    orderFilled = true;
                    orderedResetEvent.Set();
                }

                if (orderEvent.Status == OrderStatus.Canceled)
                {
                    canceledResetEvent.Set();
                }
            };

            var brokerModel = new MomCryptoBrokerageModel();

            var transactionHandler = new BrokerageTransactionHandler();
            var security = CreateFutureSecurity(FutureSymbol);
            security.Cache = new SecurityCache();
            tick.MarkPrice = tick.MarkPrice == 0 ? tick.LastPrice : tick.MarkPrice;
            security.Cache.AddData(tick);

            security.SetBuyingPowerModel(brokerModel.GetBuyingPowerModel(security));
            security.SetFeeModel(brokerModel.GetFeeModel(security));

            _algorithm.SetBrokerageModel(brokerModel);
            _algorithm.SetCash("BTC", 10000000);


            _algorithm.Securities.Add(FutureSymbol, security);
            _algorithm.Transactions.SetOrderProcessor(transactionHandler);

            var marketHoursDatabase = MarketHoursDatabase.FromDataFolder();
            var symbolPropertiesDatabase = SymbolPropertiesDatabase.FromDataFolder();


            var securityService = new SecurityService(
                _algorithm.Portfolio.CashBook,
                marketHoursDatabase,
                symbolPropertiesDatabase,
                _algorithm);
            _algorithm.Securities.SetSecurityService(securityService);
            _algorithm.SetLiveMode(true);


            transactionHandler.Initialize(_algorithm, mom, new BacktestingResultHandler() { Algorithm = _algorithm });

            _algorithm.SetFinishedWarmingUp();

            Thread.Sleep(250);

            //开始策略
            var results = ((QCAlgorithm)_algorithm).LimitOrder(FutureSymbol, buyFutureQuantity, limitPrice);
            orderedResetEvent.WaitOne(3000);
            Assert.IsFalse(placeOrderFailed, $"PlaceOrder Failed : {placeOrderFailedMsg}");

            var result = results.First();


            try
            {
                for (int i = 0; i < 10; i++)
                {
                    var order = _algorithm.Transactions.GetOrders(x => x.Id == result.OrderId).First();
                    var ticket = _algorithm.Transactions.GetOrderTicket(order.Id);
                    ticket.Cancel();
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                Assert.Fail("撤单异常");
            }

            var o = _algorithm.Transactions.GetOrders(x => x.Id == result.OrderId).First();
            Assert.AreEqual(o.Status, OrderStatus.Canceled);
        }


        internal static Security CreateFutureSecurity(Symbol symbol)
        {

            var security = new Future(symbol,
                SecurityExchangeHours.AlwaysOpen(TimeZones.Utc),
                new Cash("BTC", 0, 1),
                new SymbolProperties("", "BTC", 1m, 0.5m,10m),
                ErrorCurrencyConverter.Instance
            );
            return security;
        }


        private static Order AssertOrderOpened(bool orderFilled, MomCryptoBrokerage mom, Order order)
        {
            // if the order didn't fill check for it as an open order
            if (!orderFilled)
            {
                // find the right order and return it
                foreach (var openOrder in mom.GetOpenOrders())
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


    }
}

