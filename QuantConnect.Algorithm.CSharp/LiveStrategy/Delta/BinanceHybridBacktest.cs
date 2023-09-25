using FtxApi.Rest.Models;
using Newtonsoft.Json;
using QuantConnect.Algorithm.CSharp.LiveStrategy.DataType;
using QuantConnect.Data;
using QuantConnect.Orders;
using QuantConnect.Parameters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;

namespace QuantConnect.Algorithm.CSharp.LiveStrategy.Delta
{
    public class BinanceHybridBacktest : BinanceHybrid
    {
        [Parameter("start")] public DateTime Start = DateTime.MinValue;
        [Parameter("end")] public DateTime End = DateTime.Now;
        [Parameter("output-base-directory")] public string OutputBaseDirectory = Directory.GetCurrentDirectory();
        [Parameter("get-base")] public bool GetBase = false;
        [Parameter("statistics-summary")] public string StatisticsSummaryFilePath = Directory.GetCurrentDirectory();
        private Dictionary<string, decimal> _assets = new();
        private Dictionary<string, string> _pnls = new();
        private StringBuilder _trades = new();

        protected override void UpdateTargetPositions()
        {
            return;
        }

        public override void Initialize()
        {
            base.Initialize();
            ImportantMessageKey = RegularMessageKey;
            ImportantMessageToken = RegularMessageToken;
            if (!Directory.Exists(OutputBaseDirectory))
            {
                Directory.CreateDirectory(OutputBaseDirectory);
            }

            File.Delete($"{OutputBaseDirectory}/Total-PNL.csv");
            var configs = DataLoader.FetchData<BinanceLiveAssetAllocation>(AssetAllocationConfigFile).ToList();
            if (configs.Count == 0)
            {
                return;
            }

            BullAssetAllocationConfigMap =
                configs.Where(c => c.IsBull).ToDictionary(x => x.Ticker, x => x);
            BearAssetAllocationConfigMap =
                configs.Where(c => !c.IsBull).ToDictionary(x => x.Ticker, x => x);
            foreach (var key in BullAssetAllocationConfigMap.Keys.Union(BearAssetAllocationConfigMap.Keys))
            {
                CoinPairs.Add(key);
                AddCoinPair(key);
                _assets.Add(key,
                    (BullAssetAllocationConfigMap.ContainsKey(key) ? BullAssetAllocationConfigMap[key].Asset : 0) +
                    (BearAssetAllocationConfigMap.ContainsKey(key) ? BearAssetAllocationConfigMap[key].Asset : 0));
            }
            BullResetCoins = BullAssetAllocationConfigMap.Keys.ToList();
            _bearResetCoins = BearAssetAllocationConfigMap.Keys.ToList();
            _pnls = CoinPairs.ToDictionary(x => x, x => string.Empty);
            _pnls.Add("total", string.Empty);
            if (GetBase)
            {
                foreach (var coin in CoinPairs)
                {
                    _pnls.Add(coin + "-BAH", string.Empty);
                }
            }

            foreach (var coin in CoinPairs)
            {
                File.Delete($"{OutputBaseDirectory}/{coin}-PNL.csv");
                if (GetBase)
                {
                    File.Delete($"{OutputBaseDirectory}/{coin}-BAH-PNL.csv");
                }
            }

            SetStartDate(Start);
            SetEndDate(End);
            SetCash("USDT",
                BullAssetAllocationConfigMap.Values.Select(m => m.Asset).Sum() +
                BearAssetAllocationConfigMap.Values.Select(m => m.Asset).Sum());
        }

        protected override void AddCoinPair(string coinPair)
        {
            var cryptoSymbol = AddCrypto(coinPair + "USDT", Resolution.Minute, TradeMarket).Symbol;
            var futuresSymbol = AddPerpetual(coinPair + "-PERPETUAL", Resolution.Minute, TradeMarket).Symbol;

            _underlyings[coinPair] = new Underlying(coinPair, cryptoSymbol is not null, futuresSymbol is not null);
            _isFirstRuns[coinPair] = true;
            _underlyingPrices[coinPair] = 0;
            _priceDict[coinPair] = new List<double>();
            _symbols.Add(coinPair, new Tuple<Symbol, Symbol>(cryptoSymbol, futuresSymbol));
        }

        public override void OnWarmupFinished()
        {
            base.OnWarmupFinished();
            Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromMinutes(1)), RecordPnls);
        }

        private decimal GetPnl(string coin)
        {
            return Securities[_symbols[coin].Item1].Holdings.NetProfit +
                                Securities[_symbols[coin].Item1].Holdings.UnrealizedProfit +
                                Securities[_symbols[coin].Item2].Holdings.NetProfit +
                                Securities[_symbols[coin].Item2].Holdings.UnrealizedProfit +
                                _assets[coin];
        }

        private void RecordPnls()
        {
            _pnls["total"] += Time + "," + Portfolio.TotalPortfolioValue + Environment.NewLine;
            foreach (var coin in CoinPairs)
            {
                var pnl = GetPnl(coin);
                _pnls[coin] += $"{Time},{pnl}\n";
                if (GetBase)
                {
                    _pnls[coin + "-BAH"] += $"{Time},{Securities[_symbols[coin].Item1].Price}\n";
                }
            }

            if (_pnls.Values.First().Length > 10000)
            {
                File.AppendAllText($"{OutputBaseDirectory}/Total-PNL.csv", _pnls["total"]);
                _pnls["total"] = string.Empty;
                foreach (var key in CoinPairs)
                {
                    File.AppendAllText($"{OutputBaseDirectory}/{key}-PNL.csv", _pnls[key]);
                    _pnls[key] = string.Empty;
                    if (GetBase)
                    {
                        File.AppendAllText($"{OutputBaseDirectory}/{key}-BAH-PNL.csv", _pnls[key + "-BAH"]);
                        _pnls[key + "-BAH"] = string.Empty;
                    }
                }
            }
        }

        public override void OnData(Slice slice)
        {
            if (IsWarmingUp)
            {
                if (Time.Second != 0)
                {
                    return;
                }
                foreach (var coin in CoinPairs)
                {
                    _priceDict[coin].Add((double)Securities[_symbols[coin].Item1].Price);
                }
                return;
            }

            var spotBalances = Portfolio.CashBook.ToDictionary(x => x.Key, x => x.Value.Amount);
            foreach (var coin in CoinPairs)
            {
                TradeCoinPair(coin, slice, spotBalances);
            }
        }

        public override void OnEndOfAlgorithm()
        {
            File.WriteAllText($"{OutputBaseDirectory}/trades.csv", _trades.ToString());
            _trades.Clear();
            CalculateStatistics("PNL");
            if (GetBase)
            {
                CalculateStatistics("BAH-PNL");
            }

            base.OnEndOfAlgorithm();
        }

        private void CalculateStatistics(string fileSuffix)
        {
            if (!File.Exists(StatisticsSummaryFilePath))
            {
                File.AppendAllText(StatisticsSummaryFilePath,
                    "Strategy,DateTime,StartTime,EndTime,Coin,SharpeRatio,CalmarRatio,MaxDD,ARR,MaxSingleDayLoss,OutputDirectory\n");
            }

            foreach (var coin in _symbols.Keys)
            {
                var pnlPath = $"{OutputBaseDirectory}/{coin}-{fileSuffix}.csv";
                if (File.Exists(pnlPath))
                {
                    var rawData = File.ReadAllLines(pnlPath);
                    var assets = new SortedDictionary<DateTime, decimal>();
                    foreach (var line in rawData)
                    {
                        var data = line.Split(',');
                        if (data.Length > 1)
                        {
                            var date = Convert.ToDateTime(data[0]);
                            if (date.Hour == 0 && date.Minute == 2)
                            {
                                assets.Add(Convert.ToDateTime(data[0]), Convert.ToDecimal(line.Split(',')[1]));
                            }
                        }
                    }

                    var drawDrown = Statistics.Statistics.DrawdownPercent(assets);
                    var returns = new List<double>();
                    for (var i = 1; i < assets.Count; i++)
                    {
                        var return_ = (assets.ElementAt(i).Value - assets.ElementAt(i - 1).Value) /
                                      assets.ElementAt(i - 1).Value;
                        returns.Add(Convert.ToDouble(return_));
                    }

                    var sharpeRatio = Statistics.Statistics.SharpeRatio(returns, 0.07, 365);
                    var annualReturn = Statistics.Statistics.CompoundingAnnualPerformance(_assets[coin],
                        assets.Values.Last(), assets.Count / 365m);
                    var calmar = Math.Round(annualReturn / drawDrown, 2);

                    File.AppendAllText(StatisticsSummaryFilePath,
                        $"{AccountName}-{fileSuffix},{DateTime.Now},{Start},{End},{coin},{sharpeRatio},{calmar},{drawDrown},{annualReturn},{returns.Min()},{OutputBaseDirectory}\n");
                }
            }
        }

        protected override void SaveJsonAndSendImportantMessages()
        {
            return;
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            base.OnOrderEvent(orderEvent);
            if (orderEvent.Status == OrderStatus.Filled)
            {
                if (_trades.Length == 0)
                {
                    _trades.AppendLine("Time,Symbol,FillQty,FillPrice");
                }
                _trades.AppendLine($"{Time},{orderEvent.Symbol},{orderEvent.FillQuantity},{orderEvent.FillPrice}");
            }
            if (_trades.Length > 1000000)
            {
                File.AppendAllText($"{OutputBaseDirectory}/trades.csv", _trades.ToString());
                _trades = new StringBuilder();
            }
        }
    }
}
