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
 *
*/

using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Data.Market;
using QuantConnect.Securities.Option;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// This example demonstrates how to add futures for a given underlying asset.
    /// It also shows how you can prefilter contracts easily based on expirations, and how you
    /// can inspect the futures chain to pick a specific contract to trade.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="benchmarks" />
    /// <meta name="tag" content="futures" />
    public class Delta_IH_backtest_v2 : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string UnderlyingTicker = "SH510050";
        //public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Equity, Market.SSE);
        //public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.SSE);

        private const string RootSZ50 = Futures.Indices.SZ50;
        public Symbol SZ50 = QuantConnect.Symbol.Create(RootSZ50, SecurityType.Future, Market.CFFEX);
        //private const string RootHS300 = Futures.Indices.HS300;
        //public Symbol HS300 = QuantConnect.Symbol.Create(RootHS300, SecurityType.Future, Market.CFFEX);

        //private const string RootZZ500 = Futures.Indices.ZZ500;
        //public Symbol ZZ500 = QuantConnect.Symbol.Create(RootZZ500, SecurityType.Future, Market.CFFEX);


        private decimal balance_now = 0m;
        public static bool hasopt = false;
        public bool firstrun = true;
        public decimal underlying_price_now = 0;
        public decimal upline_now = 0;
        public DateTime expirydatenow = new DateTime(1991, 2, 7);
        public static List<decimal> price_list = new List<decimal> { };
        public static List<decimal> price_list_long = new List<decimal> { };
        static TimeSpan endOfDay = new DateTime(1, 1, 1, 14, 59, 0).TimeOfDay;
        public bool iscrossupline = false;
        FuturesContract future_trade = null;
        private decimal underlying_price = 0;
        private ISyntheticOptionPriceModel _priceModel = SyntheticOptionPriceModels.BlackScholes();
        private decimal last_calc_prc = 0;
        private decimal target_delta_call = 0;
        private decimal target_delta_put = 0;


        /// <summary>
        /// Initialize your algorithm and add desired assets.
        /// </summary>
        public override void Initialize()
        {
            SetTimeZone(TimeZones.Shanghai);
            //SetStartDate(2018, 11, 21);
            //SetEndDate(2018, 11, 30);
            SetStartDate(2015, 04, 10);
            SetEndDate(2020, 11, 30);
            SetCash(1000000000);

            var equity = AddEquity(UnderlyingTicker, Resolution.Minute, Market.SSE);
            var option = AddOption(UnderlyingTicker, Resolution.Minute, Market.SSE);

            var futureSZ50 = AddFuture(RootSZ50, Resolution.Minute, Market.CFFEX);

            //var futureHS300 = AddFuture(RootHS300, Resolution.Minute, Market.CFFEX);
            //var futureZZ500 = AddFuture(RootZZ500, Resolution.Minute, Market.CFFEX);

            // set our strike/expiry filter for this option chain
            //option.SetFilter(u => u.Strikes(-100, +100).Expiration(TimeSpan.Zero, TimeSpan.FromDays(200)));

            // set our expiry filter for this futures chain
            futureSZ50.SetFilter(TimeSpan.Zero, TimeSpan.FromDays(3000));
            //futureHS300.SetFilter(TimeSpan.Zero, TimeSpan.FromDays(365));
            //futureZZ500.SetFilter(TimeSpan.Zero, TimeSpan.FromDays(365));

            // use the underlying equity as the benchmark
            SetBenchmark(equity.Symbol);
        }

        /// <summary>
        /// Event - v3.0 DATA EVENT HANDLER: (Pattern) Basic template for user to override for receiving all subscription data in a single event
        /// </summary>
        /// <param name="slice">The current slice of data keyed by symbol string</param>
        public override void OnData(Slice slice)
        {

            FuturesChain future_chain;
            if (slice.FutureChains.TryGetValue(SZ50, out future_chain))
            {


                if (true)
                {
                    if (firstrun)
                    {
                        var future_list = future_chain.Where(x => x.Expiry > slice.Time.AddDays(16)).OrderBy(x => x.Expiry);
                        if (future_list.Count() > 0)
                        {
                            future_trade = future_list.FirstOrDefault();
                        }

                    }

                    if (future_trade != null)
                    {
                        var test = future_chain.Where(x => x.Symbol == future_trade.Symbol).FirstOrDefault();
                        if (test != null)
                        {
                            future_trade = test;
                            underlying_price =
                                (future_trade.BidPrice + future_trade.AskPrice) / 2;
                        }
                        else
                        {
                            var debug = 0;
                        }
                    }




                    if (slice.Time.TimeOfDay == endOfDay)
                    {
                        price_list.Add(underlying_price);
                        price_list_long.Add(underlying_price);
                        //File.AppendAllText(path1, slice.Time.Date + "," + Portfolio.TotalPortfolioValue.ToString() + Environment.NewLine);

                    }
                    //价格列表长度为15
                    if (price_list.Count() > 16)
                    {
                        price_list.RemoveAt(0);
                    }
                    //第一次运行代码，到期日，计算balance
                    if (firstrun || slice.Time.Date > expirydatenow.Date.AddDays(-5))// || (underlying_price_now != 0 && Math.Abs(underlying_price / underlying_price_now - 1) > 0.3m))//target_call.Expiry.Date)
                    {
                        hasopt = false;
                        firstrun = false;
                        balance_now = Math.Round(Portfolio.TotalPortfolioValue / underlying_price / 200m * 0.8m);

                        var holding = Portfolio.Securities.Values.Where(x => x.HoldStock == true);
                        foreach (var hd in holding)
                        {
                            MarketOrder(hd.Symbol, -hd.Holdings.Quantity);
                        }
                        var future_list = future_chain.Where(x => x.Expiry > slice.Time.AddDays(10)).OrderBy(x => x.Expiry);
                        if (future_list.Count() > 0)
                        {
                            future_trade = future_list.FirstOrDefault();
                        }

                    }
                    //存够收盘价后，选期权，记录当前价格，上线价格，到期日，将突破上线设置为false
                    if (price_list.Count() > 15 && !hasopt)
                    {

                        var future_list = future_chain.Where(x => x.Expiry > slice.Time.AddDays(10)).OrderBy(x => x.Expiry);
                        if (future_list.Count() > 0)
                        {
                            future_trade = future_list.FirstOrDefault();
                        }
                        var expiry_list = future_chain.Where(x => x.Expiry > slice.Time.AddDays(10)).OrderBy(x => x.Expiry);
                        if (expiry_list.Count() > 0)
                        {
                            hasopt = true;
                            underlying_price_now = underlying_price;
                            expirydatenow = expiry_list.FirstOrDefault().Expiry;
                            upline_now = underlying_price_now * 1.06m;
                            iscrossupline = false;
                        }


                    }
                    //选好期权后进行对冲逻辑
                    if (hasopt)
                    {
                        //当前标的价格
                        double S = Convert.ToDouble(underlying_price);
                        //put的strike price，平直put
                        double X = Convert.ToDouble(underlying_price_now);
                        //call的strike price
                        double X_call = Convert.ToDouble(upline_now);
                        //利率
                        double r = 0.03;
                        //计算历史波动率
                        List<decimal> ret_list = new List<decimal> { };
                        for (int i = 1; i < price_list.Count; i++)
                        {
                            ret_list.Add(
                                Convert.ToDecimal(Math.Log(Convert.ToDouble(price_list[i] / price_list[i - 1]), Math.E))
                            );
                        }

                        double sigma = Math.Sqrt(Convert.ToDouble(Var(ret_list))) * Math.Sqrt(252);
                        //到期日
                        double t = (expirydatenow.Date - slice.Time.Date).TotalDays / 252;
                        //未突破上线时，进行collar对冲
                        if (!iscrossupline)
                        {

                            if (t > 0)
                            {
                                //MC模型中的参数, 当且仅当这一次标的价格比上一次计算的时候，价格变动超过2%.
                                if (last_calc_prc == 0 || Math.Abs(underlying_price / last_calc_prc - 1) > 0.02m)
                                {
                                    var callPrice = _priceModel.Evaluate(S, X_call, 0, r, sigma, expirydatenow, EPutCall.Call);
                                    var putPrice = _priceModel.Evaluate(S, X, 0, r, sigma, expirydatenow, EPutCall.Put);
                                    //call的delta
                                    target_delta_call = (decimal)callPrice.Greeks.Delta * balance_now;
                                    //put的delta
                                    target_delta_put = (decimal)putPrice.Greeks.Delta * balance_now;
                                    last_calc_prc = underlying_price;
                                }

                                //组合的detla，卖2call，买1put，持有1现货
                                var target_delta = -2 * target_delta_call + target_delta_put + balance_now;
                                var delta_up = target_delta + 0.2m * balance_now;
                                var delta_down = target_delta - 0.2m * balance_now;

                                //查询现货持仓
                                decimal holding_underlying_vol = 0;
                                var holding_underlying = Portfolio.Securities.Values.Where(
                                    x =>
                                        x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Future
                                );
                                if (holding_underlying.Count() > 0)
                                {
                                    holding_underlying_vol = holding_underlying.FirstOrDefault().Holdings.Quantity;
                                }

                                var cash_delta = holding_underlying_vol;
                                //超出对冲带，进行对冲
                                if (cash_delta > delta_up || cash_delta < delta_down)
                                {

                                    var target_vol = (target_delta - cash_delta);
                                    MarketOrder(future_trade.Symbol, target_vol, false, "target delta: " + (target_delta / balance_now).ToString() + ", strike: " + X + ",S: " + S + ", expiry: " + expirydatenow.Date + ", hold delta: " + cash_delta / balance_now + ", upline: " + upline_now);
                                }

                            }
                        }

                        //当价格突破上线时，iscrossupline设置为true
                        if (underlying_price > upline_now)
                        {
                            iscrossupline = true;
                            //修改到期日


                        }

                        if (iscrossupline)
                        {
                            if (last_calc_prc == 0 || Math.Abs(underlying_price / last_calc_prc - 1) > 0.02m)
                            {
                                var putPrice = _priceModel.Evaluate(S, X, 0, r, sigma, expirydatenow, EPutCall.Put);
                                target_delta_put = (decimal)putPrice.Greeks.Delta * balance_now;
                                last_calc_prc = underlying_price;
                            }
                            //组合的detla,买1put，持有1现货
                            var target_delta = target_delta_put + balance_now;
                            var delta_up = target_delta + 0.2m * balance_now;
                            var delta_down = target_delta - 0.2m * balance_now;
                            decimal holding_underlying_vol = 0;
                            var holding_underlying = Portfolio.Securities.Values.Where(
                                x =>
                                    x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Future
                            );
                            if (holding_underlying.Count() > 0)
                            {
                                holding_underlying_vol = holding_underlying.FirstOrDefault().Holdings.Quantity;
                            }

                            var cash_delta = holding_underlying_vol;
                            //超出对冲带，进行对冲
                            if (cash_delta > delta_up || cash_delta < delta_down)
                            {
                                var target_vol = (target_delta - cash_delta);
                                MarketOrder(future_trade.Symbol, target_vol, false, "crossupline -- target delta: " + (target_delta / balance_now).ToString() + ", strike: " + X + ",S: " + S + ", expiry: " + expirydatenow.Date + ", hold delta: " + cash_delta / balance_now + ", upline: " + upline_now);
                            }
                        }
                    }
                }
            }


        }
        static decimal Var(List<decimal> v)
        {
            //    double tt = 2;
            //double mm = tt ^ 2;

            decimal sum1 = 0;
            for (int i = 0; i < v.Count; i++)
            {
                decimal temp = v[i] * v[i];
                sum1 = sum1 + temp;

            }

            decimal sum = 0;
            foreach (decimal d in v)
            {
                sum = sum + d;
            }

            decimal var = sum1 / v.Count - (sum / v.Count) * (sum / v.Count);
            return var;
        }
        /// <summary>
        /// Order fill event handler. On an order fill update the resulting information is passed to this method.
        /// </summary>
        /// <param name="orderEvent">Order event details containing details of the evemts</param>
        /// <remarks>This method can be called asynchronously and so should only be used by seasoned C# experts. Ensure you use proper locks on thread-unsafe objects</remarks>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            Log(orderEvent.ToString());
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
        };
    }
}
