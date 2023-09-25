using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Interfaces;
using QuantConnect.Securities.Option;
using System.Diagnostics.Contracts;
using QuantConnect.Securities.Future;
using System.Diagnostics;

namespace QuantConnect.Algorithm.CSharp
{
    public class DeribitPutCallParity_backtest : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string UnderlyingTicker = "btcusd";
        private const string PerpetualTicker = "btc-perpetual";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Crypto, Market.Deribit);
        public readonly Symbol Perpetual = QuantConnect.Symbol.Create(PerpetualTicker, SecurityType.Future, Market.Deribit);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.Deribit);
        private static decimal option_order_unit = 1m;
        private Dictionary<DateTime, Dictionary<decimal, List<OrderTicket>>> spread_trade_orders = new Dictionary<DateTime, Dictionary<decimal, List<OrderTicket>>>();
        private Dictionary<DateTime, decimal> futures_under_expiry = new Dictionary<DateTime, decimal>();
        private Dictionary<DateTime, Dictionary<decimal, decimal>> spread_expected_payoff = new Dictionary<DateTime, Dictionary<decimal, decimal>>();

        TimeSpan liquidationTime = new DateTime(1, 1, 1, 7, 30, 0).TimeOfDay; //deribit取出每日交易时间内的时刻，7：30：00交易；
        public override string PortfolioManagerName { get; } = "deribit";
        private int arb_cnt = 0;

        public override void Initialize()
        {
            SetTimeZone(TimeZones.Utc);
            SetStartDate(2019, 10, 26);
            SetEndDate(2020, 05, 30);
            SetCash("BTC", 50m);
            var crypto = AddCrypto(UnderlyingTicker, Resolution.Minute, Market.Deribit);
            //获取所有期权合约
            var option = AddOption(UnderlyingTicker, Resolution.Minute, Market.Deribit);
            //获取所有期货合约
            var future = AddFuture(PerpetualTicker, Resolution.Minute, Market.Deribit);

            option.SetFilter(u => u.Strikes(-100, 100).Expiration(TimeSpan.Zero, TimeSpan.FromDays(30)));
            future.SetFilter(u => u.Expiration(TimeSpan.Zero, TimeSpan.FromDays(5000)));
            SetBenchmark(Underlying);
        }

        public override void OnData(Slice slice)
        {
            //var option_holdings = Portfolio.Securities.Values.Where(x => x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Option).ToList();
            var date = this.Time.Date;
            bool isliquidationTime = slice.Time.TimeOfDay >= liquidationTime;
            //bool pastLastTradeTime = slice.Time.TimeOfDay >= lastTradeTime;

            if (IsMarketOpen(OptionSymbol))
            {
                FuturesChain future_chain;
                FuturesContract perpetual_contract = null;
                if (slice.FuturesChains.TryGetValue(Perpetual, out future_chain))
                {
                    foreach (var contract in future_chain)
                    {
                        if (contract.Symbol.Value.Contains("BTC-PERPETUAL"))
                        {
                            perpetual_contract = contract;
                        }
                    }
                }
                OptionChain chain;
                if (slice.OptionChains.TryGetValue(OptionSymbol, out chain) && perpetual_contract != null)
                {
                    var Expiry_List = chain.OrderBy(x => x.Expiry).Select(x => x.Expiry).Distinct().ToList();//到期日列表
                    var Strike_List = chain.OrderBy(x => x.Strike).Select(x => x.Strike).Distinct().ToList();//执行价格列表

                    for (int i = 0; i < Expiry_List.Count; i++)
                    {

                        for (int j = 0; j < Strike_List.Count; j++)
                        {
                            List<OrderTicket> order_list = new List<OrderTicket>();
                            if (spread_trade_orders.ContainsKey(Expiry_List[i]))
                            {
                                if (spread_trade_orders[Expiry_List[i]].ContainsKey(Strike_List[j]))
                                {
                                    order_list = spread_trade_orders[Expiry_List[i]][Strike_List[j]];
                                }
                            }
                            //var holding_list = option_holdings.Where(x => x.Symbol.ID.Date == Expiry_List[i] && x.Symbol.ID.StrikePrice == Strike_List[j]).ToList();
                            bool all_filled_flag = true;
                            //如果該組合下有訂單，則判斷訂單是否已經全部完成狀態；
                            if (order_list.Count > 0)
                            {
                                int cnt = 0;
                                foreach (var order in order_list)
                                {
                                    if (order.Status == OrderStatus.Filled)
                                    {
                                        cnt += 1;
                                    }
                                }
                                if (cnt < order_list.Count)
                                {
                                    all_filled_flag = false;
                                }
                            }

                            else if (date != Expiry_List[i])
                            {
                                int Delta_Days = (Expiry_List[i] - slice.Time).Days;
                                double discount_rate = 1 / Math.Pow(1.03, (float)Delta_Days / (float)365);

                                var Call_Contract = chain.FirstOrDefault(x => x.Right == OptionRight.Call
                                                         && x.Expiry == Expiry_List[i]
                                                         && x.Strike == Strike_List[j]);
                                var Put_Contract = chain.FirstOrDefault(x => x.Right == OptionRight.Put
                                                         && x.Expiry == Expiry_List[i]
                                                         && x.Strike == Strike_List[j]);

                                if (Call_Contract != null && Put_Contract != null)
                                {
                                    var isCallTradable = Securities[Call_Contract.Symbol.Value].IsTradable;
                                    var isPutTradable = Securities[Put_Contract.Symbol.Value].IsTradable;
                                    // calculate expected arbitrage profit on exercise in unit of us dollar;
                                    var perpetual_mid_prc = (perpetual_contract.BidPrice + perpetual_contract.AskPrice) / 2;
                                    decimal positive_spread = (Strike_List[j] * (decimal)discount_rate - perpetual_contract.AskPrice) + (Call_Contract.BidPrice - Put_Contract.AskPrice) * perpetual_mid_prc;
                                    decimal negative_spread = (perpetual_contract.BidPrice - Strike_List[j] * (decimal)discount_rate) + (-Call_Contract.AskPrice + Put_Contract.BidPrice) * perpetual_mid_prc;

                                    if (positive_spread > 200m && Call_Contract.BidPrice > 0.0m && Put_Contract.AskPrice > 0.0m && isCallTradable == true && isPutTradable == true)
                                    {
                                        //Log($"time {slice.Time} expiry {Expiry_List[i]} strike {Strike_List[j]} positive_spread {positive_spread}");
                                        var order_tickets1 = MarketOrder(Call_Contract.Symbol, -option_order_unit, true, $"strike {Strike_List[j]} positive_spread {positive_spread}");
                                        var order_tickets2 = MarketOrder(Put_Contract.Symbol, option_order_unit, true, $"strike {Strike_List[j]} positive_spread {positive_spread}");
                                        var order_size = Math.Round(option_order_unit * perpetual_mid_prc / 10, 0) * 10;
                                        var order_tickets3 = MarketOrder(perpetual_contract.Symbol, order_size, true, $"strike {Strike_List[j]} positive_spread {positive_spread}");
                                        var a = order_tickets1.Concat(order_tickets2).ToList();
                                        var b = a.Concat(order_tickets3).ToList();
                                        if (spread_trade_orders.ContainsKey(Expiry_List[i]))
                                        {
                                            if (spread_trade_orders[Expiry_List[i]].ContainsKey(Strike_List[j]))
                                            {
                                                spread_trade_orders[Expiry_List[i]][Strike_List[j]] = b;
                                            }
                                            else
                                            {
                                                spread_trade_orders[Expiry_List[i]].Add(Strike_List[j], b);
                                            }
                                        }
                                        else
                                        {
                                            spread_trade_orders.Add(Expiry_List[i], new Dictionary<decimal, List<OrderTicket>>());
                                            spread_trade_orders[Expiry_List[i]].Add(Strike_List[j], b);
                                        }
                                        if (futures_under_expiry.ContainsKey(Expiry_List[i]))
                                        {
                                            futures_under_expiry[Expiry_List[i]] += order_size;
                                        }
                                        else
                                        {
                                            futures_under_expiry[Expiry_List[i]] = order_size;
                                        }
                                        arb_cnt += 1;
                                    }
                                    if (negative_spread > 200m && Call_Contract.AskPrice > 0.0m && Put_Contract.BidPrice > 0.0m && isCallTradable == true && isPutTradable == true)
                                    {
                                        //Log($"time {slice.Time} expiry {Expiry_List[i]} strike {Strike_List[j]} negative_spread {negative_spread}");
                                        var order_tickets1 = MarketOrder(Call_Contract.Symbol, option_order_unit, true, $"strike {Strike_List[j]} negative_spread {negative_spread}");
                                        var order_tickets2 = MarketOrder(Put_Contract.Symbol, -option_order_unit, true, $"strike {Strike_List[j]} negative_spread {negative_spread}");
                                        var order_size = Math.Round(option_order_unit * perpetual_mid_prc / 10, 0) * 10;
                                        var order_tickets3 = MarketOrder(perpetual_contract.Symbol, -order_size, true, $"strike {Strike_List[j]} negative_spread {negative_spread}");
                                        var a = order_tickets1.Concat(order_tickets2).ToList<OrderTicket>();
                                        var b = a.Concat(order_tickets3).ToList<OrderTicket>();
                                        if (spread_trade_orders.ContainsKey(Expiry_List[i]))
                                        {
                                            if (spread_trade_orders[Expiry_List[i]].ContainsKey(Strike_List[j]))
                                            {
                                                spread_trade_orders[Expiry_List[i]][Strike_List[j]] = b;
                                            }
                                            else
                                            {
                                                spread_trade_orders[Expiry_List[i]].Add(Strike_List[j], b);
                                            }
                                        }
                                        else
                                        {
                                            spread_trade_orders.Add(Expiry_List[i], new Dictionary<decimal, List<OrderTicket>>());
                                            spread_trade_orders[Expiry_List[i]].Add(Strike_List[j], b);
                                        }
                                        if (futures_under_expiry.ContainsKey(Expiry_List[i]))
                                        {
                                            futures_under_expiry[Expiry_List[i]] -= order_size;
                                        }
                                        else
                                        {
                                            futures_under_expiry[Expiry_List[i]] = -order_size;
                                        }
                                        arb_cnt += 1;
                                    }
                                }
                            }


                        }
                    }

                    foreach (var k in futures_under_expiry.ToArray())
                    {
                        if (date == k.Key && isliquidationTime && k.Value != 0)
                        {
                            MarketOrder(perpetual_contract.Symbol, -k.Value, true, $"liquidate perpetuals on expiry {k.Key}");
                            futures_under_expiry[k.Key] = 0;
                            arb_cnt = 0;
                        }
                    }
                }
            }
            //Log("------------" + stopwatch.Elapsed.TotalMilliseconds);
        }

        /// <summary>
        /// Order fill event handler. On an order fill update the resulting information is passed to this method.
        /// </summary>
        /// <param name="orderEvent">Order event details containing details of the evemts</param>
        /// <remarks>This method can be called asynchronously and so should only be used by seasoned C# experts. Ensure you use proper locks on thread-unsafe objects</remarks>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
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
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string> {
        };
    }
}