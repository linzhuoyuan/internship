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
using QuantConnect.Algorithm;
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
using QuantConnect.Tests.Engine;
using QuantConnect.Tests.Engine.BrokerageTransactionHandlerTests;
using Newtonsoft.Json;
using TheOne.Deribit;

namespace QuantConnect.Tests.Brokerages.Deribit
{
    [TestFixture]
    public class DeribitBrokerageTests
    {
        private readonly List<Order> _orders = new List<Order>();
        private DeribitBrokerage _deribitBrokerage;
        private const decimal buyQuantity = 0.1m;
        private TickBaseIdGen _idGen = new TickBaseIdGen();

        /// <summary>
        /// Gets the symbol to be traded, must be shortable
        /// </summary>
        protected Symbol OptionSymbol => Symbol.CreateOption(
            "BTCUSD",
            Market.Deribit,
            OptionStyle.European,
            OptionRight.Call,
            10000,
            new DateTime(2020, 12, 25),
            "BTC-25DEC20-19000-C"
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


        internal static Security CreateOption(Symbol symbol)
        {
            var cash = symbol.Value.Substring(0, 3).ToUpper();
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
                new Cash(cash, 0, 1m),
                SymbolProperties.GetDefault(cash),
                ErrorCurrencyConverter.Instance
            );
        }

        



        [SetUp]
        public void InitializeBrokerage()
        {
            try
            {
                var securities = new SecurityManager(new TimeKeeper(DateTime.UtcNow, new[] {TimeZones.Utc}));
                securities.Add(OptionSymbol, CreateOption(OptionSymbol));
                var transactions = new SecurityTransactionManager(null, securities);
                transactions.SetOrderProcessor(new BrokerageTransactionHandler());

                var algorithm = new Mock<IAlgorithm>();
                algorithm.Setup(a => a.Transactions).Returns(transactions);
                algorithm.Setup(a => a.BrokerageModel).Returns(new DeribitBrokerageModel(AccountType.Margin));
                algorithm.Setup(a => a.Portfolio).Returns(new SecurityPortfolioManager(securities, transactions));
                algorithm.Setup(a => a.OptionChainProvider).Returns(new LiveDeribitOptionChainProvider());
                algorithm.Setup(a => a.FutureChainProvider).Returns(new LiveDeribitFutureChainProvider());

                var priceProvider = new Mock<IPriceProvider>();
                priceProvider.Setup(a => a.GetLastPrice(It.IsAny<Symbol>())).Returns(1.234m);

                _deribitBrokerage = new DeribitBrokerage(Config.Get("deribit-url"),
                    Config.Get("deribit-rest"),
                    Config.Get("deribit-api-key"),
                    Config.Get("deribit-api-secret"),
                    algorithm.Object,
                    priceProvider.Object);
                _deribitBrokerage.Connect();

                var stopwatch = Stopwatch.StartNew();
                while (!_deribitBrokerage.IsLogin && stopwatch.Elapsed.TotalSeconds < 10)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"{ex.Message}");
            }
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

        [Test, Description ("连接测试")]
        public void ClientConnects()
        {
            var deribit = _deribitBrokerage;
            Assert.IsTrue(deribit.IsConnected);
        }

        [Test, Description("测试IsConnected状态是否正确")]
        public void IsConnectedUpdatesCorrectly()
        {
            var deribit = _deribitBrokerage;
            Assert.IsTrue(deribit.IsConnected);

            deribit.Disconnect();
            Assert.IsFalse(deribit.IsConnected);

            deribit.Connect();
            Thread.Sleep(2000);
            Assert.IsTrue(deribit.IsConnected);
        }

        [Test, Description("测试IsLogin状态是否正确")]
        public void ConnectDisconnectLogin()
        {
            var deribit = _deribitBrokerage;

            Assert.IsTrue(deribit.IsLogin);

            const int iterations = 2;
            for (var i = 0; i < iterations; i++)
            {
                deribit.Disconnect();
                Thread.Sleep(2000);
                Assert.IsFalse(deribit.IsLogin);

                deribit.Connect();
                Thread.Sleep(5000);

                Assert.IsTrue(deribit.IsLogin);
            }
        }

        [Test, Description("连接测试")]
        public void ConnectDisconnectLoop()
        {
            var deribit = _deribitBrokerage;
            Assert.IsTrue(deribit.IsConnected);

            const int iterations = 2;
            for (var i = 0; i < iterations; i++)
            {
                deribit.Disconnect();
                Thread.Sleep(1000);
                Assert.IsFalse(deribit.IsConnected);

                deribit.Connect();
                Thread.Sleep(1000);

                Assert.IsTrue(deribit.IsConnected);
            }
        }

        [Test, Description("测试报单是否正确创建了brokerageID")]
        public void PlacedOrderHasNewBrokerageOrderID()
        {
            var deribit = _deribitBrokerage;
            var id = 0;
            //var symbol_future = Symbol.Create("BTC-PERPETUAL", SecurityType.Future, Market.Deribit);
            var order = new MarketOrder(OptionSymbol, buyQuantity, DateTime.UtcNow) { Id = _idGen.Next() };
            _orders.Add(order);
            deribit.PlaceOrder(order);

            var brokerageID = order.BrokerId.Single();
            Assert.AreNotEqual(0, brokerageID);

            order = new MarketOrder(OptionSymbol, buyQuantity, DateTime.UtcNow) { Id = _idGen.Next() };
            _orders.Add(order);
            deribit.PlaceOrder(order);

            Assert.AreNotEqual(brokerageID, order.BrokerId.Single());
        }

        [Test, Description ("测试买入市价单是否正常")]
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

            var order = new MarketOrder(OptionSymbol, buyQuantity, DateTime.UtcNow) {Id = _idGen.Next() };
            _orders.Add(order);
            deribit.PlaceOrder(order);

            manualResetEvent.WaitOne(5000);
            var orderFromderibit = AssertOrderOpened(orderFilled, deribit, order);
            Assert.AreEqual(OrderDirection.Buy, orderFromderibit.Direction);
        }

        [Test, Description("测试卖出市价单是否正常")]
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
            var order = new MarketOrder(OptionSymbol, -buyQuantity, DateTime.UtcNow) { Id = _idGen.Next() }; ;
            _orders.Add(order);
            deribit.PlaceOrder(order);

            manualResetEvent.WaitOne(5000);

            var orderFromderibit = AssertOrderOpened(orderFilled, deribit, order);
            Assert.AreEqual(OrderDirection.Sell, orderFromderibit.Direction);
        }

        [Test, Description("测试限价单能否正常成交")]
        public void ClientPlacesLimitOrder()
        {
            var manualResetEvent = new ManualResetEvent(false);
            var deribit = _deribitBrokerage;
            int id = 0;
            var tick = GetTickPrice(OptionSymbol);
            var price = tick.AskSize > tick.BidSize ? tick.AskPrice:tick.BidPrice;
            var buysell = tick.AskSize > tick.BidSize ? 1 : -1;

            var order = new LimitOrder(OptionSymbol, buyQuantity * buysell, price, DateTime.UtcNow, null) { Id = _idGen.Next() };

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

            deribit.PlaceOrder(order);

            manualResetEvent.WaitOneAssertFail(5000,"not receive filled");

            Assert.AreEqual(order.Status, OrderStatus.Filled);
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

            var tick = GetTickPrice(OptionSymbol);
            var price1 = tick.AskPrice + 0.01m;
            var order = new LimitOrder(OptionSymbol, -buyQuantity, price1, DateTime.UtcNow) {Id = _idGen.Next() };
            _orders.Add(order);
            deribit.PlaceOrder(order);

            Thread.Sleep(1000);
            order.LimitPrice = price1 + 0.01m;
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
            var tick = GetTickPrice(OptionSymbol);
            var order = new LimitOrder(OptionSymbol, -buyQuantity, tick.AskPrice + 0.01m, DateTime.UtcNow, null) { Id = _idGen.Next() };

            deribit.OrderStatusChanged += (sender, orderEvent) =>
            {
                if (order.Id == orderEvent.OrderId)
                {
                    order.Status = orderEvent.Status;
                }

                if (orderEvent.Status == OrderStatus.Submitted)
                {
                    
                    orderedResetEvent.Set();
                }
                if (orderEvent.Status == OrderStatus.Canceled)
                {
                    canceledResetEvent.Set();
                }
            };

            
            deribit.PlaceOrder(order);
            orderedResetEvent.WaitOneAssertFail(5000, "Limit order failed to be submitted.");

            Thread.Sleep(1000);

            deribit.CancelOrder(order);

            canceledResetEvent.WaitOneAssertFail(2500, "Canceled event did not fire.");

            Assert.AreEqual(order.Status, OrderStatus.Canceled);
        }

        [Test]
        public void ClientFiresSingleOrderFilledEvent()
        {
            var deribit = _deribitBrokerage;
            var tick = GetTickPrice(OptionSymbol);
            var price = tick.AskSize > tick.BidSize ? tick.AskPrice : tick.BidPrice;
            var buysell = tick.AskSize > tick.BidSize ? 1 : -1;
            var orderId = _idGen.Next();

            var order = new MarketOrder(OptionSymbol, buyQuantity* buysell, new DateTime()) {Id = orderId };

            _orders.Add(order);

            int orderFilledEventCount = 0;
            var orderFilledResetEvent = new ManualResetEvent(false);
            deribit.OrderStatusChanged += (sender, fill) =>
            {
                if (orderId == fill.OrderId)
                {
                    if (fill.Status == OrderStatus.Filled)
                    {
                        orderFilledEventCount++;
                        orderFilledResetEvent.Set();
                    }

                    // mimic what the transaction handler would do
                    order.Status = fill.Status;
                }
            };

            var ret = deribit.PlaceOrder(order);

            orderFilledResetEvent.WaitOneAssertFail(2500, "Didnt fire order filled event");

            // wait a little to see if we get multiple fill events
            Thread.Sleep(2000);

            Assert.AreEqual(1, orderFilledEventCount);
        }

        [Test]
        public void GetsAccountHoldings()
        {
            var deribit = _deribitBrokerage;

            Thread.Sleep(500);

            var previousHoldings = deribit.GetAccountHoldings().Where(x => x.Symbol.Value == OptionSymbol.Value)
                .Select(x => x.Quantity).FirstOrDefault();


            var tick = GetTickPrice(OptionSymbol);
            var price = tick.AskSize > tick.BidSize ? tick.AskPrice : tick.BidPrice;
            var buysell = tick.AskSize > tick.BidSize ? 1 : -1;
            var orderId = _idGen.Next();

            var order = new MarketOrder(OptionSymbol, buyQuantity * buysell, new DateTime()) { Id = orderId };

            _orders.Add(order);

            int orderFilledEventCount = 0;
            var orderFilledResetEvent = new ManualResetEvent(false);
            deribit.OrderStatusChanged += (sender, fill) =>
            {
                if (orderId == fill.OrderId)
                {
                    if (fill.Status == OrderStatus.Filled)
                    {
                        orderFilledEventCount++;
                        orderFilledResetEvent.Set();
                    }

                    // mimic what the transaction handler would do
                    order.Status = fill.Status;
                }
            };
            var ret = deribit.PlaceOrder(order);

            orderFilledResetEvent.WaitOneAssertFail(2500, "Didnt fire order filled event");


            var newHoldings = deribit.GetAccountHoldings().Where(x => x.Symbol.Value == OptionSymbol.Value)
                .Select(x => x.Quantity).FirstOrDefault();

            Assert.AreEqual(previousHoldings+ buyQuantity * buysell, newHoldings);
               
        }

        [Test]
        public void GetsCashBalanceAfterConnect()
        {
            var deribit = _deribitBrokerage;
            var cashBalance = deribit.GetCashBalance();
            Assert.IsTrue(cashBalance.Any(x => x.Currency == "BTC"));
            Assert.IsTrue(cashBalance.Any(x => x.Currency == "ETH"));
            foreach (var cash in cashBalance)
            {
                Console.WriteLine(cash);
                if (cash.Currency == "BTC")
                {
                    Assert.AreNotEqual(0m, cashBalance.Single(x => x.Currency == "BTC" && x.Settled==true));
                }
                if (cash.Currency == "ETH")
                {
                    Assert.AreNotEqual(0m, cashBalance.Single(x => x.Currency == "ETH" && x.Settled == true));
                }
            }
        }


        [Test]
        public void GetsCashBalanceAfterTrade()
        {
            var deribit = _deribitBrokerage;

            decimal balance = deribit.GetCashBalance().Single(x => x.Currency == "BTC" && x.Settled == true).Amount;

            var tick = GetTickPrice(OptionSymbol);
            var price = tick.AskSize > tick.BidSize ? tick.AskPrice : tick.BidPrice;
            var buysell = tick.AskSize > tick.BidSize ? 1 : -1;
            var orderId = _idGen.Next();

            var order = new MarketOrder(OptionSymbol, buyQuantity * buysell, new DateTime()) { Id = orderId };

            _orders.Add(order);

            var orderFilledResetEvent = new ManualResetEvent(false);
            deribit.OrderStatusChanged += (sender, fill) =>
            {
                if (orderId == fill.OrderId)
                {
                    if (fill.Status == OrderStatus.Filled)
                    {
                        orderFilledResetEvent.Set();
                    }

                    // mimic what the transaction handler would do
                    order.Status = fill.Status;
                }
            };
            var ret = deribit.PlaceOrder(order);

            orderFilledResetEvent.WaitOneAssertFail(10000, "Didn't receive order changed event");

            decimal balanceAfterTrade = deribit.GetCashBalance().Single(x => x.Currency =="BTC" && x.Settled == true).Amount;

            Assert.AreNotEqual(balance, balanceAfterTrade);
        }

        [Test, Ignore("This test requires disconnecting the internet to test for connection resiliency")]
        public void ClientReconnectsAfterInternetDisconnect()
         {
            var deribit = _deribitBrokerage;
            Assert.IsTrue(deribit.IsConnected);
            
            deribit.CloseWebSocket();
            Thread.Sleep(10000);

            Assert.IsTrue(deribit.IsConnected);
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


        [Test, Ignore("测试获取历史订单")]
        public void GetHistoryOrdersTest()
        {
            var deribit = _deribitBrokerage;

            var order = new MarketOrder(OptionSymbol, buyQuantity, DateTime.UtcNow) { Id = 1 };
            deribit.PlaceOrder(order);


            Config.Set("deribit-history-trade-last-days", "4");
            var orders = deribit.GetHistoryOrders();

            Assert.IsTrue(orders.Count>0);
        }

        [Test, Ignore("测试获取历史成交,成交单根据订单或得")]
        public void GetHistoryTradesTest()
        {
            var deribit = _deribitBrokerage;
            var order = new MarketOrder(OptionSymbol, buyQuantity, DateTime.UtcNow) { Id = 1 };
            deribit.PlaceOrder(order);


            Config.Set("deribit-history-trade-last-days", "4");
            var orders = deribit.GetHistoryOrders();
            var trades = deribit.GetHistoryTrades();
            Assert.IsTrue(trades.Count > 0);
        }

        [Test, Ignore("测试期货Symbol转换")]
        public void GetFutureSymbolTest()
        {
            var mapper = new DeribitSymbolMapper();
            mapper.OptionChainProvider = new LiveDeribitOptionChainProvider();
            mapper.FutureChainProvider = new LiveDeribitFutureChainProvider();
            var symbol = mapper.GetFutureSymbol("BTC-PERPETUAL");
            Assert.AreEqual(symbol.SecurityType, SecurityType.Future);
        }

        [Test, Ignore("测试期权Symbol转换")]
        public void GetOptionSymbolTest()
        {
            var mapper = new DeribitSymbolMapper();
            mapper.OptionChainProvider = new LiveDeribitOptionChainProvider();
            mapper.FutureChainProvider = new LiveDeribitFutureChainProvider();
            var symbol = mapper.GetOptionSymbol("BTC-25DEC20-10000-C");
            Assert.AreEqual(symbol.SecurityType, SecurityType.Option);
        }

        [Test, Ignore("测试从名字解析Symbol")]
        public void ParseSymbolFromNameTest()
        {
            var s1 = DeribitSymbolMapper.ParseSymbolFromName("BTC-PERPETUAL");
            Assert.AreEqual(s1.SecurityType, SecurityType.Future);

            var s2 = DeribitSymbolMapper.ParseSymbolFromName("BTC-25DEC20-10000-C");
            Assert.AreEqual(s2.SecurityType, SecurityType.Option);

            var s3 = DeribitSymbolMapper.ParseSymbolFromName("btc_usd");
            Assert.AreEqual(s3.SecurityType, SecurityType.Crypto);
        }

        [Test, Ignore("历史市价条件单转qc order, get_stop_order_history的查询结果")]
        public void DeribitHistoryStopMarketOrderToQcOrderTest()
        {
            DeribitMessages.StopOrderEntity morder=null;
            //市价触发
            var json1 = @"
                        {
                    'trigger': 'last_price',
                    'timestamp': 1597050298406,
                    'stop_price': 11990,
                    'stop_id': 'SLTB-3120273',
                    'request': 'trigger:order',
                    'reduce_only': false,
                    'price': 12169,
                    'post_only': false,
                    'order_type': 'market',
                    'order_state': 'new',
                    'order_id': '4303587198',
                    'last_update_timestamp': 1597050276425,
                    'instrument_name': 'BTC-PERPETUAL',
                    'direction': 'buy',
                    'amount': 1000
                }
             ";
            try
            {
                morder = JsonConvert.DeserializeObject<DeribitMessages.StopOrderEntity>(json1);
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
            }
            var order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is StopMarketOrder);
            Assert.AreEqual(order.BrokerId.Count,2);
            Assert.AreEqual(order.BrokerId[0], "4303587198");
            Assert.AreEqual(order.Status,OrderStatus.Submitted);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);
            //市价撤销
            var json2 = @"
                        {
                'trigger': 'last_price',
                'timestamp': 1597050187423,
                'stop_price': 12500,
                'stop_id': 'SLTB-3120264',
                'request': 'cancel',
                'reduce_only': null,
                'price': 'market_price',
                'post_only': null,
                'order_type': 'market',
                'order_state': 'cancelled',
                'order_id': null,
                'last_update_timestamp': 1597050139611,
                'instrument_name': 'BTC-PERPETUAL',
                'direction': 'buy',
                'amount': 10000
            }
             ";

            if (json2.Contains("\'price\': \'market_price'"))
            {
                json2 = json2.Replace("\'price\': \'market_price'", "\'price\':0");
            }
            //市价触发
            try
            {
                morder = JsonConvert.DeserializeObject<DeribitMessages.StopOrderEntity>(json2);
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
            }
            order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is StopMarketOrder);
            Assert.AreEqual(order.Status, OrderStatus.Canceled);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);
        }


        [Test, Ignore("历史现价条件单转qc order, get_stop_order_history的查询结果")]
        public void DeribitHistoryStopLimitOrderToQcOrderTest()
        {
            DeribitMessages.StopOrderEntity morder = null;
            //市价触发
            var json1 = @"
                        {
                'trigger': 'last_price',
                'timestamp': 1596595143741,
                'stop_price': 11171.5,
                'stop_id': 'SLTB-3097949',
                'request': 'trigger:order',
                'reduce_only': false,
                'price': 11171.5,
                'post_only': false,
                'order_type': 'limit',
                'order_state': 'new',
                'order_id': '4288975187',
                'last_update_timestamp': 1596595114140,
                'instrument_name': 'BTC-PERPETUAL',
                'direction': 'buy',
                'amount': 1000
            }
             ";
            try
            {
                morder = JsonConvert.DeserializeObject<DeribitMessages.StopOrderEntity>(json1);
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
            }
            var order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is StopLimitOrder);
            Assert.AreEqual(order.BrokerId.Count, 2);
            Assert.AreEqual(order.BrokerId[0], "4288975187");
            Assert.AreEqual(order.Status, OrderStatus.Submitted);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);
            //市价撤销
            var json2 = @"
                        {
                'trigger': 'last_price',
                'timestamp': 1597048291654,
                'stop_price': 12500,
                'stop_id': 'SLTB-3120121',
                'request': 'cancel',
                'reduce_only': null,
                'price': 11000,
                'post_only': null,
                'order_type': 'limit',
                'order_state': 'cancelled',
                'order_id': null,
                'last_update_timestamp': 1597047937886,
                'instrument_name': 'BTC-PERPETUAL',
                'direction': 'buy',
                'amount': 10000
            }
             ";

            if (json2.Contains("\'price\': \'market_price'"))
            {
                json2 = json2.Replace("\'price\': \'market_price'", "\'price\':0");
            }
            //市价触发
            try
            {
                morder = JsonConvert.DeserializeObject<DeribitMessages.StopOrderEntity>(json2);
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.Message);
            }
            order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is StopLimitOrder);
            Assert.AreEqual(order.Status, OrderStatus.Canceled);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);
        }


        [Test, Ignore("历史订单转qc order, get_order_history_by_currency 的查询结果")]
        public void DeribitHistoryOrderToQcOrderTest()
        {
            //限价单
            var json = @"
                        {
                'web': true,
                'time_in_force': 'good_til_cancelled',
                'replaced': false,
                'reduce_only': false,
                'profit_loss': 0.015,
                'price': 0.036,
                'post_only': false,
                'order_type': 'limit',
                'order_state': 'cancelled',
                'order_id': '3680502929',
                'max_show': 3,
                'last_update_timestamp': 1584411056987,
                'label': '',
                'is_liquidation': false,
                'instrument_name': 'BTC-27MAR20-6000-C',
                'filled_amount': 1.2,
                'direction': 'sell',
                'creation_timestamp': 1584410975167,
                'commission': 0.00048,
                'average_price': 0.036,
                'api': false,
                'amount': 3
            }
             ";
            var morder = JsonConvert.DeserializeObject<DeribitMessages.Order>(json);
            var order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is LimitOrder);
            Assert.AreEqual(order.BrokerId.Count, 1);
            Assert.AreEqual(order.BrokerId[0], "3680502929");
            Assert.AreEqual(order.Status, OrderStatus.Canceled);
            Assert.AreEqual(order.Direction, OrderDirection.Sell);

            //市价单
            json = @"
                        {
                'web': true,
                'time_in_force': 'good_til_cancelled',
                'replaced': false,
                'reduce_only': false,
                'profit_loss': 0,
                'price': 0.1265,
                'post_only': false,
                'order_type': 'market',
                'order_state': 'filled',
                'order_id': '3469261473',
                'max_show': 1,
                'last_update_timestamp': 1581064990864,
                'label': '',
                'is_liquidation': false,
                'instrument_name': 'BTC-27MAR20-10000-C',
                'filled_amount': 1,
                'direction': 'buy',
                'creation_timestamp': 1581064990864,
                'commission': 0.0004,
                'average_price': 0.1018,
                'api': false,
                'amount': 1
            }
             ";
            morder = JsonConvert.DeserializeObject<DeribitMessages.Order>(json);
            order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is MarketOrder);
            Assert.AreEqual(order.BrokerId.Count, 1);
            Assert.AreEqual(order.BrokerId[0], "3469261473");
            Assert.AreEqual(order.Status, OrderStatus.Filled);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);

            //限价条件单
            json = @"
                        {
                'web': true,
                'triggered': true,
                'trigger': 'last_price',
                'time_in_force': 'good_til_cancelled',
                'stop_price': 11171.5,
                'stop_order_id': 'SLTB-3097949',
                'replaced': false,
                'reduce_only': false,
                'profit_loss': -0.00174845,
                'price': 11171.5,
                'post_only': false,
                'order_type': 'limit',
                'order_state': 'filled',
                'order_id': '4288975187',
                'max_show': 1000,
                'last_update_timestamp': 1596595143740,
                'label': '',
                'is_liquidation': false,
                'instrument_name': 'BTC-PERPETUAL',
                'filled_amount': 1000,
                'direction': 'buy',
                'creation_timestamp': 1596595143740,
                'commission': 0.00006714,
                'average_price': 11171.5,
                'api': false,
                'amount': 1000
            }
             ";
            morder = JsonConvert.DeserializeObject<DeribitMessages.Order>(json);
            order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is StopLimitOrder);
            Assert.AreEqual(order.BrokerId.Count, 2);
            Assert.AreEqual(order.BrokerId[0], "4288975187");
            Assert.AreEqual(order.Status, OrderStatus.Filled);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);

            //市价条件单
            json = @"
                        {
                'web': true,
                'triggered': true,
                'trigger': 'last_price',
                'time_in_force': 'good_til_cancelled',
                'stop_price': 11991,
                'stop_order_id': 'SLTB-3120343',
                'replaced': false,
                'reduce_only': false,
                'profit_loss': 0,
                'price': 12170,
                'post_only': false,
                'order_type': 'market',
                'order_state': 'filled',
                'order_id': '4303629397',
                'max_show': 1000,
                'last_update_timestamp': 1597051613490,
                'label': '',
                'is_liquidation': false,
                'instrument_name': 'BTC-PERPETUAL',
                'filled_amount': 1000,
                'direction': 'buy',
                'creation_timestamp': 1597051613490,
                'commission': 0.00006254,
                'average_price': 11991.97,
                'api': false,
                'amount': 1000
            }
             ";
            morder = JsonConvert.DeserializeObject<DeribitMessages.Order>(json);
            order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is StopMarketOrder);
            Assert.AreEqual(order.BrokerId.Count, 2);
            Assert.AreEqual(order.BrokerId[0], "4303629397");
            Assert.AreEqual(order.Status, OrderStatus.Filled);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);
        }

        [Test, Ignore("挂单转qc order, get_open_orders_by_currency 的查询结果")]
        public void DeribitGetOpenOrderToQcOrderTest()
        {
            //普通限价单
            var json = @"
                       {
                'web': true,
                'time_in_force': 'good_til_cancelled',
                'replaced': false,
                'reduce_only': false,
                'profit_loss': 0,
                'price': 10000,
                'post_only': false,
                'order_type': 'limit',
                'order_state': 'open',
                'order_id': '4416696761',
                'max_show': 10000,
                'last_update_timestamp': 1599099388103,
                'label': '',
                'is_liquidation': false,
                'instrument_name': 'BTC-PERPETUAL',
                'filled_amount': 0,
                'direction': 'buy',
                'creation_timestamp': 1599099388103,
                'commission': 0,
                'average_price': 0,
                'api': false,
                'amount': 10000
            }
             ";
            var morder = JsonConvert.DeserializeObject<DeribitMessages.Order>(json);
            var order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is LimitOrder);
            Assert.AreEqual(order.BrokerId.Count, 1);
            Assert.AreEqual(order.BrokerId[0], "4416696761");
            Assert.AreEqual(order.Status, OrderStatus.Submitted);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);

            //条件限价单 未触发
            json = @"
                     {
                'web': true,
                'triggered': false,
                'trigger': 'last_price',
                'time_in_force': 'good_til_cancelled',
                'stop_price': 11800,
                'replaced': false,
                'reduce_only': false,
                'profit_loss': 0,
                'price': 10000,
                'post_only': false,
                'order_type': 'stop_limit',
                'order_state': 'untriggered',
                'order_id': 'SLTB-3395356',
                'max_show': 10000,
                'last_update_timestamp': 1599099422120,
                'label': '',
                'is_liquidation': false,
                'instrument_name': 'BTC-PERPETUAL',
                'direction': 'buy',
                'creation_timestamp': 1599099422120,
                'api': false,
                'amount': 10000
            }
             ";
            morder = JsonConvert.DeserializeObject<DeribitMessages.Order>(json);
            order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is StopLimitOrder);
            Assert.AreEqual(order.BrokerId.Count, 1);
            Assert.AreEqual(order.BrokerId[0], "SLTB-3395356");
            Assert.AreEqual(order.Status, OrderStatus.Submitted);
            Assert.AreEqual(((StopLimitOrder)order).StopTriggered, false);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);


            //条件限价单 已触发
            json = @"
                     {
                'web': true,
                'triggered': true,
                'trigger': 'last_price',
                'time_in_force': 'good_til_cancelled',
                'stop_price': 11420,
                'stop_order_id': 'SLTB-3395370',
                'replaced': false,
                'reduce_only': false,
                'profit_loss': 0,
                'price': 11000,
                'post_only': false,
                'order_type': 'limit',
                'order_state': 'open',
                'order_id': '4416712493',
                'max_show': 10000,
                'last_update_timestamp': 1599099851325,
                'label': '',
                'is_liquidation': false,
                'instrument_name': 'BTC-PERPETUAL',
                'filled_amount': 0,
                'direction': 'buy',
                'creation_timestamp': 1599099851325,
                'commission': 0,
                'average_price': 0,
                'api': false,
                'amount': 10000
            }
             ";
            morder = JsonConvert.DeserializeObject<DeribitMessages.Order>(json);
            order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is StopLimitOrder);
            Assert.AreEqual(order.BrokerId.Count, 2);
            Assert.AreEqual(order.BrokerId[0], "4416712493");
            Assert.AreEqual(order.Status, OrderStatus.Submitted);
            Assert.AreEqual(((StopLimitOrder)order).StopTriggered, true);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);


            //条件现价单 未触发
            json = @"
                   {
                'web': true,
                'triggered': false,
                'trigger': 'last_price',
                'time_in_force': 'good_til_cancelled',
                'stop_price': 11800,
                'replaced': false,
                'reduce_only': false,
                'profit_loss': 0,
                'price': 'market_price',
                'post_only': false,
                'order_type': 'stop_market',
                'order_state': 'untriggered',
                'order_id': 'SLTB-3395355',
                'max_show': 10000,
                'last_update_timestamp': 1599099408961,
                'label': '',
                'is_liquidation': false,
                'instrument_name': 'BTC-PERPETUAL',
                'direction': 'buy',
                'creation_timestamp': 1599099408961,
                'api': false,
                'amount': 10000
            }
             ";
            if (json.Contains("\'price\': \'market_price'"))
            {
                json = json.Replace("\'price\': \'market_price'", "\'price\':0");
            }
            morder = JsonConvert.DeserializeObject<DeribitMessages.Order>(json);
            order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is StopMarketOrder);
            Assert.AreEqual(order.BrokerId.Count, 1);
            Assert.AreEqual(order.BrokerId[0], "SLTB-3395355");
            Assert.AreEqual(order.Status, OrderStatus.Submitted);
            Assert.AreEqual(((StopMarketOrder)order).StopTriggered, false);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);


            //条件现价单 已触发
            json = @"
                   {
                  'web': true,
                  'triggered': true,
                  'trigger': 'last_price',
                  'time_in_force': 'good_til_cancelled',
                  'stop_price': 11440,
                  'replaced': false,
                  'reduce_only': false,
                  'profit_loss': 0,
                  'price': 'market_price',
                  'post_only': false,
                  'order_type': 'stop_market',
                  'order_state': 'triggered',
                  'order_id': 'SLTB-3395396',
                  'max_show': 10000,
                  'last_update_timestamp': 1599101876140,
                  'label': '',
                  'is_liquidation': false,
                  'instrument_name': 'BTC-PERPETUAL',
                  'filled_amount': 0,
                  'direction': 'buy',
                  'creation_timestamp': 1599101876140,
                  'commission': 0,
                  'average_price': 0,
                  'api': false,
                  'amount': 10000
                }
             ";
            if (json.Contains("\'price\': \'market_price'"))
            {
                json = json.Replace("\'price\': \'market_price'", "\'price\':0");
            }
            morder = JsonConvert.DeserializeObject<DeribitMessages.Order>(json);
            order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is StopMarketOrder);
            Assert.AreEqual(order.BrokerId.Count, 1);
            Assert.AreEqual(order.BrokerId[0], "SLTB-3395396");
            Assert.AreEqual(order.Status, OrderStatus.Submitted);
            Assert.AreEqual(((StopMarketOrder)order).StopTriggered, true);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);

            //条件现价单 已触发
            json = @"
                   {
                  'web': true,
                  'triggered': true,
                  'trigger': 'last_price',
                  'time_in_force': 'good_til_cancelled',
                  'stop_price': 11440,
                  'stop_order_id': 'SLTB-3395396',
                  'replaced': false,
                  'reduce_only': false,
                  'profit_loss': 0,
                  'price': 11611,
                  'post_only': false,
                  'order_type': 'market',
                  'order_state': 'filled',
                  'order_id': '4416787027',
                  'max_show': 10000,
                  'last_update_timestamp': 1599102006473,
                  'label': '',
                  'is_liquidation': false,
                  'instrument_name': 'BTC-PERPETUAL',
                  'filled_amount': 10000,
                  'direction': 'buy',
                  'creation_timestamp': 1599102006473,
                  'commission': 0.00043695,
                  'average_price': 11443,
                  'api': false,
                  'amount': 10000
                }
             ";
            if (json.Contains("\'price\': \'market_price'"))
            {
                json = json.Replace("\'price\': \'market_price'", "\'price\':0");
            }
            morder = JsonConvert.DeserializeObject<DeribitMessages.Order>(json);
            order = _deribitBrokerage.ConvertOrder(morder);
            Assert.IsTrue(order is StopMarketOrder);
            Assert.AreEqual(order.BrokerId.Count, 2);
            Assert.AreEqual(order.BrokerId[0], "4416787027");
            Assert.AreEqual(order.Status, OrderStatus.Filled);
            Assert.AreEqual(((StopMarketOrder)order).StopTriggered, true);
            Assert.AreEqual(order.Direction, OrderDirection.Buy);
        }
    }
}