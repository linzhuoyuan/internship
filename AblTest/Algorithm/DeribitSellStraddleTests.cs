using System.Globalization;
using AblTest.Engine.DataFeeds;
using Fasterflect;
using FtxApi.Rest.Models;
using Newtonsoft.Json;
using QuantConnect;
using QuantConnect.Algorithm.CSharp;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Securities.Future;


namespace AblTest.Algorithm;


public class HedgeCase
{
    /*
     * 把需要模拟的行情和持仓都写在这里，初始化时加载对应的instance的值。
     * 1.行情分为两部分，需要分别加载给algorithm，一部分是已经有的期权持仓的行情。另外一部分是未持仓的期权的行情。
     * 2.strikes,call_asks,call_bids,put_asks,put_bids代表未持仓的期权的行情。
     * 3.hold系列代表已持仓的期权，他们的行情单独写在hold系列变量中，且不与2中的行情重复。
     * 4.每个测试用例的正确答案使用gt_系列变量存储。
     * 5.每个测试用例单独加入注释，需要说明所测试的情况是什么。
     * 6.所有的期权当前的测试用例都只用于一天到期。如果是多个到期日的期权后续需要扩展HedgeCase的字段以支持其他到期日的期权。当前版本
     * 为了简化操作，只统一写一个期权的到期日而不为单个期权分别保留到期日字段。
     */
    public decimal _targetCallPosition;
    public decimal _targetPutPosition;
    public decimal markPrice;
    public string coin = "BTC";
    public decimal cash;
    public bool start_hedge;
    public int hedge_side;
    public decimal hedged_position;
    public decimal close_hedge_price;
    public decimal hedged_future;
    public decimal hedged_future_price;
    public int expiryYY = 2023;
    public int expiryMM = 3;
    public int expirydd = 16;
    public List<decimal> strikes = null; // 一个list，除了holding option之外的行情都写在这里
    public List<string> call_asks = null; // 对应strikes的大小，把call的asks写进来
    public List<string> call_bids = null;
    public List<string> put_asks = null;
    public List<string> put_bids = null;
    public List<decimal> hold_call_strikes = null;
    public List<decimal> hold_call_volume = null;
    public List<decimal> hold_call_premium = null;
    public List<decimal> hold_put_strikes = null;
    public List<decimal> hold_put_volume = null;
    public List<decimal> hold_put_premium = null;
    public decimal gt_strike;
    public decimal gt_quantity;
    public decimal hedge_cost;
    public string comment = "no comment";
    public int gt_future_order_num;
    public int gt_option_call_order_num;
    public int gt_option_put_order_num;
    public List<decimal> gt_option_call_order_strikes = null;
    public List<decimal> gt_option_call_order_quantities = null;
    public List<decimal> gt_option_call_order_premiums = null;
    public List<decimal> gt_option_put_order_strikes = null;
    public List<decimal> gt_option_put_order_quantities = null;
    public List<decimal> gt_option_put_order_premiums = null;
    public decimal gt_future_order_price;
    public decimal gt_future_order_quantity;
}

public class HedgeCases
{
    public List<HedgeCase>? cases;
}


[TestClass]
public class DeribitSellStraddleTests
{
    private DeribitSellStraddleBacktest _algorithm;


    [DataTestMethod]
    [DataRow("BTC", 0.1, "896.433%", DisplayName = "BTC")]
    public void DeribitSellStraddle(string coin, double lotSize, string pnl)
    {
        _algorithm = TestSetupHandler.TestAlgorithm = new DeribitSellStraddleBacktest();
        _algorithm.StartDate_ = new DateTime(2022, 9, 10);
        _algorithm.EndDate_ = new DateTime(2022, 9, 15);
        _algorithm.Coin = coin;
        _algorithm.OptionLotSize = (decimal)lotSize;

        var result = AlgorithmRunner.RunLocalBacktest(nameof(DeribitSellStraddleBacktest),
            new Dictionary<string, string> { { "Compounding Annual Return", pnl } },
            null!,
            Language.CSharp,
            AlgorithmStatus.Completed,
            setupHandler: typeof(TestSetupHandler).FullName!,
            initialCash: 10000);
    }

    public class TestSetupHandler : AlgorithmRunner.RegressionSetupHandlerWrapper
    {
        public static DeribitSellStraddleBacktest? TestAlgorithm { get; set; }

        public override IAlgorithm CreateAlgorithmInstance(AlgorithmNodePacket algorithmNodePacket, string assemblyPath, int loadTimeLimit = 60)
        {
            Algorithm = TestAlgorithm!;
            return Algorithm;
        }
    }

    private static Tick NewTick(Symbol symbol, string asks, string bids)
    {
        var asksData = JsonConvert.DeserializeObject<decimal[][]>(asks);
        var bidsData = JsonConvert.DeserializeObject<decimal[][]>(bids);
        return NewTick(symbol, asksData, bidsData);
    }

    private static Tick NewMarkPrice(Symbol symbol, decimal markPrice = 0)
    {
        return NewTick(symbol, Array.Empty<decimal[]>(), Array.Empty<decimal[]>(), markPrice);
    }

    private static Tick NewTick(Symbol symbol, decimal[][] asks, decimal[][] bids, decimal markPrice = 0)
    {
        var tick = new Tick();
        tick.Time = DateTime.UtcNow;
        tick.symbol = symbol;
        tick.MarkPrice = markPrice;
        tick.Value = markPrice;
        tick.AskPrice = asks.Length > 0 ? asks[0][0] : 0;
        tick.AskSize = asks.Length > 0 ? asks[0][1] : 0;
        tick.AskPrice1 = tick.AskPrice;
        tick.AskSize1 = tick.AskSize;
        tick.AskPrice2 = asks.Length > 1 ? asks[1][0] : 0;
        tick.AskSize2 = asks.Length > 1 ? asks[1][1] : 0;
        tick.AskPrice3 = asks.Length > 2 ? asks[2][0] : 0;
        tick.AskSize3 = asks.Length > 2 ? asks[2][1] : 0;
        tick.AskPrice4 = asks.Length > 3 ? asks[3][0] : 0;
        tick.AskSize4 = asks.Length > 3 ? asks[3][1] : 0;
        tick.AskPrice5 = asks.Length > 4 ? asks[4][0] : 0;
        tick.AskSize5 = asks.Length > 4 ? asks[4][1] : 0;

        tick.BidPrice = bids.Length > 0 ? bids[0][0] : 0;
        tick.BidSize = bids.Length > 0 ? bids[0][1] : 0;
        tick.BidPrice1 = tick.BidPrice;
        tick.BidSize1 = tick.BidSize;
        tick.BidPrice2 = bids.Length > 1 ? bids[1][0] : 0;
        tick.BidSize2 = bids.Length > 1 ? bids[1][1] : 0;
        tick.BidPrice3 = bids.Length > 2 ? bids[2][0] : 0;
        tick.BidSize3 = bids.Length > 2 ? bids[2][1] : 0;
        tick.BidPrice4 = bids.Length > 3 ? bids[3][0] : 0;
        tick.BidSize4 = bids.Length > 3 ? bids[3][1] : 0;
        tick.BidPrice5 = bids.Length > 4 ? bids[4][0] : 0;
        tick.BidSize5 = bids.Length > 4 ? bids[4][1] : 0;
        return tick;
    }

    private Ticks InitTestCase1(DeribitSellStraddleT algorithm)
    {
        var markPrice = 29474m;
        var coin = "BTC";

        algorithm.SetCash(coin, 10, markPrice);
        var ticks = new Ticks();
        var future = algorithm.AddPerpetual(coin, Resolution.Tick, Market.Deribit);
        var futureTick = NewMarkPrice(future.symbol, markPrice);
        ticks.Add(future.symbol, new List<Tick> { futureTick });
        future.SetMarketPrice(futureTick);

        var crypto = algorithm.AddCrypto($"{coin}USD", Resolution.Tick, Market.Deribit);
        var cryptoTick = NewMarkPrice(crypto.symbol, markPrice);
        ticks.Add(crypto.symbol, new List<Tick> { cryptoTick });
        crypto.SetMarketPrice(cryptoTick);

        algorithm.CryptoSymbol = crypto.symbol;
        algorithm.OptionSymbol = Symbol.Create($"{coin}USD", SecurityType.Option, Market.Deribit);
        algorithm.FuturesSymbol = future.symbol;

        var optionExpiry = new DateTime(2023, 3, 16);
        var list = new[]
        {
            new
            {
                righ = OptionRight.Call,
                strike = 30000,
                expiry = optionExpiry,
                asks = "[[0.0055, 8.4], [0.006, 11.2]]",
                bids="[[0.0045, 5.6], [0.004, 13.7], [0.0035, 41], [0.0025, 52.2]]"
            },
            new
            {
                righ = OptionRight.Call,
                strike = 30000,
                expiry = optionExpiry.AddDays(10),
                asks = "[[0.0055, 8.4], [0.006, 11.2]]",
                bids="[[0.0145, 5.6], [0.004, 13.7], [0.0035, 41], [0.0025, 52.2]]"
            },
            new
            {
                righ = OptionRight.Call,
                strike = 29750,
                expiry = optionExpiry,
                asks = "[[0.008, 7.1], [0.0085, 9.8], [0.012, 0.1]]",
                bids="[[0.007, 4.6], [0.0065 ,20.5], [0.0005, 0.1]]"
            },
            new
            {
                righ = OptionRight.Call,
                strike = 29500,
                expiry = optionExpiry,
                asks = "[[0.0515, 52.5]]",
                bids="[[0.0405, 5], [0.04, 16.4], [0.0095, 20.3], [0.0085, 2]]"
            },
            new
            {
                righ = OptionRight.Call,
                strike = 29250,
                expiry = optionExpiry,
                asks = "[[0.096, 9.2], [0.0965, 3.7]]",
                bids="[[0.085, 7.2], [0.0845, 5.2], [0.014, 3.5]]"
            },
        };
        foreach (var item in list)
        {
            var expiry = item.expiry.ToString("ddMMMyy", CultureInfo.CreateSpecificCulture("en-GB")).ToUpper();
            var alias = $"{crypto.symbol.value[..3]}-{expiry}-{item.strike:F0}-{(item.righ == OptionRight.Call ? "C" : "P")}";
            var optionSymbol = Symbol.CreateOption(crypto.symbol, Market.Deribit, OptionStyle.European, item.righ, item.strike, item.expiry, alias);
            var option = algorithm.AddOptionContract(optionSymbol, Resolution.Tick);
            var optionTick = NewTick(optionSymbol, item.asks, item.bids);
            ticks.Add(optionSymbol, new List<Tick> { optionTick });
            option.SetMarketPrice(optionTick);
        }

        algorithm._targetPutPosition = 47;
        algorithm._targetCallPosition = 132;

        return ticks;
    }

    private Ticks InitTestCase2(DeribitSellStraddleT algorithm)
    {
        var markPrice = 29474m;
        var coin = "BTC";

        algorithm.SetCash(coin, 10, markPrice);
        var ticks = new Ticks();
        var future = algorithm.AddPerpetual(coin, Resolution.Tick, Market.Deribit);
        var futureTick = NewMarkPrice(future.symbol, markPrice);
        ticks.Add(future.symbol, new List<Tick> { futureTick });
        future.SetMarketPrice(futureTick);

        var crypto = algorithm.AddCrypto($"{coin}USD", Resolution.Tick, Market.Deribit);
        var cryptoTick = NewMarkPrice(crypto.symbol, markPrice);
        ticks.Add(crypto.symbol, new List<Tick> { cryptoTick });
        crypto.SetMarketPrice(cryptoTick);

        algorithm.CryptoSymbol = crypto.symbol;
        algorithm.OptionSymbol = Symbol.Create($"{coin}USD", SecurityType.Option, Market.Deribit);
        algorithm.FuturesSymbol = future.symbol;

        var optionExpiry = new DateTime(2023, 3, 16);
        var list = new[]
        {
            new
            {
                righ = OptionRight.Put,
                strike = 28750,
                expiry = optionExpiry,
                asks = "[[0.0035,7], [0.004, 22.1], [0.0045, 5.1]]",
                bids="[[0.0025, 8.1], [0.002,10.1], [0.0005, 1]]"
            },
            new
            {
                righ = OptionRight.Put,
                strike = 28750,
                expiry = optionExpiry.AddDays(10),
                asks = "[[0.0035,7], [0.004, 22.1], [0.0045, 5.1]]",
                bids="[[0.0125, 8.1], [0.002,10.1], [0.0005, 1]]"
            },
            new
            {
                righ = OptionRight.Put,
                strike = 29000,
                expiry = optionExpiry,
                asks = "[[0.0055,29.3], [0.006, 11], [0.0065,3.4]]",
                bids="[[0.0045,7.2], [0.004,11.9], [0.0035, 2.9], [0.002, 1]]"
            },
            new
            {
                righ = OptionRight.Put,
                strike = 29250,
                expiry = optionExpiry,
                asks = "[]",
                bids="[[0.0275,8.9], [0.007,11.9], [0.0065, 5.9], [0.006, 2.3]]"
            },
            new
            {
                righ = OptionRight.Call,
                strike = 29500,
                expiry = optionExpiry,
                asks = "[[0.0425,0.2], [0.0430, 37.8]]",
                bids="[[0.0415,0.6], [0.041,51], [0.0105, 3.9], [0.0095, 2]]"
            },
        };
        foreach (var item in list)
        {
            var expiry = item.expiry.ToString("ddMMMyy", CultureInfo.CreateSpecificCulture("en-GB")).ToUpper();
            var alias = $"{crypto.symbol.value[..3]}-{expiry}-{item.strike:F0}-{(item.righ == OptionRight.Call ? "C" : "P")}";
            var optionSymbol = Symbol.CreateOption(crypto.symbol, Market.Deribit, OptionStyle.European, item.righ, item.strike, item.expiry, alias);
            var option = algorithm.AddOptionContract(optionSymbol, Resolution.Tick);
            var optionTick = NewTick(optionSymbol, item.asks, item.bids);
            ticks.Add(optionSymbol, new List<Tick> { optionTick });
            option.SetMarketPrice(optionTick);
        }

        algorithm._targetPutPosition = 33m;
        algorithm._targetCallPosition = 9.4m;

        return ticks;
    }

    public Ticks InitHedgeTestCase(DeribitSellStraddleT algorithm, HedgeCase hedgeCase)
    {
        // set strategy cash
        algorithm.SetCash(hedgeCase.coin, hedgeCase.cash, hedgeCase.markPrice);

        // set price ticks
        var ticks = new Ticks();
        var future = algorithm.AddPerpetual(hedgeCase.coin, Resolution.Tick, Market.Deribit);
        var futureTick = NewMarkPrice(future.symbol, hedgeCase.markPrice);
        ticks.Add(future.symbol, new List<Tick> { futureTick });
        future.SetMarketPrice(futureTick);

        var crypto = algorithm.AddCrypto($"{hedgeCase.coin}USD", Resolution.Tick, Market.Deribit);
        var cryptoTick = NewMarkPrice(crypto.symbol, hedgeCase.markPrice);
        ticks.Add(crypto.symbol, new List<Tick> { cryptoTick });
        crypto.SetMarketPrice(cryptoTick);
        algorithm.OptionSymbol = Symbol.Create($"{hedgeCase.coin}USD", SecurityType.Option, Market.Deribit);
        algorithm.FuturesSymbol = future.symbol;
        // algorithm._optionCushion = hedgeCase.hold_call_premium.Sum() + hedgeCase.hold_put_premium.Sum();
        algorithm._optionCushion = 0;

        algorithm.Securities.TryGetValue(future.Symbol, out var future_security);
        future_security.Holdings.SetHoldings(hedgeCase.hedged_future_price, hedgeCase.hedged_future);
        algorithm._hedgeCost = hedgeCase.hedge_cost;
        if (hedgeCase.start_hedge)
        {
            algorithm._startHedge = hedgeCase.start_hedge;
            algorithm._hedgeSide = hedgeCase.hedge_side;
            algorithm._hedgedDelta = hedgeCase.hedged_position;
            algorithm._closeHedgePrice = hedgeCase.close_hedge_price;
        }
        
        var optionExpiry = new DateTime(hedgeCase.expiryYY, hedgeCase.expiryMM, hedgeCase.expirydd);

        // add other option ticks
        for (int i = 0; i < hedgeCase.strikes.Count; i++)
        {
            // call
            var expiry = optionExpiry.ToString("ddMMMyy", CultureInfo.CreateSpecificCulture("en-GB")).ToUpper();
            var alias = $"{crypto.symbol.value[..3]}-{expiry}-{hedgeCase.strikes[i]:F0}-C";
            var optionSymbol = Symbol.CreateOption(crypto.symbol, Market.Deribit, OptionStyle.European, OptionRight.Call, hedgeCase.strikes[i], optionExpiry, alias);
            var option = algorithm.AddOptionContract(optionSymbol, Resolution.Tick);
            var optionTick = NewTick(optionSymbol, hedgeCase.call_asks[i], hedgeCase.call_bids[i]); ;
            ticks.Add(optionSymbol, new List<Tick> { optionTick });
            option.SetMarketPrice(optionTick);

            // put
            expiry = optionExpiry.ToString("ddMMMyy", CultureInfo.CreateSpecificCulture("en-GB")).ToUpper();
            alias = $"{crypto.symbol.value[..3]}-{expiry}-{hedgeCase.strikes[i]:F0}-P";
            optionSymbol = Symbol.CreateOption(crypto.symbol, Market.Deribit, OptionStyle.European, OptionRight.Put, hedgeCase.strikes[i], optionExpiry, alias);
            option = algorithm.AddOptionContract(optionSymbol, Resolution.Tick);
            optionTick = NewTick(optionSymbol, hedgeCase.put_asks[i], hedgeCase.put_bids[i]); ;
            ticks.Add(optionSymbol, new List<Tick> { optionTick });
            option.SetMarketPrice(optionTick);
        }

        for (int i = 0; i < hedgeCase.hold_call_volume.Count; i++)
        {
            if (hedgeCase.hold_call_volume[i] < 0)
            {
                algorithm._optionCushion += hedgeCase.hold_call_premium[i];
            } 
        }
        for(int i = 0; i < hedgeCase.hold_put_volume.Count; i++)
        {
            if (hedgeCase.hold_put_volume[i] < 0)
            {
                algorithm._optionCushion += hedgeCase.hold_put_premium[i];
            }
        }
        

        // add holding option positions to ticks
        for (int i = 0; i < hedgeCase.hold_call_strikes.Count; i++)
        {
            var expiry = optionExpiry.ToString("ddMMMyy", CultureInfo.CreateSpecificCulture("en-GB")).ToUpper();
            var alias = $"{crypto.symbol.value[..3]}-{expiry}-{hedgeCase.hold_call_strikes[i]:F0}-C";
            var optionSymbol = Symbol.CreateOption(crypto.symbol, Market.Deribit, OptionStyle.European, OptionRight.Call, hedgeCase.hold_call_strikes[i], optionExpiry, alias);
            //var option = algorithm.AddOptionContract(optionSymbol, Resolution.Tick);
            //var optionTick = NewTick(optionSymbol, hedgeCase.hold_call_asks[i], hedgeCase.hold_call_bids[i]);
            //ticks.Add(optionSymbol, new List<Tick> { optionTick });
            //option.SetMarketPrice(optionTick);
            
            algorithm.Securities.TryGetValue(optionSymbol, out var security);
            security.Holdings.SetHoldings(hedgeCase.hold_call_premium[i] / hedgeCase.markPrice / Math.Abs(hedgeCase.hold_call_volume[i]), hedgeCase.hold_call_volume[i]);
        }

        // add holding put option positions to ticks
        for (int i = 0; i < hedgeCase.hold_put_strikes.Count; i++)
        {
            var expiry = optionExpiry.ToString("ddMMMyy", CultureInfo.CreateSpecificCulture("en-GB")).ToUpper();
            var alias = $"{crypto.symbol.value[..3]}-{expiry}-{hedgeCase.hold_put_strikes[i]:F0}-P";
            var optionSymbol = Symbol.CreateOption(crypto.symbol, Market.Deribit, OptionStyle.European, OptionRight.Put, hedgeCase.hold_put_strikes[i], optionExpiry, alias);
            //var option = algorithm.AddOptionContract(optionSymbol, Resolution.Tick);
            //var optionTick = NewTick(optionSymbol, hedgeCase.hold_put_asks[i], hedgeCase.hold_put_bids[i]); ;
            //ticks.Add(optionSymbol, new List<Tick> { optionTick });
            //option.SetMarketPrice(optionTick);

            algorithm.Securities.TryGetValue(optionSymbol, out var security);
            security.Holdings.SetHoldings(hedgeCase.hold_put_premium[i] / hedgeCase.markPrice / Math.Abs(hedgeCase.hold_put_volume[i]), hedgeCase.hold_put_volume[i]);
        }

        


        algorithm._targetCallPosition = hedgeCase._targetCallPosition;
        algorithm._targetPutPosition = hedgeCase._targetPutPosition;

        return ticks;
    }

    [TestMethod]
    public void TestHedge()
    {


        string TestCasePath = "../../../TestCases/Hedge.json";

        try
        {
            using (StreamReader reader = new StreamReader(TestCasePath))
            {
                string jsonContent = reader.ReadToEnd();
                List<HedgeCase>? cases = JsonConvert.DeserializeObject<List<HedgeCase>>(jsonContent);

                if (cases == null)
                {
                    Assert.Fail("no test cases detected");
                }
                else
                {
                    foreach (HedgeCase c in cases)
                    {
                        var algorithm = new DeribitSellStraddleT();
                        // for algorithm to write temp json file in test environment
                        algorithm.TempJsonPath = "../../../temp/temp.json";
                        algorithm.SetDateTime(new DateTime(2023, 3, 15, 15, 20, 0, DateTimeKind.Utc));
                        var realtime = new TestRealTimeHandler();
                        algorithm.Schedule.SetEventSchedule(realtime);
                        var processor = new TestOrderProcessor();
                        processor.Initialize(algorithm);

                        algorithm._lossRatio = 1.0m;
                        algorithm._maxLoss = 0.02m;
                        algorithm.Transactions.SetOrderProcessor(processor);
                        algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
                        algorithm.SetLiveMode(true);
                        algorithm.SetFinishedWarmingUpWithoutNotify();

                        var ticks = this.InitHedgeTestCase(algorithm, c);
                        algorithm.InitTwap(100, 2);
                        algorithm.Coin = c.coin.ToUpper();
                        var comment = c.comment;
                        algorithm.Hedge(c.markPrice, 0, ticks);

                        algorithm.SetDateTime(new DateTime(2023, 3, 15, 15, 25, 0, DateTimeKind.Utc));
                        realtime.Run();
                        realtime.Run();
                        realtime.Run();

                        var all_orders = processor.GetOpenOrderTickets();
                        var call_option_orders = all_orders.Where(v => v.SecurityType == SecurityType.Option && v.Symbol.ID.OptionRight == OptionRight.Call)
                            .OrderBy(v => v.Symbol.ID.StrikePrice)
                            .ThenBy(v => v.SubmitRequest.LimitPrice)
                            .ToArray();
                        var put_option_orders = all_orders.Where(v => v.SecurityType == SecurityType.Option && v.Symbol.ID.OptionRight == OptionRight.Put)
                            .OrderBy(v => v.Symbol.ID.StrikePrice)
                            .ThenBy(v => v.SubmitRequest.LimitPrice)
                            .ToArray();
                        var future_orders = all_orders.Where(v => v.SecurityType == SecurityType.Future).ToArray();

                        // future order num
                        Assert.AreEqual(c.gt_future_order_num, future_orders.Length);
                        foreach (var order in future_orders)
                        {
                            Assert.AreEqual(c.gt_future_order_price, order.SubmitRequest.LimitPrice);
                            Assert.AreEqual(c.gt_future_order_quantity, order.Quantity);
                        }
                                       
                        // option order num
                        Assert.AreEqual(c.gt_option_call_order_num, call_option_orders.Length);
                        Assert.AreEqual(c.gt_option_put_order_num, put_option_orders.Length);
                        for (int idx = 0; idx < c.gt_option_call_order_num; idx++)
                        {
                            Assert.AreEqual(c.gt_option_call_order_strikes[idx], call_option_orders[idx].Symbol.ID.StrikePrice);
                            Assert.AreEqual(c.gt_option_call_order_quantities[idx], call_option_orders[idx].Quantity);
                            Assert.AreEqual(c.gt_option_call_order_premiums[idx], call_option_orders[idx].SubmitRequest.LimitPrice);
                        }

                        for (int idx = 0; idx < c.gt_option_put_order_num; idx++)
                        {
                            Assert.AreEqual(c.gt_option_put_order_strikes[idx], put_option_orders[idx].Symbol.ID.StrikePrice);
                            Assert.AreEqual(c.gt_option_put_order_quantities[idx], put_option_orders[idx].Quantity);
                            Assert.AreEqual(c.gt_option_put_order_premiums[idx], put_option_orders[idx].SubmitRequest.LimitPrice);
                        }

                        Console.WriteLine($"{c.comment} passed");
                    }
                }

            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            Assert.Fail(e.Message);
            throw;
        }
    }

    [TestMethod]
    public void TestFixOneSideTrade1()
    {
        var algorithm = new DeribitSellStraddleT();
        algorithm.SetDateTime(new DateTime(2023, 3, 15, 15, 20, 0, DateTimeKind.Utc));
        var processor = new TestOrderProcessor();
        processor.Initialize(algorithm);
        algorithm.Transactions.SetOrderProcessor(processor);
        algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
        algorithm.SetLiveMode(true);
        algorithm.SetFinishedWarmingUpWithoutNotify();
        var ticks = InitTestCase1(algorithm);
        algorithm.FixOneSideTrade(ticks);

        var orders = processor
            .GetOpenOrderTickets()
            .OrderBy(n => n.Symbol.ID.StrikePrice)
            .ThenByDescending(n => n.SubmitRequest.LimitPrice)
            .ToArray();
        Assert.AreEqual(5, orders.Length);

        var ticket = orders[0];
        Assert.AreEqual(29500, ticket.Symbol.ID.StrikePrice);
        Assert.AreEqual(-5, ticket.Quantity);
        Assert.AreEqual(0.0405m, ticket.SubmitRequest.LimitPrice);

        ticket = orders[1];
        Assert.AreEqual(29500, ticket.Symbol.ID.StrikePrice);
        Assert.AreEqual(-16.4m, ticket.Quantity);
        Assert.AreEqual(0.04m, ticket.SubmitRequest.LimitPrice);

        ticket = orders[2];
        Assert.AreEqual(29750, ticket.Symbol.ID.StrikePrice);
        Assert.AreEqual(-4.6m, ticket.Quantity);
        Assert.AreEqual(0.007m, ticket.SubmitRequest.LimitPrice);

        ticket = orders[3];
        Assert.AreEqual(29750, ticket.Symbol.ID.StrikePrice);
        Assert.AreEqual(-20.5m, ticket.Quantity);
        Assert.AreEqual(0.0065m, ticket.SubmitRequest.LimitPrice);

        ticket = orders[4];
        Assert.AreEqual(30000, ticket.Symbol.ID.StrikePrice);
        Assert.AreEqual(-5.6m, ticket.Quantity);
        Assert.AreEqual(0.0045m, ticket.SubmitRequest.LimitPrice);

        Console.WriteLine("Test fix one side function 1 passed");
    }

    [TestMethod]
    public void TestFixOneSideTrade2()
    {
        var algorithm = new DeribitSellStraddleT();
        algorithm.SetDateTime(new DateTime(2023, 3, 15, 15, 20, 0, DateTimeKind.Utc));
        var processor = new TestOrderProcessor();
        processor.Initialize(algorithm);
        algorithm.Transactions.SetOrderProcessor(processor);
        algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(algorithm));
        algorithm.SetLiveMode(true);
        algorithm.SetFinishedWarmingUpWithoutNotify();
        var ticks = InitTestCase2(algorithm);
        algorithm.FixOneSideTrade(ticks);

        var orders = processor
            .GetOpenOrderTickets()
            .OrderBy(n => n.Symbol.ID.StrikePrice)
            .ThenByDescending(n => n.SubmitRequest.LimitPrice)
            .ToArray();
        Assert.AreEqual(3, orders.Length);

        var ticket = orders[0];
        Assert.AreEqual(28750, ticket.Symbol.ID.StrikePrice);
        Assert.AreEqual(-8.1m, ticket.Quantity);
        Assert.AreEqual(0.0025m, ticket.SubmitRequest.LimitPrice);

        ticket = orders[1];
        Assert.AreEqual(29000, ticket.Symbol.ID.StrikePrice);
        Assert.AreEqual(-6.6m, ticket.Quantity);
        Assert.AreEqual(0.0045m, ticket.SubmitRequest.LimitPrice);

        ticket = orders[2];
        Assert.AreEqual(29250, ticket.Symbol.ID.StrikePrice);
        Assert.AreEqual(-8.9m, ticket.Quantity);
        Assert.AreEqual(0.0275m, ticket.SubmitRequest.LimitPrice);
    }
}