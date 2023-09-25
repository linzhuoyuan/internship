using System;
using System.Collections.Generic;
using System.Linq;
using NodaTime;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Orders;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Securities;
using Org.BouncyCastle.Tsp;

namespace QuantConnect.Algorithm.CSharp
{
    class TestETFChain : QCAlgorithm, IRegressionAlgorithmDefinition
    {
        private const string UnderlyingTicker = "sh510050";

        public readonly Symbol Underlying = QuantConnect.Symbol.Create(
            UnderlyingTicker,
            SecurityType.Equity,
            Market.SSE
        );

        public readonly Symbol OptionSymbol = QuantConnect.Symbol.Create(
            UnderlyingTicker,
            SecurityType.Option,
            Market.SSE
        );

        private readonly object _locker = new object();
        private bool _traded = false;
        private int _volume = -1;
        private DateTime _lasTime = DateTime.Now;
        private bool _placeLimitOrder = true;
        private int _count = 0;
        private readonly Random _rand = new Random();
        private SerilogFileLogHandler _logger;

        private readonly List<Symbol> _optionList = new();
        private FuncSecurityDerivativeFilter _optionFilter;

        private void SetOptionFilter(Func<OptionFilterUniverse, OptionFilterUniverse> universeFunc)
        {
            _optionFilter = new FuncSecurityDerivativeFilter(universe =>
            {
                var optionUniverse = universe as OptionFilterUniverse;
                var result = universeFunc(optionUniverse);
                return result.ApplyOptionTypesFilter();
            });
        }

        public TestETFChain()
        {
            SetTimeZone(TimeZones.Shanghai);
            //SetStartDate(2022, 01, 01);
        }

        public override void Initialize()
        {
            _logger = new SerilogFileLogHandler("test_log.txt");

            //SetEndDate(2021,04,30);

            var equity = AddEquity(
                UnderlyingTicker,
                Resolution.Tick,
                Market.SSE,
                dataNormalizationMode: DataNormalizationMode.Raw,
                fillDataForward: false);
            //AddOptions(equity.Symbol);

            //AddSubscription(UnderlyingTicker);
            //AddEquity(
            //    UnderlyingTicker, 
            //    Resolution.Minute, 
            //    Market.SSE, 
            //    dataNormalizationMode: DataNormalizationMode.Raw,
            //    fillDataForward: false);
            //AddEquity(
            //    "sz159915", 
            //    Resolution.Tick, 
            //    Market.SZSE, 
            //    dataNormalizationMode: DataNormalizationMode.Raw);
            //AddEquity(
            //    "sh510500", 
            //    Resolution.Tick, 
            //    Market.SSE, 
            //    dataNormalizationMode: DataNormalizationMode.Raw);
            //var option = AddOption(UnderlyingTicker, Resolution.Tick, Market.SSE);
            //SetOptionFilter(u => u.Strikes(-1, +1).Expiration(TimeSpan.FromDays(0), TimeSpan.FromDays(100)));
            //// set our strike/expiry filter for this option chain
            //option.SetFilter(
            //    u => u.Strikes(-5, +5)
            //        .Expiration(TimeSpan.FromDays(0), TimeSpan.FromDays(100))
            //);

            // use the underlying equity as the benchmark
            SetBenchmark(equity.Symbol);
            _lasTime = DateTime.Now;

            SetWarmUp(TimeSpan.FromDays(20), Resolution.Minute);
            Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromMinutes(1)), MinutesAction);
        }

        public override void OnWarmupFinished()
        {
            //Transactions.CancelOpenOrders();
            //foreach (var security in Securities.Values)
            //{
            //    var lastDateTime = security.GetLastDataTime();
            //    if (lastDateTime.Date < DateTime.UtcNow.Date)
            //    {

            //    }
            //}
            //AddOptions(Securities[UnderlyingTicker].Symbol);
            SetDateTime(StartDate);
            Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromMinutes(1)), MinutesAction);
        }

        private void MinutesAction(string name, DateTime time)
        {
            var stocksAccount = BrokerageAccountMap["test1-stocks"];
            Log($"{stocksAccount.AccountId}, BuyingPower:{stocksAccount.BuyingPower}, Time:{time}");
            foreach (var security in Securities.Values)
            {
                if (security.Type == SecurityType.Option && security.HoldStock)
                {
                    Log($"{security}, p:{security.Holdings.Price}, hv: {security.Holdings.HoldingsValue}");
                }
            }
        }

        private void AddOptions(Symbol symbol)
        {
            foreach (var option in OptionChainProvider.GetOptionContractList(symbol, DateTime.Today))
            {
                AddOptionContract(option, Resolution.Tick, fillDataForward: false);
            }
        }

        private void UpdateOptionSubscription()
        {
            var underlying = Securities[Underlying];
            var last = underlying.GetLastData();
            if (last == null)
            {
                return;
            }

            if (_optionList.Count == 0)
            {
                var allOptions = OptionChainProvider.GetOptionContractList(Underlying, DateTime.Today);
                var selected = _optionFilter.Filter(new OptionFilterUniverse(allOptions, last));
                foreach (var symbol in selected)
                {
                    AddOptionContract(symbol, Resolution.Tick, fillDataForward: false);
                }
                _optionList.AddRange(selected);
            }
        }

        public override void OnData(Slice slice)
        {
            if (IsWarmingUp)
            {
                var s = Securities[UnderlyingTicker];
                //Log($"{slice.Time}, o:{s.Open}, h:{s.High}, l:{s.Low}, c:{s.Close}");
                return;
            }
            
            //UpdateOptionSubscription();
            foreach (var pair in slice.Ticks)
            {
                var tick = pair.Value.FirstOrDefault();
                var symbol = pair.Key;
                if (tick != null)
                {
                    //Log($"{slice.Time}, {tick.Time}, {symbol}, {Math.Max(symbol.SymbolProperties.LotSize, symbol.SymbolProperties.ContractMultiplier)}, {tick.LastPrice}");
                    //_logger.Debug($"{tick.Time}, {tick.LastPrice}");
                }
            }
            //if (slice.Ticks.TryGetValue(Underlying, out var ticks))
            //{
            //    var tick = ticks.FirstOrDefault();
            //    if (tick.IsFillForward)
            //    {
            //        return;
            //    }

            //    if (tick != null)
            //    {
            //        Log($"{tick.Time}, {tick.LastPrice}");
            //        //_logger.Debug($"{tick.Time}, {tick.LastPrice}");
            //    }
            //}

            if (!_traded && Transactions.GetOpenOrders(Underlying).Count == 0)
            {
                var security = Securities[Underlying];
                if (security.Holdings.HoldStock)
                {
                    var canClose = security.Holdings.QuantityT0;
                    if (security.Holdings.IsLong && canClose > 0)
                    {
                        LimitOrder(security.Symbol, -canClose, security.BidPrice);
                        _traded = true;
                        return;
                    }
                }
                else
                {
                    LimitOrder(security.Symbol, 10000, security.AskPrice);
                    //LimitOrder(security.Symbol, 5000, security.AskPrice);
                    //_traded = true;
                }
            }

            if (slice.OptionChains.TryGetValue(OptionSymbol, out var chain))
            {
                ////Log($"chain count: {chain.Contracts.Count}");
                //foreach (var c in chain.Contracts)
                //{
                //    //if (c.Key.Value == "10003112")
                //    {
                //        System.Diagnostics.Debug.WriteLine($"{c.Key.Value} LastPrice:{c.Value.LastPrice}");
                //    }
                //}

                //foreach (var c in chain.Contracts)
                //{
                //    lock (_locker)
                //    {
                //        var security = Securities[c.Key];
                //        if (!_traded && c.Key.Value == "10003052" && c.Value.LastPrice > 0 && security.Price>0)
                //        {
                //            _traded = true;
                //            _volume = _volume * -1;
                //            System.Diagnostics.Debug.WriteLine($"MarketOrder {c.Key.Value} {_volume}");
                //            LimitOrder(c.Key, _volume, c.Value.LastPrice-0.05m);
                //        }
                //    }
                //}

                foreach (var pair in chain.Contracts)
                {
                    //continue;
                    //pair.Key.Underlying
                    //System.Diagnostics.Debug.WriteLine($"{pair.Key.Value},{pair.Value.LastPrice},underlying:{pair.Value.UnderlyingLastPrice}");
                    continue;

                    var price = pair.Value.UnderlyingLastPrice;

                    var security = Securities[pair.Key];
                    if (security.HoldStock)
                    {
                        if (security.LongHoldings.HoldStock)
                        {
                            var longVolume = security.ShortHoldings.AbsoluteQuantity;
                            //
                        }

                        if (security.ShortHoldings.HoldStock)
                        {
                            var shortVolume = security.ShortHoldings.AbsoluteQuantity;
                        }
                    }
                }
                return;
                foreach (var pair in chain.Contracts)
                {
                    //System.Diagnostics.Debug.WriteLine($"{c.Key.Value} {c.Value.LastPrice}");
                    //continue;

                    lock (_locker)
                    {
                        var security = Securities[pair.Key];
                        // 10 秒检查一次
                        // 撤单
                        var now = DateTime.Now;
                        if ((now - _lasTime).TotalSeconds >= 10)
                        {
                            var openOrders = Transactions.GetOpenOrders(x => x.Symbol.Value == pair.Key.Value);
                            if (openOrders.Count > 0)
                            {
                                foreach (var o in openOrders)
                                {
                                    Transactions.CancelOrder(o.Id);
                                }
                                _lasTime = now;
                                continue;
                            }
                        }

                        if (!_traded
                            && pair.Key.Value == "10003264"
                            && pair.Value.LastPrice > 0
                            && security.Price > 0
                            && (now - _lasTime).TotalSeconds >= 10)
                        {
                            var tickSize = pair.Key.SymbolProperties.MinimumPriceVariation;
                            _lasTime = now;
                            if (!this.AutoOpenClose)
                            {
                                //var quantity = 1m;
                                //if (Securities[c.Key].LongHoldings.HoldStock)
                                //{
                                //    quantity = Securities[c.Key].LongHoldings.Quantity;
                                //    Log($"============LongHoldings Holdings :{quantity}");
                                //    Log($"============MarketOrder :{quantity * -2}");
                                //    MarketOrder(c.Key, quantity * -2);
                                //}
                                //else if (Securities[c.Key].ShortHoldings.HoldStock)
                                //{
                                //    quantity = Securities[c.Key].ShortHoldings.Quantity;
                                //    Log($"============ShortHoldings Holdings :{quantity}");
                                //    Log($"============MarketOrder :{quantity * -2}");
                                //    MarketOrder(c.Key, quantity * -2);
                                //}
                                //else
                                //{
                                //    Log($"============MarketOrder :{quantity * 2}");
                                //    MarketOrder(c.Key, quantity * 2);
                                //}

                                //_traded = true;

                                //LimitOrder(c.Key, 1, c.Value.LastPrice);

                            }
                            else
                            {
                                var quantity = 1m;
                                if (Securities[pair.Key].LongHoldings.HoldStock)
                                {
                                    quantity = Securities[pair.Key].LongHoldings.Quantity;
                                    System.Diagnostics.Debug.WriteLine($"============PlaceOrder :{quantity * -1}  LongHoldings Holdings :{quantity}");
                                    if (_placeLimitOrder)
                                    {
                                        LimitOrder(pair.Key, quantity * -1, pair.Value.AskPrice);
                                        _placeLimitOrder = true;
                                        _count++;
                                    }
                                    else
                                    {
                                        MarketOrder(pair.Key, quantity * -1);
                                        _placeLimitOrder = true;
                                        _count++;
                                    }

                                }
                                else if (Securities[pair.Key].ShortHoldings.HoldStock)
                                {
                                    quantity = Securities[pair.Key].ShortHoldings.Quantity;
                                    System.Diagnostics.Debug.WriteLine($"============PlaceOrder :{quantity * -1} ShortHoldings Holdings :{quantity}");
                                    if (_placeLimitOrder)
                                    {
                                        LimitOrder(pair.Key, quantity * -1, pair.Value.BidPrice);
                                        _placeLimitOrder = true;
                                        _count++;
                                    }
                                    else
                                    {
                                        MarketOrder(pair.Key, quantity * -1);
                                        _placeLimitOrder = true;
                                        _count++;
                                    }
                                }
                                else
                                {
                                    var direction = GetRandomNumber(1, 3) % 2 == 0 ? 1 : -1;
                                    System.Diagnostics.Debug.WriteLine($"============PlaceOrder :{quantity * direction}");
                                    if (_placeLimitOrder)
                                    {
                                        var price = direction > 0
                                            ? pair.Value.BidPrice - tickSize * 10
                                            : pair.Value.AskPrice + tickSize * 10;
                                        LimitOrder(pair.Key, quantity * direction, price);
                                        _placeLimitOrder = true;
                                        _count++;
                                    }
                                    else
                                    {
                                        MarketOrder(pair.Key, quantity * direction);
                                        _placeLimitOrder = true;
                                        _count++;
                                    }

                                }

                                _traded = true;

                                //LimitOrder(c.Key, 2, c.Value.LastPrice);

                            }
                        }

                    }
                }
            }

        }

        public override void OnOrderEvent(OrderEvent orderEvent)
        {
            //Log($"============OnOrderEvent :{orderEvent.OrderId} {orderEvent.BrokerId[0]} {orderEvent.Direction} {orderEvent.Offset} {orderEvent.Status} {orderEvent.FillQuantity}" );
            System.Diagnostics.Debug.WriteLine($"============OnOrderEvent :{orderEvent.OrderId} {orderEvent.BrokerId.FirstOrDefault()} {orderEvent.Direction} {orderEvent.Offset} {orderEvent.Status} {orderEvent.FillQuantity}");

            ////部成部撤
            //if (orderEvent.Status == OrderStatus.PartiallyFilled)
            //{
            //    //Transactions.CancelOrder(orderEvent.OrderId);
            //}

            if (orderEvent.Status.IsClosed())
            {
                lock (_locker)
                {
                    //_traded = false;
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

        private int GetRandomNumber(int min, int max)
        {
            return _rand.Next(min, max);
        }
    }
}
