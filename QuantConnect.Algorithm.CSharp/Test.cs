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
using System.Diagnostics;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Interfaces;
using QuantConnect.Securities.Option;
using System.IO;
using System.Threading;
using QuantConnect.Securities;
using QuantConnect.Data.Fundamental;

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


    public class Test : QCAlgorithm, IRegressionAlgorithmDefinition
    {

        public override string PortfolioManagerName { get; } = "deribit";

        public List<AddOneStrategy> AddOneStrategies = new List<AddOneStrategy>() { };
        private const string UnderlyingTicker = "BTCUSD";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Crypto, Market.Deribit);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.Deribit);
        private static DateTime startDate = new DateTime(2019, 10, 13);
        private static DateTime endDate = new DateTime(2020, 05, 30);

        decimal cash_deposit = 1;

//
        FuturesContract perpetual_contract = null;


        public static decimal last_underlying_close = 0;
        //记录订单
        public static List<OrderClass> PendingOrders = new List<OrderClass>() { };
        public class OrderClass
        {
            public Symbol symbol;
            public double quantity;
            public OrderClass(Symbol symbol, double quantity)
            {
                this.symbol = symbol;
                this.quantity = quantity;
            }
        }

        public override void Initialize()
        {
            SetTimeZone(TimeZones.Utc);
            SetStartDate(startDate);
            SetEndDate(endDate);
            SetCash("BTC", cash_deposit);
            var equity = AddCrypto(UnderlyingTicker, Resolution.Minute, Market.Deribit);
            var option = AddOption(UnderlyingTicker, Resolution.Minute, Market.Deribit);
            // set our strike/expiry filter for this option chain
            option.SetFilter(u => u.Strikes(-100, +100)
                                   .Expiration(TimeSpan.Zero, TimeSpan.FromDays(200)));

            //future.SetFilter(u => u.Expiration(TimeSpan.Zero, TimeSpan.FromDays(5000))); //这里一定要这么写

            // use the underlying equity as the benchmark
            //SetBenchmark(equity.Symbol);
            //SetWarmUp(TimeSpan.FromDays(30));
        }


        public class AddOneStrategy
        {
            //public OptionContract target_call_now = null;
            //public OptionContract target_put_now = null;
            //期权组合列表
            public List<decimal> strikes = new List<decimal>();
            public List<OptionPortfolio> listops = new List<OptionPortfolio>();
            public class OptionPortfolio
            {
                public OptionContract target_call = null;
                public OptionContract target_put = null;

            }


            public bool hasfirstopt = false;

            public bool bothexpired = false;

            public decimal underlying_price_now = 0;

            //public DateTime expirychoose = startDate;

            public decimal DetectionStrategy(SecurityPortfolioManager Portfolio, Slice slice, Symbol OptionSymbol, DateTime UtcTime)
            {
                OptionChain chain;
                if (slice.OptionChains.TryGetValue(OptionSymbol, out chain) && chain.Underlying.Price != 0)
                {
                    var holding = Portfolio.Securities.Values.Where(x => x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Option);
                    var holding_symbols = holding.Select(x => x.Symbol);
                    listops.RemoveAll(x => !holding_symbols.Contains(x.target_call.Symbol) && !holding_symbols.Contains(x.target_put.Symbol));

                    if (hasfirstopt)
                    {
                        if (listops.Count() == 0)
                        {
                            hasfirstopt = false;
                            bothexpired = true;
                        }

                    }
                    //直接建仓,只建一次
                    if (listops.Count() == 0 && !bothexpired)
                    {

                        var expiryList = chain.Where(x => x.Expiry >= UtcTime.Date.AddDays(5)).OrderBy(x => x.Expiry).Select(x => x.Expiry).Distinct().ToList();

                        if (expiryList.Count() > 0)
                        {
                            OptionContract target_call_now = null;
                            OptionContract target_put_now = null;
                            var expirydate = expiryList.FirstOrDefault();
                            var call_1 = chain.Where(x => x.Right == OptionRight.Call).Where(x => x.Expiry == expirydate && x.Strike > chain.Underlying.Price && x.BidSize != 0 && x.AskSize != 0 && x.BidPrice != 0 && x.AskPrice != 0).OrderByDescending(x => Math.Abs(chain.Underlying.Price * (decimal)1 - x.Strike)).ToList();
                            var put_1 = chain.Where(x => x.Right == OptionRight.Put).Where(x => x.Expiry == expirydate && x.Strike < chain.Underlying.Price && x.BidSize != 0 && x.AskSize != 0 && x.BidPrice != 0 && x.AskPrice != 0).OrderBy(x => Math.Abs(chain.Underlying.Price * (decimal)1 - x.Strike)).ToList();
                            target_call_now = call_1.FirstOrDefault();
                            target_put_now = put_1.FirstOrDefault();
                            if (target_call_now != null && target_put_now != null)
                            {

                                listops.Add(new OptionPortfolio {
                                    target_call = target_call_now,
                                    target_put = target_put_now
                                });

                                //upLine = target_call_now.Strike;
                                PendingOrders.Add(new OrderClass(target_call_now.Symbol, 1));
                                PendingOrders.Add(new OrderClass(target_put_now.Symbol, 1));
                                underlying_price_now = chain.Underlying.Price;
                                hasfirstopt = true;
                            }

                        }
                    }



                    if (hasfirstopt && chain.Underlying.Price != 0 && listops.Count() > 0)
                    {
                        for (int i = listops.Count() - 1; i >= 0; i--)
                        {
                            OptionPortfolio opt = listops[i];

                            OptionContract occs = chain.Where(x => x.Symbol == opt.target_call.Symbol).FirstOrDefault();
                            OptionContract ocps = chain.Where(x => x.Symbol == opt.target_put.Symbol).FirstOrDefault();
                            if (occs != null && ocps != null)
                            {


                                var expiryList = chain.Where(x => x.Expiry >= UtcTime.Date.AddDays(10) && x.Expiry >= opt.target_call.Expiry).OrderBy(x => x.Expiry).Select(x => x.Expiry).Distinct().ToList();
                                if (expiryList.Count() > 0)
                                {
                                    var expirydate = expiryList.FirstOrDefault();
                                    var call_1 = chain.Where(x => x.Right == OptionRight.Call).Where(x => x.Expiry == expirydate && x.Strike > chain.Underlying.Price && x.BidSize != 0 && x.AskSize != 0 && x.BidPrice != 0 && x.AskPrice != 0).OrderByDescending(x => Math.Abs(chain.Underlying.Price * (decimal)1 - x.Strike)).ToList();
                                    var put_1 = chain.Where(x => x.Right == OptionRight.Put).Where(x => x.Expiry == expirydate && x.Strike < chain.Underlying.Price && x.BidSize != 0 && x.AskSize != 0 && x.BidPrice != 0 && x.AskPrice != 0).OrderBy(x => Math.Abs(chain.Underlying.Price * (decimal)1 - x.Strike)).ToList();

                                }

                            }

                        }
                        foreach (OptionPortfolio op in listops)
                        {
                            var pricenow = chain.Underlying.Price;

                        }
                    }
                }
                return chain.Underlying.Price;
            }
        }

        /// <summary>
        /// Event - v3.0 DATA EVENT HANDLER: (Pattern) Basic template for user to override for receiving all subscription data in a single event
        /// </summary>
        /// <param name="slice">The current slice of data keyed by symbol string</param>
        public override void OnData(Slice slice)
        {
            OptionChain chain;
            slice.OptionChains.TryGetValue(OptionSymbol, out chain);
            return;

            if (slice.OptionChains.TryGetValue(OptionSymbol, out chain) && chain.Underlying.Price != 0)
            {
                decimal target_underlying_vol = 0;
                var cashbook = Portfolio.CashBook;
                var holding_underlying = cashbook.Where(x => x.Key == "BTC").Select(x => x.Value.Amount).FirstOrDefault();
                AddOneStrategies.RemoveAll(x => x.bothexpired);

                while (AddOneStrategies.Count == 0)
                {
                    AddOneStrategy aos = new AddOneStrategy();
                    AddOneStrategies.Add(aos);
                }

                for (int i = AddOneStrategies.Count() - 1; i >= 0; i--)
                {
                    //运行DetectionStrategy过程中，将需要下单的期权存入PendingOrders
                    AddOneStrategy aos = AddOneStrategies[i];

                    PendingOrders.Clear();
                    var target_underlying_vol_aos = aos.DetectionStrategy(Portfolio, slice, OptionSymbol, UtcTime);
                    foreach (OrderClass oc in PendingOrders)
                    {
                        MarketOrder(oc.symbol, oc.quantity);
                    }
                    target_underlying_vol = target_underlying_vol + target_underlying_vol_aos;
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




            //System.Diagnostics.Debug.WriteLine(orderEvent.ToString());
            //Log(orderEvent.ToString());


        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = { Language.CSharp };

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

