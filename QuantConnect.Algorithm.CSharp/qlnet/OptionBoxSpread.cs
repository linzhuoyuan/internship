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
    public class OptionBoxSpread : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string UnderlyingTicker = "510050.SH";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Equity, Market.SSE);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.SSE);
        private DateTime _lastTradeTime = DateTime.Now;
        private decimal _thres = 50;
        private int _openUnit = 2;

        public override void Initialize()
        {
            SetTimeZone(TimeZones.Shanghai);
            SetCash(1000000);
            SetStartDate(2015, 03, 01);
            SetEndDate(2020, 04, 26);

            var equity = AddEquity(UnderlyingTicker, Resolution.Tick, Market.SSE);
            var option = AddOption(UnderlyingTicker, Resolution.Tick, Market.SSE);

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
            var openOrders = Transactions.GetOpenOrders();
            if (IsMarketOpen(OptionSymbol))
            {
                OptionChain chain;
                if (slice.OptionChains.TryGetValue(OptionSymbol, out chain) && chain.Underlying.Price != 0)
                {
                    var expiryList = chain.OrderBy(x => x.Expiry).Select(x => x.Expiry).Distinct().ToList();
                    foreach (var expiryDate in expiryList)
                    {
                        var strikeList = chain.Where(x => x.Expiry == expiryDate).OrderBy(x => x.Strike).Select(x => x.Strike).Distinct().ToList();
                        List<decimal> higherPrcList = new List<decimal>();
                        List<decimal> lowerPrcList = new List<decimal>();
                        List<decimal> kList = new List<decimal>();
                        foreach (var strikePrice in strikeList)
                        {
                            var callContract = (
                                from optionContract in chain
                                where optionContract.Right == OptionRight.Call
                                where optionContract.Expiry == expiryDate
                                where optionContract.Strike == strikePrice
                                select optionContract
                                ).FirstOrDefault();
                            var putContract = (
                                from optionContract in chain
                                where optionContract.Right == OptionRight.Put
                                where optionContract.Expiry == expiryDate
                                where optionContract.Strike == strikePrice
                                select optionContract
                                ).FirstOrDefault();
                            if (callContract != null && putContract != null)
                            {
                                decimal higherPrc = callContract.AskPrice - putContract.BidPrice + strikePrice;
                                decimal lowerPrc = callContract.BidPrice - putContract.AskPrice + strikePrice;
                                higherPrcList.Add(higherPrc);
                                lowerPrcList.Add(lowerPrc);
                                kList.Add(strikePrice);
                            }

                        }
                        decimal potentialMaxPayoff = 0;
                        decimal strike1 = 0;
                        decimal strike2 = 0;
                        if (higherPrcList.Count > 0)
                        {
                            int maxI = Maxi(lowerPrcList);
                            int minI = Mini(higherPrcList);
                            strike1 = kList[minI];
                            strike2 = kList[maxI];
                            potentialMaxPayoff = (lowerPrcList[maxI] - higherPrcList[minI]) * 10000;
                        }

                        // open position if potential maximum payoff is greater than the threshold we set.
                        // check current position for option chain of this specific expiry.
                        var holdings_expiry = holdings.Where(x => x.Symbol.ID.Date == expiryDate).ToList();
                        var orders_expiry = openOrders.Where(x => x.Symbol.ID.Date == expiryDate).ToList();
                        if (potentialMaxPayoff >= _thres && holdings_expiry.Count == 0 && orders_expiry.Count == 0)
                        {
                            // buy min higherPrc group of option contracts and sell max lowerPrc group of option contracts.
                            var call1 = (
                                from optionContract in chain
                                where optionContract.Right == OptionRight.Call
                                where optionContract.Expiry == expiryDate
                                where optionContract.Strike == strike1
                                select optionContract
                                ).FirstOrDefault();
                            var put1 = (
                                from optionContract in chain
                                where optionContract.Right == OptionRight.Put
                                where optionContract.Expiry == expiryDate
                                where optionContract.Strike == strike1
                                select optionContract
                                ).FirstOrDefault();
                            var call2 = (
                                from optionContract in chain
                                where optionContract.Right == OptionRight.Call
                                where optionContract.Expiry == expiryDate
                                where optionContract.Strike == strike2
                                select optionContract
                                ).FirstOrDefault();
                            var put2 = (
                                from optionContract in chain
                                where optionContract.Right == OptionRight.Put
                                where optionContract.Expiry == expiryDate
                                where optionContract.Strike == strike2
                                select optionContract
                                ).FirstOrDefault();
                            /*OpenBuyLimit(call1.Symbol, 5, call1.AskPrice, tag: "construct portfolio for expiry date " + expiryDate.ToString());
                            OpenSellLimit(put1.Symbol, 5, put1.BidPrice, tag: "construct portfolio for expiry date " + expiryDate.ToString());
                            OpenSellLimit(call2.Symbol, 5, call2.BidPrice, tag: "contruct portfolio for expiry date " + expiryDate.ToString());
                            OpenBuyLimit(put2.Symbol, 5, put2.AskPrice, tag: "contruct portfolio for expiry date " + expiryDate.ToString());*/

                            OpenBuy(call1.Symbol, _openUnit, true, tag: $"construct portfolio for expiry date: {expiryDate}, expected fill price: {call1.AskPrice}");
                            OpenSell(put1.Symbol, _openUnit, true, tag: $"construct portfolio for expiry date: {expiryDate}, expected fill price: {put1.BidPrice}");
                            OpenSell(call2.Symbol, _openUnit, true, tag: $"construct portfolio for expiry date: {expiryDate}, expected fill price: {call2.BidPrice}");
                            OpenBuy(put2.Symbol, _openUnit, true, tag: $"construct portfolio for expiry date: {expiryDate}, expected fill price: {put2.AskPrice}");
                            Log($"construct portfolio for expiry date: {expiryDate}, expected fill price: {call1.AskPrice}");
                            Log($"construct portfolio for expiry date: {expiryDate}, expected fill price: {put1.BidPrice}");
                            Log($"construct portfolio for expiry date: {expiryDate}, expected fill price: {call2.BidPrice}");
                            Log($"construct portfolio for expiry date: {expiryDate}, expected fill price: {put2.AskPrice}");
                            Log($"expected payoff: {potentialMaxPayoff * _openUnit}");

                        }
                        else if (holdings_expiry.Count > 0 && orders_expiry.Count == 0)
                        {
                            // check shall close position in advance.
                            // calculate profit for immediate liquidation of position.
                            decimal expectedProfit = 0;
                            decimal positionCost = 0;
                            decimal expectedExcercisePayoff = (strike2 - strike1) * _openUnit * 10000;
                            foreach (var holding in holdings_expiry)
                            {
                                expectedProfit += (holding.Holdings.UnrealizedProfit + holding.LongHoldings.UnrealizedProfit + holding.ShortHoldings.UnrealizedProfit);
                                positionCost += (holding.Holdings.HoldingsCost + holding.LongHoldings.HoldingsCost + holding.ShortHoldings.HoldingsCost);
                                // var a = holding as Option;
                                // expectedProfit += a.getIntrinsicValue(a.Underlying.Price);
                            }
                            expectedExcercisePayoff -= positionCost;
                            if (holdings_expiry.Count < 4 || expectedProfit > 1000)
                            //if (expectedProfit > expectedExcercisePayoff)
                            {
                                foreach (var holding in holdings_expiry)
                                {
                                    if (holding.LongHoldings.Quantity != 0)
                                    {
                                        //CloseSellLimit(holding.Symbol, holding.LongHoldings.AbsoluteQuantity, holding.BidPrice, tag: "liquidate portfolio for expiry date " + expiryDate.ToString());
                                        CloseSell(holding.Symbol, holding.LongHoldings.AbsoluteQuantity, true, tag: "liquidate portfolio for expiry: " + expiryDate.ToString());
                                    }
                                    if (holding.ShortHoldings.Quantity != 0)
                                    {
                                        //CloseBuyLimit(holding.Symbol, holding.ShortHoldings.AbsoluteQuantity, holding.AskPrice, tag: "liquidate portfolio for expiry date " + expiryDate.ToString());
                                        CloseBuy(holding.Symbol, holding.ShortHoldings.AbsoluteQuantity, true, tag: "liquidate portfolio for expiry: " + expiryDate.ToString());
                                    }
                                }
                            }
                        }
                        else if (orders_expiry.Count > 0)
                        {
                            //将未完成订单cancel掉
                            foreach (var order in orders_expiry)
                            {
                                if (order.Status == OrderStatus.PartiallyFilled && (slice.Time - order.CreatedTime).TotalSeconds > 30)
                                {
                                    Transactions.CancelOrder(order.Id);
                                }
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
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
        };
    }
}
