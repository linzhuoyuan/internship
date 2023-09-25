using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Interfaces;
using QuantConnect.Securities.Option;
using System.Diagnostics;
using Newtonsoft.Json;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    public class PutCallParityPaperTrade : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string UnderlyingTicker = "btcusd";
        private const string PerpetualTicker = "btc-perpetual";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Crypto, Market.Deribit);
        public readonly Symbol Perpetual = QuantConnect.Symbol.Create(PerpetualTicker, SecurityType.Future, Market.Deribit);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.Deribit);
        private static decimal option_order_unit = 0.1m;
        private static decimal order_depth = 0.5m;
        private static decimal fee_rate_perpetual = 0.00075m;
        private Dictionary<DateTime, Dictionary<decimal, List<OrderTicket>>> spread_trade_orders = new Dictionary<DateTime, Dictionary<decimal, List<OrderTicket>>>();
        private List<OrderTicket> perpetual_liquidation_orders = new List<OrderTicket>();
        private Dictionary<DateTime, decimal> futures_under_expiry = new Dictionary<DateTime, decimal>();

        TimeSpan liquidationTime = new DateTime(1, 1, 1, 7, 30, 0).TimeOfDay; //deribit取出每日交易时间内的时刻，7：30：00交易；
        public override string PortfolioManagerName { get; } = "deribit";

        private Symbol perpetual_symbol = null;
        public string jsonpath = Directory.GetCurrentDirectory() + "\\PutCallParityPaperTradePerpetualCount.json";

        public override void Initialize()
        {
            SetTimeZone(TimeZones.Shanghai);
            SetStartDate(2019, 10, 26);
            SetEndDate(2020, 05, 30);
            var crypto = AddCrypto(UnderlyingTicker, Resolution.Tick, Market.Deribit);
            //获取所有期权合约
            var option_contracts = OptionChainProvider.GetOptionContractList(Underlying, DateTime.Now);
            foreach (Symbol symbol in option_contracts)
            {
                if (symbol.Value.Contains("BTC"))
                {
                    var option = AddOptionContract(symbol, Resolution.Tick);
                }
                AddOptionContract(symbol, Resolution.Tick);
            }
            //获取所有期货合约
            var future_contracts = FutureChainProvider.GetFutureContractList(Underlying, DateTime.Now);
            foreach (Symbol symbol in future_contracts)
            {
                //永续合约
                if (symbol.Value.Contains("BTC-PERPETUAL"))
                {
                    perpetual_symbol = symbol;
                    var future = AddFutureContract(symbol, Resolution.Tick);
                }
            }
            //记录不同到期日下用于套利的永续合约
            if (File.Exists(jsonpath))
            {
                string Jsonsting1 = File.ReadAllText(jsonpath);
                futures_under_expiry = JsonConvert.DeserializeObject<Dictionary<DateTime, decimal>>(Jsonsting1);
            }
            else
            {
                string outputJson1 = JsonConvert.SerializeObject(futures_under_expiry);
                File.WriteAllText(jsonpath, outputJson1);
            }
        }

        public override void OnData(Slice slice)
        {
            //Stopwatch stopwatch = new Stopwatch();
            //stopwatch.Start();
            var date = this.Time.Date;
            bool isliquidationTime = slice.Time.TimeOfDay >= liquidationTime;
            //bool pastLastTradeTime = slice.Time.TimeOfDay >= lastTradeTime;
            if (IsMarketOpen(OptionSymbol))
            {
                OptionChain chain;
                if (slice.OptionChains.TryGetValue(OptionSymbol, out chain))
                {
                    var Expiry_List = chain.OrderBy(x => x.Expiry).Select(x => x.Expiry).Distinct().ToList();//到期日列表
                    var Strike_List = chain.OrderBy(x => x.Strike).Select(x => x.Strike).Distinct().ToList();//执行价格列表

                    for (int i = 0; i < Expiry_List.Count; i++)
                    {
                        for (int j = 0; j < Strike_List.Count; j++)
                        {
                            int Delta_Days = (Expiry_List[i] - slice.Time).Days;
                            double discount_rate = 1 / Math.Pow(1.03, (float)Delta_Days / (float)365);
                            List<OrderTicket> order_list = new List<OrderTicket>();
                            if (spread_trade_orders.ContainsKey(Expiry_List[i]))
                            {
                                if (spread_trade_orders[Expiry_List[i]].ContainsKey(Strike_List[j]))
                                {
                                    order_list = spread_trade_orders[Expiry_List[i]][Strike_List[j]];
                                }
                            }
                            bool all_closed_flag = true;
                            bool all_submitted_flag = false;
                            bool option_filled_flag = false;
                            OrderTicket call_order = null;
                            OrderTicket put_order = null;
                            OrderTicket perpetual_order = null;
                            //如果该组合下有订单，则判断订单是否处于已经全部完成状态，或全部已报单未成交状态， 或有期权合约成交状态；
                            if (order_list.Count > 0)
                            {
                                int cnt1 = 0;
                                int cnt2 = 0;
                                foreach (var order in order_list)
                                {
                                    if (order.Status == OrderStatus.Filled || order.Status == OrderStatus.Canceled)
                                    {
                                        cnt1 += 1;
                                    }
                                    if (order.Status == OrderStatus.Submitted)
                                    {
                                        cnt2 += 1;
                                    }
                                    if (order.Symbol.SecurityType == SecurityType.Option)
                                    {
                                        if (order.Status == OrderStatus.Filled || order.Status == OrderStatus.PartiallyFilled)
                                        {
                                            option_filled_flag = true;
                                        }
                                        var con = chain.Where(x => x.Symbol == order.Symbol).FirstOrDefault();
                                        if (con.Right == OptionRight.Call)
                                        {
                                            call_order = order;
                                        }
                                        else
                                        {
                                            put_order = order;
                                        }
                                    }else if (order.Symbol.SecurityType == SecurityType.Future)
                                    {
                                        perpetual_order = order;
                                    }
                                }
                                if (cnt1 < order_list.Count)
                                {
                                    all_closed_flag = false;
                                }
                                else
                                {
                                    spread_trade_orders[Expiry_List[i]][Strike_List[j]].Clear();
                                }
                                if (cnt2 == order_list.Count)
                                {
                                    all_submitted_flag = true;
                                }
                            }
                            //如果该组合下全部订单已经全部成交，则继续观望是否有新的机会；
                            if (all_closed_flag == true && date != Expiry_List[i])
                            {
                                var Call_Contract = chain.Where(x => x.Right == OptionRight.Call).Where(x => x.Expiry == Expiry_List[i]).Where(x => x.Strike == Strike_List[j]).FirstOrDefault();
                                var Put_Contract = chain.Where(x => x.Right == OptionRight.Put).Where(x => x.Expiry == Expiry_List[i]).Where(x => x.Strike == Strike_List[j]).FirstOrDefault();


                                if (Call_Contract != null && Put_Contract != null)
                                {
                                    if (Call_Contract.LastPrice != 0 && Put_Contract.LastPrice != 0)
                                    {
                                        // calculate expected arbitrage profit on exercise in unit of us dollar;
                                        decimal positive_spread = (Strike_List[j] * (decimal)discount_rate - Securities["BTC-PERPETUAL"].AskPrice) + (Call_Contract.BidPrice - Put_Contract.AskPrice) * Securities["BTC-PERPETUAL"].Price;
                                        decimal negative_spread = (Securities["BTC-PERPETUAL"].BidPrice - Strike_List[j] * (decimal)discount_rate) + (-Call_Contract.AskPrice + Put_Contract.BidPrice) * Securities["BTC-PERPETUAL"].Price;

                                        if (positive_spread > 50m && Call_Contract.BidPrice > 0.0m && Put_Contract.AskPrice > 0.0m)
                                        {
                                            var order_tickets1 = LimitOrder(Call_Contract.Symbol, -option_order_unit, Call_Contract.BidPrice, tag:$"positive spread with price {Call_Contract.BidPrice}");
                                            var order_tickets2 = LimitOrder(Put_Contract.Symbol, option_order_unit, Put_Contract.AskPrice, tag:$"positive spread with price {Put_Contract.AskPrice}");
                                            var order_size = (int) Math.Round(option_order_unit * Securities["BTC-PERPETUAL"].Price / 10, 0) * 10;
                                            var order_tickets3 = LimitOrder(perpetual_symbol, order_size, Securities[perpetual_symbol].AskPrice, tag:$"positive spread with price {Securities[perpetual_symbol].AskPrice}");

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
                                                futures_under_expiry[Expiry_List[i]] += order_size;
                                            }
                                            else
                                            {
                                                futures_under_expiry[Expiry_List[i]] = order_size;
                                            }
                                        }
                                        if (negative_spread > 50m && Call_Contract.AskPrice > 0.0m && Put_Contract.BidPrice > 0.0m)
                                        {
                                            var order_tickets1 = LimitOrder(Call_Contract.Symbol, option_order_unit, Call_Contract.AskPrice, tag: $"negative spread with price {Call_Contract.AskPrice}");
                                            var order_tickets2 = LimitOrder(Put_Contract.Symbol, -option_order_unit, Put_Contract.BidPrice, tag: $"negative spread with price {Put_Contract.BidPrice}");
                                            var order_size = (int) Math.Round(option_order_unit * Securities["BTC-PERPETUAL"].Price / 10, 0) * 10;
                                            var order_tickets3 = LimitOrder(perpetual_symbol, -order_size, Securities[perpetual_symbol].BidPrice, tag: $"negative spread with price {Securities[perpetual_symbol].BidPrice}");

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
                                        }
                                    }
                                }
                            }
                            //如果该组合下订单未全部成交(out-legged or all submitted but no fill)，则根据情况做订单调整；
                            if (all_closed_flag == false)
                            {
                                var Call_Contract = chain.Where(x => x.Right == OptionRight.Call).Where(x => x.Expiry == Expiry_List[i]).Where(x => x.Strike == Strike_List[j]).FirstOrDefault();
                                var Put_Contract = chain.Where(x => x.Right == OptionRight.Put).Where(x => x.Expiry == Expiry_List[i]).Where(x => x.Strike == Strike_List[j]).FirstOrDefault();
                                bool isPositiveSpread = false;
                                decimal call_prc = call_order.AverageFillPrice;
                                decimal put_prc = put_order.AverageFillPrice;
                                decimal perpetual_prc = perpetual_order.AverageFillPrice;
                                decimal spread = 0.0m;
                                if (call_order.Quantity < 0)
                                {
                                    isPositiveSpread = true;
                                }
                                if (isPositiveSpread == true)
                                {
                                    if (call_order.Status == OrderStatus.PartiallyFilled || call_order.Status == OrderStatus.Submitted)
                                    {
                                        call_prc = Call_Contract.BidPrice;
                                    }
                                    if (put_order.Status == OrderStatus.PartiallyFilled || put_order.Status == OrderStatus.Submitted)
                                    {
                                        put_prc = Put_Contract.AskPrice;
                                    }
                                    if (perpetual_order.Status == OrderStatus.PartiallyFilled || perpetual_order.Status == OrderStatus.Submitted)
                                    {
                                        perpetual_prc = Securities["BTC-PERPETUAL"].AskPrice;
                                    }

                                    spread = (Strike_List[j] * (decimal)discount_rate - perpetual_prc) + (call_prc - put_prc) * perpetual_prc;
                                }
                                else
                                {
                                    if (call_order.Status == OrderStatus.PartiallyFilled || call_order.Status == OrderStatus.Submitted)
                                    {
                                        call_prc = Call_Contract.AskPrice;
                                    }
                                    if (put_order.Status == OrderStatus.PartiallyFilled || put_order.Status == OrderStatus.Submitted)
                                    {
                                        put_prc = Put_Contract.BidPrice;
                                    }
                                    if (perpetual_order.Status == OrderStatus.PartiallyFilled || perpetual_order.Status == OrderStatus.Submitted)
                                    {
                                        perpetual_prc = Securities["BTC-PERPETUAL"].BidPrice;
                                    }
                                    spread = (perpetual_prc - Strike_List[j] * (decimal)discount_rate) + (-call_prc + put_prc) * perpetual_prc;
                                }
                                if (spread > 30m)
                                {
                                    // 仍有套利空间，更新订单价格
                                    if (call_order.Status == OrderStatus.PartiallyFilled || call_order.Status == OrderStatus.Submitted)
                                    {
                                        call_order.Update(new UpdateOrderFields {
                                            LimitPrice = call_prc,
                                            Tag = $"Update limit price {call_prc}"
                                        });
                                    }
                                    if (put_order.Status == OrderStatus.PartiallyFilled || put_order.Status == OrderStatus.Submitted)
                                    {
                                        put_order.Update(new UpdateOrderFields {
                                            LimitPrice = put_prc,
                                            Tag = $"Update limit price {put_prc}"
                                        });
                                    }
                                    if (perpetual_order.Status == OrderStatus.PartiallyFilled || perpetual_order.Status == OrderStatus.Submitted)
                                    {
                                        perpetual_order.Update(new UpdateOrderFields {
                                            LimitPrice = perpetual_prc,
                                            Tag = $"Update limit price {perpetual_prc}"
                                        });
                                    }
                                }
                                else
                                {
                                    if (all_submitted_flag == true)
                                    {
                                        // 一单未成交，且已失去套利空间，则cancel所有订单
                                        call_order.Cancel(tag: $"Cancel since spread closed, new price {call_prc}");
                                        put_order.Cancel(tag: $"Cancel since spread closed, new price {put_prc}");
                                        perpetual_order.Cancel(tag: $"Cancel since spread closed, new price {perpetual_prc}");
                                    }
                                    else 
                                    {
                                        // 部分单成交，但腿不全（Out-legged), 如果期权合约已有成交，则无论如何将腿补全，如果没有期权合约成交，则将永续合约的订单
                                        // we do not close call or put orders because we do not want to take liquidity risk for options contracts
                                        if (option_filled_flag == true)
                                        {
                                            if (call_order.Status == OrderStatus.PartiallyFilled || call_order.Status == OrderStatus.Submitted)
                                            {
                                                call_order.Update(new UpdateOrderFields {
                                                    LimitPrice = call_prc,
                                                    Tag = $"Update limit price {call_prc}"
                                                });
                                            }
                                            if (put_order.Status == OrderStatus.PartiallyFilled || put_order.Status == OrderStatus.Submitted)
                                            {
                                                put_order.Update(new UpdateOrderFields {
                                                    LimitPrice = put_prc,
                                                    Tag = $"Update limit price {put_prc}"
                                                });
                                            }
                                            if (perpetual_order.Status == OrderStatus.PartiallyFilled || perpetual_order.Status == OrderStatus.Submitted)
                                            {
                                                perpetual_order.Update(new UpdateOrderFields {
                                                    LimitPrice = perpetual_prc,
                                                    Tag = $"Update limit price {perpetual_prc}"
                                                });
                                            }
                                        }
                                        else
                                        {
                                            // cancel all options orders and close relevant perpetual contract position.
                                            // expected extra payout for this action will be perpetual fees and liquidity cost (bid-ask-spread)
                                            call_order.Cancel(tag: $"Cancel since spread closed");
                                            put_order.Cancel(tag: $"Cancel since spread closed");
                                            if (perpetual_order.Status == OrderStatus.PartiallyFilled)
                                            {
                                                perpetual_order.Cancel(tag: $"Cancel rest of the order since spread closed");
                                            }
                                            var quantity_filled = perpetual_order.QuantityFilled;
                                            if (quantity_filled > 0)
                                            {
                                                var order_ticket = LimitOrder(perpetual_symbol, -quantity_filled, Securities[perpetual_symbol].BidPrice, 
                                                    tag: $"liquidate perpetual position for incomplete pcp spread trades");
                                                // perpetual orders to track
                                               foreach (var o in order_ticket)
                                                {
                                                    perpetual_liquidation_orders.Add(o);
                                                }
                                                
                                            }
                                            if (quantity_filled < 0)
                                            {
                                                var order_ticket = LimitOrder(perpetual_symbol, -quantity_filled, Securities[perpetual_symbol].AskPrice,
                                                    tag: $"liquidate perpetual position for incomplete pcp spread trades");
                                                // perpetual orders to track
                                                foreach (var o in order_ticket)
                                                {
                                                    perpetual_liquidation_orders.Add(o);
                                                }
                                            }
                                        }
                                    }
                                }
                            }


                            //如果当天是到期日，则提前将对应的现货平仓；
                            // keep track of all perpetual liquidation orders
                            if (date == Expiry_List[i] && isliquidationTime == true)
                            {
                                decimal amt_to_liquidate = 0.0m;
                                if (futures_under_expiry.ContainsKey(Expiry_List[i]))
                                {
                                    amt_to_liquidate = futures_under_expiry[Expiry_List[i]];
                                }
                                if (amt_to_liquidate > 0)
                                {
                                    LimitOrder(perpetual_symbol, -amt_to_liquidate, Securities[perpetual_symbol].BidPrice);
                                    futures_under_expiry[Expiry_List[i]] = 0;
                                }
                                if (amt_to_liquidate < 0)
                                {
                                    LimitOrder(perpetual_symbol, -amt_to_liquidate, Securities[perpetual_symbol].AskPrice);
                                    futures_under_expiry[Expiry_List[i]] = 0;
                                }
                            }
                        }
                    }
                }

                foreach (var order_ticket in perpetual_liquidation_orders)
                {
                    if (order_ticket.Status == OrderStatus.Submitted || order_ticket.Status == OrderStatus.PartiallyFilled)
                    {
                        if (order_ticket.Quantity > 0)
                        {
                            order_ticket.Update(new UpdateOrderFields {
                                LimitPrice = Securities["BTC-PERPETUAL"].AskPrice,
                                Tag = $"Update limit price {Securities["BTC - PERPETUAL"].AskPrice}"
                            });
                        }
                        if (order_ticket.Quantity < 0)
                        {
                            order_ticket.Update(new UpdateOrderFields {
                                LimitPrice = Securities["BTC-PERPETUAL"].BidPrice,
                                Tag = $"Update limit price {Securities["BTC - PERPETUAL"].BidPrice}"
                            });
                        }
                    }
                }

                string outputJson1 = JsonConvert.SerializeObject(futures_under_expiry);
                File.WriteAllText(jsonpath, outputJson1);

            }

            //stopwatch.Stop();
            //Log($"----OnData--------{ stopwatch.Elapsed.TotalMilliseconds}");
        }

        /// <summary>
        /// Order fill event handler. On an order fill update the resulting information is passed to this method.
        /// </summary>
        /// <param name="orderEvent">Order event details containing details of the evemts</param>
        /// <remarks>This method can be called asynchronously and so should only be used by seasoned C# experts. Ensure you use proper locks on thread-unsafe objects</remarks>
        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            Log(">>>>>>>>OnOrderEvent>>>>>>>>>>>>>>>" + orderEvent.ToString());
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