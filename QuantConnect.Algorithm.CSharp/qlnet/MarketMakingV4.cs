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
    public class MarketMakingV4 : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string UnderlyingTicker = "SH510050";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Equity, Market.SSE);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.SSE);
        private DateTime last_day = new DateTime(2015, 1, 1);
        TimeSpan openTime = new DateTime(1, 1, 1, 14, 55, 0).TimeOfDay;
        TimeSpan checkTime = new DateTime(1, 1, 1, 14, 55, 0).TimeOfDay;
        private List<decimal> close_list = new List<decimal>();
        private decimal riskOnThres = 28m;
        private decimal order_unit = 70;
        private bool already_vega_hedged = false;
        private decimal lastHoldK = 0m;
        private decimal stopLossThres = -0.08m;
        private List<double> diff_list = new List<double>();

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
            var stop_loss = false;
            decimal unrealizedProfit = 0m;
            decimal cost = 0m;
            foreach (var h in holdings)
            {
                unrealizedProfit += h.Holdings.UnrealizedProfit;
                cost += h.Holdings.HoldingsCost;
            }
            var margin = 2 * order_unit * 5000;
            var open_cost = margin + cost;
            var profitPct = unrealizedProfit / open_cost;
            if (profitPct < stopLossThres)
            {
                stop_loss = true;
            }
            // find front-month atm straddle contract and trade
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
                if (slice.Time.TimeOfDay == checkTime)
                {
                    iv = GetVix(chain);
                    var etf_prc = Securities[Underlying].Close;
                    close_list.Add(etf_prc);
                    rv = GetHistoryVol(close_list)*100;
                    if (iv != 0 && rv != 0){
                        diff_list.Add(decimal.ToDouble(iv-rv));
                        std = GetRollingStd(diff_list);
                    }
                }
                if (iv == 0 || rv == 0 || std == 0) { return; }
                Log($"{slice.Time} vix: {iv} rv: {rv} std: {std}");
                decimal k_add = 0m;
                if (iv < riskOnThres)
                {
                    k_add = 0.05m; ;
                }

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
                        if ((expiryList[0] - today).TotalDays < 3)
                        {
                            expiry_date_front = expiryList[1];
                            expiry_date_far = expiryList[2];
                        }
                        var atm_dict = SelectATMContracts(chain, expiry_date_front, k_add);
                        var call_front = atm_dict["call"];
                        var put_front = atm_dict["put"];

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

                        if (trade_direction == -1)
                        {
                            if (!Portfolio.Invested)
                            {
                                MarketOrder(call_front.Symbol, -order_unit, tag: "sell straddle");
                                MarketOrder(put_front.Symbol, -order_unit, tag: "sell straddle");
                                lastHoldK = call_front.Strike;
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
                                if (lastHoldK != call_front.Strike)
                                {
                                    need_reopen = true;
                                }
                                else
                                {
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
                                        MarketOrder(con.Symbol, -h.Holdings.Quantity, tag: "reopen: close all straddle position");
                                    }
                                    already_vega_hedged = false;
                                    MarketOrder(call_front.Symbol, -order_unit, tag: "sell straddle");
                                    MarketOrder(put_front.Symbol, -order_unit, tag: "sell straddle");
                                    lastHoldK = call_front.Strike;
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


        public decimal GetRollingStd(List<double> diff_list)
        {
            while (diff_list.Count > 22)
            {
                diff_list.RemoveAt(0);
            }
            if (diff_list.Count != 22)
            {
                return 0m;
            }
            var std = MathNet.Numerics.Statistics.Statistics.StandardDeviation(diff_list);
            return Convert.ToDecimal(std);
        }


        public decimal GetHistoryVol(List<decimal> prc_list)
        {
            while (prc_list.Count > 23)
            {
                prc_list.RemoveAt(0);
            }
            if (prc_list.Count != 23)
            {
                return 0m;
            }
            List<double> log_rtns = new List<double>();
            for (int i = 1; i < prc_list.Count; ++i)
            {
                var log_rtn = Math.Log(decimal.ToDouble(prc_list[i] / prc_list[i - 1]));
                log_rtns.Add(log_rtn);
            }
            var std = MathNet.Numerics.Statistics.Statistics.StandardDeviation(log_rtns);
            var ret = std * Math.Sqrt(250);
            return Convert.ToDecimal(ret);
        }


        public decimal GetVix(OptionChain chain)
        {
            // step 1: 计算近月和次近月合约的到期剩余时间: T1 T2
            // 超过7天的，即t2m小于7/365=0.01918的，则取下月合约的t2m为T1，然后距离下月合约最近的到期日合约的t2m为T2
            var today = chain.Time.Date;
            var expiryList = chain.OrderBy(x => x.Expiry).Select(x => x.Expiry).Distinct().ToList();
            var near_month1 = expiryList[0];
            var near_month2 = expiryList[1];
            if ((expiryList[0] - today).TotalDays < 7)
            {
                near_month1 = expiryList[1];
                near_month2 = expiryList[2];
            }
            var T1 = Convert.ToDecimal((near_month1 - today).TotalDays / 365);
            var T2 = Convert.ToDecimal((near_month2 - today).TotalDays / 365);

            // step 2: 确认标的远期价格水平: F1 F2； 确定平值执行价K01 K02, 用于计算参与计算的虚值期权
            var tmp1 = GetFandK(chain, near_month1, 0.03m);
            var tmp2 = GetFandK(chain, near_month2, 0.03m);
            var F1 = tmp1["F"];
            var K1 = tmp1["K"];
            var F2 = tmp2["F"];
            var K2 = tmp2["K"];

            //step3: 填充参与VIX计算的平值和虚值期权的价格, 并计算方差贡献；
            var var_cont1 = GetVarContribution(chain, near_month1, 0.03m, K1);
            var var_cont2 = GetVarContribution(chain, near_month2, 0.03m, K2);
            var a = var_cont2.Sum();
            var var1 = (2 / T1) * var_cont1.Sum() - (F1 / K1 - 1) * (F1 / K1 - 1) / T1;
            var var2 = (2 / T2) * var_cont2.Sum() - (F2 / K2 - 1) * (F2 / K2 - 1) / T2;
            // step4: 计算VIX指数
            var tmp = (T1 * var1 * (T2 - 1m / 12) + T2 * var2 * (1m / 12 - T1)) * 12 / (T2 - T1);
            var VIX = 100 * Math.Sqrt(decimal.ToDouble(tmp));
            return Convert.ToDecimal(VIX);
        }


        public List<decimal> GetVarContribution(OptionChain chain, DateTime expiry, decimal rf, decimal K)
        {
            var today = chain.Time.Date;
            var T = (expiry - today).TotalDays / 365;
            Dictionary<string, decimal> ret = new Dictionary<string, decimal>();
            var calls = (from optionContract in chain
                         where optionContract.Right == OptionRight.Call
                         where optionContract.Expiry == expiry
                         select optionContract).ToList();
            var strikeList = calls.OrderBy(x => x.Strike).Select(x => x.Strike).Distinct().ToList();
            List<decimal> var_contribute = new List<decimal>();
            var last_k = 0m;
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
                if (p == null || c == null)
                {
                    //Log($"lack of put/call contract on {chain.Time} for k {k}.");
                    continue;
                }
                var c_mid = (c.BidPrice + c.AskPrice) / 2;
                var p_mid = (p.BidPrice + p.AskPrice) / 2;
                decimal delta_k = 0.05m;
                if (last_k > 0)
                {
                    delta_k = k - last_k;
                }
                last_k = k;
                decimal use_prc;
                if (k < K)
                {
                    use_prc = p_mid;
                }
                else if (k == K)
                {
                    use_prc = (c_mid + p_mid) / 2;
                }
                else
                {
                    use_prc = c_mid;
                }
                var vc = delta_k * Convert.ToDecimal(Math.Exp(decimal.ToDouble(rf) * T)) * use_prc / (k * k);
                var_contribute.Add(vc);
                //Log($"k: {k}, c: {vc}");
            }
            return var_contribute;
        }


        public static Dictionary<string, decimal> GetFandK(OptionChain chain, DateTime expiry, decimal rf)
        {
            var today = chain.Time.Date;
            var T = (expiry - today).TotalDays / 365;
            Dictionary<string, decimal> ret = new Dictionary<string, decimal>();
            var calls = (from optionContract in chain
                         where optionContract.Right == OptionRight.Call
                         where optionContract.Expiry == expiry
                         select optionContract).ToList();
            List<decimal> c_p = new List<decimal>();
            List<decimal> gap = new List<decimal>();
            for (int i = 0; i < calls.Count; i++)
            {
                var k = calls[i].Strike;
                var c_mid = (calls[i].BidPrice + calls[i].AskPrice) / 2;
                var p = (from optionContract in chain
                         where optionContract.Right == OptionRight.Put
                         where optionContract.Expiry == expiry
                         where optionContract.Strike == k
                         select optionContract).FirstOrDefault();
                if (p == null)
                {
                    //Log($"lack of put contract on {chain.Time} for k {k}.");
                    continue;
                }
                var p_mid = (p.BidPrice + p.AskPrice) / 2;
                gap.Add(Math.Abs(c_mid - p_mid));
                c_p.Add(c_mid - p_mid);
            }
            var mini = Mini(gap);
            var k0 = calls[mini].Strike;
            var Forward = k0 + Convert.ToDecimal(Math.Exp(decimal.ToDouble(rf) * T)) * c_p[mini];
            var K = Forward;
            List<decimal> k_f = new List<decimal>();
            List<decimal> k_list = new List<decimal>();
            for (int i = 0; i < calls.Count; i++)
            {
                var k = calls[i].Strike;
                var c_mid = (calls[i].BidPrice + calls[i].AskPrice) / 2;
                var p = (from optionContract in chain
                         where optionContract.Right == OptionRight.Put
                         where optionContract.Expiry == expiry
                         where optionContract.Strike == k
                         select optionContract).FirstOrDefault();
                if (p == null)
                {
                    //Log($"lack of put contract on {chain.Time} for k {k}.");
                    continue;
                }
                if (k < Forward)
                {
                    k_f.Add(k - Forward);
                    k_list.Add(k);
                }
            }
            if (k_f.Count > 0)
            {
                var maxi = Maxi(k_f);
                K = k_list[maxi];
            }
            ret.Add("K", K);
            ret.Add("F", Forward);
            return ret;
        }


        public static Dictionary<string, OptionContract> SelectATMContracts(OptionChain chain, DateTime expiry, decimal k_add)
        {
            Dictionary<string, OptionContract> ret = new Dictionary<string, OptionContract>();
            var calls = (from optionContract in chain
                         where optionContract.Right == OptionRight.Call
                         where optionContract.Expiry == expiry
                         select optionContract).ToList();
            List<decimal> c_p = new List<decimal>();
            for (int i = 0; i < calls.Count; i++)
            {
                var k = calls[i].Strike;
                var c_mid = (calls[i].BidPrice + calls[i].AskPrice) / 2;
                var p = (from optionContract in chain
                         where optionContract.Right == OptionRight.Put
                         where optionContract.Expiry == expiry
                         where optionContract.Strike == k
                         select optionContract).FirstOrDefault();
                if (p == null)
                {
                    //Log($"lack of put contract on {chain.Time} for k {k}.");
                    continue;
                }
                var p_mid = (p.BidPrice + p.AskPrice) / 2;
                c_p.Add(Math.Abs(c_mid - p_mid));
            }
            var mini = Mini(c_p);
            var k0 = calls[mini].Strike + k_add;
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
