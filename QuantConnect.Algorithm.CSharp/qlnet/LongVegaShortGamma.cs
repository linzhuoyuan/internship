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
using Accord.Statistics.Kernels;

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
    public class LongVegaShortGamma : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string UnderlyingTicker = "SH510050";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Equity, Market.SSE);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.SSE);
        private DateTime last_day = new DateTime(2015, 1, 1);
        TimeSpan openTime = new DateTime(1, 1, 1, 14, 59, 0).TimeOfDay;
        TimeSpan checkTime = new DateTime(1, 1, 1, 14, 55, 0).TimeOfDay;
        private decimal upThres = 1.1m;
        private decimal downThres = 0.9m;
        private decimal order_unit = 70;
        private decimal lastHoldKUpper = 0m;
        private decimal lastHoldKLower = 0m;
        private int holdDaysLeft = 0;

        public override void Initialize()
        {
            SetTimeZone(TimeZones.Shanghai);
            SetCash(1000000);
            SetStartDate(2015, 03, 01);
            SetEndDate(2020, 02, 01);

            var equity = AddEquity(UnderlyingTicker, Resolution.Minute, Market.SSE);
            var option = AddOption(UnderlyingTicker, Resolution.Minute, Market.SSE);

            // set our strike/expiry filter for this option chain
            option.SetFilter(u => u.Strikes(-100, +100)
                                  .Expiration(TimeSpan.FromDays(0), TimeSpan.FromDays(1800)));

            // use the underlying equity as the benchmark
            SetBenchmark(equity.Symbol);
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
            if (slice.Time.Date == new DateTime(2016, 11, 29))
            {
                last_day = slice.Time.Date;
            }


            // find front-month atm straddle contract and trade
            OptionChain chain;
            if (slice.OptionChains.TryGetValue(OptionSymbol, out chain))
            {
                int trade_direction = 0;
                if (slice.Time.TimeOfDay == openTime)
                {
                    bool clear_pos = false;
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
                            clear_pos = true;
                            break;
                        }
                    }
                    if (clear_pos == true)
                    {
                        foreach (var h in holdings)
                        {
                            var con = (
                            from optionContract in chain
                            where optionContract.Symbol == h.Symbol
                            select optionContract
                            ).FirstOrDefault();
                            MarketOrder(con.Symbol, -h.Holdings.Quantity, tag: "near expiry: close all strangle position");
                        }
                        holdDaysLeft = 0;
                    }

                    if (holdDaysLeft > 0)
                    {
                        holdDaysLeft -= 1;
                    }
                    if (holdDaysLeft > 0)
                    {
                        return;
                    }
                    // select expiry date
                    var expiryList = chain.OrderBy(x => x.Expiry).Select(x => x.Expiry).Distinct().ToList();
                    var expiry_date_front = expiryList[0];
                    var expiry_date_far = expiryList[2];
                    if ((expiryList[0] - today).TotalDays <= 7)
                    {
                        expiry_date_front = expiryList[1];
                        expiry_date_far = expiryList[3];
                    }
                    var atm_dict = SelectATMContracts(chain, expiry_date_front, 0m);
                    var call_atm = atm_dict["call"];
                    decimal k_unit = 0.05m;
                    if (call_atm.Strike >= 3)
                    {
                        k_unit = 0.1m;
                    }
                    var k_lower = call_atm.Strike - k_unit;
                    var k_upper = call_atm.Strike + k_unit;
                    var call_front = (
                                from optionContract in chain
                                where optionContract.Right == OptionRight.Call
                                where optionContract.Expiry == expiry_date_front
                                where optionContract.Strike == k_upper
                                select optionContract
                                ).FirstOrDefault();
                    var put_front = (
                                from optionContract in chain
                                where optionContract.Right == OptionRight.Put
                                where optionContract.Expiry == expiry_date_front
                                where optionContract.Strike == k_lower
                                select optionContract
                                ).FirstOrDefault();
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
                    decimal ratio = 1m;
                    if (call_front != null && put_front != null && call_far != null && put_far != null)
                    {
                        //Log($"time: {slice.Time}, call_front: {call_front.Symbol}, call_far: {call_far.Symbol}, put_front: {put_front.Symbol}, put_far: {put_far.Symbol}");
                        var call_front_impvol = call_front.ImpliedVolatility;
                        var put_front_impvol = put_front.ImpliedVolatility;
                        var call_far_impvol = call_far.ImpliedVolatility;
                        var put_far_impvol = put_far.ImpliedVolatility;
                        if (call_front_impvol != 0m && put_front_impvol != 0m && call_far_impvol != 0m && put_far_impvol != 0m)
                        {
                            ratio = (call_far_impvol + put_far_impvol) / (call_front_impvol + put_front_impvol);
                            Log($"time: {slice.Time}, ratio: {ratio}");
                        }
                    }
                    if (ratio <= 0.85m)
                    {
                        trade_direction = -1;
                    }

                    if (trade_direction != 0)
                    {

                        if (trade_direction == -1)
                        {
                            if (!Portfolio.Invested)
                            {
                                //MarketOrder(call_front.Symbol, -order_unit, tag: "sell front strangle");
                                //MarketOrder(put_front.Symbol, -order_unit, tag: "sell front strangle");
                                MarketOrder(call_far.Symbol, order_unit, tag: "buy far strangle");
                                MarketOrder(put_far.Symbol, order_unit, tag: "buy far strangle");
                                lastHoldKUpper = k_upper;
                                lastHoldKLower = k_lower;
                                holdDaysLeft = 10;
                            }
                            else
                            {
                                bool need_reopen = false;
                                if (lastHoldKUpper != k_upper || lastHoldKLower != k_lower)
                                {
                                    need_reopen = true;
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
                                        MarketOrder(con.Symbol, -h.Holdings.Quantity, tag: "reopen: close all strangle position");
                                    }
                                    //MarketOrder(call_front.Symbol, -order_unit, tag: "sell front strangle");
                                    //MarketOrder(put_front.Symbol, -order_unit, tag: "sell front strangle");
                                    MarketOrder(call_far.Symbol, order_unit, tag: "buy far strangle");
                                    MarketOrder(put_far.Symbol, order_unit, tag: "buy far strangle");
                                    lastHoldKUpper = k_upper;
                                    lastHoldKLower = k_lower;
                                    holdDaysLeft = 10;
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
                                MarketOrder(con.Symbol, -h.Holdings.Quantity, tag: "close all strangle position");
                            }
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


        public static Dictionary<string, OptionContract> SelectATMContracts(OptionChain chain, DateTime expiry, decimal k_add)
        {
            Dictionary<string, OptionContract> ret = new Dictionary<string, OptionContract>();
            var calls = (from optionContract in chain
                         where optionContract.Right == OptionRight.Call
                         where optionContract.Expiry == expiry
                         select optionContract).ToList();
            var strikeList = calls.OrderBy(x => x.Strike).Select(x => x.Strike).Distinct().ToList();
            List<decimal> c_p = new List<decimal>();
            List<decimal> k_list = new List<decimal>();
            for (int i = 0; i < strikeList.Count; i++)
            {
                var k = strikeList[i];
                var c = (from optionContract in chain
                         where optionContract.Right == OptionRight.Call
                         where optionContract.Expiry == expiry
                         where optionContract.Strike == k
                         select optionContract).FirstOrDefault();

                var p = (from optionContract in chain
                         where optionContract.Right == OptionRight.Put
                         where optionContract.Expiry == expiry
                         where optionContract.Strike == k
                         select optionContract).FirstOrDefault();
                if (p == null || c == null || k % 0.05m != 0)
                {
                    //Log($"lack of put contract on {chain.Time} for k {k}.");
                    continue;
                }
                var c_mid = (c.BidPrice + c.AskPrice) / 2;
                var p_mid = (p.BidPrice + p.AskPrice) / 2;
                c_p.Add(Math.Abs(c_mid - p_mid));
                k_list.Add(k);
            }
            var mini = Mini(c_p);
            var k0 = k_list[mini] + k_add;
            var call = (from optionContract in chain
                        where optionContract.Right == OptionRight.Call
                        where optionContract.Expiry == expiry
                        where optionContract.Strike == k0
                        select optionContract).FirstOrDefault();
            var put = (from optionContract in chain
                       where optionContract.Right == OptionRight.Put
                       where optionContract.Expiry == expiry
                       where optionContract.Strike == k0
                       select optionContract).FirstOrDefault();
            if (call != null && put != null)
            {
                ret.Add("call", call);
                ret.Add("put", put);
            }
            else
            {
                var k1 = calls[mini].Strike;
                var call1 = (from optionContract in chain
                             where optionContract.Right == OptionRight.Call
                             where optionContract.Expiry == expiry
                             where optionContract.Strike == k1
                             select optionContract).FirstOrDefault();
                var put1 = (from optionContract in chain
                            where optionContract.Right == OptionRight.Put
                            where optionContract.Expiry == expiry
                            where optionContract.Strike == k1
                            select optionContract).FirstOrDefault();
                ret.Add("call", call1);
                ret.Add("put", put1);
            }
            return ret;
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
            //Total Trades    982
            //Average Win 1.13%
            //Average Loss    -1.16%
            //Compounding Annual Return   10.585%
            //Drawdown    13.400%
            //Expectancy  0.102
            //Net Profit  67.983%
            //Sharpe Ratio    1.162
            //Loss Rate   44%
            //Win Rate    56%
            //Profit-Loss Ratio   0.97


        };
    }
}
