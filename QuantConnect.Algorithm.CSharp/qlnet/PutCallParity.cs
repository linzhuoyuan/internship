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

namespace QuantConnect.Algorithm.CSharp
{
    public class PutCallParity : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string UnderlyingTicker = "btcusd";
        private const string PerpetualTicker = "btc-perpetual";
        public readonly Symbol Underlying = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Crypto, Market.Deribit);
        public readonly Symbol Perpetual = QuantConnect.Symbol.Create(PerpetualTicker, SecurityType.Future, Market.Deribit);
        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(UnderlyingTicker, SecurityType.Option, Market.Deribit);
        private static decimal option_order_unit = 0.1m;
        private Dictionary<DateTime, Dictionary<decimal, List<OrderTicket>>> spread_trade_orders = new Dictionary<DateTime, Dictionary<decimal, List<OrderTicket>>>();
        private Dictionary<DateTime, decimal> futures_under_expiry = new Dictionary<DateTime, decimal>();

        TimeSpan liquidationTime = new DateTime(1, 1, 1, 7, 30, 0).TimeOfDay; //deribit取出每日交易时间内的时刻，7：30：00交易；
        public override string PortfolioManagerName { get; } = "deribit";

        private Symbol perpetual_symbol = null;
        public string jsonpath = Directory.GetCurrentDirectory() + "\\PutCallParityPerpetualCount.json";

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
                    var future = AddOptionContract(symbol, Resolution.Tick);
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
            //if (!MarkPriceReady()) return;

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            double total = 0;
            bool insert_order = false;

            //var option_holdings = Portfolio.Securities.Values.Where(x => x.HoldStock == true && x.Symbol.SecurityType == SecurityType.Option).ToList();
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

                            if (all_filled_flag == true && date != Expiry_List[i])
                            {
                                int Delta_Days = (Expiry_List[i] - slice.Time).Days;
                                double discount_rate = 1 / Math.Pow(1.03, (float)Delta_Days / (float)365);

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
                                            var t1 = DateTime.Now;
                                            var order_tickets1 = MarketOrder(Call_Contract.Symbol, -option_order_unit, true);
                                            var order_tickets2 = MarketOrder(Put_Contract.Symbol, option_order_unit, true);
                                            var order_size = Math.Round(option_order_unit * Securities["BTC-PERPETUAL"].Price / 10, 0) * 10;
                                            var order_tickets3 = MarketOrder(perpetual_symbol, order_size, true);
                                            var t2 = DateTime.Now;
                                            total += (t2 - t1).TotalMilliseconds;
                                            insert_order = true;

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
                                            var t1 = DateTime.Now;
                                            var order_tickets1 = MarketOrder(Call_Contract.Symbol, option_order_unit, true);
                                            var order_tickets2 = MarketOrder(Put_Contract.Symbol, -option_order_unit, true);
                                            var order_size = Math.Round(option_order_unit * Securities["BTC-PERPETUAL"].Price / 10, 0) * 10;
                                            var order_tickets3 = MarketOrder(perpetual_symbol, -order_size, true);
                                            var t2 = DateTime.Now;
                                            total += (t2 - t1).TotalMilliseconds;
                                            insert_order = true;

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

                            if (date == Expiry_List[i] && isliquidationTime == true)
                            {
                                decimal amt_to_liquidate = 0.0m;
                                if (futures_under_expiry.ContainsKey(Expiry_List[i]))
                                {
                                    amt_to_liquidate = futures_under_expiry[Expiry_List[i]];
                                }
                                if (amt_to_liquidate != 0)
                                {
                                    var t1 = DateTime.Now;
                                    MarketOrder(perpetual_symbol, -amt_to_liquidate, true);
                                    var t2 = DateTime.Now;
                                    total += (t2 - t1).TotalMilliseconds;
                                    insert_order = true;
                                    futures_under_expiry[Expiry_List[i]] = 0;
                                }
                            }
                        }
                    }
                }

                string outputJson1 = JsonConvert.SerializeObject(futures_under_expiry);
                File.WriteAllText(jsonpath, outputJson1);

            }

            stopwatch.Stop();

            if (insert_order)
            {
                Log($"----OnData--------{ stopwatch.Elapsed.TotalMilliseconds}  {total}");
            }
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
        public Dictionary<string, string> ExpectedStatistics => new Dictionary<string, string>
        {
        };
    }
}