﻿/*
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
using System.IO;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Interfaces;
using QuantConnect.Securities.Option;
using QuantConnect.Securities;

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
    public class TestDownloadData : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        public override string PortfolioManagerName { get; } = "deribit";

        private const string PERPETUAL = Futures.Currencies.ETH_PERPETUAL;
        private const string UnderlyingTicker = "btcusd";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Crypto, Market.Deribit);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.Deribit);
        public readonly Symbol FutureSymbol = QuantConnect.Symbol.Create(PERPETUAL, SecurityType.Future, Market.Deribit);

       

        private static int i = 1;

        public override void Initialize()
        {
           
            SetTimeZone(TimeZones.Shanghai);

            SetStartDate(2020, 01, 01);
            SetEndDate(2020, 01, 06);

            SetCash("BTC", 100m);
            SetCash("ETH", 100m);

            var equity = AddCrypto(Underlying, Resolution.Minute, Market.Deribit);
            var option = AddOption(Underlying, Resolution.Minute, Market.Deribit);
            var future = AddFuture(PERPETUAL, Resolution.Minute, Market.Deribit);



            option.SetFilter(u => u.Strikes(-100, +100)
                .Expiration(TimeSpan.Zero, TimeSpan.FromDays(200)));

            future.SetFilter(u => u.Expiration(TimeSpan.Zero, TimeSpan.FromDays(5000))); //这里一定要这么写

            // use the underlying equity as the benchmark
            //SetBenchmark(equity.Symbol);

        }

        /// <summary>
        /// Event - v3.0 DATA EVENT HANDLER: (Pattern) Basic template for user to override for receiving all subscription data in a single event
        /// </summary>
        /// <param name="slice">The current slice of data keyed by symbol string</param>
        public override void OnData(Slice slice)
        {
            System.Diagnostics.Debug.WriteLine($"OnData {slice.Time.ToString()}");

            FuturesChain future_chain;
            if (slice.FutureChains.TryGetValue(FutureSymbol, out future_chain))
            {
                var list = future_chain.Where(x => x.Symbol.Value.Contains("PERPETUAL")).ToList();
                var futureContract = list.FirstOrDefault();
                if (futureContract != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"OnData futureContract {futureContract.Symbol.Value} {futureContract.Time.ToString()}");

                }

                //var s = Portfolio.Securities.Where(x => x.Value.Symbol.Value == "BTC-PERPETUAL").ToList().FirstOrDefault();
                //if (i == 1 && futureContract != null)
                //{
                //    i++;
                //    System.Diagnostics.Debug.WriteLine($" Insert Order Time : {slice.Time} " +
                //                                       $"Security Time {s.Value.LocalTime.ToString()}");
                //    LimitOrder(futureContract.Symbol, 100, futureContract.AskPrice);
                //}
            }

            OptionChain option_chain;
            if (slice.OptionChains.TryGetValue(OptionSymbol, out option_chain))
            {
                var optionContract = option_chain.FirstOrDefault();
                if (optionContract != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"OnData optionContract {optionContract.Symbol.Value} {optionContract.Time.ToString()}");
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
            //Log("------>OnOrderEvent:" + orderEvent.ToString());
            if (orderEvent.Status == OrderStatus.Submitted)
            {
                //开仓即撤
                //Transactions.CancelOrder(orderEvent.OrderId);
            }

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