using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using System.IO;
using Calculators.Estimators;
using Calculators.IO;
using Newtonsoft.Json;
using QuantConnect.Algorithm.CSharp.LiveStrategy.DataType;
using QuantConnect.Algorithm.CSharp.LiveStrategy.Strategies;
using QuantConnect.Algorithm.CSharp.qlnet.tools;
using QuantConnect.Data.Market;
using QuantConnect.Notifications;
using QuantConnect.Orders;
using QuantConnect.Parameters;
using QuantConnect.Securities;
using RestSharp;

namespace QuantConnect.Algorithm.CSharp
{
    public class BinanceHybrid : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        [Parameter("momcrypto-userId")] public string AccountName = string.Empty;
        [Parameter("bull-json")] public string BullJsonPath = string.Empty;
        [Parameter("bear-json")] public string BearJsonPath = string.Empty;
        [Parameter("dingding-key")] public string RegularMessageKey = string.Empty;
        [Parameter("dingding-token")] public string RegularMessageToken = string.Empty;
        [Parameter("twap-interval-seconds")] public double TwapIntervalSeconds = 12;
        [Parameter("total-twap-trades")] public int TotalTwapTrades = 5;

        [Parameter("warm-up-resolution")] public Resolution WarmUpResulotion = Resolution.Daily;
        [Parameter("warm-up-periods")] public double WarmUpPeriods = 15;
        [Parameter("lambda")] public double Lambda = 0.94;

        [Parameter("asset-allocation-config-file")]
        public string AssetAllocationConfigFile = string.Empty;

        [Parameter("max-open-order-seconds")] public double MaxOpenOrderSeconds = 5;

        [Parameter("max-orders-per-half-second")]
        public int MaxOrdersPerHalfSecond = 2;
        [Parameter("min-notional")] public decimal MinNotional = 100;

        protected const string TradeMarket = Market.Binance;
        
        //第一次运行字典
        public Dictionary<string, bool> _isFirstRuns = new ();

        protected Dictionary<string, List<DeltaCollarStrategyLeverage>> BullStrategies = new();

        protected Dictionary<string, List<DeltaCollarStrategyBear>> BearStrategies = new();
        
        protected IList<string> CoinPairs = new List<string>();

        //当前价格字典
        protected Dictionary<string, decimal> _underlyingPrices = new ();

        //存储收盘价的字典
        protected Dictionary<string, List<double>> _priceDict = new ();
        protected Dictionary<string, Underlying> _underlyings = new ();
        private IList<string> _actionRequiredMessages = new List<string>();
        protected string ImportantMessageKey = "ActionRequired";
        protected string ImportantMessageToken = "WARN";
        private Dictionary<string, decimal> _printOutPrices = new ();

        private Dictionary<string, EWMAVolatilityEstimator> _volEstimators = new ();

        private double _volMultiplier;
        protected Dictionary<string, BinanceLiveAssetAllocation> BullAssetAllocationConfigMap = new();
        protected Dictionary<string, BinanceLiveAssetAllocation> BearAssetAllocationConfigMap = new();
        protected IList<string> BullResetCoins = new List<string>();
        protected IList<string> _bearResetCoins = new List<string>();
        protected Dictionary<string, Tuple<Symbol, Symbol>> _symbols = new(); // item1 is CryptoSymbol, item2 is PerpetualSymbol
        private object _lock = new ();
        protected readonly IDataLoader DataLoader = new AggregatedDataLoader();



        public override void Initialize()
        {
            if (File.Exists(BullJsonPath))
            {
                var jsonList = File.ReadAllText(BullJsonPath);
                BullStrategies = JsonConvert.DeserializeObject<List<DeltaCollarStrategyLeverage>>(jsonList)
                    .GroupBy(s => s.Underlying.CoinPair)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }

            if (File.Exists(BearJsonPath))
            {
                var jsonList = File.ReadAllText(BearJsonPath);
                BearStrategies = JsonConvert.DeserializeObject<List<DeltaCollarStrategyBear>>(jsonList)
                    .GroupBy(s => s.Underlying.CoinPair)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
            CoinPairs = BullStrategies.Keys.Concat(BearStrategies.Keys).Distinct().ToList();
            
            foreach (var coinPair in CoinPairs)
            {
                AddCoinPair(coinPair);
            }

            SetTimeZone(TimeZones.Shanghai);

            switch (WarmUpResulotion)
            {
                case Resolution.Daily:
                    SetWarmUp(TimeSpan.FromDays(WarmUpPeriods), Resolution.Daily);
                    break;
                case Resolution.Minute:
                    SetWarmUp(TimeSpan.FromMinutes(WarmUpPeriods), WarmUpResulotion);
                    break;
                case Resolution.Second:
                    SetWarmUp(TimeSpan.FromMinutes(WarmUpPeriods/60), WarmUpResulotion);
                    break;
                default:
                    throw new ArgumentException($"Warm up resolution {WarmUpResulotion} invalid!!!");
            }
        }

        protected virtual void AddCoinPair(string coinPair)
        {
            Symbol cryptoSymbol = null;
            Symbol futuresSymbol = null;
            var symbolStrSpot = coinPair + ".sptbin";
            if (SymbolCache.TryGetSymbol(symbolStrSpot, out cryptoSymbol))
            {
                AddCrypto(symbolStrSpot, Resolution.Tick, TradeMarket);
            }

            var symbolStrPerp = coinPair + ".futbin";
            if (SymbolCache.TryGetSymbol(symbolStrPerp, out futuresSymbol))
            {
                AddPerpetual(symbolStrPerp, Resolution.Tick, TradeMarket);
            }
            
            _underlyings[coinPair] = new Underlying(coinPair, cryptoSymbol is not null, futuresSymbol is not null);
            _isFirstRuns[coinPair] = true;
            _underlyingPrices[coinPair] = 0;
            _priceDict[coinPair] = new List<double>();
            _symbols.Add(coinPair, new Tuple<Symbol, Symbol>(cryptoSymbol, futuresSymbol));
        }

        public override void OnWarmupFinished()
        {
            //if (!CheckPositions())
            {
                //throw new Exception($"Position mismatch!!!");
            }
            base.OnWarmupFinished();
            foreach (var pair in _priceDict)
            {
                if (!pair.Value.Any())
                {
                    Notify.MomDingDing($"{AccountName}: No historical prices for {pair.Key}!",
                        ImportantMessageKey, ImportantMessageToken);
                }

                _volEstimators.Add(pair.Key, new EWMAVolatilityEstimator(pair.Value, Lambda));
            }

            switch (WarmUpResulotion)
            {
                case Resolution.Daily:
                    Schedule.On(DateRules.EveryDay(), TimeRules.At(0, 0, 0), UpdateVolatility);
                    _volMultiplier = 365;
                    break;
                case Resolution.Minute:
                case Resolution.Second:
                    Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromMinutes(1)), UpdateVolatility);
                    _volMultiplier = 525600;
                    break;
                default:
                    throw new ArgumentException($"Warm up resolution {WarmUpResulotion} invalid!!!");
            }

            Schedule.On(DateRules.EveryDay(), TimeRules.Every(new TimeSpan(1,0,0)), SendRunningNotifications);
            Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromMinutes(2)),
                SaveJsonAndSendImportantMessages);
            InitTwap(MaxOpenOrderSeconds, MaxOrdersPerHalfSecond);
            if (!string.IsNullOrEmpty(AssetAllocationConfigFile))
            {
                var configs = DataLoader.FetchData<BinanceLiveAssetAllocation>(AssetAllocationConfigFile).ToList();
                BullAssetAllocationConfigMap =
                    configs.Where(c => c.IsBull).ToDictionary(x => x.Ticker.ToUpper(), x => x);
                foreach (var coinPair in BullStrategies.Keys.Except(BullAssetAllocationConfigMap.Keys))
                {
                    var targetVolume = BullStrategies[coinPair].Select(s => s.BalanceNow).Sum();
                    Notify.MomDingDing($"{AccountName}: missing {coinPair} bull strategy in UI config, " +
                                       $"current target volume is {Math.Round(targetVolume, 4)}, " +
                                       $"current target asset is {Math.Round(targetVolume * _underlyingPrices[coinPair])}USD",
                        ImportantMessageKey, ImportantMessageToken);
                }

                BearAssetAllocationConfigMap =
                    configs.Where(c => !c.IsBull).ToDictionary(x => x.Ticker.ToUpper(), x => x);
                foreach (var coinPair in BearStrategies.Keys.Except(BearAssetAllocationConfigMap.Keys))
                {
                    var targetVolume = BullStrategies[coinPair].Select(s => s.BalanceNow).Sum();
                    Notify.MomDingDing($"{AccountName}: missing {coinPair} bear strategy in UI config, " + 
                                       $"current target volume is {Math.Round(targetVolume, 4)}, " + 
                                       $"current target asset is {Math.Round(targetVolume * _underlyingPrices[coinPair])}USD",
                        ImportantMessageKey, ImportantMessageToken);
                }
                Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromSeconds(30)), UpdateTargetPositions);
            }
        }

        protected virtual void UpdateTargetPositions()
        {
            try
            {
                lock (_lock)
                {
                    var configs = DataLoader.FetchData<BinanceLiveAssetAllocation>(AssetAllocationConfigFile).ToList();
                    if (configs.Count == 0)
                    {
                        return;
                    }
                    var bullAssetAllocationConfigMap =
                        configs.Where(c => c.IsBull).ToDictionary(x => x.Ticker, x => x);
                    var bearAssetAllocationConfigMap =
                        configs.Where(c => !c.IsBull).ToDictionary(x => x.Ticker, x => x);
                    var bullRemovedCoins =
                        BullAssetAllocationConfigMap.Keys.Where(k => !bullAssetAllocationConfigMap.ContainsKey(k));
                    foreach (var key in bullRemovedCoins)
                    {
                        BullAssetAllocationConfigMap[key].SpotAsset = 0;
                        BullAssetAllocationConfigMap[key].LeverageAsset = 0;
                        BullAssetAllocationConfigMap[key].FuturesAsset = 0;
                        if (BullStrategies.TryGetValue(key, out var bullStrategy))
                        {
                            foreach (var strategy in bullStrategy)
                            {
                                strategy.BalanceNow = 0;
                            }
                        }

                        var holdings = 0m;
                        if (!_symbols.ContainsKey(key))
                        {
                            continue;
                        }
                        if (_symbols[key].Item1 is not null)
                        {
                            holdings += Securities[_symbols[key].Item1].Holdings.AbsoluteHoldingsValue;
                        }
                        if (_symbols[key].Item2 is not null)
                        {
                            holdings += Securities[_symbols[key].Item2].Holdings.AbsoluteHoldingsValue;
                        }

                        if (holdings > 20)
                        {
                            Notify.MomDingDing($"{AccountName}: {key} bull strategy removed from UI config",
                                RegularMessageKey, RegularMessageToken);
                        }
                    }

                    var bearRemovedCoins =
                        BearAssetAllocationConfigMap.Keys.Where(k => !bearAssetAllocationConfigMap.ContainsKey(k));
                    foreach (var key in bearRemovedCoins)
                    {
                        BearAssetAllocationConfigMap[key].SpotAsset = 0;
                        BearAssetAllocationConfigMap[key].LeverageAsset = 0;
                        BearAssetAllocationConfigMap[key].FuturesAsset = 0;
                        if (BearStrategies.TryGetValue(key, out var bearStrategy))
                        {
                            foreach (var strategy in BearStrategies[key])
                            {
                                strategy.BalanceNow = 0;
                            }
                        }
                        var holdings = 0m;
                        if (_symbols.ContainsKey(key))
                        {
                            if (_symbols[key].Item1 is not null)
                            {
                                holdings += Securities[_symbols[key].Item1].Holdings.AbsoluteHoldingsValue;
                            }
                            if (_symbols[key].Item2 is not null)
                            {
                                holdings += Securities[_symbols[key].Item2].Holdings.AbsoluteHoldingsValue;
                            }
                        }

                        if (holdings > 20)
                        {
                            Notify.MomDingDing($"{AccountName}: {key} bear strategy removed from UI config",
                                RegularMessageKey, RegularMessageToken);
                        }
                    }

                    foreach (var key in bullAssetAllocationConfigMap.Keys)
                    {
                        if (!BullAssetAllocationConfigMap.Keys.Contains(key) || !BullStrategies.ContainsKey(key))
                        {
                            if (bullAssetAllocationConfigMap[key].Asset == 0) continue;
                            BullAssetAllocationConfigMap[key] = bullAssetAllocationConfigMap[key];
                            BullResetCoins.Add(key);
                            if (CoinPairs.Contains(key))
                            {
                                _isFirstRuns[key] = true;
                            }
                            else
                            {
                                CoinPairs.Add(key);
                                AddCoinPair(key);
                                Notify.MomDingDing($"{AccountName}: Add new coin {_underlyings[key]} subscription",
                                    RegularMessageKey, RegularMessageToken);
                            }

                            continue;
                        }

                        if (BullAssetAllocationConfigMap[key].Asset != bullAssetAllocationConfigMap[key].Asset)
                        {
                            var symbol = _underlyings[key].HasCrypto ? _symbols[key].Item1 : _symbols[key].Item2;
                            var price = Securities[symbol].Price;
                            var balanceNow = bullAssetAllocationConfigMap[key].Asset / 10 / price;
                            foreach (var strategy in BullStrategies[key])
                            {
                                strategy.BalanceNow = balanceNow;
                            }

                            Notify.MomDingDing(
                                $"{AccountName}: {key} bull strategies target asset update to {bullAssetAllocationConfigMap[key].Asset}, balance now updated to {BullStrategies[key].Select(s => s.BalanceNow).Sum()}",
                                RegularMessageKey, RegularMessageToken);
                        }

                        if (BullAssetAllocationConfigMap[key].Asset == 0 &&
                            bullAssetAllocationConfigMap[key].Asset != 0 || BullAssetAllocationConfigMap[key].T2M !=
                            bullAssetAllocationConfigMap[key].T2M || (bullAssetAllocationConfigMap[key].SignalTime !=
                            BullAssetAllocationConfigMap[key].SignalTime && (UtcTime - bullAssetAllocationConfigMap[key].SignalTime).TotalMinutes < 5))
                        {
                            var symbol = _underlyings[key].HasCrypto ? _symbols[key].Item1 : _symbols[key].Item2;
                            foreach (var strategy in BullStrategies[key])
                            {
                                strategy.ExpiryDateNow =
                                    Time.AddHours(strategy.Index * bullAssetAllocationConfigMap[key].T2M / 10);
                                strategy.T2M = bullAssetAllocationConfigMap[key].T2M;
                                strategy.InitialT2M = strategy.T2M;
                                strategy.T2MUpdateRatio = bullAssetAllocationConfigMap[key].T2MUpdateRatio;
                                strategy.FirstCrossLimitPrice = false;
                                if (bullAssetAllocationConfigMap[key].UpdateStrikes)
                                {
                                    strategy.OldStrike = Securities[symbol].Price *
                                                         (bullAssetAllocationConfigMap[key].InitialStrikeRatio +
                                                          (10 - strategy.Index) *
                                                          bullAssetAllocationConfigMap[key].StrikeSpread);
                                    strategy.LimitPrice = bullAssetAllocationConfigMap[key].LimitPriceRatio *
                                                          strategy.OldStrike;
                                    strategy.ExtremePrice = Securities[symbol].Price;
                                    strategy.Options[0].Strike = strategy.OldStrike;
                                }
                            }

                            var orderedBulls = BullStrategies[key].OrderBy(b => b.Options.First().Strike).ToList();
                            Notify.MomDingDing(
                                $"{AccountName}: Restart bull strategies on coin {key}, T2M = {bullAssetAllocationConfigMap[key].T2M}, T2MUpdateRatio = {bullAssetAllocationConfigMap[key].T2MUpdateRatio}, irstCrossLimitPrice is false" +
                                $"UpdateStrikes = {bullAssetAllocationConfigMap[key].UpdateStrikes}, " +
                                $"LimitPriceRatio is {bullAssetAllocationConfigMap[key].LimitPriceRatio}, LastSignalTime is {bullAssetAllocationConfigMap[key].SignalTime}, " + 
                                $"\nbull delta: {Math.Round(orderedBulls.Select(s => s.CashDelta).Sum() / 10, 2)}, {Math.Round(orderedBulls.Select(s => s.CashDelta * s.BalanceNow).Sum() * Securities[symbol].Price, 4)}USD, " +
                                $"\nbull strikes-T2Ms-Periods-extremePrices-limitPrices: {string.Join(", ", orderedBulls.Select(s => $"{Math.Round(s.Options[0].Strike, 4)} - {Math.Round((s.ExpiryDateNow.ToUniversalTime() - UtcTime).TotalHours, 2)} hours - {s.T2M} hours - {Math.Round(s.ExtremePrice, 8)} - {Math.Round(s.LimitPrice, 8)}"))}",
                                RegularMessageKey, RegularMessageToken);
                        }

                        if (bullAssetAllocationConfigMap[key].T2MUpdateRatio !=
                            BullAssetAllocationConfigMap[key].T2MUpdateRatio)
                        {
                            foreach (var strategy in BullStrategies[key])
                            {
                                strategy.T2MUpdateRatio = bullAssetAllocationConfigMap[key].T2MUpdateRatio;
                            }

                            Notify.MomDingDing(
                                $"{AccountName}: {key} bull strategies T2MUpdateRatio update to {BullAssetAllocationConfigMap[key]}",
                                RegularMessageKey, RegularMessageToken);
                        }

                        BullAssetAllocationConfigMap[key] = bullAssetAllocationConfigMap[key];
                    }

                    foreach (var key in bearAssetAllocationConfigMap.Keys)
                    {
                        if (bearAssetAllocationConfigMap[key].SpotAsset != 0)
                        {
                            Notify.MomDingDing($"{AccountName}: cannot short spot for {key}!!!", ImportantMessageKey,
                                ImportantMessageToken);
                        }
                        if (!BearAssetAllocationConfigMap.Keys.Contains(key) || !BearStrategies.ContainsKey(key))
                        {
                            if (bearAssetAllocationConfigMap[key].Asset == 0) continue;
                            BearAssetAllocationConfigMap[key] = bearAssetAllocationConfigMap[key];
                            _bearResetCoins.Add(key);
                            if (CoinPairs.Contains(key))
                            {
                                _isFirstRuns[key] = true;
                            }
                            else
                            {
                                CoinPairs.Add(key);
                                AddCoinPair(key);
                                Notify.MomDingDing($"{AccountName}: Add new coin {_underlyings[key]} subscription",
                                    RegularMessageKey, RegularMessageToken);
                            }

                            continue;
                        }

                        if (BearAssetAllocationConfigMap[key].Asset != bearAssetAllocationConfigMap[key].Asset)
                        {
                            var symbol = _underlyings[key].HasCrypto ? _symbols[key].Item1 : _symbols[key].Item2;
                            var price = Securities[symbol].Price;
                            var balanceNow = bearAssetAllocationConfigMap[key].Asset / 10 / price;
                            foreach (var strategy in BearStrategies[key])
                            {
                                strategy.BalanceNow = balanceNow;
                            }

                            Notify.MomDingDing(
                                $"{AccountName}: {key} bear strategy target asset update to {bearAssetAllocationConfigMap[key].Asset}, balance now updated to {BearStrategies[key].Select(s => s.BalanceNow).Sum()}",
                                RegularMessageKey, RegularMessageToken);
                        }

                        if (BearAssetAllocationConfigMap[key].Asset == 0 &&
                            bearAssetAllocationConfigMap[key].Asset != 0 || BearAssetAllocationConfigMap[key].T2M !=
                            bearAssetAllocationConfigMap[key].T2M || (bearAssetAllocationConfigMap[key].SignalTime !=
                            BearAssetAllocationConfigMap[key].SignalTime && (UtcTime - bearAssetAllocationConfigMap[key].SignalTime).TotalMinutes < 5))
                        {
                            var symbol = _underlyings[key].HasCrypto ? _symbols[key].Item1 : _symbols[key].Item2;
                            foreach (var strategy in BearStrategies[key])
                            {
                                strategy.ExpiryDateNow =
                                    Time.AddHours(strategy.Index * bearAssetAllocationConfigMap[key].T2M / 10);
                                strategy.T2M = bearAssetAllocationConfigMap[key].T2M;
                                strategy.InitialT2M = strategy.T2M;
                                strategy.T2MUpdateRatio = bearAssetAllocationConfigMap[key].T2MUpdateRatio;
                                strategy.FirstCrossLimitPrice = false;
                                if (bearAssetAllocationConfigMap[key].UpdateStrikes)
                                {
                                    strategy.OldStrike = Securities[symbol].Price *
                                                         (bearAssetAllocationConfigMap[key].InitialStrikeRatio -
                                                          (10 - strategy.Index) * bearAssetAllocationConfigMap[key]
                                                              .StrikeSpread);
                                    strategy.LimitPrice = bearAssetAllocationConfigMap[key].LimitPriceRatio *
                                                          strategy.OldStrike;
                                    strategy.ExtremePrice = Securities[symbol].Price;
                                    strategy.Options[0].Strike = strategy.OldStrike;
                                }
                            }

                            var orderedBears = BearStrategies[key].OrderByDescending(b => b.Options.First().Strike).ToList();
                            Notify.MomDingDing(
                                $"{AccountName}: Restart bear strategies on coin {key}, T2M = {bearAssetAllocationConfigMap[key].T2M}, T2MUpdateRatio = {bearAssetAllocationConfigMap[key].T2MUpdateRatio}, FirstCrossLimitPrice is false, " +
                                $"UpdateStrikes = {bearAssetAllocationConfigMap[key].UpdateStrikes}" +
                                $"LimitPriceRatio = {bearAssetAllocationConfigMap[key].LimitPriceRatio}, LastSignalTime is {bearAssetAllocationConfigMap[key].SignalTime}" +
                                $"\nbear delta: {Math.Round(orderedBears.Select(s => s.CashDelta).Sum() / 10, 2)}, {Math.Round(orderedBears.Select(s => s.CashDelta * s.BalanceNow).Sum() * Securities[symbol].Price, 4)}USD, " +
                                $"\nbear strikes-T2Ms-Periods-extremePrices-limitPrices: {string.Join(", ", orderedBears.Select(s => $"{Math.Round(s.Options[0].Strike, 4)} - {Math.Round((s.ExpiryDateNow.ToUniversalTime() - UtcTime).TotalHours, 2)} hours - {s.T2M} hours - {Math.Round(s.ExtremePrice, 8)} - {Math.Round(s.LimitPrice, 8)}"))}",
                                RegularMessageKey, RegularMessageToken);
                        }

                        if (bearAssetAllocationConfigMap[key].T2MUpdateRatio !=
                            BearAssetAllocationConfigMap[key].T2MUpdateRatio)
                        {
                            foreach (var strategy in BearStrategies[key])
                            {
                                strategy.T2MUpdateRatio = bearAssetAllocationConfigMap[key].T2MUpdateRatio;
                            }

                            Notify.MomDingDing(
                                $"{AccountName}: {key} bear strategies T2MUpdateRatio update to {bearAssetAllocationConfigMap[key]}",
                                RegularMessageKey, RegularMessageToken);
                        }

                        BearAssetAllocationConfigMap[key] = bearAssetAllocationConfigMap[key];
                    }
                }
            }
            catch (Exception e)
            {
                Log($"{e}");
                Notify.MomDingDing(
                    $"{AccountName}: Error in updating target position by csv, message: {e.Message}, stack: {e.StackTrace}",
                    ImportantMessageKey, ImportantMessageToken);
            }
        }

        private decimal GetUnderlyingPrice(string coin)
        {
            var symbol = _underlyings[coin].HasCrypto ? _symbols[coin].Item1 : _symbols[coin].Item2;
            return Securities[symbol].Price;
        }

        private bool CheckPositions()
        {
            try
            {
                while (Transactions.GetOpenOrders().Any())
                {
                    System.Threading.Thread.Sleep(50);
                }

                var spotBalances = new BinanceAccountHelper(GetBrokerageSpotAccount("BUSD", true).CustomData)
                    .GetSpotBalances().ToDictionary(x => x.Asset, x => x.Available);
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

                var futurePositions = new BinanceAccountHelper(GetBrokerageFuturesAccount("BUSD", true).CustomData)
                    .FuturesPositions().ToDictionary(p => p.Symbol, p => p.Quantity);
                var message = futurePositions
                    .Where(position => (position.Value != 0 || Securities.Where(p => p.Key.SecurityType == SecurityType.Future).ToDictionary(x => x.Key.Value, x => x.Value).ContainsKey(position.Key)) &&
                                       position.Value != Securities.Where(p => p.Key.SecurityType == SecurityType.Future).ToDictionary(x => x.Key.Value, x => x.Value)[position.Key].Holdings.Quantity).Aggregate(
                        string.Empty,
                        (current, position) =>
                            current +
                            $"{position.Key}, qc {Securities.Where(p => p.Key.SecurityType == SecurityType.Future).ToDictionary(x => x.Key.Value, x => x.Value)[position.Key].Holdings.Quantity}, actual {position.Value}\n");
                message += spotBalances
                    .Where(account => account.Key != "BUSD" && account.Key != "USDT" && account.Key != "BNB" &&
                                      (account.Value != 0 || Portfolio.CashBook.ContainsKey(account.Key)) &&
                                      account.Value != Portfolio.CashBook[account.Key].Amount).Aggregate(string.Empty,
                        (current, account) =>
                            current +
                            $"{account.Key}, qc {Portfolio.CashBook[account.Key].Amount}, actual {account.Value}\n");
                if (!string.IsNullOrEmpty(message))
                {
                    Notify.MomDingDing(message, ImportantMessageKey, ImportantMessageToken);
                    return false;
                }
            }
            catch (Exception e)
            {
                Notify.MomDingDing($"{AccountName}: Error checking position, e {e.Message}, stack trace {e.StackTrace}",
                    ImportantMessageKey, ImportantMessageToken);
            }

            return true;
        }

        public override void OnData(Slice slice)
        {
            if (!TradingIsReady())
            {
                return;
            }

            var ticksByCoin = slice.Ticks.GroupBy(t => t.Key.ID.Symbol);
            if (IsWarmingUp)
            {
                foreach (var group in ticksByCoin)
                {
                    if (CoinPairs.Contains(group.Key))
                    {
                        InitializePriceList(group);
                    }
                }

                return;
            }
            
            foreach (var openOrder in Transactions.GetOpenOrders())
            {
                if ((UtcTime - openOrder.CreatedTime).TotalSeconds > MaxOpenOrderSeconds)
                {
                    Transactions.CancelOrder(openOrder.Id);
                }
                if ((UtcTime - openOrder.CreatedTime).TotalMinutes > 1)
                {
                    Notify.MomDingDing(
                        $"{AccountName}: order {openOrder.Id} hanging over 1 minute, symbol: {openOrder.Symbol}, quantity: {openOrder.Quantity}, price: {openOrder.Price}, type: {openOrder.Type}, status: {openOrder.Status}, time: {openOrder.CreatedTime}",
                        RegularMessageKey, RegularMessageToken);
                }
            }

            var spotBalances = new BinanceAccountHelper(GetBrokerageSpotAccount("BUSD", true).CustomData)
                .GetSpotBalances().ToDictionary(x => x.Asset, x => x.Available);
            foreach (var group in ticksByCoin)
            {
                lock (_lock)
                {
                    if (!CoinPairs.Contains(group.Key))
                    {
                        continue;
                    }
                    var tradeSymbol = _underlyings[group.Key].HasCrypto ? _symbols[group.Key].Item1 : _symbols[group.Key].Item2;
                    if (!slice.Ticks.ContainsKey(tradeSymbol))
                    {
                        continue;
                    }
                }
                TradeCoinPair(group.Key, slice, spotBalances);
            }
            
            foreach (var kvp in _printOutPrices)
            {
                MonitorPriceChange(kvp.Key, kvp.Value);
            }
        }

        protected void TradeCoinPair(string coinPair, Slice slice, Dictionary<string, decimal> spotBalances)
        {
            lock (_lock)
            {
                if (CoinPairs.Contains(coinPair))
                {
                    var tradeSymbol = _underlyings[coinPair].HasCrypto ? _symbols[coinPair].Item1 : _symbols[coinPair].Item2;
                    var price = GetUnderlyingPrice(coinPair);
                    _printOutPrices[coinPair] = price;

                    if (_isFirstRuns[coinPair])
                    {
                        _underlyingPrices[coinPair] = price;
                        _isFirstRuns[coinPair] = false;
                        if (BullResetCoins.Contains(coinPair))
                        {
                            BullResetCoins.Remove(coinPair);
                            if (!_volEstimators.ContainsKey(coinPair))
                            {
                                _volEstimators.Add(coinPair,
                                    new EWMAVolatilityEstimator((double)price,
                                        1.96 / _volMultiplier, Lambda));
                            }

                            BullStrategies[coinPair] = new List<DeltaCollarStrategyLeverage>();
                            var balanceNow = BullAssetAllocationConfigMap[coinPair].Asset / price /
                                                10;
                            for (var i = 1; i <= 10; i++)
                            {
                                var strategy = new DeltaCollarStrategyLeverage(_underlyings[coinPair]);
                                strategy.T2M = BullAssetAllocationConfigMap[coinPair].T2M;
                                strategy.InitialT2M = strategy.T2M;
                                strategy.BalanceNow = balanceNow;
                                strategy.HasOption = true;
                                strategy.OldStrike = price *
                                                        (BullAssetAllocationConfigMap[coinPair].InitialStrikeRatio +
                                                        (10 - i) * BullAssetAllocationConfigMap[coinPair].StrikeSpread);
                                strategy.LimitPrice = BullAssetAllocationConfigMap[coinPair].LimitPriceRatio *
                                                        strategy.OldStrike;
                                strategy.ExtremePrice = price;
                                strategy.Options = new List<OptionInPortfolio>
                                {
                                    new OptionInPortfolio("put", strategy.OldStrike, 1, false, false)
                                };
                                strategy.T2MUpdateRatio = BullAssetAllocationConfigMap[coinPair].T2MUpdateRatio;
                                strategy.Index = i;
                                strategy.ExpiryDateNow =
                                    Time.AddHours(BullAssetAllocationConfigMap[coinPair].T2M / 10 * i);
                                strategy.TargetUnderlyingVolume = 1;
                                BullStrategies[coinPair].Add(strategy);
                            }

                            Notify.MomDingDing($"{AccountName}: Add bull strategies for coin {coinPair}, target asset {BullAssetAllocationConfigMap[coinPair].Asset}",
                                RegularMessageKey, RegularMessageToken);
                        }

                        if (price == 0)
                        {
                            Notify.MomDingDing($"{coinPair} underlying price is 0 !!!", ImportantMessageKey, ImportantMessageToken);
                            return;
                        }

                        if (_bearResetCoins.Contains(coinPair))
                        {
                            _bearResetCoins.Remove(coinPair);
                            if (!_volEstimators.ContainsKey(coinPair))
                            {
                                _volEstimators.Add(coinPair,
                                    new EWMAVolatilityEstimator((double) price,
                                        1.96 / _volMultiplier, Lambda));
                            }

                            BearStrategies[coinPair] = new List<DeltaCollarStrategyBear>();
                            var balanceNow = BearAssetAllocationConfigMap[coinPair].Asset / price /
                                                10;
                            for (var i = 1; i <= 10; i++)
                            {
                                var strategy = new DeltaCollarStrategyBear(_underlyings[coinPair]);
                                strategy.T2M = BearAssetAllocationConfigMap[coinPair].T2M;
                                strategy.InitialT2M = strategy.T2M;
                                strategy.BalanceNow = balanceNow;
                                strategy.HasOption = true;
                                strategy.OldStrike = price *
                                                        (BearAssetAllocationConfigMap[coinPair].InitialStrikeRatio -
                                                        (10 - i) * BearAssetAllocationConfigMap[coinPair].StrikeSpread);
                                strategy.LimitPrice = BearAssetAllocationConfigMap[coinPair].LimitPriceRatio *
                                                        strategy.OldStrike;
                                strategy.ExtremePrice = price;
                                strategy.Options = new List<OptionInPortfolio>
                                {
                                    new OptionInPortfolio("put", strategy.OldStrike, 1, false, false)
                                };
                                strategy.T2MUpdateRatio = BearAssetAllocationConfigMap[coinPair].T2MUpdateRatio;
                                strategy.Index = i;
                                strategy.ExpiryDateNow =
                                    Time.AddHours(BearAssetAllocationConfigMap[coinPair].T2M / 10 * i);
                                BearStrategies[coinPair].Add(strategy);
                            }

                            Notify.MomDingDing($"{AccountName}: Add bear strategies for coin {coinPair}, target asset {BearAssetAllocationConfigMap[coinPair].Asset}",
                                RegularMessageKey, RegularMessageToken);
                        }
                    }

                    var totalBalance = 0m;
                    var totalTargetDelta = 0m;
                    var paramMessage = "";
                    var markPriceSecurity = _symbols[coinPair].Item1 is not null
                        ? Securities[_symbols[coinPair].Item1]
                        : Securities[_symbols[coinPair].Item2];
                    if (BullStrategies.TryGetValue(coinPair, out var bullStrategies))
                    {
                        var bullMoveStrike = false;
                        var updateStrikeRatio = 1m;
                        paramMessage += "\nbull parameters: ";
                        if (!BullAssetAllocationConfigMap.TryGetValue(coinPair, out var bullConfig) &&
                            BullStrategies[coinPair].Select(s => s.BalanceNow).Sum() != 0)
                        {
                            Notify.MomDingDing(
                                $"{AccountName}: CANNOT find config for {coinPair} bull strategy!!!",
                                ImportantMessageKey, ImportantMessageToken);
                        }
                        else
                        {
                            updateStrikeRatio = bullConfig.UpdateStrikeRatio;
                            paramMessage +=
                                $"initial strike ratio: {bullConfig.InitialStrikeRatio}, strike spread: {bullConfig.StrikeSpread}, ";
                        }

                        paramMessage += $"update strike ratio: {updateStrikeRatio}, move strike: {bullMoveStrike}";

                        foreach (var strategy in bullStrategies)
                        {
                            var strategyMessages = strategy.DetectionStrategy(slice.Time, price,
                                Math.Sqrt(_volEstimators[coinPair].CurrentVolatility * _volMultiplier), markPriceSecurity.Cache.MarkPrice, - 1, true,
                                bullMoveStrike, updateStrikeRatio);
                            if (strategyMessages.Any())
                            {
                                Notify.MomDingDing($"{AccountName}: " + string.Join("\n", strategyMessages),
                                    RegularMessageKey, RegularMessageToken);
                            }

                            totalBalance += strategy.BalanceNow;
                            totalTargetDelta +=
                                strategy.CashDelta * strategy.BalanceNow;
                        }
                    }

                    if (BearStrategies.TryGetValue(coinPair, out var bearStrategies))
                    {
                        var bearMoveStrike = false;
                        var updateStrikeRatio = 1m;
                        paramMessage += "\nbear parameters: ";
                        if (!BearAssetAllocationConfigMap.TryGetValue(coinPair, out var bearConfig) &&
                            BearStrategies[coinPair].Select(s => s.BalanceNow).Sum() != 0)
                        {
                            Notify.MomDingDing(
                                $"{AccountName}: CANNOT find config for {coinPair} bear strategy!!!",
                                ImportantMessageKey, ImportantMessageToken);
                        }
                        else
                        {
                            updateStrikeRatio = bearConfig.UpdateStrikeRatio;
                            paramMessage +=
                                $"initial strike ratio: {bearConfig.InitialStrikeRatio}, strike spread: {bearConfig.StrikeSpread}, ";
                        }

                        paramMessage += $"update strike ratio: {updateStrikeRatio}, move strike: {bearMoveStrike}";

                        var rebalanceNow = BearAssetAllocationConfigMap[coinPair].Asset / 10 / price;
                        foreach (var strategy in bearStrategies)
                        {
                            var strategyMessages = strategy.DetectionStrategy(slice.Time, price,
                                Math.Sqrt(_volEstimators[coinPair].CurrentVolatility * _volMultiplier), markPriceSecurity.Cache.MarkPrice, rebalanceNow,
                                true, bearMoveStrike, updateStrikeRatio);
                            if (strategyMessages.Any())
                            {
                                Notify.MomDingDing($"{AccountName}: " + string.Join("\n", strategyMessages),
                                    RegularMessageKey, RegularMessageToken);
                            }

                            totalBalance += strategy.BalanceNow;
                            totalTargetDelta +=
                                strategy.CashDelta * strategy.BalanceNow;
                            strategy.NeedHedge();
                        }
                    }
                    
                    if (TryTradeDelta(totalTargetDelta, coinPair, spotBalances[Securities[tradeSymbol].QuoteCurrency.Symbol], price, out var message))
                    {
                        var averageDelta = totalBalance != 0 ? totalTargetDelta / totalBalance : 0;
                        message += $", average delta is {Math.Round(averageDelta, 4)}, total quantity balance now is {Math.Round(totalBalance, 4)}, price is {price}";//, target asset is {_bullAssetAllocationConfigMap[coinPair].Asset}USD";
                        if (message.Contains("place"))
                        {
                            message += paramMessage;
                            if (BullStrategies.TryGetValue(coinPair, out var bulls))
                            {
                                var orderedBulls = bulls.OrderBy(b => b.Options.First().Strike).ToList();
                                message +=
                                    $"\nbull delta: {Math.Round(orderedBulls.Select(s => s.CashDelta).Sum() / 10, 2)}, {Math.Round(orderedBulls.Select(s => s.CashDelta * s.BalanceNow).Sum() * price, 4)}USD, ";
                                message +=
                                    $"\nbull strikes-T2Ms-Periods-extremePrices-limitPrices-cross: {string.Join(", ", orderedBulls.Select(s => $"{Math.Round(s.Options[0].Strike, 4)} - {Math.Round((s.ExpiryDateNow.ToUniversalTime() - UtcTime).TotalHours, 2)} hours - {s.T2M} hours - {Math.Round(s.ExtremePrice, 8)} - {Math.Round(s.LimitPrice, 8)} - {s.FirstCrossLimitPrice}"))}";
                            }

                            if (BearStrategies.TryGetValue(coinPair, out var bears))
                            {
                                var orderedBears = bears.OrderByDescending(b => b.Options.First().Strike).ToList();
                                message +=
                                    $"\nbear delta: {Math.Round(orderedBears.Select(s => s.CashDelta).Sum() / 10, 2)}, {Math.Round(orderedBears.Select(s => s.CashDelta * s.BalanceNow).Sum() * price, 4)}USD, ";
                                message +=
                                    $"\nbear strikes-T2Ms-Periods-extremePrices-limitPrices-cross: {string.Join(", ", orderedBears.Select(s => $"{Math.Round(s.Options[0].Strike, 4)} - {Math.Round((s.ExpiryDateNow.ToUniversalTime() - UtcTime).TotalHours, 2)} hours - {s.T2M} hours - {Math.Round(s.ExtremePrice, 8)} - {Math.Round(s.LimitPrice, 8)} - {s.FirstCrossLimitPrice}"))}";
                            }
                        }

                        Notify.MomDingDing(message, RegularMessageKey, RegularMessageToken);
                    }
                }
            }
        }

        private bool TryTradeDelta(decimal targetDelta, string coinPair, decimal spotCashBalance, decimal price, out string response)
        {
            var minNotional = 0m;
            var sumCashDelta = (BullStrategies.TryGetValue(coinPair, out var bS) ? bS.Sum(s => s.CashDelta) : 0) +  (BearStrategies.TryGetValue(coinPair, out var beS) ? beS.Sum(s => s.CashDelta) : 0);
            if (sumCashDelta == 0 || sumCashDelta == 10 || sumCashDelta == -10)
            {
                minNotional = 0;
            }
            else
            {
                minNotional = 0.1m * Math.Max(BullStrategies.TryGetValue(coinPair, out var bs) ? bs.Sum(s => s.BalanceNow * s.UnderlyingPriceNow) : 0, BearStrategies.TryGetValue(coinPair, out var bes) ? bes.Sum(s => s.BalanceNow * s.UnderlyingPriceNow) : 0);
            }
            response = string.Empty;
            if (_underlyings[coinPair].HasCrypto && (Transactions.GetOpenOrders(_symbols[coinPair].Item1).Any() || IsTwapTrading(_symbols[coinPair].Item1)) ||
                _underlyings[coinPair].HasFutures && (Transactions.GetOpenOrders((_symbols[coinPair].Item2)).Any() || IsTwapTrading(_symbols[coinPair].Item2)))
            {
                return false;
            }
            if (_underlyings[coinPair].HasCrypto && !_underlyings[coinPair].HasFutures)
            {
                var security = Securities[_symbols[coinPair].Item1];
                var tradeVolume = targetDelta - security.Holdings.Quantity;
                if (Math.Abs(tradeVolume * price) < minNotional)
                {
                    return false;
                }
                // if (tradeVolume * price > spotCashBalance)
                // {
                //     tradeVolume = spotCashBalance / price;
                //     Notify.MomDingDing($"{AccountName}: Not enough spot cash balance for {coinPair}!!!",
                //         ImportantMessageKey, ImportantMessageToken);
                //     Notify.VoiceCallSaved(TemplateType.MarginCall, AccountName);
                // }

                if (TryTradeDeltaForSecurity(security, tradeVolume, out response))
                {
                    response =
                        $"{AccountName}: {response}, target spot quantity is {Math.Round(targetDelta, 4)}, hold spot quantity is {Math.Round(security.Holdings.Quantity, 4)}";
                    return true;
                }

                return false;
            }

            if (!_underlyings[coinPair].HasCrypto && _underlyings[coinPair].HasFutures)
            {
                var security = Securities[_symbols[coinPair].Item2];
                var tradeVolume = targetDelta - security.Holdings.Quantity;
                if (Math.Abs(tradeVolume * price) < minNotional)
                {
                    return false;
                }
                if (TryTradeDeltaForSecurity(security, tradeVolume, out response))
                {
                    response =
                        $"{AccountName}: {response}, target perpetual quantity is {Math.Round(targetDelta, 4)}, hold perpetual quantity is {Math.Round(security.Holdings.Quantity, 4)}";
                    return true;
                }

                return false;
            }

            var spot = Securities[_symbols[coinPair].Item1];
            var perpetual = Securities[_symbols[coinPair].Item2];
            var totalVolume = targetDelta - spot.Holdings.Quantity - perpetual.Holdings.Quantity;
            if (Math.Abs(totalVolume * price) < minNotional)
            {
                return false;
            }
            var spotAvailableAsset = -spot.Holdings.HoldingsCost;
            if (BullAssetAllocationConfigMap.TryGetValue(coinPair, out var bullAsset))
            {
                spotAvailableAsset += bullAsset.SpotAsset;
            }

            if (totalVolume * (spot.Holdings.Quantity + perpetual.Holdings.Quantity) >= 0)
            {
                var maxSpotVolume = spotAvailableAsset / price;
                var spotVolume = Math.Max(-spot.Holdings.Quantity, Math.Min(totalVolume, maxSpotVolume));
                if (TryTradeDeltaForSecurity(spot, spotVolume, out var spotMessage))
                {
                    response += spotMessage + "\n";
                }

                if (TryTradeDeltaForSecurity(perpetual, totalVolume - spotVolume,
                        out var perpetualMessage))
                {
                    response += perpetualMessage + "\n";
                }

                if (string.IsNullOrEmpty(response))
                {
                    return false;
                }

                response =
                    $"{AccountName}: {response}target quantity is {Math.Round(targetDelta,4)}, spot quantity is {Math.Round(spot.Holdings.Quantity,4)}, perpetual quantity is {Math.Round(perpetual.Holdings.Quantity, 4)}";
                return true;
            }

            var perpetualVolume = Math.Sign(totalVolume) *
                                  Math.Min(perpetual.Holdings.AbsoluteQuantity, Math.Abs(totalVolume));
            var leftVolume = totalVolume - perpetualVolume;
            var spotV = leftVolume > 0
                ? Math.Min(leftVolume, spotAvailableAsset / price)
                : Math.Max(leftVolume, -spot.Holdings.Quantity);
            if (TryTradeDeltaForSecurity(spot, spotV, out var spotM))
            {
                response += spotM + "\n";
            }

            perpetualVolume += leftVolume - spotV;
            if (TryTradeDeltaForSecurity(perpetual, perpetualVolume, out var perpetualM))
            {
                response += perpetualM + "\n";
            }
            if (string.IsNullOrEmpty(response))
            {
                return false;
            }

            response =
                $"{AccountName}: {response}target quantity is {Math.Round(targetDelta, 4)}, spot quantity is {Math.Round(spot.Holdings.Quantity,4)}, perpetual quantity is {Math.Round(perpetual.Holdings.Quantity,4)}";
            return true;
        }

        private bool TryTradeDeltaForSecurity(Security security, decimal tradeVolume, out string response)
        {
            response = string.Empty;
            tradeVolume = Utils.RoundTradeVolume(tradeVolume, security.SymbolProperties.LotSize);
            if (tradeVolume + security.Holdings.Quantity != 0 && (Math.Abs(tradeVolume) < security.SymbolProperties.LotSize ||
                 Math.Abs((tradeVolume) * security.Price) < security.SymbolProperties.MinNotional))
            {
                return false;
            }
            if (NeedTwap(security, tradeVolume, out var quantityPerOrder))
            {
                if (Twap(security.Symbol, quantityPerOrder, TwapIntervalSeconds, TotalTwapTrades,
                        OrderType.Limit, out response))
                {
                    return true;
                }

                return false;
            }
            if (quantityPerOrder == 0)
            {
                return false;
            }
            MarketOrder(security.Symbol, quantityPerOrder);
            response = $"place market order for {quantityPerOrder} {security.Symbol}";
            return true;
        }

        protected virtual bool NeedTwap(Security security, decimal quantity, out decimal quantityPerOrder)
        {
            quantityPerOrder = Utils.RoundTradeVolume(quantity / TotalTwapTrades, security.SymbolProperties.LotSize);
            if (Math.Abs(quantityPerOrder) < security.SymbolProperties.LotSize ||
                Math.Abs(quantityPerOrder) * security.Price < security.SymbolProperties.MinNotional)
            {
                quantityPerOrder = quantity;
                return false;
            }

            return true;
        }

        private void InitializePriceList(IGrouping<string, KeyValuePair<Symbol, List<Tick>>> ticks)
        {
            var n = ticks.Count();
            if (n > 2)
            {
                throw new Exception($"More than 2 warm up data points for {ticks.Key}!");
            }

            var priceData = n == 2 ? ticks.Single(g => g.Key.SecurityType == SecurityType.Crypto) : ticks.Single();
            _priceDict[ticks.Key].Add((double) priceData.Value.Last().LastPrice);
        }

        private void SendRunningNotifications()
        {
            var message = "";
            foreach (var coin in CoinPairs)
            {
                var symbol = _underlyings[coin].HasCrypto ? _symbols[coin].Item1 : _symbols[coin].Item2;
                var price = Securities[symbol].Price;
                message += $"Alarm: Account {AccountName} is running {coin}, price now is {price}\n";
            }

            Notify.MomDingDing(message, RegularMessageKey, RegularMessageToken);
        }

        private void UpdateVolatility()
        {
            foreach (var pair in _volEstimators)
            {
                var symbol = _underlyings[pair.Key].HasCrypto ? _symbols[pair.Key].Item1 : _symbols[pair.Key].Item2;
                var price = Securities[symbol].Price;
                pair.Value.AddPrice((double) price);
            }
        }

        private void MonitorPriceChange(string coinPair, decimal price)
        {
            if (price / _underlyingPrices[coinPair] > 1.01m)
            {
                _underlyingPrices[coinPair] = price;
                Notify.MomDingDing($"{coinPair} price increase 1%", RegularMessageKey, RegularMessageToken);
            }
        }

        protected virtual void SaveJsonAndSendImportantMessages()
        {
            lock (_lock)
            {
                var removedCoins = new List<string>();
                foreach (var coin in CoinPairs)
                {
                    if (_underlyings[coin].HasCrypto && Securities[_symbols[coin].Item1].Holdings.AbsoluteHoldingsValue > 10 || 
                        _underlyings[coin].HasFutures && Securities[_symbols[coin].Item2].Holdings.Quantity != 0) 
                    {
                        continue;
                    }

                    if (BearStrategies.TryGetValue(coin, out var strategies))
                    {
                        if (strategies.First().BalanceNow != 0) continue;
                        BearStrategies.Remove(coin);
                    }

                    if (BullStrategies.TryGetValue(coin, out var strategies_))
                    {
                        if (strategies_.First().BalanceNow != 0) continue;
                        BullStrategies.Remove(coin);
                    }
                    if (BearAssetAllocationConfigMap.TryGetValue(coin, out var config))
                    {
                        if (config.Asset != 0) continue;
                        BearAssetAllocationConfigMap.Remove(coin);
                    }

                    if (BullAssetAllocationConfigMap.TryGetValue(coin, out config))
                    {
                        if (config.Asset != 0) continue;
                        BullAssetAllocationConfigMap.Remove(coin);
                    }

                    removedCoins.Add(coin);
                }

                foreach (var coin in removedCoins)
                {
                    CoinPairs.Remove(coin);
                    _underlyings.Remove(coin);
                    _symbols.Remove(coin);
                    _volEstimators.Remove(coin);
                    _printOutPrices.Remove(coin);
                }
            }

            var bullStrategies = BullStrategies.SelectMany(s => s.Value).ToList();
            var strategiesJson = JsonConvert.SerializeObject(bullStrategies);
            File.WriteAllText(BullJsonPath, strategiesJson);
            var bearStrategies = BearStrategies.SelectMany(s => s.Value).ToList();
            strategiesJson = JsonConvert.SerializeObject(bearStrategies);
            File.WriteAllText(BearJsonPath, strategiesJson);

            var prices = _printOutPrices.ToList();
            foreach (var price in prices)
            {
                Log($"{AccountName} --- {price.Key} price is {price.Value}");
            }

            if (_actionRequiredMessages.Any())
            {
                var messages = string.Join("\n", _actionRequiredMessages);
                Notify.MomDingDing(messages, ImportantMessageKey, ImportantMessageToken);
                _actionRequiredMessages.Clear();
            }
        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            base.OnOrderEvent(orderEvent);
            if (orderEvent.Status == OrderStatus.Invalid)
            {
                Notify.MomDingDing(
                    $"{AccountName}: ORDER REJECTED!!! order id: {orderEvent.OrderId}, symbol: {orderEvent.Symbol + "-" + orderEvent.Symbol.SecurityType}, message: {orderEvent.Message}",
                    ImportantMessageKey, ImportantMessageToken);
                Notify.VoiceCallSaved(TemplateType.MarginCall, AccountName + " " + orderEvent.Symbol.Value);
            }
        }

        public override void OnProgramExit()
        {
            var bullStrategies = JsonConvert.SerializeObject(BullStrategies);
            File.WriteAllText(BullJsonPath, bullStrategies);
            var bearStrategiesa = JsonConvert.SerializeObject(BearStrategies);
            File.WriteAllText(BearJsonPath, bearStrategiesa);

            Notify.MomDingDing($"{AccountName} EXIT!!!!!!!", "SError", "SExit");
            Notify.VoiceCallSaved(TemplateType.SystemAlert, new[] {AccountName, "Exited"});
        }



        public override void OnRiskDegreeChanged(decimal riskDegree, decimal userRiskDegree)
        {
            if (riskDegree >= 0.25m)
            {
                Notify.MomDingDing($"{AccountName}: MARGIN CALL!!!", ImportantMessageKey, ImportantMessageToken);
                Notify.VoiceCallSaved(TemplateType.MarginCall, AccountName);
            }
        }

        /// <summary>
        /// This is used by the regression test system to indicate if the open source Lean repository has the required data to run this algorithm.
        /// </summary>
        public bool CanRunLocally { get; } = true;

        /// <summary>
        /// This is used by the regression test system to indicate which languages this algorithm is written in.
        /// </summary>
        public Language[] Languages { get; } = {Language.CSharp, Language.Python};

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
