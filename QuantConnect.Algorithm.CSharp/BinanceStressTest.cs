using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Algorithm.CSharp.qlnet.tools;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Interfaces;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// 
    /// </summary>
    public class BinanceStressTest : QCAlgorithm, IRegressionAlgorithmDefinition
    {

        private string MessageHeader = "Test3";
        private readonly List<string> futuresList = new List<string>
        {
            "BTCUSDT", "ETHUSDT", "BNBUSDT", "LTCUSDT", "FTMUSDT", "DOTUSDT", "DASHUSDT",
            "TRXUSDT", "ADAUSDT", "XRPUSDT", "LINKUSDT", "EOSUSDT"
        };

        private readonly List<string> cryptoList = new List<string>
        {
            "BTCUSD", "ETHUSD", "LTCUSD"
        };
        private Dictionary<string,Symbol> symbol_dict = new Dictionary<string, Symbol>{};

        private DateTime _lastCleanUpTime;
        private readonly string ddKey = "BinanceStressTest";
        private bool flag = true;


        public override void Initialize()
        {
            //var crypto = AddCrypto(coin_btc, Resolution.Tick, Market.Binance);
            AddCrypto("USDTUSD", Resolution.Tick, Market.Binance);
            Settings.LiquidateEnabled = true;

            foreach (string coin in futuresList)
            {
                //生成qc的symbol格式
                symbol_dict[coin] = QuantConnect.Symbol.Create(
                    coin,
                    SecurityType.Future,
                    Market.Binance
                );
                //订阅期货数据
                var future_contracts = FutureChainProvider.GetFutureContractList(symbol_dict[coin], DateTime.Now);
                foreach (var symbol in future_contracts)
                {
                    if (symbol.Value.Contains(coin))
                    {
                        AddFutureContract(symbol, Resolution.Tick);
                    }
                }

                
                //上次运行时间设置
               // lastrunning_time_list[coin] = new DateTime(2020, 08, 10, 16, 02, 0);//startDate.AddDays(-1);
                //第一次运行设置
               // first_run_list[coin] = true;
                //记录最新价设置
               // underlying_price_list[coin] = 0;

            }

            foreach (string coin in cryptoList)
            {
                AddCrypto(coin, Resolution.Tick, Market.Binance);
            }

            //SetWarmUp(TimeSpan.FromDays(2));
            //SetWarmUp(TimeSpan.FromDays(2),Resolution.Minute);
            //SetWarmUp(TimeSpan.FromDays(15), Resolution.Daily);

            // set our strike/expiry filter for this option chain
            // use the underlying equity as the benchmark
            //SetWarmUp(TimeSpan.FromDays(10));

            // SetBenchmark(crypto.Symbol);
            Liquidate();
            _lastCleanUpTime = DateTime.Now;
        }

        private bool InCoinsList(string key)
        {
            foreach (var coins in futuresList)
            {
                if (key.Contains(coins)) return true;
            }

            return false;
        }

        public override void OnData(Slice slice)
        {
            if ((DateTime.Now - _lastCleanUpTime).TotalSeconds > 2)
            {
                Liquidate();
                _lastCleanUpTime = DateTime.Now;
            }
            var openOrders = Transactions.GetOpenOrders();
            var timeoutOrders = openOrders.Where(order => (DateTime.UtcNow - order.Time).TotalSeconds > 5).Select(order => order.Id);
            foreach (var o in timeoutOrders)
            {
                Transactions.CancelOrder(o);
            }
            if (IsWarmingUp)
            {
                //System.Diagnostics.Debug.WriteLine($"OnData: slice.Time {slice.Time.ToString("s")}");
                var lst = slice.Ticks.Where(x => InCoinsList(x.Key.Value)).Select(x => x.Value);
                var first = lst.First();
                if (first != null)
                {
                    var lstValue = first.First();
                    if (lstValue != null)
                    {
                        var p = lstValue.LastPrice;
                        System.Diagnostics.Debug.WriteLine($"OnData: IsWarmingUp {lstValue.Symbol.Value} {lstValue.Time.ToString("s")} {p}");
                    }
                }

                return;
            }

            FuturesChain futureChain;
            foreach (var coin in futuresList)
            {
                if (slice.FutureChains.TryGetValue(symbol_dict[coin], out futureChain))
                {
                    foreach (var item in futureChain.Contracts)
                    {
                        if (futuresList.Contains(item.Key.ID.Symbol))
                        {
                            var contract = item.Value;
                            System.Diagnostics.Debug.WriteLine($"{contract.Symbol.ID.Symbol} BidPrice:{contract.BidPrice} BidSize:{contract.BidSize} AskPrice:{contract.AskPrice} AskSize:{contract.AskSize}");
                            var c = 0;
                            if (c < 2 && contract.LastPrice != 0)
                            {
                                c++;
                                var security = Securities[contract.Symbol];
                                var b = security.SymbolProperties.LotSize;
                                if (b * contract.LastPrice < 5)
                                {
                                    b = 5/contract.LastPrice + 1;
                                }
                               // LimitOrder(contract.Symbol, b, contract.LastPrice);
                            }
                        }
                    }
                }
            }
            foreach(var coin in cryptoList)
            {
                if (slice.ContainsKey(coin))
                {
                    var tmpData = slice[coin];
                    MarketOrder(coin, 1);
                }

            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            //_sw.WriteLine("=============================================================================");
            Debug($"Algo OrderEvent:{orderEvent.OrderId} {orderEvent.Status} FillQuantity:{orderEvent.FillQuantity} Message: {orderEvent.Message}");
            
            if (orderEvent.Status == OrderStatus.Invalid)
            {
                Notify.MomDingDing($"{MessageHeader}:{orderEvent.OrderId} FillQuantity:{orderEvent.FillQuantity} Symbol: {orderEvent.Symbol} Message: {orderEvent.Message}", ddKey);
            }
            if (orderEvent.Message.Contains("资金不足"))
            {
                Liquidate();
            }
        }

        public override void OnBrokerageDisconnect()
        {
            Liquidate();
            base.OnBrokerageDisconnect();
            Notify.MomDingDing($"{MessageHeader}: Brokerage disconnected!", ddKey);
        }

        public override void OnEndOfAlgorithm()
        {
            Liquidate();
            base.OnEndOfAlgorithm();
            Notify.MomDingDing($"{MessageHeader}: Algorithm ended!", ddKey);
        }

        public override void OnProgramExit()
        {
            Liquidate();
            Notify.MomDingDing("{MessageHeader}: BinanceStressTest exited!", ddKey);
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

