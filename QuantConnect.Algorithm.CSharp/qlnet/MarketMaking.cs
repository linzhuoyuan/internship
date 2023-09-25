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
using Newtonsoft.Json;
using QuantConnect.Statistics;
using Accord.MachineLearning;
using System.Data.SqlTypes;

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
    public class MarketMaking : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string UnderlyingTicker = "SH510050";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Equity, Market.SSE);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.SSE);
        private DateTime last_day = new DateTime(2015, 1, 1);
        private Dictionary<DateTime, decimal> vix = new Dictionary<DateTime, decimal>();
        private Dictionary<DateTime, decimal> realizedVol = new Dictionary<DateTime, decimal>();
        private Dictionary<DateTime, decimal> rollingStd = new Dictionary<DateTime, decimal>();
        private Dictionary<DateTime, decimal> VIXHLDiff = new Dictionary<DateTime, decimal>();
        public string jsonpath1 = Directory.GetCurrentDirectory() + "\\VIX.json";
        public string jsonpath2 = Directory.GetCurrentDirectory() + "\\realizedVol.json";
        public string jsonpath3 = Directory.GetCurrentDirectory() + "\\rollingStd.json";
        public string jsonpath4 = Directory.GetCurrentDirectory() + "\\5dayVIXHLDiff.json";
        TimeSpan openTime = new DateTime(1, 1, 1, 14, 55, 0).TimeOfDay;
        private decimal stopLossThres = -0.08m;
        private decimal _thres = 50;
        private decimal order_unit = 50;
        private int _openUnit = 2;
        private bool already_vega_hedged = false;

        public override void Initialize()
        {
            SetTimeZone(TimeZones.Shanghai);
            SetCash(1000000);
            SetStartDate(2015, 03, 01);
            SetEndDate(2020, 04, 26);

            var equity = AddEquity(UnderlyingTicker, Resolution.Minute, Market.SSE);
            var option = AddOption(UnderlyingTicker, Resolution.Minute, Market.SSE);

            // set our strike/expiry filter for this option chain
            option.SetFilter(u => u.Strikes(-100, +100)
                                  .Expiration(TimeSpan.FromDays(0), TimeSpan.FromDays(1800)));

            // use the underlying equity as the benchmark
            SetBenchmark(equity.Symbol);
            // deserialize
            if (File.Exists(jsonpath1))
            {
                string Jsonstring1 = File.ReadAllText(jsonpath1);
                vix = JsonConvert.DeserializeObject<Dictionary<DateTime, decimal>>(Jsonstring1);
            }
            if (File.Exists(jsonpath2))
            {
                string Jsonstring2 = File.ReadAllText(jsonpath2);
                realizedVol = JsonConvert.DeserializeObject<Dictionary<DateTime, decimal>>(Jsonstring2);
            }
            if (File.Exists(jsonpath3))
            {
                string Jsonstring3 = File.ReadAllText(jsonpath3);
                rollingStd = JsonConvert.DeserializeObject<Dictionary<DateTime, decimal>>(Jsonstring3);
            }
            if (File.Exists(jsonpath4))
            {
                string Jsonstring4 = File.ReadAllText(jsonpath4);
                VIXHLDiff = JsonConvert.DeserializeObject<Dictionary<DateTime, decimal>>(Jsonstring4);
            }

        }

        public override void OnWarmupFinished()
        {
            Log(Transactions.GetOpenOrders().Count());
            Transactions.CancelOpenOrders();
        }

        /// <summary>
        /// Event - v3.0 DATA EVENT HANDLER: (Pattern) Basic template for user to override for receiving all subscription data in a single event
        /// </summary>
        /// <param name="slice">The current slice of data keyed by symbol string</param>
        public override void OnData(Slice slice)
        {
            var holdings = Portfolio.Securities.Values.Where(x => x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Option).ToList();
            var today = slice.Time.Date;
            var stop_loss = false;
            /*decimal unrealizedProfit = 0m;
            foreach (var h in holdings)
            {
                unrealizedProfit += h.Holdings.UnrealizedProfit;
            }
            var margin = 2 * order_unit * 5000;
            var profitPct = unrealizedProfit/margin;
            if (profitPct < stopLossThres)
            {
                stop_loss = true;
            }*/
            if (today == new DateTime(2019, 2, 25))
            {
                stop_loss = true;
            }
            // find front-month atm straddle contract
            OptionChain chain;
            if (slice.OptionChains.TryGetValue(OptionSymbol, out chain))
            {
                if (stop_loss == true && Portfolio.Invested == true)
                {
                    foreach (var h in holdings)
                    {
                        var con = (
                        from optionContract in chain
                        where optionContract.Symbol == h.Symbol
                        select optionContract
                        ).FirstOrDefault();
                        MarketOrder(con.Symbol, -h.Holdings.Quantity, tag: "stop loss: close straddle position");
                    }
                    already_vega_hedged = false;
                    return;
                }
               
                decimal iv = 0m;
                decimal rv = 0m;
                decimal std = 0m;
                decimal vhld = 0m;
                if (vix.ContainsKey(today))
                {
                    iv = vix[today];
                }
                if (realizedVol.ContainsKey(today))
                {
                    rv = realizedVol[today];
                }
                if (rollingStd.ContainsKey(today))
                {
                    std = rollingStd[today];
                }
                if (VIXHLDiff.ContainsKey(today))
                {
                    vhld = VIXHLDiff[today];
                }
                if (iv == 0 || rv == 0 || std == 0) { return; }

                int trade_direction = 0;
                if (iv - rv > std) //&& vhld < 0.3m
                {
                    trade_direction = -1;
                }
                if (slice.Time.TimeOfDay == openTime)
                {
                    if (trade_direction != 0)
                    {
                        // select expiry date
                        var expiryList = chain.OrderBy(x => x.Expiry).Select(x => x.Expiry).Distinct().ToList();
                        var expiry_date_front = expiryList[0];
                        var expiry_date_far = expiryList[2];
                        decimal k_unit = 0.05m;
                        if (chain.Underlying.Price >= 3)
                        {
                            k_unit = 0.1m;
                        }
                        if ((expiryList[0] - today).TotalDays < 7)
                        {
                            expiry_date_front = expiryList[1];
                            expiry_date_far = expiryList[2];
                        }
                        var call_front = (
                                    from optionContract in chain
                                    where optionContract.Right == OptionRight.Call
                                    where optionContract.Expiry == expiry_date_front
                                    orderby Math.Abs(chain.Underlying.Price - optionContract.Strike)
                                    select optionContract
                                    ).FirstOrDefault();
                        var put_front = (
                                    from optionContract in chain
                                    where optionContract.Right == OptionRight.Put
                                    where optionContract.Expiry == expiry_date_front
                                    orderby Math.Abs(chain.Underlying.Price - optionContract.Strike)
                                    select optionContract
                                    ).FirstOrDefault();

                        var k_lower = call_front.Strike - 2 * k_unit;
                        var k_upper = call_front.Strike + 2 * k_unit;
                        var call_far = (
                                    from optionContract in chain
                                    where optionContract.Right == OptionRight.Call
                                    where optionContract.Expiry == expiry_date_far
                                    where optionContract.Strike == k_upper
                                    select optionContract
                                    ).FirstOrDefault();
                        var put_far = (
                                    from optionContract in chain
                                    where optionContract.Right == OptionRight.Put
                                    where optionContract.Expiry == expiry_date_far
                                    where optionContract.Strike == k_lower
                                    select optionContract
                                    ).FirstOrDefault();
                        int hedge_order_unit = 0;
                        /*if (call_far != null && put_far != null)
                        {
                            var vega_hedge_ratio = (call_front.Greeks.Vega + put_front.Greeks.Vega) / (call_far.Greeks.Vega + put_far.Greeks.Vega);
                            hedge_order_unit = (int)Math.Round(order_unit * vega_hedge_ratio*0.5m, 0);
                        }*/
                        
                        if (trade_direction == -1)
                        {
                            if (!Portfolio.Invested)
                            {
                                MarketOrder(call_front.Symbol, -order_unit, tag: "sell straddle");
                                MarketOrder(put_front.Symbol, -order_unit, tag: "sell straddle");
                                if (hedge_order_unit > 0)
                                {
                                    MarketOrder(call_far.Symbol, hedge_order_unit, tag: "buy far straddle");
                                    MarketOrder(put_far.Symbol, hedge_order_unit, tag: "buy far straddle");
                                    already_vega_hedged = true;
                                }
                            }
                            else
                            {
                                bool need_reopen = false;
                                foreach (var h in holdings)
                                {
                                    // if expiry is really close, we close first, and reopen
                                    var con = (
                                    from optionContract in chain
                                    where optionContract.Symbol == h.Symbol
                                    select optionContract
                                    ).FirstOrDefault();
                                    if ((con.Expiry - today).TotalDays < 2)
                                    {
                                        need_reopen = true;
                                        break;
                                    }
                                }
                                if (need_reopen == true)
                                {
                                    foreach (var h in holdings)
                                    {
                                        var con = (
                                        from optionContract in chain
                                        where optionContract.Symbol == h.Symbol
                                        select optionContract
                                        ).FirstOrDefault();
                                        MarketOrder(con.Symbol, -h.Holdings.Quantity, tag: "close all straddle position");
                                    }
                                    already_vega_hedged = false;
                                    MarketOrder(call_front.Symbol, -order_unit, tag: "sell straddle");
                                    MarketOrder(put_front.Symbol, -order_unit, tag: "sell straddle");
                                    if (hedge_order_unit > 0)
                                    {
                                        MarketOrder(call_far.Symbol, hedge_order_unit, tag: "buy far straddle");
                                        MarketOrder(put_far.Symbol, hedge_order_unit, tag: "buy far straddle");
                                        already_vega_hedged = true;
                                    }
                                }
                                else if (already_vega_hedged == false)
                                {
                                    if (hedge_order_unit > 0)
                                    {
                                        MarketOrder(call_far.Symbol, hedge_order_unit, tag: "buy far straddle");
                                        MarketOrder(put_far.Symbol, hedge_order_unit, tag: "buy far straddle");
                                        already_vega_hedged = true;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (Portfolio.Invested)
                        {
                            foreach (var h in holdings)
                            {
                                var con = (
                                from optionContract in chain
                                where optionContract.Symbol == h.Symbol
                                select optionContract
                                ).FirstOrDefault();
                                MarketOrder(con.Symbol, -h.Holdings.Quantity, tag: "close straddle position");
                            }
                            already_vega_hedged = false;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Order fill event handler. On an order fill update the resulting information is passed to this method.
        /// </summary>
        /// <param name="orderEvent">Order event details containing details of the evemts</param>
        /// <remarks>This method can be called asynchronously and so should only be used by seasoned C# experts. Ensure you use proper locks on thread-unsafe objects</remarks>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {

        }


        public static int Maxi(List<decimal> array)
        {
            int maxIndex = 0;
            for (int i = 1; i < array.Count; i++)
            {

                if (array[maxIndex] < array[i])
                {

                    maxIndex = i;

                }

            }
            return maxIndex;
        }

        public static int Mini(List<decimal> array)
        {
            int minIndex = 0;
            for (int i = 1; i < array.Count; i++)
            {

                if (array[minIndex] > array[i])
                {

                    minIndex = i;

                }

            }
            return minIndex;
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
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string> {
            // Total Trades    352
            // Average Win 1.44%
            // Average Loss    -1.64%
            // Compounding Annual Return   5.298%
            // Drawdown    15.600%
            // Expectancy  0.107
            // Net Profit  30.491%
            // Sharpe Ratio    0.626
        };
    }
}
