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
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Interfaces;
using QuantConnect.Securities.Option;
using System.IO;
using Newtonsoft.Json;
using System.Threading;
using QuantConnect.Securities;
using QuantConnect.Data.Fundamental;
using Accord;

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

    public class Call_Delta_50ETF_backtest : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string UnderlyingTicker = "SH510050";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Equity, Market.SSE);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.SSE);
        public decimal spread = 0.01M;
        private static DateTime startDate = new DateTime(2015, 2, 9);
        private static DateTime endDate = new DateTime(2020, 10, 9);

        private string path1 = Directory.GetCurrentDirectory() + "\\Greeks.csv";

        private decimal balance_now = 200m;

        public static bool opennew = true;
        private static TimeSpan endOfDay = new DateTime(1, 1, 1, 15, 0, 0).TimeOfDay;

        public static bool hasopt = false;
        public bool firstrun = true;
        public decimal underlying_price_now = 0;
        public DateTime expirydatenow = new DateTime(1991, 2, 7);
        private ISyntheticOptionPriceModel _priceModel = SyntheticOptionPriceModels.MonteCarlo();

        //记录当前加仓期权上线
        //记录订单
        public static List<OrderClass> PendingOrders = new List<OrderClass>() { };

        public static List<decimal> price_list = new List<decimal> { };

        public class OrderClass
        {
            public Symbol symbol;
            public double quantity;
            public string message;

            public OrderClass(Symbol symbol, double quantity, string message)
            {
                this.symbol = symbol;
                this.quantity = quantity;
                this.message = message;
            }
        }

        public override void Initialize()
        {
            SetTimeZone(TimeZones.Shanghai);
            SetStartDate(startDate);
            SetEndDate(endDate);
            SetCash(10000000);
            var equity = AddEquity(UnderlyingTicker, Resolution.Minute, Market.SSE);
            var option = AddOption(UnderlyingTicker, Resolution.Minute, Market.SSE);
            // set our strike/expiry filter for this option chain
            option.SetFilter(u => u.Strikes(-100, +100)
                                   .Expiration(TimeSpan.Zero, TimeSpan.FromDays(200)));

            // use the underlying equity as the benchmark
            SetBenchmark(equity.Symbol);
            SetWarmUp(TimeSpan.FromDays(30));
        }

        /// <summary>
        /// Event - v3.0 DATA EVENT HANDLER: (Pattern) Basic template for user to override for receiving all subscription data in a single event
        /// </summary>
        /// <param name="slice">The current slice of data keyed by symbol string</param>
        public override void OnData(Slice slice)
        {
            //Log($"{slice.Time.ToString()}");
            //return;
            if (firstrun)
            {
                //MarketOrder(perpetual_contract.Symbol, -cash_deposit*perpetual_contract.BidPrice, false, "no position");
                firstrun = false;
            }

            OptionChain chain;
            if (slice.OptionChains.TryGetValue(OptionSymbol, out chain) && chain.Underlying.Price != 0)
            {
                if (slice.Time.TimeOfDay == endOfDay)
                {
                    price_list.Add(chain.Underlying.Price);
                    File.AppendAllText(path1, slice.Time.Date + "," + Portfolio.TotalPortfolioValue.ToString() + Environment.NewLine);
                }

                //price_list.Add(chain.Underlying.Price);

                if (price_list.Count() > 15)
                {
                    price_list.RemoveAt(0);
                }

                if (slice.Time.Date == expirydatenow.Date || (underlying_price_now != 0 && Math.Abs(chain.Underlying.Price / underlying_price_now - 1) > 0.2m))//target_call.Expiry.Date)
                {
                    hasopt = false;

                    balance_now = Math.Round(Portfolio.TotalPortfolioValue / chain.Underlying.Price / 10000);
                }

                if (price_list.Count() > 14 && !hasopt)
                {
                    List<decimal> ret_list = new List<decimal> { };
                    for (int i = 1; i < price_list.Count; i++)
                    {
                        ret_list.Add(Convert.ToDecimal(Math.Log(Convert.ToDouble(price_list[i] / price_list[i - 1]), Math.E)));
                    }

                    hasopt = true;
                    underlying_price_now = chain.Underlying.Price;
                    var expiryList = chain.Where(x => x.Expiry >= UtcTime.Date.AddDays(50)).OrderBy(x => x.Expiry).Select(x => x.Expiry).Distinct().ToList();
                    expirydatenow = expiryList.FirstOrDefault();
                }
                if (hasopt)
                {
                    double S = Convert.ToDouble(chain.Underlying.Price);
                    double X = Convert.ToDouble(underlying_price_now);//(target_call.Symbol.ID.StrikePrice);
                    double r = 0.03;
                    List<decimal> ret_list = new List<decimal> { };
                    for (int i = 1; i < price_list.Count; i++)
                    {
                        ret_list.Add(Convert.ToDecimal(Math.Log(Convert.ToDouble(price_list[i] / price_list[i - 1]), Math.E)));
                    }
                    double sigma = Math.Sqrt(Convert.ToDouble(Var(ret_list))) * Math.Sqrt(252);
                    double t = (expirydatenow.Date - slice.Time.Date).TotalDays / 365;//(target_call.Symbol.ID.Date - slice.Time.Date).TotalDays / 365.0;
                    if (t > 0)
                    {
                        double risk = 1;
                        var callPrice = _priceModel.Evaluate(S, X, 0, r, sigma, expirydatenow, EPutCall.Call);
                        var target_delta_call = (decimal)callPrice.Greeks.Delta * balance_now;
                        var target_delta = target_delta_call;// + target_delta_put;
                        var delta_up = target_delta + 0.2m * balance_now;
                        var delta_down = target_delta - 0.2m * balance_now;
                        decimal holding_etf_vol = 0;
                        var holding_etf = Portfolio.Securities.Values.Where(x =>
                            x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Equity);
                        if (holding_etf.Count() > 0)
                        {
                            holding_etf_vol = holding_etf.FirstOrDefault().Holdings.Quantity;
                        }

                        var cash_delta = holding_etf_vol / 10000;
                        if (cash_delta > delta_up || cash_delta < delta_down)
                        {
                            var target_vol = (target_delta_call - cash_delta) * 10000;
                            MarketOrder(Underlying, target_vol);
                        }
                    }
                }
            }
        }

        private static decimal Var(List<decimal> v)
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
            System.Diagnostics.Debug.WriteLine(orderEvent.ToString());
            //Log(orderEvent.ToString());
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
            {"Total Trades", "2"},
            {"Average Win", "0%"},
            {"Average Loss", "-0.28%"},
            {"Compounding Annual Return", "-78.282%"},
            {"Drawdown", "0.300%"},
            {"Expectancy", "-1"},
            {"Net Profit", "-0.282%"},
            {"Sharpe Ratio", "0"},
            {"Loss Rate", "100%"},
            {"Win Rate", "0%"},
            {"Profit-Loss Ratio", "0"},
            {"Alpha", "0"},
            {"Beta", "0"},
            {"Annual Standard Deviation", "0"},
            {"Annual Variance", "0"},
            {"Information Ratio", "0"},
            {"Tracking Error", "0"},
            {"Treynor Ratio", "0"},
            {"Total Fees", "$2.00"}
        };
    }
}