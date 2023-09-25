using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using Moq;
using QuantConnect.Algorithm;
using QuantConnect.Brokerages;
using QuantConnect.Brokerages.Deribit;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Securities;
using QuantConnect.Logging;
using Log = QuantConnect.Logging.Log;
using System.Diagnostics;
using QuantConnect.Orders;
using System.Linq;

namespace QuantConnect.Tests.Brokerages.Deribit
{
    [TestFixture]
    class DeribitBrokerageQuoteTest
    {
        private DeribitBrokerage _deribitBrokerage;

        public readonly Symbol Underlying = QuantConnect.Symbol.Create("BTCUSD", SecurityType.Crypto, Market.Deribit);

        [SetUp]
        public void InitializeBrokerage()
        {
            var securities = new SecurityManager(new TimeKeeper(DateTime.UtcNow, new[] { TimeZones.NewYork }));
            securities.Add(Underlying, CreateSecurity(Underlying));
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


                var openOrdersText = _deribitBrokerage.GetOpenOrders().Select(x => x.Symbol.ToString() + " " + x.Quantity);
                Log.Trace("deribitBrokerageTests.Teardown(): Open orders: " + string.Join(", ", openOrdersText));
                Assert.AreEqual(0, openOrdersText.Count(), "Failed to verify that there are zero open orders.");
            }
            finally
            {
                _deribitBrokerage?.Dispose();
            }
        }

        /// 测试行情数据延时（按交易所时间戳)
        



        /// 测试行情数据完整性
        /// 

        /// 测试期权链生成时间
        /// 

        /// 测试数据下载,能否正确下载，数据格式是否正确，数据是否正确，数据支持哪些类型
        /// 






    }
}
