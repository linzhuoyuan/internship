using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Brokerages;
using QuantConnect.Indicators;
using QuantConnect.Orders;
using QuantConnect.Interfaces;
using QuantConnect.Securities.Crypto;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Securities;
using QuantConnect.Util;
using DateTime = System.DateTime;

namespace QuantConnect.Algorithm.CSharp
{
    class BinanceBacktesting : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string coin = "ETHUSDT";
        private const string PERPETUAL = Futures.Currencies.ETH_PERPETUAL;

        private readonly Symbol _futureSymbol = QuantConnect.Symbol.Create(PERPETUAL, SecurityType.Future, Market.Binance);
        private bool _futureSymbolInit;

        public readonly Symbol coinSymbol = QuantConnect.Symbol.Create(
            coin,
            SecurityType.Crypto,
            Market.Binance
        );

        //private static DateTime startDate = new DateTime(2020, 03, 20);
        //private static DateTime endDate = new DateTime(2020, 03, 23);

        private static DateTime startDate = new DateTime(2020, 7, 25);
        private static DateTime endDate = new DateTime(2020, 12, 30);

        private bool _traded = false;

        public override void Initialize()
        {
            SetStartDate(startDate);
            SetEndDate(endDate);
            SetCash("USDT", 1000000000000);

            var crypto = AddCrypto(coin, Resolution.Minute, Market.Binance);
            // var future = AddFuture(PERPETUAL, Resolution.Minute, Market.Binance);
            //AddFutureContract(FutureSymbolDetail, Resolution.Minute);
            // set our strike/expiry filter for this option chain

            //这里一定要这么写
            // future.SetFilter(u => u.Expiration(TimeSpan.Zero, TimeSpan.FromDays(5000))); 

            //SetWarmUp(TimeSpan.FromDays(1));

            //var futureContracts = FutureChainProvider.GetFutureContractList(FutureSymbol, startDate);
            // use the underlying equity as the benchmark
            //SetBenchmark(crypto.Symbol);
            //ChangeLeverage();
        }

        private void SetFutureSymbol(Slice slice)
        {
            if (_futureSymbolInit)
            {
                return;
            }

            if (slice.FutureChains.TryGetValue(_futureSymbol, out var futureChain))
            {
                if (futureChain.Contracts.Count > 0)
                {
                    //_futureSymbol = futureChain.Contracts.First().Value.Symbol;
                    _futureSymbolInit = true;
                }
            }
        }

        public override void OnData(Slice slice)
        {
            Log($"{slice.Time}");
            var security = Securities[coinSymbol];
            if (!security.HoldStock)
            {
                MarketOrder(security.Symbol, 0.2);
            }
            else
            {
                if (security.Holdings.IsLong)
                {
                    MarketOrder(security.Symbol, -0.2);
                }
                else
                {
                    MarketOrder(security.Symbol, 0.2);
                }
            }

            //if (IsWarmingUp)
            //{
            //    System.Diagnostics.Debug.WriteLine($"IsWarmingUp slice.Time:{slice.Time.ToString("yyyy-MM-dd hh:mm:ss")}");
            //    return;
            //}
            // SetFutureSymbol(slice);

            //var futures = Securities[_futureSymbol];

            // if (slice.FutureChains.TryGetValue(_futureSymbol, out var futureChain))
            // {
            //     foreach (var i in futureChain.Contracts)
            //     {
            //         var contract = i.Value;
            //         //System.Diagnostics.Debug.WriteLine($"OnData: {contract.Symbol.Value} Time:{contract.Time:yyyy-MM-dd HH:mm:ss} Volume:{contract.Volume} LastPrice:{contract.LastPrice } BidPrice:{contract.BidPrice} AskPrice:{contract.AskPrice}");
            //         var security = Securities[contract.Symbol];

            //         if (!security.HoldStock)
            //         {
            //             MarketOrder(contract.Symbol, 1000);
            //         }
            //         else
            //         {
            //             if (security.Holdings.IsLong)
            //             {
            //                 MarketOrder(contract.Symbol, -1000);
            //             }
            //             else
            //             {
            //                 MarketOrder(contract.Symbol, 1000);
            //             }
            //         }
            //     }
            // }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            Log($"orderEvent:{orderEvent.Symbol} {orderEvent.Status} {orderEvent.FillQuantity} {orderEvent.UtcTime}");
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
