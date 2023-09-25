using System;
using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Securities;
using QuantConnect.Tests.Engine.DataFeeds;
using QuantConnect.Securities.Option;

namespace QuantConnect.Tests.Common.Securities.Options
{
    [TestFixture]
    class DeribitOptionMarginModelTest
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
            var security = algo.AddFutureContract(Symbol.CreateFuture("BTC-PERPETUAL",Market.Deribit,new DateTime(2030,12,31), "BTC-PERPETUAL"),Resolution.Tick);
            algo.Portfolio.Securities.Add(security);
            security.FeeModel = new ConstantFeeModel(0);
            return security;
        }

        private Security CreateCallOption(QCAlgorithm algo)
        {
            var security = algo.AddOptionContract(CallOptionSymbol,Resolution.Tick);
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
                algo.Portfolio.CashBook[security.Symbol.value.Substring(0,3)].Update(tick);
            }
        }

        //////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        [Test]
        public void GetUnitMaintenanceMarginTest()
        {
            var algo =GetAlgorithm();
            
            algo.Portfolio.CashBook.Clear();
            algo.Portfolio.SetAccountCurrency("USD");
            algo.Portfolio.CashBook.Add("USD", new Cash("USD", 0, 1));
            var btcCoin = new Cash("BTC", 0, 0);
            algo.Portfolio.CashBook.Add("BTC",btcCoin);

            var underlying = InitAndGetCrypto(algo);
            //var future = CreateFuture(algo);
            var callOption = CreateCallOption(algo);
            var putOption = CreatePutOption(algo);


            callOption.BuyingPowerModel = new DeribitOptionMarginModel();
            putOption.BuyingPowerModel = new DeribitOptionMarginModel();


            ((Option)callOption).Underlying = underlying;
            ((Option)putOption).Underlying = underlying;

            SetPrice(algo,underlying, 10000m);

            //SetPrice(algo, future, 10000);


            
            SetPrice(algo, callOption, 0.025m);
            var buyCallMarigin = ((DeribitOptionMarginModel)callOption.BuyingPowerModel).GetUnitMaintenanceMargin(callOption, 1m);
            Assert.AreEqual(0m, buyCallMarigin);

            SetPrice(algo, putOption, 0.025m);
            var buyPutMarigin = ((DeribitOptionMarginModel)putOption.BuyingPowerModel).GetUnitMaintenanceMargin(putOption, 1m);
            Assert.AreEqual(0m, buyPutMarigin);

            // call Maintenance margin(BTC): 0.075 + mark price of the option
            SetPrice(algo, callOption, 0.025m);
            var sellCallMarigin = ((DeribitOptionMarginModel)callOption.BuyingPowerModel).GetUnitMaintenanceMargin(callOption, -1m);
            Assert.AreEqual(1000m, sellCallMarigin);

            // put Maintenance margin (BTC):Maximum (0.075, 0.075 * markprice_option) + mark_price_option
            SetPrice(algo, putOption, 0.025m);
            var sellPutMarigin = ((DeribitOptionMarginModel)putOption.BuyingPowerModel).GetUnitMaintenanceMargin(putOption, -1m);
            Assert.AreEqual(1000m, sellPutMarigin);

        }

        [Test]
        public void GetUnitInitialMarginTest()
        {
            //订单初始保证金公式
            var algo = GetAlgorithm();

            algo.Portfolio.CashBook.Clear();
            algo.Portfolio.SetAccountCurrency("USD");
            algo.Portfolio.CashBook.Add("USD", new Cash("USD", 0, 1));
            var btcCoin = new Cash("BTC", 0, 0);
            algo.Portfolio.CashBook.Add("BTC", btcCoin);

            var underlying = InitAndGetCrypto(algo);
            //var future = CreateFuture(algo);
            var callOption = CreateCallOption(algo);
            var putOption = CreatePutOption(algo);


            callOption.BuyingPowerModel = new DeribitOptionMarginModel();
            putOption.BuyingPowerModel = new DeribitOptionMarginModel();

            ((Option) callOption).Underlying = underlying;
            ((Option)putOption).Underlying = underlying;

            SetPrice(algo, underlying, 10000m);

            //订单的初始保证金 call put buy sell
            SetPrice(algo, callOption, 0.025m);
            var buyCallMarigin = ((DeribitOptionMarginModel)callOption.BuyingPowerModel).GetUnitInitialMargin(callOption, 1m,true);
            Assert.AreEqual(250m, buyCallMarigin);

            SetPrice(algo, putOption, 0.025m);
            var buyPutMarigin = ((DeribitOptionMarginModel)putOption.BuyingPowerModel).GetUnitInitialMargin(putOption, 1m, true);
            Assert.AreEqual(250m, buyPutMarigin);

            //Initial margin (BTC): Maximum (0.15 - Out of the Money Amount/Underlying MarkPrice, 0.1) + Mark Price of the option
            SetPrice(algo, callOption, 0.025m);
            var sellCallMarigin = ((DeribitOptionMarginModel)callOption.BuyingPowerModel).GetUnitInitialMargin(callOption, -1m, true);
            Assert.AreEqual(1750m, sellCallMarigin);

            //Initial margin (BTC): Maximum (Maximum (0.15 - Out of the Money Amount/Underlying MarkPrice, 0.1 )+ markprice_option, Maintenance Margin)
            SetPrice(algo, putOption, 0.025m);
            var sellPutMarigin = ((DeribitOptionMarginModel)putOption.BuyingPowerModel).GetUnitInitialMargin(putOption, -1m, true);
            Assert.AreEqual(1250m, sellPutMarigin);

            
            //持仓的初始保证金 call put long short
            SetPrice(algo, callOption, 0.025m);
            buyCallMarigin = ((DeribitOptionMarginModel)callOption.BuyingPowerModel).GetUnitInitialMargin(callOption, 1m, false);
            Assert.AreEqual(0m, buyCallMarigin);

            SetPrice(algo, putOption, 0.025m);
            buyPutMarigin = ((DeribitOptionMarginModel)putOption.BuyingPowerModel).GetUnitInitialMargin(putOption, 1m, false);
            Assert.AreEqual(0m, buyPutMarigin);

            //Initial margin (BTC): Maximum (0.15 - Out of the Money Amount/Underlying MarkPrice, 0.1) + Mark Price of the option
            SetPrice(algo, callOption, 0.025m);
            sellCallMarigin = ((DeribitOptionMarginModel)callOption.BuyingPowerModel).GetUnitInitialMargin(callOption, -1m, false);
            Assert.AreEqual(1750m, sellCallMarigin);


            //Initial margin (BTC): Maximum (Maximum (0.15 - Out of the Money Amount/Underlying MarkPrice, 0.1 )+ markprice_option, Maintenance Margin)
            SetPrice(algo, putOption, 0.025m);
            sellPutMarigin = ((DeribitOptionMarginModel)putOption.BuyingPowerModel).GetUnitInitialMargin(putOption, -1m, false);
            Assert.AreEqual(1250m, sellPutMarigin);

        }

        [Test]
        public void GetOptionInitialMarginTest()
        {
            //订单初始保证金公式
            var algo = GetAlgorithm();

            algo.Portfolio.CashBook.Clear();
            algo.Portfolio.SetAccountCurrency("USD");
            algo.Portfolio.CashBook.Add("USD", new Cash("USD", 0, 1));
            var btcCoin = new Cash("BTC", 0, 0);
            algo.Portfolio.CashBook.Add("BTC", btcCoin);

            var underlying = InitAndGetCrypto(algo);
            //var future = CreateFuture(algo);
            var callOption = CreateCallOption(algo);
            var putOption = CreatePutOption(algo);


            callOption.BuyingPowerModel = new DeribitOptionMarginModel();
            putOption.BuyingPowerModel = new DeribitOptionMarginModel();

            ((Option)callOption).Underlying = underlying;
            ((Option)putOption).Underlying = underlying;

            SetPrice(algo, underlying, 10000m);

            callOption.holdings.SetHoldings(0.02m, 1m);
            SetPrice(algo, callOption, 0.025m);
            _fakeOrderProcessor.AddOrder(new LimitOrder()
            {
                id = 1,
                Symbol = callOption.symbol,
                LimitPrice = 0.02m,
                Quantity = 0.1m,
            });

            var marigin =DeribitOptionMarginModel.GetOptionInitialMargin(algo.Portfolio, callOption);
            Assert.AreEqual(25m, marigin);


            SetPrice(algo, putOption, 0.025m);
            putOption.holdings.SetHoldings(0.02m, -1m);
            _fakeOrderProcessor.AddOrder(new LimitOrder()
            {
                id = 2,
                Symbol = putOption.symbol,
                LimitPrice = 0.02m,
                Quantity = 0.1m,
            });
            marigin = DeribitOptionMarginModel.GetOptionInitialMargin(algo.Portfolio, putOption);
            Assert.AreEqual(1250m, marigin);


            _fakeOrderProcessor.AddOrder(new LimitOrder()
            {
                id = 3,
                Symbol = putOption.symbol,
                LimitPrice = 0.02m,
                Quantity = 1.1m,
            });
            marigin = DeribitOptionMarginModel.GetOptionInitialMargin(algo.Portfolio, putOption);
            Assert.AreEqual(1300m, marigin);
        }
    }
}
