using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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

namespace QuantConnect.Tests.Brokerages.Deribit
{
    [TestFixture]
    public class DeribitBrokerageOrderTest
    {
        private DeribitBrokerage _deribitBrokerage;
        private readonly List<Order> _orders = new List<Order>();


        /// <summary>
        /// Gets the symbol to be traded, must be shortable
        /// </summary>
        protected Symbol OptionSymbol => Symbol.CreateOption(
            "BTCUSD",
            Market.Deribit,
            OptionStyle.European,
            OptionRight.Call,
            8000,
            new DateTime(2020, 6, 26),
            "BTC-26JUN20-8000-C"
        );
        protected Symbol BtcPerpetual => Symbol.CreateFuture("BTC-PERPETUAL", Market.Deribit, DateTime.Now.AddYears(1), "BTC-PERPETUAL");

        internal static Security CreateSecurity(Symbol symbol)
        {
            return new Security(
                SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork),
                new SubscriptionDataConfig(
                    typeof(TradeBar),
                    symbol,
                    Resolution.Tick,
                    TimeZones.NewYork,
                    TimeZones.NewYork,
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
            var securities = new SecurityManager(new TimeKeeper(DateTime.UtcNow, new[] { TimeZones.NewYork }));
            securities.Add(OptionSymbol, CreateSecurity(OptionSymbol));
            securities.Add(BtcPerpetual, CreateSecurity(BtcPerpetual));
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
        }

        [TearDown]
        public void Teardown()
        {
            try
            { // give the tear down a header so we can easily find it in the logs
                Log.Trace("-----");
                Log.Trace("DeribitBrokerageTests.Teardown(): Starting teardown...");
                Log.Trace("-----");

                var canceledResetEvent = new ManualResetEvent(false);
                var filledResetEvent = new ManualResetEvent(false);
                _deribitBrokerage.OrderStatusChanged += (sender, orderEvent) => {
                    if (orderEvent.Status == OrderStatus.Filled)
                    {
                        filledResetEvent.Set();
                    }
                    if (orderEvent.Status == OrderStatus.Canceled)
                    {
                        canceledResetEvent.Set();
                    }
                };


                Log.Trace("DeribitBrokerageTests.Teardown(): Canceling open orders...");

                var orders = _deribitBrokerage.GetOpenOrders();
                foreach (var order in orders)
                {
                    _deribitBrokerage.CancelOrder(order);
                    canceledResetEvent.WaitOne(3000);
                    canceledResetEvent.Reset();
                }

                Log.Trace("DeribitBrokerageTests.Teardown(): Liquidating open positions...");

        
                var openOrdersText = _deribitBrokerage.GetOpenOrders().Select(x => x.Symbol.ToString() + " " + x.Quantity);
                Log.Trace("deribitBrokerageTests.Teardown(): Open orders: " + string.Join(", ", openOrdersText));
                Assert.AreEqual(0, openOrdersText.Count(), "Failed to verify that there are zero open orders.");

                _orders.Clear();
            }
            finally
            {
                _deribitBrokerage?.Dispose();
            }
        }

        private int nextId = 0;

        [Test, Description("永续合约下单，然后等下单结果，查询持仓和报单变化")]
        public void PlacePerpetualOrder()
        {
            int quantity = 10;
            Assert.True(_deribitBrokerage.IsConnected);

            var oldholdings = _deribitBrokerage.GetAccountHoldings().Where(x => x.Symbol == BtcPerpetual).Sum(x => x.Quantity);

            var orderResetEvent = new ManualResetEvent(true);
            _deribitBrokerage.OrderStatusChanged += (sender, orderEvent) =>
            {
                int orderStatus = (int)orderEvent.Status;
                switch (orderEvent.Status)
                {
                    case OrderStatus.Filled:
                    {
                            // 查一下持仓
                       var holdings = _deribitBrokerage.GetAccountHoldings().Where(x=>x.Symbol == BtcPerpetual).Sum(x=>x.Quantity);

                       Assert.IsTrue(System.Math.Abs(holdings - oldholdings) == quantity);
                       orderResetEvent.Set();

                    }  
                        break;
                    case OrderStatus.Submitted:
                    {
                        // 查一下未成交单

                    }
                        break;
                    case OrderStatus.CancelPending:
                    {

                    }
                        break;
                    default:
                    {
                        int a = 0;
                    }
                        break;
                }
            };

            //     var order = new MarketOrder(BtcPerpetual, quantity, DateTime.Now);
            var order = new LimitOrder(BtcPerpetual, quantity, 10933.50m, DateTime.Now);
            _orders.Add(order);
            if (!_deribitBrokerage.PlaceOrder(order))
            {
                Log.Trace("_deribitBrokerage.PlaceOrder 报单失败");
                return;
            }

            orderResetEvent.WaitOne(-1);
        }


        /*
        [Test, Description("连接测试")]
        public void Connect()
        {

            bool connect = _deribitBrokerage.IsConnected;
            Assert.AreEqual(_deribitBrokerage.IsConnected, true);

           
        }

        [Test]
        public void ReConnect()
        {
            if (!_deribitBrokerage.IsConnected)
                return;

            _deribitBrokerage.Disconnect();

            Thread.Sleep(3000);

            Assert.IsTrue(!_deribitBrokerage.IsConnected);

            _deribitBrokerage.Connect();

            Thread.Sleep(3000);

            Assert.IsTrue(_deribitBrokerage.IsConnected);
        }
        */
    }
}
