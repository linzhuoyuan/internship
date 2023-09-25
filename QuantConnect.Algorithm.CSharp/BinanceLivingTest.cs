using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Algorithm.CSharp.qlnet.tools;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// 
    /// </summary>
    public class BinanceLivingTest : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string CrvBusdSymbol = "CRVBUSD";
        private const string BtcBusdSymbol = "BTCBUSD.futbin";
        private const string EthBusdSymbol = "ETHBUSD.futbin";

        private readonly Symbol _spotSymbol = QuantConnect.Symbol.Create(
            CrvBusdSymbol,
            SecurityType.Future,
            Market.Binance
        );

        private Symbol _futureSymbol;

        private bool _futureSymbolInit;

        private bool _startTrade = false;
        private bool _clearOrderAndHolding = false;
        private bool _quoteReady = false;

        private decimal _limitPrice = 0.1m;
        private decimal _limitVolume = 0.4m;
        private long _lastOrderId = 0;
        private DateTime _lastTradeTime = DateTime.Now;

        private int _spotTraded = 0;
        private int _futuresTraded = 0;

        private DateTime _lastCheckTime;

        private bool _isFirstRun = true;

        public override void Initialize()
        {
            AddCrypto("ETHBUSD.sptbin", Resolution.Tick, Market.Binance);
            _futureSymbol = AddPerpetual(EthBusdSymbol, Resolution.Tick, Market.Binance).Symbol;
            //SetWarmUp(TimeSpan.FromDays(2));
            //SetWarmUp(TimeSpan.FromDays(2),Resolution.Minute);
            //SetWarmUp(TimeSpan.FromDays(15), Resolution.Daily);
            
            SymbolCache.TryGetSymbol("BTCUSDT.sptbin", out var spotSymbol);
            SymbolCache.TryGetSymbol("BTCUSDT.futbin", out var futureSymbol);

            // set our strike/expiry filter for this option chain
            // use the underlying equity as the benchmark
            //SetWarmUp(TimeSpan.FromDays(10));

            // SetBenchmark(crypto.Symbol);

        }

        public override void OnData(Slice slice)
        {
            if (_isFirstRun)
            {
                //Transfer(1900m, UserTransferType.UmFuture2Spot);
                //_isFirstRun = false;
            }

            //if (BrokerageAccountMap.TryGetValue("binance-futures-busd-yixuan_binance", out var accountInfo))
            //if (BrokerageAccountMap.TryGetValue("binance-futures-busd-test", out var accountInfo))
            var accountInfo = GetBrokerageFuturesAccount("BUSD", true);
            if (accountInfo != null)
            {
                var data = accountInfo.CustomData;
                if (data != null)
                {
                    var helper = new BinanceAccountHelper(data);
                    foreach (var asset in helper.FuturesAssets())
                    {
                        if (asset.WalletBalance == 0)
                        {
                            continue;
                        }
                        Log($"{asset.Asset},{asset.WalletBalance}");
                    }

                    foreach (var position in helper.FuturesPositions())
                    {
                        if (position.Quantity == 0)
                        {
                            continue;
                        }
                        Log($"{position.Symbol},{position.PositionSide},{position.Quantity}");
                    }
                }
            }

            foreach (var (symbol, ticks) in slice.Ticks)
            {
                Log($"{symbol.Value}, {symbol.SecurityType}, {ticks.First().Price}");
            }

            var futures = Securities[_futureSymbol];

            var openOrders = Transactions.GetOpenOrders();
            if (openOrders.Count > 0)
            {
                if ((DateTime.UtcNow - _lastCheckTime).TotalSeconds >= 30)
                {
                    foreach (var o in openOrders)
                    {
                        Transactions.CancelOrder(o.Id);
                    }
                }
                return;
            }

            _lastCheckTime = DateTime.UtcNow;

            if (_futuresTraded == 0 && futures.Price > 0)
            {
                ++_futuresTraded;
                if (futures.Holdings.AbsoluteQuantity > 0)
                {
                    //LimitOrder(futures.Symbol, -futures.Holdings.Quantity, futures.AskPrice);
                }
                else
                {
                    var size = Math.Round(futures.SymbolProperties.MinNotional / futures.BidPrice, 3) + 0.001m;
                    //LimitOrder(futures.Symbol, size, futures.BidPrice);
                }
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            //_sw.WriteLine("=============================================================================");
            Debug($"Algo OrderEvent:{orderEvent.Symbol.Value} {orderEvent.OrderId} {orderEvent.Status} FillQuantity:{orderEvent.FillQuantity}");

            if (Transactions.GetOpenOrders().Count == 0)
            {
                _spotTraded = 0;
                _futuresTraded = 0;
            }
        }

        private void PlaceLimitOrder(FuturesContract contract)
        {
            if ((DateTime.Now - _lastTradeTime).TotalSeconds > 30)
            {
                _lastTradeTime = DateTime.Now;
                var result = LimitOrder(contract.Symbol, _limitVolume, _limitPrice);
                _lastOrderId = result.First().OrderId;
                Debug($"下限价单 {_lastOrderId} {_limitVolume}");
            }
        }

        private void PlaceMarketOrder(FuturesContract contract)
        {
            if ((DateTime.Now - _lastTradeTime).TotalSeconds > 30 && _lastOrderId > 0)
            {
                var order = Transactions.GetOrderById(_lastOrderId);
                if (order.Status != OrderStatus.Filled)
                {
                    _lastTradeTime = DateTime.Now;
                    var volume = (-1) * _limitVolume / 4;
                    var result = MarketOrder(contract.Symbol, volume);
                    var lastOrderId = result.First().OrderId;
                    Debug($"下市价单 {lastOrderId} {volume}");
                }
            }
        }

        public override void OnProgramExit()
        {

        }

        public override void OnWarmupFinished()
        {



            base.OnWarmupFinished();
        }

        public override void OnRiskDegreeChanged(decimal riskDegree, decimal userRiskDegree)
        {
            Log($"Risk degree is {riskDegree}");
        }

        public override void OnTransferCompleted(decimal amount, UserTransferType type, string currency)
        {
            Log($"Transfer completed amount {amount} type {type} currency {currency}");
        }

        public override void OnTransferFailed(decimal amount, UserTransferType type, string currency, string error)
        {
            Log($"Transfer failed amount {amount} type {type} currency {currency} error {error}");
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

