using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Threading;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Brokerages;
using QuantConnect.Configuration;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Interfaces;
using QuantConnect.Securities.Crypto;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.CSharp
{
    /// <summary>
    /// 
    /// </summary>
    public class BinanceLivingTestYX : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string coin = "DOGE";

        private Symbol futureSymbol = QuantConnect.Symbol.Create(
            coin + "-PERPETUAL",
            SecurityType.Future,
            Market.Binance
        );

        private Symbol spotSymbol = QuantConnect.Symbol.Create(
            coin + "USDT",
            SecurityType.Crypto,
            Market.Binance
        );

        private DateTime _starTime = DateTime.Now;
        private bool _startTrade = false;
        private bool _clearOrderAndHolding = false;
        private bool _quoteReady = false;

        private decimal _limitPrice = 0.1m;
        private decimal _limitVolume = 0.4m;
        private long _lastOrderId = 0;
        private DateTime _lastTradeTime = DateTime.Now;

        private int count = 0;


        public override void Initialize()
        {
            AddCrypto(coin + "USDT", Resolution.Tick, Market.Binance);
            AddCrypto("USDTUSD", Resolution.Tick, Market.Binance);

            var future_contracts = FutureChainProvider.GetFutureContractList(futureSymbol, DateTime.Now);
            foreach (var symbol in future_contracts)
            {
                if (symbol.Value.Contains(coin))
                {
                    AddFutureContract(symbol, Resolution.Tick);
                    futureSymbol = symbol;
                }
            }

            SetCash("USDT", 10m);

            //SetWarmUp(TimeSpan.FromDays(2));
            //SetWarmUp(TimeSpan.FromDays(2),Resolution.Minute);
            //SetWarmUp(TimeSpan.FromDays(15), Resolution.Daily);

            // set our strike/expiry filter for this option chain
            // use the underlying equity as the benchmark
            //SetWarmUp(TimeSpan.FromDays(10));

            // SetBenchmark(crypto.Symbol);

        }


        private static string GetBaseCurrency(Security security)
        {
            return security.Symbol.Value.Substring(0,
                security.Symbol.Value.Length - security.SymbolProperties.QuoteCurrency.Length);
        }

        public override void OnData(Slice slice)
        {
            var spot = Securities[spotSymbol];
            var futures = Securities[futureSymbol];

            //价格
            //spot.AskPrice;
            //spot.BidPrice;
            //spot.Price;

            //最小市值
            var spotMinNotional = spot.SymbolProperties.MinNotional;
            var futuresMinNotional = futures.SymbolProperties.MinNotional;

            var baseCurrency = GetBaseCurrency(spot);
            //获取现货持仓
            Portfolio.CashBook.TryGetValue(baseCurrency, out var baseCash);
            Portfolio.CashBook.TryGetValue(spot.SymbolProperties.QuoteCurrency, out var quoteCash);

            //下单
            if (count == 0)
            {
                ++count;
                MarketOrder(spot.Symbol, -baseCash.Amount);
            }
            //MarketOrder(futures.Symbol, 5m);

            foreach (var item in slice.Ticks)
            {
                if (item.Value.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"{item.Key.Value} {item.Key.SecurityType} {item.Key.ID.Market} {item.Value.FirstOrDefault().LastPrice}");
                }
            }

            

            //if (IsWarmingUp)
            //{
            //    //System.Diagnostics.Debug.WriteLine($"OnData: slice.Time {slice.Time.ToString("s")}");
            //    var lst = slice.Ticks.Where(x => x.Key.Value.Contains(coinPair)).Select(x => x.Value);
            //    var first = lst.First();
            //    if (first != null)
            //    {
            //        var lstValue = first.First();
            //        if (lstValue != null)
            //        {
            //            var p = lstValue.LastPrice;
            //            System.Diagnostics.Debug.WriteLine($"OnData: IsWarmingUp {lstValue.Symbol.Value} {lstValue.Time.ToString("s")} {p}");
            //        }
            //    }

            //    return;
            //}

            //return;
            //FuturesChain futureChain;
            //if (slice.FutureChains.TryGetValue(FutureSymbol, out futureChain))
            //{
            //    foreach (var item in futureChain.Contracts)
            //    {
            //        if (item.Key.ID.Symbol == coinPair)
            //        {
            //            var contract = item.Value;
            //            System.Diagnostics.Debug.WriteLine($"{contract.Symbol.ID.Symbol} BidPrice:{contract.BidPrice} BidSize:{contract.BidSize} AskPrice:{contract.AskPrice} AskSize:{contract.AskSize}");

            //            if (count == 0)
            //            {
            //                count++;
            //                //MarketOrder(contract.Symbol, 0.001);
            //            }
            //        }
            //    }
            //}
            //return;
            //if (!_quoteReady)
            //{
            //    var t = slice.Ticks.Where(x => x.Key.ID.Symbol == coinPair).Select(x => x.Value).FirstOrDefault();
            //    if (t != null)
            //    {
            //        var q = t.First();
            //        if (q.LastPrice != 0)
            //        {
            //            _quoteReady = true;
            //        }
            //    }

            //    return;
            //}

            //if ((DateTime.Now - _starTime).TotalMinutes < 1)
            //{
            //    if (!_clearOrderAndHolding)
            //    {
            //        _clearOrderAndHolding = true;
            //        foreach (var ticket in Transactions.GetOpenOrderTickets())
            //        {
            //            if (ticket.Symbol.ID.Symbol == coinPair)
            //            {
            //                var result = ticket.Cancel();
            //                if (result.IsError)
            //                {
            //                    Error($"撤单失败");
            //                }
            //            }
            //        }

            //        foreach (var item in Securities)
            //        {
            //            if (item.Value.HoldStock && item.Value.Symbol.ID.Symbol == coinPair)
            //            {
            //                var result = MarketOrder(item.Key, item.Value.Holdings.Quantity * (-1));
            //            }
            //        }
            //    }
            //    return;
            //}

            //if (!_startTrade)
            //{
            //    if (Transactions.GetOpenOrders(x=>x.Symbol.ID.Symbol == coinPair).Count > 0)
            //    {
            //        Error($"撤单失败");
            //        return;
            //    }
            //    foreach (var item in Securities)
            //    {
            //        if (item.Value.HoldStock && item.Value.Symbol.ID.Symbol == coinPair)
            //        {
            //            Error($"平仓失败");
            //            return;
            //        }
            //    }

            //    _startTrade = true;
            //    return;
            //}


            //FuturesChain chain;
            //if (slice.FutureChains.TryGetValue(FutureSymbol, out chain))
            //{
            //    foreach (var item in chain.Contracts)
            //    {
            //        if (item.Key.ID.Symbol == coinPair)
            //        {
            //            var contract = item.Value;
            //            System.Diagnostics.Debug.WriteLine($"{contract.Symbol.ID.Symbol} BidPrice:{contract.BidPrice} BidSize:{contract.BidSize} AskPrice:{contract.AskPrice} AskSize:{contract.AskSize}");

            //            PlaceMarketOrder(contract);

            //            if (_lastOrderId == 0) //直接下单
            //            {
            //                PlaceLimitOrder(contract);
            //            }
            //            else
            //            {
            //                //先检查订单状态
            //                var ticket = Transactions.GetOrderTicket(_lastOrderId);
            //                if (!ticket.SubmitRequest.Response.IsProcessed)
            //                {
            //                    //下单还未处理，继续等待
            //                    continue;
            //                }
            //                else
            //                {
            //                    if (ticket.SubmitRequest.Response.IsError)
            //                    {
            //                        //重新下单
            //                        PlaceLimitOrder(contract);
            //                    }
            //                    else
            //                    {
            //                        //下单成功 检查状态
            //                        var order =Transactions.GetOrderById(_lastOrderId);
            //                        if (order.Status == OrderStatus.Invalid)
            //                        {
            //                            PlaceLimitOrder(contract);
            //                        }
            //                        else if(order.Status == OrderStatus.Filled)
            //                        {
            //                            _lastOrderId = 0;
            //                        }
            //                    }
            //                }
            //            }
            //        }
            //    }
            //}
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            //_sw.WriteLine("=============================================================================");
            Debug($"Algo OrderEvent:{orderEvent.Symbol.Value} {orderEvent.OrderId} {orderEvent.Status} FillQuantity:{orderEvent.FillQuantity}");
            

            //var order = Transactions.GetOrderById(orderEvent.OrderId);
            //Debug($"Algo Order:{order.Id} {order.Status} FillQuantity:{order.FillQuantity}");
            //var ticket = Transactions.GetOrderTicket(orderEvent.OrderId);
            //if (order.FillQuantity > 0 && order.FillQuantity < _placeOrderVolume)
            //{
            //    ticket.Cancel();
            //}
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

