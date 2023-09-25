using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Interfaces;
using QuantConnect.Securities.Option;
using QuantConnect.Securities;
using QLNet;
using System.Diagnostics;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// This example demonstrates how to add options for a given underlying equity security.
    /// It also shows how you can prefilter contracts easily based on strikes and expirations, and how you
    /// can inspect the option chain to pick a specific option contract to trade.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="options" />
    /// <meta name="tag" content="filter selection" />
    public class SVIMarketMaking : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        public override string PortfolioManagerName { get; } = "deribit";
        private const string UnderlyingTicker = "BTCUSD";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Crypto, Market.Deribit);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.Deribit);
        private double _forward = 0.03;
        private double _tau = 1.0;
        private decimal _minimum_bid_ask_spread = 0.005m;
        private decimal _minimum_tick = 0.0001m;
        private int _order_size = 1;
        private int _tick_precision = 4;

        public override void Initialize()
        {
            //SetTimeZone(TimeZones.Shanghai);
            SetCash(1000000);
            SetStartDate(2020, 05, 01);
            SetEndDate(2020, 05, 04);
            SetCash("BTC", 100);

            var equity = AddCrypto(UnderlyingTicker, Resolution.Minute, Market.Deribit);
            var option = AddOption(UnderlyingTicker, Resolution.Minute, Market.Deribit);

            // set our strike/expiry filter for this option chain
            option.SetFilter(u => u.Strikes(-100, +100)
                                   .Expiration(TimeSpan.FromDays(7), TimeSpan.FromDays(200)));

            // use the underlying equity as the benchmark
            SetBenchmark(equity.Symbol);

        }

        public override void OnWarmupFinished()
        {
        }

        /// <summary>
        /// Event - v3.0 DATA EVENT HANDLER: (Pattern) Basic template for user to override for receiving all subscription data in a single event
        /// </summary>
        /// <param name="slice">The current slice of data keyed by symbol string</param>
        public override void OnData(Slice slice)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            int pricing_count = 0;
            OptionChain chain;
            if (slice.OptionChains.TryGetValue(OptionSymbol, out chain) && chain.Underlying.Price != 0)
            {
                var expiryList = chain.OrderBy(x => x.Expiry).Select(x => x.Expiry).Distinct().ToList();
                var near_month = expiryList[0];
                var call = chain.Where(x => x.Right == OptionRight.Call).Where(x => x.Expiry == near_month).OrderBy(x => x.Strike);
                var put = chain.Where(x => x.Right == OptionRight.Put).Where(x => x.Expiry == near_month).OrderBy(x => x.Strike);
                var callTrade = call.Where(x => x.BidPrice > 0).Where(x => x.AskPrice > 0).Where(x => (x.AskPrice - x.BidPrice) / x.AskPrice < 0.5m);
                var putTrade = put.Where(x => x.BidPrice > 0).Where(x => x.AskPrice > 0).Where(x => (x.AskPrice - x.BidPrice) / x.AskPrice < 0.5m);
                var strikeList = callTrade.Select(x => x.Strike).Distinct().ToList();

                List<double> strikes = new InitializedList<double>(strikeList.Count, 0.0);
                List<double> BSVols = new InitializedList<double>(strikeList.Count, 0.0);
                List<double> sviVols = new InitializedList<double>(strikeList.Count, 0.0);
                for (int i = 0; i < strikeList.Count; ++i)
                {
                    // take calls only;
                    var callContract = (
                                from optionContract in chain
                                where optionContract.Right == OptionRight.Call
                                where optionContract.Expiry == near_month
                                where optionContract.Strike == strikeList[i]
                                select optionContract
                                ).FirstOrDefault();

                    BSVols[i] = decimal.ToDouble(callContract.ImpliedVolatility);
                    strikes[i] = decimal.ToDouble(strikeList[i]);
                }
                //Log(strikes.ToString());
                //Log(sviVols.ToString());


                SviInterpolation svi = new SviInterpolation(strikes, strikes.Count, BSVols, _tau,
                                                             _forward, null, null, null,
                                                             null, null, false, false, false,
                                                             false, false, false, null,
                                                             null, 1E-8, false,
                                                             0); //don't allow for random start values

                svi.enableExtrapolation();
                svi.update();
                Log($"datetime: {slice.Time} a: {svi.a()} b: {svi.b()} sigma: {svi.sigma()} rho: {svi.rho()} m: {svi.m()} residual: {svi.rmsError()}");
                for (int i = 0; i < strikes.Count; ++i)
                {
                    sviVols[i] = svi.value(strikes[i]);
                    // take calls only;
                    var callContract = (
                                from optionContract in chain
                                where optionContract.Right == OptionRight.Call
                                where optionContract.Expiry == near_month
                                where optionContract.Strike == strikeList[i]
                                select optionContract
                                ).FirstOrDefault();
                    callContract.PricingVolatility = (decimal)sviVols[i];
                    
                    //Log($"time: {slice.Time} strike: {strikes[i]} impliedVolatility: {BSVols[i]} localVol: {sviVols[i]}");
                    //Log($"time: {slice.Time} strike: {strikes[i]} marketMidPrice: {(callContract.BidPrice + callContract.AskPrice) / 2} theoPrice: {callContract.TheoreticalPrice}");
                    var mid_prc = (callContract.BidPrice + callContract.AskPrice) / 2;
                    //callContract.ResetOptionPriceModel();
                    var theo_prc = Math.Round(callContract.TheoreticalPrice, _tick_precision, MidpointRounding.AwayFromZero);
                    var bid_ask_spread = Math.Max(callContract.AskPrice - callContract.BidPrice, _minimum_bid_ask_spread);
                    var theo_bid = theo_prc - bid_ask_spread / 2;
                    var theo_ask = theo_prc + bid_ask_spread / 2;

                    /*// quote the bid;
                    LimitOrder(callContract.Symbol, _order_size, theo_bid);
                    // quote the ask;
                    LimitOrder(callContract.Symbol, -_order_size, theo_ask);*/
                }
                pricing_count = strikes.Count;
            }
            stopwatch.Stop();
            Log("------------elapse" + stopwatch.Elapsed.TotalMilliseconds + "-----------contracts" + pricing_count);
        }

        /// <summary>
        /// Order fill event handler. On an order fill update the resulting information is passed to this method.
        /// </summary>
        /// <param name="orderEvent">Order event details containing details of the evemts</param>
        /// <remarks>This method can be called asynchronously and so should only be used by seasoned C# experts. Ensure you use proper locks on thread-unsafe objects</remarks>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {

        }


        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp, Language.Python };

        /// <summary>
        /// This is used by the regression test system to indicate what the expected statistics are from running the algorithm
        /// </summary>
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
        };
    }
}