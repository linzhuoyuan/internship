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
using QLNet;

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


    public class SVIDeribit : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        public override string PortfolioManagerName { get; } = "deribit";

        private const string UnderlyingTicker = "BTCUSD";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Crypto, Market.Deribit);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.Deribit);
        public decimal spread = 0.01M;
        private static DateTime startDate = new DateTime(2020, 5, 7);
        private static DateTime endDate = new DateTime(2020, 05, 8);

        private DateTime lastDate = startDate.AddDays(-1);
        static TimeSpan endOfDay = new DateTime(1, 1, 1, 16, 02, 0).TimeOfDay;

        private double _forward = 0.03;
        private double _tau = 1.0;

        private decimal quantity = 0.1m;
        private decimal max_position = 1m;
        private decimal tick_size = 0.0005m;
        private int min_quote_width = 4;
        private int max_pos_backoff = 3;
        private int _tick_precision = 4;

        private Dictionary<Symbol, OrderStatus> localBuyOrders = new Dictionary<Symbol, OrderStatus>();
        private Dictionary<Symbol, OrderStatus> localSellOrders = new Dictionary<Symbol, OrderStatus>();

        
        //记录当前加仓期权上线
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
            //SetTimeZone(TimeZones.Shanghai);
            SetStartDate(startDate);
            SetEndDate(endDate);
            //SetCash(100000);
            SetCash("BTC", 10);
            var equity = AddCrypto(UnderlyingTicker, Resolution.Minute, Market.Deribit);
            var option = AddOption(UnderlyingTicker, Resolution.Minute, Market.Deribit);

            // set our strike/expiry filter for this option chain
            option.SetFilter(u => u.Strikes(-100, +100)
                                   .Expiration(TimeSpan.FromDays(7), TimeSpan.FromDays(200)));

            // use the underlying equity as the benchmark
            SetBenchmark(equity.Symbol);
            //SetWarmUp(TimeSpan.FromDays(30));
        }


        /// <summary>
        /// Event - v3.0 DATA EVENT HANDLER: (Pattern) Basic template for user to override for receiving all subscription data in a single event
        /// </summary>
        /// <param name="slice">The current slice of data keyed by symbol string</param>
        public override void OnData(Slice slice)
        {
            OptionChain chain;
            if (slice.OptionChains.TryGetValue(OptionSymbol, out chain) && chain.Underlying.Price != 0)
            {
                //if(slice.Time.TimeOfDay< new DateTime(1, 1, 1, 9, 0, 0).TimeOfDay)
                //{
                //    return;
                //}
                var expiryList = chain.OrderBy(x => x.Expiry).Select(x => x.Expiry).Distinct().ToList();
                var near_month = expiryList[0];
                near_month = new DateTime(2020, 05, 29);
                var call = chain.Where(x => x.Right == OptionRight.Call).Where(x => x.Expiry == near_month).OrderBy(x => x.Strike);
                var put = chain.Where(x => x.Right == OptionRight.Put).Where(x => x.Expiry == near_month).OrderBy(x => x.Strike);
                var callTrade = call.Where(x => x.BidPrice > 0).Where(x => x.AskPrice > 0).Where(x => (x.AskPrice - x.BidPrice) / x.AskPrice < 0.5m).Where(x => (x.AskPrice - x.BidPrice) > tick_size).ToList();
                var putTrade = put.Where(x => x.BidPrice > 0).Where(x => x.AskPrice > 0).Where(x => (x.AskPrice - x.BidPrice) / x.AskPrice < 0.5m).Where(x => (x.AskPrice - x.BidPrice) > tick_size).ToList();
                var callStrikeList = callTrade.Select(x => x.Strike).Distinct().ToList();
                var putStrikeList = putTrade.Select(x => x.Strike).Distinct().ToList();
                
                List<double> callBSVols = new InitializedList<double>(callStrikeList.Count, 0.0);
                List<double> callSVIVols = new InitializedList<double>(callStrikeList.Count, 0.0);
                List<double> putBSVols = new InitializedList<double>(putStrikeList.Count, 0.0);
                List<double> putSVIVols = new InitializedList<double>(putStrikeList.Count, 0.0);
                List<double> callStrikes = new InitializedList<double>(callStrikeList.Count, 0.0);
                List<double> putStrikes = new InitializedList<double>(putStrikeList.Count, 0.0);

                for (int i = 0; i < callTrade.Count; ++i)
                {
                    // take calls only;
                    
                    var callContract = callTrade[i];   
                    //if(callContract.Symbol.Value== "BTCUSD  200529C07000000" && slice.Time.TimeOfDay == new DateTime(1, 1, 1, 9, 1, 0).TimeOfDay)
                    //{
                    //    Log("-------------hit breakpoint-------------");
                    //}
                    //callContract
                    callBSVols[i] = decimal.ToDouble(callContract.ImpliedVolatility);
                    callStrikes[i] = decimal.ToDouble(callStrikeList[i]);
                }
                for (int i = 0; i < putStrikeList.Count; ++i)
                {
                    // take puts only;
                    var putContract = (
                                from optionContract in putTrade
                                where optionContract.Strike == putStrikeList[i]
                                select optionContract
                                ).FirstOrDefault();

                    //putContract
                    putBSVols[i] = decimal.ToDouble(putContract.ImpliedVolatility);
                    putStrikes[i] = decimal.ToDouble(putStrikeList[i]);
                }
                
                //Log(strikes.ToString());
                //Log(sviVols.ToString());
                

                SviInterpolation callSVI = new SviInterpolation(callStrikes, callStrikeList.Count, callBSVols, _tau,
                                                             _forward, null, null, null,
                                                             null, null, false, false, false,
                                                             false, false, false, null,
                                                             null, 1E-8, false,
                                                             0); //don't allow for random start values

                callSVI.enableExtrapolation();
                callSVI.update();

                //Log($"datetime: {slice.Time} a: {callSVI.a()} b: {callSVI.b()} sigma: {callSVI.sigma()} rho: {callSVI.rho()} m: {callSVI.m()}");
                for (int i = 0; i < callTrade.Count; ++i)
                {
                    callSVIVols[i] = callSVI.value(callStrikes[i]);
                    var callContract = callTrade[i];
                    callContract.PricingVolatility = (decimal)callSVIVols[i];
                    var p = callContract.TheoreticalPrice;
                    
                    //Log($"strike: {callStrikes[i]} impliedVolatility: {callBSVols[i]} localVol: {callSVIVols[i]}");

                }

                CustomerChartData customerChartData = new CustomerChartData(slice.Time, "Strikes", "ImpliedVolatility", "LocalVolatility", 0, 2);
                customerChartData.XList = callStrikes.ToArray();
                customerChartData.YList = callBSVols.ToArray();
                customerChartData.Y1List = callSVIVols.ToArray();
                Draw(customerChartData);

                var holding = Portfolio.Securities.Values.Where(x => x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Option).ToList();
                var hold_contract = Portfolio.Securities.Values.Where(x => x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Option);
                var hold_expiry = hold_contract.OrderBy(x => x.Symbol.ID.Date).Select(x => x.Symbol.ID.Date).Distinct().ToList();
                
                
                decimal delta = 0;
                decimal gamma = 0;
                decimal theta = 0;
                decimal vega = 0;

                foreach (var con in call)
                {
                    var sec = Securities[con.Symbol.Value];
                    delta += con.Greeks.Delta * sec.Holdings.Quantity;
                }

                foreach (var con in holding)
                {
                    
                }

                List<Order> openOrders = Transactions.GetOpenOrders();



                foreach(var order in openOrders)
                {
                    var ticket = Transactions.GetOrderTicket(order.Id);
                    var symbol = order.Symbol;
                    var con = (from optionContract in call
                               where optionContract.Symbol == symbol
                               select optionContract
                                ).FirstOrDefault();
                    var theo_price = Math.Round(con.TheoreticalPrice, _tick_precision, MidpointRounding.AwayFromZero);
                    var bid_ask_spread = Math.Max((con.AskPrice - con.BidPrice) / tick_size, min_quote_width);
                    var theo_bid = theo_price - bid_ask_spread / 2 * tick_size;
                    var theo_ask = theo_price + bid_ask_spread / 2 * tick_size;
                    var current_price = ((LimitOrder)order).LimitPrice;
                    var target_price = ((LimitOrder)order).LimitPrice;
                    if (ticket.Quantity > 0)
                    {
                        target_price = con.BidPrice;
                        
                        if (delta > 0)
                        {
                            target_price = Math.Min(theo_bid, con.BidPrice);
                        }
                        if(delta < 0)
                        {
                            target_price = Math.Max(theo_bid, con.BidPrice);
                        }
                        if (Securities[symbol.Value].Holdings.Quantity >= max_position)
                        {
                            target_price -= max_pos_backoff * tick_size;
                        }
                        
                    
                    }
                    if (ticket.Quantity < 0)
                    {
                        target_price = con.AskPrice;

                        if (delta < 0)
                        {
                            target_price = Math.Max(theo_ask, con.AskPrice);
                        }
                        if(delta > 0)
                        {
                            target_price = Math.Min(theo_ask, con.AskPrice);
                        }
                    
                        
                        if (Securities[symbol.Value].Holdings.Quantity <= -max_position)
                        {
                            target_price += max_pos_backoff * tick_size;
                        }
                        

                    }
                    if (current_price != target_price)
                    {
                        var updateSettings = new UpdateOrderFields();
                        updateSettings.LimitPrice = target_price;
                        //updateSettings.Tag = "Limit Price Updated"
                        var response = ticket.Update(updateSettings);
                    }
                }



                foreach (var con in callTrade)
                {
                    if (!localBuyOrders.ContainsKey(con.Symbol))
                    {
                        LimitOrder(con.Symbol, quantity, con.BidPrice);
                    }
                    if (!localSellOrders.ContainsKey(con.Symbol))
                    {
                        LimitOrder(con.Symbol, -quantity, con.AskPrice);
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

            System.Diagnostics.Debug.WriteLine(orderEvent.ToString());
            Log(orderEvent.ToString());
            var order = Transactions.GetOrderById(orderEvent.OrderId);
            if(orderEvent.Status == OrderStatus.Submitted)
            {
                if (orderEvent.Direction == OrderDirection.Buy)
                {
                    localBuyOrders[order.Symbol] = orderEvent.Status;
                }
                else
                {
                    localSellOrders[order.Symbol] = orderEvent.Status;
                }
            }
            if (orderEvent.Status == OrderStatus.Filled || orderEvent.Status == OrderStatus.Canceled)
            {
                if (orderEvent.Direction == OrderDirection.Buy)
                {
                    localBuyOrders.Remove(order.Symbol);
                }
                else
                {
                    localSellOrders.Remove(order.Symbol);
                }
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
