using System;
using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Securities;
using QuantConnect.Tests.Engine.DataFeeds;
using QuantConnect.Securities.Future;


namespace QuantConnect.Tests.Common.Securities.Futures
{
    public class DeribitFutureMarginModelTest
    {
        private static Symbol _symbol;
        private static readonly string _cashSymbol = "BTC";
        private static FakeOrderProcessor _fakeOrderProcessor;
        protected Symbol CallOptionSymbol => Symbol.CreateOption(
            "BTCUSD",
            Market.Deribit,
            OptionStyle.European,
            OptionRight.Call,
            9000,
            new DateTime(2020, 9, 22),
            "BTC-22SEP20-9000-C"
        );

        protected Symbol PutOptionSymbol => Symbol.CreateOption(
            "BTCUSD",
            Market.Deribit,
            OptionStyle.European,
            OptionRight.Put,
            9000,
            new DateTime(2020, 9, 22),
            "BTC-22SEP20-9000-P"
        );


        private static QCAlgorithm GetAlgorithm()
        {
            SymbolCache.Clear();
            // Initialize algorithm
            var algo = new QCAlgorithm();
            algo.SetFinishedWarmingUp();
            _fakeOrderProcessor = new FakeOrderProcessor();
            algo.Transactions.SetOrderProcessor(_fakeOrderProcessor);
            return algo;
        }


        private Security InitAndGetCrypto(QCAlgorithm algo, string symbol = "BTCUSD")
        {
            algo.SubscriptionManager.SetDataManager(new DataManagerStub(algo));
            var security = algo.AddCrypto(symbol);
            algo.Portfolio.Securities.Add(security);
            security.FeeModel = new ConstantFeeModel(0);
            return security;
        }

        private Security CreateFuture(QCAlgorithm algo)
        {
            var security = algo.AddFutureContract(Symbol.CreateFuture("BTC-PERPETUAL", Market.Deribit, new DateTime(2030, 12, 31), "BTC-PERPETUAL"), Resolution.Tick);
            algo.Portfolio.Securities.Add(security);
            security.FeeModel = new ConstantFeeModel(0);
            return security;
        }

        private Security CreateCallOption(QCAlgorithm algo)
        {
            var security = algo.AddOptionContract(CallOptionSymbol, Resolution.Tick);
            algo.Portfolio.Securities.Add(security);
            security.FeeModel = new ConstantFeeModel(0);
            return security;
        }

        private Security CreatePutOption(QCAlgorithm algo)
        {
            var security = algo.AddOptionContract(PutOptionSymbol, Resolution.Tick);
            algo.Portfolio.Securities.Add(security);
            security.FeeModel = new ConstantFeeModel(0);
            return security;
        }

        private static void SetPrice(QCAlgorithm algo, Security security, decimal price)
        {
            var tick = new Tick()
            {
                Time = DateTime.Now,
                Symbol = security.Symbol,
                Value = price,
                MarkPrice = price,
                SettlementPrice = price,
            };
            security.SetMarketPrice(tick);


            if (security.Type == SecurityType.Crypto)
            {
                algo.Portfolio.CashBook[security.Symbol.value.Substring(0, 3)].Update(tick);
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        [Test]
        public void GetMaintenanceMarginRequirementTest()
        {
            var algo = GetAlgorithm();

            algo.Portfolio.CashBook.Clear();
            algo.Portfolio.SetAccountCurrency("USD");
            algo.Portfolio.CashBook.Add("USD", new Cash("USD", 0, 1));
            var btcCoin = new Cash("BTC", 0, 0);
            algo.Portfolio.CashBook.Add("BTC", btcCoin);

            var underlying = InitAndGetCrypto(algo);
            var future = CreateFuture(algo);

            future.BuyingPowerModel = new DeribitFutureMarginModel();


            SetPrice(algo, underlying, 10000m);


            SetPrice(algo, future, 9000m);
            var quantity = 9000m;
            var buyMargin = DeribitFutureMarginModel.GetMaintenanceMarginRequirement(future, Math.Abs(quantity)) * Math.Abs(quantity);
            Assert.AreEqual(52.2m, buyMargin);

            quantity = -9000m;
            var sellMargin = DeribitFutureMarginModel.GetMaintenanceMarginRequirement(future, Math.Abs(quantity)) * Math.Abs(quantity);
            Assert.AreEqual(52.2m, sellMargin);
        }

        [Test]
        public void GetInitialMarginRequirementTest()
        {

            var algo = GetAlgorithm();

            algo.Portfolio.CashBook.Clear();
            algo.Portfolio.SetAccountCurrency("USD");
            algo.Portfolio.CashBook.Add("USD", new Cash("USD", 0, 1));
            var btcCoin = new Cash("BTC", 0, 0);
            algo.Portfolio.CashBook.Add("BTC", btcCoin);

            var underlying = InitAndGetCrypto(algo);
            var future = CreateFuture(algo);

            future.BuyingPowerModel = new DeribitFutureMarginModel();


            SetPrice(algo, underlying, 10000m);


            SetPrice(algo, future, 9000m);
            var quantity = 9000m;
            var buyMargin = DeribitFutureMarginModel.GetInitialMarginRequirement(future, Math.Abs(quantity)) * Math.Abs(quantity);
            Assert.AreEqual(90.45m, buyMargin);

            quantity = -9000m;
            var sellMargin = DeribitFutureMarginModel.GetInitialMarginRequirement(future, Math.Abs(quantity)) * Math.Abs(quantity);
            Assert.AreEqual(90.45m, sellMargin);
        }


        [Test]
        public void GetFutureInitialMarginTest()
        {
            var algo = GetAlgorithm();

            algo.Portfolio.CashBook.Clear();
            algo.Portfolio.SetAccountCurrency("USD");
            algo.Portfolio.CashBook.Add("USD", new Cash("USD", 0, 1));
            var btcCoin = new Cash("BTC", 0, 0);
            algo.Portfolio.CashBook.Add("BTC", btcCoin);

            var underlying = InitAndGetCrypto(algo);
            var future = CreateFuture(algo);

            future.BuyingPowerModel = new DeribitFutureMarginModel();


            SetPrice(algo, underlying, 5000m);


            future.Holdings.SetHoldings(10000, 10000);
            SetPrice(algo, future, 5000m);

            _fakeOrderProcessor.AddOrder(new LimitOrder()
            {
                id = 1,
                Symbol = future.symbol,
                LimitPrice = 8000m,
                Quantity = 10000m,
            });

            var marigin = DeribitFutureMarginModel.GetFutureInitialMargin(algo.Portfolio, future);
            Assert.AreEqual(202m, marigin);


            _fakeOrderProcessor.AddOrder(new LimitOrder()
            {
                id = 2,
                Symbol = future.symbol,
                LimitPrice = 11000m,
                Quantity = -40000m,
            });

            marigin = DeribitFutureMarginModel.GetFutureInitialMargin(algo.Portfolio, future);
            Assert.AreEqual(511m, marigin);
        }
    }
}
