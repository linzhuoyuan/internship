/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Fasterflect;
using QuantConnect.Algorithm;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.Alpha;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.RealTime;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Lean.Engine.Server;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;
using QuantConnect.Securities.Option;
using QuantConnect.Securities.Volatility;
using QuantConnect.Logging;
using Serilog;
using Log = QuantConnect.Logging.Log;

namespace QuantConnect.Lean.Engine
{
    /// <summary>
    /// Algorithm manager class executes the algorithm and generates and passes through the algorithm events.
    /// </summary>
    public class AlgorithmManager
    {
        private const string DateTimeFormat = "yyyyMMddTHH:mm:ss";
        private DateTime _previousTime;
        private IAlgorithm _algorithm;
        //public static IAlgorithmTime _algorithmTime;
        private readonly object _lock = new();
        private string _algorithmId = "";
        private DateTime _currentTimeStepTime;
        private readonly TimeSpan _timeLoopMaximum = TimeSpan.FromMinutes(Config.GetDouble("algorithm-manager-time-loop-maximum", 20));
        private long _dataPointCount;
        private int _pause = 0;
        private readonly bool _infiniteBuyingPower = Config.GetBool("infinite-buying-power");
        /// <summary>
        /// Publicly accessible algorithm status
        /// </summary>
        public AlgorithmStatus State => _algorithm?.Status ?? AlgorithmStatus.Running;

        /// <summary>
        /// Publicly accessible algorithm exit code
        /// </summary>
        public int AlgorithmExitCode => _algorithm?.ExitCode ?? -1;
        
        /// <summary>
        /// Publicly accessible exit code within algorithm manager, e.g. TimeoutException that applies in Isolator.MonitorTask.
        /// </summary>
        public int ExitCode { get; set; } = 0;

        /// <summary>
        /// Public access to the currently running algorithm id.
        /// </summary>
        public string AlgorithmId => _algorithmId;


        /// <summary>
        /// Gets the amount of time spent on the current time step
        /// </summary>
        public TimeSpan CurrentTimeStepElapsed
        {
            get
            {
                if (_currentTimeStepTime == DateTime.MinValue)
                {
                    _currentTimeStepTime = DateTime.UtcNow;
                    return TimeSpan.Zero;
                }

                return DateTime.UtcNow - _currentTimeStepTime;
            }
        }

        /// <summary>
        /// Gets a function used with the Isolator for verifying we're not spending too much time in each
        /// algo manager timer loop
        /// </summary>
        public readonly Func<IsolatorLimitResult> TimeLoopWithinLimits;

        private readonly bool _liveMode;

        /// <summary>
        /// Quit state flag for the running algorithm. When true the user has requested the backtest stops through a Quit() method.
        /// </summary>
        /// <seealso cref="QCAlgorithm.Quit(String)"/>
        public bool QuitState => State == AlgorithmStatus.Deleted;

        /// <summary>
        /// Gets the number of data points processed per second
        /// </summary>
        public long DataPoints => _dataPointCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="AlgorithmManager"/> class
        /// </summary>
        /// <param name="liveMode">True if we're running in live mode, false for backtest mode</param>
        public AlgorithmManager(bool liveMode)
        {
            TimeLoopWithinLimits = () =>
            {
                var message = string.Empty;
                var elapsed = CurrentTimeStepElapsed;
                if (elapsed > _timeLoopMaximum)
                {
                    message = $"Algorithm took longer than {_timeLoopMaximum.TotalMinutes} minutes on a single time loop.";
                }

                return new IsolatorLimitResult(elapsed, message);
            };
            _liveMode = liveMode;
        }

        public void ProcessGreekPnl(Slice slice)
        {
            var watch = new Stopwatch();
            watch.Start();
            var pnldata = _algorithm.Portfolio.CalcGreekPnl(slice);
            if (pnldata != null)
            {
                if (_algorithm is QCAlgorithm algorithm)
                {
                    algorithm.Draw(pnldata);
                }
            }
            watch.Stop();
        }

        private static ILogger GetFileLogger(IAlgorithm algorithm)
        {
            var logFile = $"{algorithm.Name}_run_{DateTime.Now:yyyy-MM-ddThhmmss_fff}.txt";
            return SerilogFileLogHandler.GetFileLogger(logFile, fileSizeLimitBytes: 2 * 1024 * 1024, retainedFileCountLimit: 2);
        }

        /// <summary>
        /// Launch the algorithm manager to run this strategy
        /// </summary>
        /// <param name="job">Algorithm job</param>
        /// <param name="algorithm">Algorithm instance</param>
        /// <param name="synchronizer">Instance which implements <see cref="ISynchronizer"/>. Used to stream the data</param>
        /// <param name="transactions">Transaction manager object</param>
        /// <param name="results">Result handler object</param>
        /// <param name="realtime">Realtime processing object</param>
        /// <param name="leanManager">ILeanManager implementation that is updated periodically with the IAlgorithm instance</param>
        /// <param name="alphas">Alpha handler used to process algorithm generated insights</param>
        /// <param name="token">Cancellation token</param>
        /// <remarks>Modify with caution</remarks>
        public void Run(
            AlgorithmNodePacket job,
            IAlgorithm algorithm,
            ISynchronizer synchronizer,
            ITransactionHandler transactions,
            IResultHandler results,
            IRealTimeHandler realtime,
            ILeanManager leanManager,
            IAlphaHandler alphas,
            CancellationToken token)
        {
            //Initialize:
            var margin_call_frequency = Config.GetInt("margin-call-frequency", 1);
            _dataPointCount = 0;
            _algorithm = algorithm;
            var portfolioValue = algorithm.Portfolio.TotalPortfolioValue;
            var backtestMode = (job.Type == PacketType.BacktestNode);
            var methodInvokers = new Dictionary<Type, MethodInvoker>();
            var marginCallFrequency = TimeSpan.FromMinutes(margin_call_frequency);
            var nextMarginCallTime = DateTime.MinValue;
            var settlementScanFrequency = TimeSpan.FromMinutes(30);
            var nextSettlementScanTime = DateTime.MinValue;

            var delistingList = new HashSet<Delisting>(new DelistingEqualityComparer());
            var removingList = new List<Delisting>();
            var splitWarnings = new List<Split>();

            //Initialize Properties:
            _algorithmId = job.AlgorithmId;
            _algorithm.Status = AlgorithmStatus.Running;
            _previousTime = algorithm.StartDate.Date;

            //Create the method accessors to push generic types into algorithm: Find all OnData events:

            // Algorithm 2.0 data accessors
            var hasOnDataTradeBars = AddMethodInvoker<TradeBars>(algorithm, methodInvokers);
            var hasOnDataQuoteBars = AddMethodInvoker<QuoteBars>(algorithm, methodInvokers);
            var hasOnDataOptionChains = AddMethodInvoker<OptionChains>(algorithm, methodInvokers);
            var hasOnDataTicks = AddMethodInvoker<Ticks>(algorithm, methodInvokers);

            // dividend and split events
            var hasOnDataDividends = AddMethodInvoker<Dividends>(algorithm, methodInvokers);
            var hasOnDataSplits = AddMethodInvoker<Splits>(algorithm, methodInvokers);
            var hasOnDataDelistings = AddMethodInvoker<Delistings>(algorithm, methodInvokers);
            var hasOnDataSymbolChangedEvents = AddMethodInvoker<SymbolChangedEvents>(algorithm, methodInvokers);
            var hasOnDataSymbolBonusEvents = AddMethodInvoker<SymbolBonusEvents>(algorithm, methodInvokers);

            // Algorithm 3.0 data accessors
            var hasOnDataSlice = algorithm
                .GetType()
                .GetMethods()
                .Where(x => x.Name == "OnData"
                            && x.GetParameters().Length == 1
                            && x.GetParameters()[0].ParameterType == typeof(Slice))
                .FirstOrDefault(x => x.DeclaringType == algorithm.GetType()) != null;

            var pnlTimer = 0;
            var lastPnlTime = DateTime.MinValue;

            if (!_liveMode)
            {
                var greekPnlSample = Config.Get("greek-pnl-sample");

                if (!string.IsNullOrEmpty(greekPnlSample))
                {
                    pnlTimer = Convert.ToInt32(greekPnlSample);
                }
            }


            //Go through the subscription types and create invokers to trigger the event handlers for each custom type:
            foreach (var config in algorithm.SubscriptionManager.Subscriptions)
            {

                //If type is a custom feed, check for a dedicated event handler
                if (config.IsCustomData)
                {
                    //Get the matching method for this event handler - e.g. public void OnData(Quandl data) { .. }
                    var genericMethod = (algorithm.GetType()).GetMethod("OnData", new[] { config.Type });

                    //If we already have this Type-handler then don't add it to invokers again.
                    if (methodInvokers.ContainsKey(config.Type)) continue;

                    if (genericMethod != null)
                    {
                        methodInvokers.Add(config.Type, genericMethod.DelegateForCallMethod());
                    }
                }
            }

            var swApp = new Stopwatch();
            var swAlg = new Stopwatch();
            var appElapsed = TimeSpan.Zero;
            var sliceElapsed = TimeSpan.Zero;
            var algElapsed = TimeSpan.Zero;
            var setTimeElapsed = TimeSpan.Zero;
            var sliceCount = 0;
            //var lastDate = DateTime.MinValue;
            var gc0 = 0;
            var gc1 = 0;
            var gc2 = 0;
            var runLogger = GetFileLogger(algorithm);
            //Loop over the queues: get a data collection, then pass them all into relevant methods in the algorithm.
            Log.Trace("AlgorithmManager.Run(): Begin DataStream - Start: " + algorithm.StartDate + " Stop: " + algorithm.EndDate);
            foreach (var timeSlice in Stream(algorithm, synchronizer, results, token))
            {
                //pause function 
                while (Thread.VolatileRead(ref _pause) > 0)
                {
                    Log.Trace("Algorithm has paused ...");
                    Thread.Sleep(1000);
                }
                ++sliceCount;

                if (timeSlice.Time.Date != _previousTime.Date)
                {
                    if (!_liveMode)
                    {
                        foreach (var delisting in removingList)
                        {
                            //algorithm.Securities[delisting.symbol].RemoveLocalTimeKeeper();
                            algorithm.Securities.Remove(delisting.symbol, true);
                            delistingList.Remove(delisting);
                        }
                    }
                    removingList.Clear();

                    if (sliceCount > 0)
                    {
                        Log.Trace($"{_previousTime:yyyyMMdd}," +
                                  $"slice:{Math.Round(sliceElapsed.TotalMilliseconds, 0)}," +
                                  $"count:{sliceCount}," +
                                  $"alg:{algElapsed.TotalMilliseconds}," +
                                  $"setTime:{setTimeElapsed.TotalMilliseconds}," +
                                  $"gc0:{GC.CollectionCount(0) - gc0}," +
                                  $"gc1:{GC.CollectionCount(1) - gc1}," +
                                  $"gc2:{GC.CollectionCount(2) - gc2}," +
                                  $"app:{Math.Round((appElapsed - algElapsed).TotalMilliseconds, 0)}");
                    }
                    //lastDate = timeSlice.Time.Date;
                    sliceElapsed = TimeSpan.Zero;
                    sliceCount = 0;
                    algElapsed = TimeSpan.Zero;
                    appElapsed = TimeSpan.Zero;
                    setTimeElapsed = TimeSpan.Zero;
                    gc0 = GC.CollectionCount(0);
                    gc1 = GC.CollectionCount(1);
                    gc2 = GC.CollectionCount(2);
                }

                // reset our timer on each loop
                _currentTimeStepTime = DateTime.MinValue;
                swApp.Restart();

                //Check this backtest is still running:
                if (_algorithm.Status != AlgorithmStatus.Running)
                {
                    Log.Error(
                        $"AlgorithmManager.Run(): Algorithm state changed to {_algorithm.Status} at {timeSlice.Time}"
                    );
                    break;
                }

                //Execute with TimeLimit Monitor:
                if (token.IsCancellationRequested)
                {
                    Log.Error("AlgorithmManager.Run(): Cancellation Requisition at " + timeSlice.Time);
                    return;
                }

                // Update the ILeanManager
                leanManager.Update();

                var time = timeSlice.Time;
                _dataPointCount += timeSlice.DataPointCount;

                //If we're in backtest mode we need to capture the daily performance. We do this here directly
                //before updating the algorithm state with the new data from this time step, otherwise we'll
                //produce incorrect samples (they'll take into account this time step's new price values)
                if (backtestMode)
                {
                    //On day-change sample equity and daily performance for statistics calculations
                    if (_previousTime.Date != time.Date)
                    {
                        _algorithm.Portfolio.TradingDayChanged();
                        SampleBenchmark(algorithm, results, _previousTime.Date);

                        var currentPortfolioValue = algorithm.Portfolio.TotalPortfolioValue;

                        //Sample the portfolio value over time for chart.
                        results.SampleEquity(_previousTime, Math.Round(currentPortfolioValue, 4));

                        //Check for divide by zero
                        if (portfolioValue == 0m)
                        {
                            results.SamplePerformance(_previousTime.Date, 0);
                        }
                        else
                        {
                            results.SamplePerformance(_previousTime.Date, Math.Round((currentPortfolioValue - portfolioValue) * 100 / portfolioValue, 10));
                        }
                        portfolioValue = currentPortfolioValue;
                    }

                    if (!_infiniteBuyingPower && portfolioValue <= 0)
                    {
                        const string logMessage = "AlgorithmManager.Run(): Portfolio value is less than or equal to zero, stopping algorithm.";
                        Log.Error(logMessage);
                        results.SystemDebugMessage(logMessage);
                        break;
                    }
                }
                else
                {
                    // live mode continuously sample the benchmark
                    SampleBenchmark(algorithm, results, time);
                }

                //Update algorithm state after capturing performance from previous day

                // If backtesting, we need to check if there are realtime events in the past
                // which didn't fire because at the scheduled times there was no data (i.e. markets closed)
                // and fire them with the correct date/time.
                if (backtestMode)
                {
                    realtime.ScanPastEvents(time);
                }

                swAlg.Restart();
                //Set the algorithm and real time handler's time
                algorithm.SetDateTime(time);
                setTimeElapsed += swAlg.Elapsed;
                swAlg.Stop();

                // Update the current slice before firing scheduled events or any other task
                algorithm.SetCurrentSlice(timeSlice.Slice);

                if (timeSlice.Slice.SymbolChangedEvents.Count != 0)
                {
                    if (hasOnDataSymbolChangedEvents)
                    {
                        methodInvokers[typeof(SymbolChangedEvents)](algorithm, timeSlice.Slice.SymbolChangedEvents);
                    }
                    foreach (var symbol in timeSlice.Slice.SymbolChangedEvents.Keys)
                    {
                        // cancel all orders for the old symbol
                        foreach (var ticket in transactions.GetOpenOrderTickets(x => x.Symbol == symbol))
                        {
                            ticket.Cancel("Open order cancelled on symbol changed event");
                        }
                    }
                }

                if (timeSlice.SecurityChanges != SecurityChanges.None)
                {
                    foreach (var security in timeSlice.SecurityChanges.AddedSecurities)
                    {
                        security.IsTradable = true;
                        if (!algorithm.Securities.ContainsKey(security.Symbol))
                        {
                            // add the new security
                            algorithm.Securities.Add(security);
                        }
                    }

                    var activeSecurities = algorithm.UniverseManager.ActiveSecurities;
                    foreach (var security in timeSlice.SecurityChanges.RemovedSecurities)
                    {
                        if (!activeSecurities.ContainsKey(security.Symbol))
                        {
                            security.IsTradable = false;
                        }
                    }
                    realtime.OnSecuritiesChanged(timeSlice.SecurityChanges);
                    results.OnSecuritiesChanged(timeSlice.SecurityChanges);
                }

                if (timeSlice.Slice.SymbolBonusEvents != null && timeSlice.Slice.SymbolBonusEvents.Count != 0)
                {
                    if (hasOnDataSymbolBonusEvents)
                    {
                        methodInvokers[typeof(SymbolBonusEvents)](algorithm, timeSlice.Slice.SymbolBonusEvents);
                    }
                }

                //Update the securities properties: first before calling user code to avoid issues with data
                foreach (var update in timeSlice.SecuritiesUpdateData)
                {
                    var security = update.Target;
                    foreach (var data in update.Data)
                    {
                        var tick = data as Tick;
                        security.SetMarketPrice(data);
                    }

                    // Send market price updates to the TradeBuilder
                    algorithm.TradeBuilder.SetMarketPrice(security.Symbol, security.Price);
                }

                //Update the securities properties with any universe data
                if (timeSlice.UniverseData.Count > 0)
                {
                    foreach (var kvp in timeSlice.UniverseData)
                    {
                        foreach (var data in kvp.Value.Data)
                        {
                            if (algorithm.Securities.TryGetValue(data.Symbol, out var security))
                            {
                                security.Cache.StoreData(data);
                            }
                        }
                    }
                }

                // poke each cash object to update from the recent security data
                foreach (var kvp in algorithm.Portfolio.CashBook)
                {
                    var cash = kvp.Value;
                    var updateData = cash.ConversionRateSecurity?.GetLastData();
                    if (updateData != null)
                    {
                        cash.Update(updateData);
                    }
                }

                if (algorithm.LiveMode)
                {
                    runLogger.Debug($"1.InvalidateTotalPortfolioValue(), time:{time.ToString(DateTimeFormat)}");
                }
                // security prices got updated
                algorithm.Portfolio.InvalidateTotalPortfolioValue();

                // fire real time events after we've updated based on the new data
                realtime.SetTime(timeSlice.Time);

                if (algorithm.LiveMode)
                {
                    runLogger.Debug($"2.transactions.ProcessSynchronousEvents(), time:{time.ToString(DateTimeFormat)}");
                }
                // process fill models on the updated data before entering algorithm, applies to all non-market orders
                transactions.ProcessSynchronousEvents();

                if (algorithm.LiveMode)
                {
                    runLogger.Debug($"3.ProcessDelistingSymbols(), time:{time.ToString(DateTimeFormat)}");
                }

                // process end of day delistingList
                ProcessDelistingSymbols(algorithm, transactions, delistingList);

                if (algorithm.LiveMode)
                {
                    runLogger.Debug($"4.ProcessSplitSymbols(), time:{time.ToString(DateTimeFormat)}");
                }
                // process split warnings for options
                ProcessSplitSymbols(algorithm, splitWarnings);

                //Check if the user's signaled Quit: loop over data until day changes.
                if (algorithm.Status == AlgorithmStatus.Stopped)
                {
                    Log.Trace("AlgorithmManager.Run(): Algorithm quit requested.");
                    break;
                }
                if (algorithm.RunTimeError != null)
                {
                    _algorithm.Status = AlgorithmStatus.RuntimeError;
                    Log.Trace(
                        $"AlgorithmManager.Run(): Algorithm encountered a runtime error at {timeSlice.Time}. Error: {algorithm.RunTimeError}"
                    );
                    break;
                }

                var marketReady = false;
                if (algorithm.PortfolioManagerName == "deribit" && !_liveMode)
                {
                    marketReady = algorithm.MarkPriceReady();
                }

                // perform margin calls, in live mode we can also use realtime to emit these
                if (marketReady && (time >= nextMarginCallTime || (_liveMode && nextMarginCallTime > DateTime.UtcNow)))
                {
                    // determine if there are possible margin call orders to be executed
                    var marginCallOrders =
                        algorithm.Portfolio.MarginCallModel.GetMarginCallOrders(out var issueMarginCallWarning);
                    if (marginCallOrders.Count != 0)
                    {
                        var executingMarginCall = false;
                        try
                        {
                            // tell the algorithm we're about to issue the margin call
                            algorithm.OnMarginCall(marginCallOrders);

                            executingMarginCall = true;

                            // execute the margin call orders
                            var executedTickets = algorithm.Portfolio.MarginCallModel.ExecuteMarginCall(marginCallOrders);
                            foreach (var ticket in executedTickets)
                            {
                                algorithm.Error(
                                    $"{algorithm.Time} - Executed MarginCallOrder: {ticket.Symbol} - Quantity: {ticket.Quantity} @ {ticket.AverageFillPrice}");
                            }
                        }
                        catch (Exception err)
                        {
                            algorithm.RunTimeError = err;
                            _algorithm.Status = AlgorithmStatus.RuntimeError;
                            var locator = executingMarginCall ? "Portfolio.MarginCallModel.ExecuteMarginCall" : "OnMarginCall";
                            Log.Error($"AlgorithmManager.Run(): RuntimeError: {locator}: " + err);
                            return;
                        }
                    }
                    // we didn't perform a margin call, but got the warning flag back, so issue the warning to the algorithm
                    else if (issueMarginCallWarning)
                    {
                        try
                        {
                            algorithm.OnMarginCallWarning();
                        }
                        catch (Exception err)
                        {
                            algorithm.RunTimeError = err;
                            _algorithm.Status = AlgorithmStatus.RuntimeError;
                            Log.Error("AlgorithmManager.Run(): RuntimeError: OnMarginCallWarning: " + err);
                            return;
                        }
                    }

                    nextMarginCallTime = time + marginCallFrequency;
                }

                // perform check for settlement of unsettled funds
                if (time >= nextSettlementScanTime || (_liveMode && nextSettlementScanTime > DateTime.UtcNow))
                {
                    algorithm.Portfolio.ScanForCashSettlement(algorithm.UtcTime);

                    nextSettlementScanTime = time + settlementScanFrequency;
                }

                // before we call any events, let the algorithm know about universe changes
                if (timeSlice.SecurityChanges != SecurityChanges.None)
                {
                    Log.Debug("OnSecuritiesChanged: {timeSlice.SecurityChanges} in algo manager");
                    try
                    {
                        algorithm.OnSecuritiesChanged(timeSlice.SecurityChanges);
                        algorithm.OnFrameworkSecuritiesChanged(timeSlice.SecurityChanges);
                    }
                    catch (Exception err)
                    {
                        algorithm.RunTimeError = err;
                        _algorithm.Status = AlgorithmStatus.RuntimeError;
                        Log.Error("AlgorithmManager.Run(): RuntimeError: OnSecuritiesChanged event: " + err);
                        return;
                    }
                }

                // apply dividends
                foreach (var dividend in timeSlice.Slice.Dividends.Values)
                {
                    Log.Debug($"AlgorithmManager.Run(): {algorithm.Time}: Applying Dividend: {dividend}");

                    Security? security = null;
                    if (_liveMode && algorithm.Securities.TryGetValue(dividend.Symbol, out security))
                    {
                        Log.Trace($"AlgorithmManager.Run(): {algorithm.Time}: Pre-Dividend: {dividend}. " +
                            $"Security Holdings: {security.Holdings.Quantity} Account Currency Holdings: " +
                            $"{algorithm.Portfolio.CashBook[algorithm.AccountCurrency].Amount}");
                    }

                    var mode = algorithm.SubscriptionManager.SubscriptionDataConfigService
                        .GetSubscriptionDataConfigs(dividend.Symbol)
                        .DataNormalizationMode();

                    // apply the dividend event to the portfolio
                    algorithm.Portfolio.ApplyDividend(dividend, _liveMode, mode);

                    if (_liveMode && security != null)
                    {
                        Log.Trace($"AlgorithmManager.Run(): {algorithm.Time}: Post-Dividend: {dividend}. Security " +
                            $"Holdings: {security.Holdings.Quantity} Account Currency Holdings: " +
                            $"{algorithm.Portfolio.CashBook[algorithm.AccountCurrency].Amount}");
                    }
                }

                // apply splits
                foreach (var split in timeSlice.Slice.Splits.Values)
                {
                    try
                    {
                        // only process split occurred events (ignore warnings)
                        if (split.Type != SplitType.SplitOccurred)
                        {
                            continue;
                        }

                        Log.Debug($"AlgorithmManager.Run(): {algorithm.Time}: Applying Split for {split.Symbol}");

                        Security security = null;
                        if (_liveMode && algorithm.Securities.TryGetValue(split.Symbol, out security))
                        {
                            Log.Trace($"AlgorithmManager.Run(): {algorithm.Time}: Pre-Split for {split}. Security Price: {security.Price} Holdings: {security.Holdings.Quantity}");
                        }

                        var mode = algorithm.SubscriptionManager.SubscriptionDataConfigService
                            .GetSubscriptionDataConfigs(split.Symbol)
                            .DataNormalizationMode();

                        // apply the split event to the portfolio
                        algorithm.Portfolio.ApplySplit(split, _liveMode, mode);

                        if (_liveMode && security != null)
                        {
                            Log.Trace($"AlgorithmManager.Run(): {algorithm.Time}: Post-Split for {split}. Security Price: {security.Price} Holdings: {security.Holdings.Quantity}");
                        }

                        // apply the split to open orders as well in raw mode, all other modes are split adjusted
                        if (_liveMode || mode == DataNormalizationMode.Raw)
                        {
                            // in live mode we always want to have our order match the order at the brokerage, so apply the split to the orders
                            var openOrders = transactions.GetOpenOrderTickets(ticket => ticket.Symbol == split.Symbol);
                            algorithm.BrokerageModel.ApplySplit(openOrders.ToList(), split);
                        }
                    }
                    catch (Exception err)
                    {
                        algorithm.RunTimeError = err;
                        _algorithm.Status = AlgorithmStatus.RuntimeError;
                        Log.Error("AlgorithmManager.Run(): RuntimeError: Split event: " + err);
                        return;
                    }
                }

                //Update registered consolidators for this symbol index
                try
                {
                    if (timeSlice.ConsolidatorUpdateData.Count > 0)
                    {
                        var timeKeeper = algorithm.TimeKeeper;
                        foreach (var update in timeSlice.ConsolidatorUpdateData)
                        {
                            var consolidators = update.Target.Consolidators;
                            foreach (var consolidator in consolidators)
                            {
                                foreach (var dataPoint in update.Data)
                                {
                                    // only push data into consolidators on the native, subscribed to resolution
                                    if (EndTimeIsInNativeResolution(update.Target, dataPoint.EndTime))
                                    {
                                        consolidator.Update(dataPoint);
                                    }
                                }

                                // scan for time after we've pumped all the data through for this consolidator
                                consolidator.Scan(timeKeeper.GetLocalTimeKeeper(update.Target.ExchangeTimeZone).LocalTime);
                            }
                        }
                    }
                }
                catch (Exception err)
                {
                    algorithm.RunTimeError = err;
                    _algorithm.Status = AlgorithmStatus.RuntimeError;
                    Log.Error("AlgorithmManager.Run(): RuntimeError: Consolidators update: " + err);
                    return;
                }

                // fire custom event handlers
                foreach (var update in timeSlice.CustomData)
                {
                    if (!methodInvokers.TryGetValue(update.DataType, out var methodInvoker))
                    {
                        continue;
                    }

                    try
                    {
                        foreach (var dataPoint in update.Data)
                        {
                            if (update.DataType.IsInstanceOfType(dataPoint))
                            {
                                methodInvoker(algorithm, dataPoint);
                            }
                        }
                    }
                    catch (Exception err)
                    {
                        algorithm.RunTimeError = err;
                        _algorithm.Status = AlgorithmStatus.RuntimeError;
                        Log.Error("AlgorithmManager.Run(): RuntimeError: Custom Data: " + err);
                        return;
                    }
                }

                try
                {
                    // fire off the dividend and split events before pricing events
                    if (hasOnDataDividends && timeSlice.Slice.Dividends.Count != 0)
                    {
                        methodInvokers[typeof(Dividends)](algorithm, timeSlice.Slice.Dividends);
                    }
                    if (hasOnDataSplits && timeSlice.Slice.Splits.Count != 0)
                    {
                        methodInvokers[typeof(Splits)](algorithm, timeSlice.Slice.Splits);
                    }
                    if (hasOnDataDelistings && timeSlice.Slice.Delistings.Count != 0)
                    {
                        methodInvokers[typeof(Delistings)](algorithm, timeSlice.Slice.Delistings);
                    }
                }
                catch (Exception err)
                {
                    algorithm.RunTimeError = err;
                    _algorithm.Status = AlgorithmStatus.RuntimeError;
                    Log.Error("AlgorithmManager.Run(): RuntimeError: Dividends/Splits/Delistings: " + err);
                    return;
                }

                if (algorithm.LiveMode)
                {
                    runLogger.Debug($"5.HandleDelistedSymbols(), time:{time.ToString(DateTimeFormat)}");
                }
                // run the delisting logic after firing delisting events
                HandleDelistedSymbols(algorithm, timeSlice.Slice.Delistings, delistingList);

                if (algorithm.LiveMode)
                {
                    runLogger.Debug($"6.HandleSplitSymbols(), time:{time.ToString(DateTimeFormat)}");
                }
                // run split logic after firing split events
                HandleSplitSymbols(timeSlice.Slice.Splits, splitWarnings);

                // 分红调整改动：更新合约乘数； writer:lh
                // run the previous nearest history bonus ContractMultiplier
                //if (algorithm.IsNeedUpdateContractMultiplier && timeSlice.Slice.HasData)
                //{
                //    var date = algorithm.Time;
                //    algorithm.IsNeedUpdateContractMultiplier = false;
                //    if (SymbolBonusDatabase.TryGetSymbolBonusEarliestDate(date, out var firstDate))
                //    {
                //        foreach (var security in algorithm.Securities.Values)
                //        {
                //            if (security.Symbol.SecurityType == SecurityType.Option && !security.symbol.IsCanonical())
                //            {
                //                var symbolBonus = SymbolBonusDatabase.GetSymbolBonus(security.symbol.id.contractId, firstDate);
                //                var option = security as Option;
                //                if (symbolBonus != null)
                //                {
                //                    option.symbolProperties.ContractMultiplier = symbolBonus.ContractMultiplier;
                //                    option.ContractUnitOfTrade = (int)symbolBonus.ContractMultiplier;
                //                }
                //            }
                //        }
                //    }
                //}

                //After we've fired all other events in this second, fire the pricing events:
                try
                {
                    // TODO: For backwards compatibility only. Remove in 2017
                    // For compatibility with Forex Trade data, moving
                    if (timeSlice.Slice.QuoteBars.Count > 0)
                    {
                        foreach (var tradeBar in timeSlice.Slice.QuoteBars.Where(x => x.Key.ID.SecurityType == SecurityType.Forex))
                        {
                            timeSlice.Slice.Bars.Add(tradeBar.Value.Collapse());
                        }
                    }
                    if (hasOnDataTradeBars && timeSlice.Slice.Bars.Count > 0) methodInvokers[typeof(TradeBars)](algorithm, timeSlice.Slice.Bars);
                    if (hasOnDataQuoteBars && timeSlice.Slice.QuoteBars.Count > 0) methodInvokers[typeof(QuoteBars)](algorithm, timeSlice.Slice.QuoteBars);
                    if (hasOnDataOptionChains && timeSlice.Slice.OptionChains.Count > 0) methodInvokers[typeof(OptionChains)](algorithm, timeSlice.Slice.OptionChains);
                    if (hasOnDataTicks && timeSlice.Slice.Ticks.Count > 0) methodInvokers[typeof(Ticks)](algorithm, timeSlice.Slice.Ticks);
                }
                catch (Exception err)
                {
                    algorithm.RunTimeError = err;
                    _algorithm.Status = AlgorithmStatus.RuntimeError;
                    Log.Error("AlgorithmManager.Run(): RuntimeError: New Style Mode: " + err);
                    return;
                }

                try
                {
                    if (timeSlice.Slice.HasData)
                    {
                        // EVENT HANDLER v3.0 -- all data in a single event
                        if (algorithm.LiveMode)
                        {
                            runLogger.Debug($"7.OnData(), time:{time.ToString(DateTimeFormat)}");
                        }
                        swAlg.Restart();
                        algorithm.OnData(timeSlice.Slice);
                        swAlg.Stop();
                        algElapsed += swAlg.Elapsed;

                        if (pnlTimer > 0)
                        {
                            if (timeSlice.Slice.Time - lastPnlTime >= TimeSpan.FromMinutes(pnlTimer))
                            {
                                ProcessGreekPnl(timeSlice.Slice);
                                lastPnlTime = timeSlice.Slice.Time;
                            }
                        }
                    }

                    // always turn the crank on this method to ensure universe selection models function properly on day changes w/out data
                    algorithm.OnFrameworkData(timeSlice.Slice);
                }
                catch (Exception err)
                {
                    algorithm.RunTimeError = err;
                    _algorithm.Status = AlgorithmStatus.RuntimeError;
                    Log.Error("AlgorithmManager.Run(): RuntimeError: Slice: " + err);
                    return;
                }


                //If its the historical/paper trading models, wait until market orders have been "filled"
                // Manually trigger the event handler to prevent thread switch.
                if (algorithm.LiveMode)
                {
                    runLogger.Debug($"8.transactions.ProcessSynchronousEvents(), time:{time.ToString(DateTimeFormat)}");
                }
                transactions.ProcessSynchronousEvents();

                // sample alpha charts now that we've updated time/price information and after transactions
                // are processed so that insights closed because of new order based insights get updated
                // hetao: 优化性能
                // alphas.ProcessSynchronousEvents();

                //Save the previous time for the sample calculations
                _previousTime = time;

                // send the alpha statistics to the result handler for storage/transmit with the result packets
                // hetao: 优化性能
                // results.SetAlphaRuntimeStatistics(alphas.RuntimeStatistics);

                // Process any required events of the results handler such as sampling assets, equity, or stock prices.
                if (algorithm.LiveMode)
                {
                    runLogger.Debug($"9.results.ProcessSynchronousEvents(), time:{time.ToString(DateTimeFormat)}");
                }
                results.ProcessSynchronousEvents();

                // poke the algorithm at the end of each time step
                if (algorithm.LiveMode)
                {
                    runLogger.Debug($"10.algorithm.OnEndOfTimeStep(), time:{time.ToString(DateTimeFormat)}");
                }
                algorithm.OnEndOfTimeStep();

                //记录运行时间
                swApp.Stop();
                appElapsed += swApp.Elapsed;
                sliceElapsed += timeSlice.Slice.CreateElapsed;

                //Log.Trace($"slice:{sliceCount},sw_app:{swApp.Elapsed},sw_alg:{swAlg.Elapsed}");
                //timeSlice.Clear();
            } // End of ForEach feed.Bridge.GetConsumingEnumerable

            // stop timing the loops
            _currentTimeStepTime = DateTime.MinValue;
            swApp.Reset();

            //Stream over:: Send the final packet and fire final events:
            Log.Trace("AlgorithmManager.Run(): Firing On End Of Algorithm...");
            try
            {
                algorithm.OnEndOfAlgorithm();
                if (token.IsCancellationRequested)
                {
                    algorithm.OnProgramExit();
                }
            }
            catch (Exception err)
            {
                _algorithm.Status = AlgorithmStatus.RuntimeError;
                algorithm.RunTimeError = new Exception(
                    $"Error running OnEndOfAlgorithm(): {err.Message}",
                    err.InnerException);
                Log.Error($"AlgorithmManager.OnEndOfAlgorithm(): {err}");
                return;
            }

            // final processing now that the algorithm has completed
            // hetao: 优化性能
            // alphas.ProcessSynchronousEvents();

            // send the final alpha statistics to the result handler for storage/transmit with the result packets
            // hetao: 优化性能
            // results.SetAlphaRuntimeStatistics(alphas.RuntimeStatistics);

            // Process any required events of the results handler such as sampling assets, equity, or stock prices.
            results.ProcessSynchronousEvents(true);

            //Liquidate Holdings for Calculations:
            if (_algorithm.Status == AlgorithmStatus.Liquidated && _liveMode)
            {
                Log.Trace("AlgorithmManager.Run(): Liquidating algorithm holdings...");
                algorithm.Liquidate();
                results.LogMessage("Algorithm Liquidated");
                results.SendStatusUpdate(AlgorithmStatus.Liquidated);
            }

            //Manually stopped the algorithm
            if (_algorithm.Status == AlgorithmStatus.Stopped)
            {
                Log.Trace("AlgorithmManager.Run(): Stopping algorithm...");
                results.LogMessage("Algorithm Stopped");
                results.SendStatusUpdate(AlgorithmStatus.Stopped);
            }

            //Backtest deleted.
            if (_algorithm.Status == AlgorithmStatus.Deleted)
            {
                Log.Trace("AlgorithmManager.Run(): Deleting algorithm...");
                results.DebugMessage("Algorithm Id:(" + job.AlgorithmId + ") Deleted by request.");
                results.SendStatusUpdate(AlgorithmStatus.Deleted);
            }

            //Algorithm finished, send regardless of commands:
            results.SendStatusUpdate(AlgorithmStatus.Completed);
            SetStatus(AlgorithmStatus.Completed);

            //Take final samples:
            results.SampleRange(algorithm.GetChartUpdates());
            results.SampleEquity(_previousTime, Math.Round(algorithm.Portfolio.TotalPortfolioValue, 4));
            SampleBenchmark(algorithm, results, backtestMode ? _previousTime.Date : _previousTime);

            //Check for divide by zero
            if (portfolioValue == 0m)
            {
                results.SamplePerformance(backtestMode ? _previousTime.Date : _previousTime, 0m);
            }
            else
            {
                results.SamplePerformance(backtestMode ? _previousTime.Date : _previousTime,
                    Math.Round((algorithm.Portfolio.TotalPortfolioValue - portfolioValue) * 100 / portfolioValue, 10));
            }
        } // End of Run();

        /// <summary>
        /// Set the quit state.
        /// </summary>
        public void SetStatus(AlgorithmStatus state)
        {
            lock (_lock)
            {
                //We don't want anyone else-to set our internal state to "Running".
                //This is controlled by the algorithm private variable only.
                if (state != AlgorithmStatus.Running)
                {
                    _algorithm.Status = state;
                }
            }
        }

        public void SetPause(bool pause)
        {
            if (pause)
            {
                Thread.VolatileWrite(ref _pause, 1);
            }
            else
            {
                Thread.VolatileWrite(ref _pause, 0);
            }
        }

        private IEnumerable<TimeSlice> Stream(
            IAlgorithm algorithm,
            ISynchronizer synchronizer,
            IResultHandler results,
            CancellationToken cancellationToken)
        {
            var setStartTime = false;
            var timeZone = algorithm.TimeZone;
            var history = algorithm.HistoryProvider;

            // fulfilling history requirements of volatility models in live mode
            if (algorithm.LiveMode)
            {
                //ProcessVolatilityHistoryRequirements(algorithm);
            }

            // get the required history job from the algorithm
            DateTime? lastHistoryTimeUtc = null;
            var historyRequests = algorithm.GetWarmupHistoryRequests().ToList();

            // initialize variables for progress computation
            var warmUpStartTicks = DateTime.UtcNow.Ticks;
            var nextStatusTime = DateTime.UtcNow.AddSeconds(1);
            var minimumIncrement = algorithm.UniverseManager
                .Select(x =>
                    x.Value.UniverseSettings?.Resolution.ToTimeSpan()
                    ??
                    algorithm.UniverseSettings.Resolution.ToTimeSpan())
                .DefaultIfEmpty(Time.OneSecond)
                .Min();

            minimumIncrement = minimumIncrement == TimeSpan.Zero ? Time.OneSecond : minimumIncrement;

            if (historyRequests.Count != 0)
            {
                // rewrite internal feed requests
                var subscriptions = algorithm
                    .SubscriptionManager
                    .Subscriptions
                    .Where(x => !x.IsInternalFeed)
                    .ToList();
                var minResolution = subscriptions.Count > 0
                    ? subscriptions.Min(x => x.Resolution)
                    : Resolution.Second;
                foreach (var request in historyRequests)
                {
                    if (algorithm.Securities.TryGetValue(request.Symbol, out var security)
                        && security.IsInternalFeed())
                    {
                        if (request.Resolution < minResolution)
                        {
                            request.Resolution = minResolution;
                            request.FillForwardResolution = request.FillForwardResolution.HasValue
                                ? minResolution
                                : (Resolution?)null;
                        }
                    }
                }

                // rewrite all to share the same fill forward resolution
                if (historyRequests.Any(x => x.FillForwardResolution.HasValue))
                {
                    minResolution = historyRequests
                        .Where(x => x.FillForwardResolution.HasValue)
                        .Min(x => x.FillForwardResolution.Value);
                    foreach (var request in historyRequests
                        .Where(x => x.FillForwardResolution.HasValue))
                    {
                        request.FillForwardResolution = minResolution;
                    }
                }

                foreach (var request in historyRequests)
                {
                    warmUpStartTicks = Math.Min(request.StartTimeUtc.Ticks, warmUpStartTicks);
                    Log.Trace($"AlgorithmManager.Stream(): WarmupHistoryRequest: {request.Symbol}: Start: {request.StartTimeUtc} End: {request.EndTimeUtc} Resolution: {request.Resolution}");
                }

                var timeSliceFactory = new TimeSliceFactory(timeZone);
                // make the history request and build time slices
                foreach (var slice in history.GetHistory(historyRequests, timeZone))
                {
                    TimeSlice timeSlice;
                    try
                    {
                        // we need to recombine this slice into a time slice
                        var paired = new List<DataFeedPacket>();
                        foreach (var symbol in slice.Keys)
                        {
                            var security = algorithm.Securities[symbol];
                            var data = slice[symbol];
                            var list = new List<BaseData>();
                            Type dataType;
                            SubscriptionDataConfig config;

                            if (data is List<Tick> ticks)
                            {
                                list.AddRange(ticks);
                                dataType = typeof(Tick);
                                config = algorithm
                                    .SubscriptionManager
                                    .Subscriptions
                                    .FirstOrDefault(subscription =>
                                        subscription.Symbol == symbol
                                        && dataType.IsAssignableFrom(subscription.Type));
                            }
                            else
                            {
                                list.Add(data);
                                dataType = data.GetType();
                                config = security
                                    .Subscriptions
                                    .FirstOrDefault(subscription =>
                                        dataType.IsAssignableFrom(subscription.Type));
                            }

                            if (config == null)
                            {
                                if (data is TradeBar tradeBar)
                                {
                                    list.Clear();
                                    var tick = new Tick();
                                    tick.Symbol = symbol;
                                    tick.time = tradeBar.Time;
                                    tick.Value = tradeBar.Close;
                                    tick.Quantity = tradeBar.Volume;
                                    tick.BidPrice = tick.Price;
                                    tick.BidSize = tick.Quantity;
                                    tick.AskPrice = tick.Price;
                                    tick.AskSize = tick.Quantity;
                                    tick.MarkPrice = tick.Price;
                                    tick.SettlementPrice = tick.Price;
                                    list.Add(tick);
                                    dataType = typeof(Tick);
                                    config = algorithm
                                        .SubscriptionManager
                                        .Subscriptions
                                        .FirstOrDefault(subscription =>
                                            subscription.Symbol == symbol
                                            && dataType.IsAssignableFrom(subscription.Type));
                                }
                                else if (data is QuoteBar quoteBar)
                                {
                                    list.Clear();
                                    var tick = new Tick();
                                    tick.Symbol = symbol;
                                    tick.time = quoteBar.Time;
                                    tick.Value = quoteBar.Close;
                                    tick.Quantity = 0;
                                    if (quoteBar.Bid != null)
                                    {
                                        tick.BidPrice = quoteBar.Bid.Close;
                                        tick.BidSize = quoteBar.LastBidSize;
                                    }
                                    if (quoteBar.Ask != null)
                                    {
                                        tick.AskPrice = quoteBar.Ask.Close;
                                        tick.AskSize = quoteBar.LastAskSize;
                                    }
                                    tick.MarkPrice = tick.Price;
                                    tick.SettlementPrice = tick.Price;
                                    list.Add(tick);
                                    dataType = typeof(Tick);
                                    config = algorithm
                                        .SubscriptionManager
                                        .Subscriptions
                                        .FirstOrDefault(subscription =>
                                            subscription.Symbol == symbol
                                            && dataType.IsAssignableFrom(subscription.Type));
                                }
                                else
                                    throw new Exception($"A data subscription for type '{dataType.Name}' was not found.");
                            }

                            paired.Add(new DataFeedPacket(security, config, list));
                        }

                        timeSlice = timeSliceFactory.Create(
                            slice.Time.ConvertToUtc(timeZone),
                            paired,
                            SecurityChanges.None,
                            new Dictionary<Universe, BaseDataCollection>());
                    }
                    catch (Exception err)
                    {
                        Log.Error(err);
                        algorithm.RunTimeError = err;
                        yield break;
                    }

                    if (timeSlice != null)
                    {
                        if (!setStartTime)
                        {
                            setStartTime = true;
                            _previousTime = timeSlice.Time;
                            algorithm.Debug("Algorithm warming up...");
                        }
                        if (DateTime.UtcNow > nextStatusTime)
                        {
                            // send some status to the user letting them know we're done history, but still warming up,
                            // catching up to real time data
                            nextStatusTime = DateTime.UtcNow.AddSeconds(1);
                            var percent = (int)(100 * (timeSlice.Time.Ticks - warmUpStartTicks) / (double)(DateTime.UtcNow.Ticks - warmUpStartTicks));
                            results.SendStatusUpdate(
                                AlgorithmStatus.History,
                                $"Catching up to realtime {percent}%...");
                        }
                        yield return timeSlice;
                        lastHistoryTimeUtc = timeSlice.Time;
                    }
                }
            }

            // if we're not live or didn't event request warmup, then set us as not warming up
            if (!algorithm.LiveMode || algorithm.SimulationMode || historyRequests.Count == 0)
            {
                algorithm.SetFinishedWarmingUp();
                if (historyRequests.Count != 0)
                {
                    algorithm.Debug("Algorithm finished warming up.");
                    Log.Trace("AlgorithmManager.Stream(): Finished warmup");
                }
            }

            foreach (var timeSlice in synchronizer.StreamData(cancellationToken))
            {
                if (!setStartTime)
                {
                    setStartTime = true;
                    _previousTime = timeSlice.Time;
                }
                if (algorithm.LiveMode && algorithm.IsWarmingUp)
                {
                    // this is hand-over logic, we spin up the data feed first and then request
                    // the history for warmup, so there will be some overlap between the data
                    if (lastHistoryTimeUtc.HasValue)
                    {
                        // make sure there's no historical data, this only matters for the handover
                        var hasHistoricalData = false;
                        foreach (var data in timeSlice
                            .Slice
                            .Ticks
                            .Values
                            .SelectMany(x => x)
                            .Concat<BaseData>(timeSlice.Slice.Bars.Values))
                        {
                            // check if any ticks in the list are on or after our last warmup point, if so, skip this data
                            if (data.endTime.ConvertToUtc(algorithm.Securities[data.Symbol].Exchange.TimeZone) <= lastHistoryTimeUtc)
                            {
                                hasHistoricalData = true;
                                break;
                            }
                        }
                        if (hasHistoricalData)
                        {
                            continue;
                        }

                        // prevent us from doing these checks every loop
                        lastHistoryTimeUtc = null;
                    }

                    // in live mode wait to mark us as finished warming up when
                    // the data feed has caught up to now within the min increment

                    if (timeSlice.Time > (algorithm.SimulationMode ? algorithm.StartDate : DateTime.UtcNow).Subtract(minimumIncrement))
                    {
                        algorithm.SetFinishedWarmingUp();
                        algorithm.Debug("Algorithm finished warming up.");
                        Log.Trace("AlgorithmManager.Stream(): Finished warmup");
                    }
                    else if (DateTime.UtcNow > nextStatusTime)
                    {
                        // send some status to the user letting them know we're done history, but still warming up,
                        // catching up to real time data
                        nextStatusTime = DateTime.UtcNow.AddSeconds(1);
                        var percent = (int)(100 * (timeSlice.Time.Ticks - warmUpStartTicks) / (double)(DateTime.UtcNow.Ticks - warmUpStartTicks));
                        results.SendStatusUpdate(AlgorithmStatus.History, $"Catching up to realtime {percent}%...");
                    }
                }
                yield return timeSlice;
            }
        }

        /// <summary>
        /// Helper method used to process securities volatility history requirements
        /// </summary>
        /// <remarks>Implemented as static to facilitate testing</remarks>
        /// <param name="algorithm">The algorithm instance</param>
        public static void ProcessVolatilityHistoryRequirements(IAlgorithm algorithm)
        {
            Log.Trace("ProcessVolatilityHistoryRequirements(): Updating volatility models with historical data...");

            foreach (var kvp in algorithm.Securities)
            {
                var security = kvp.Value;

                if (security.VolatilityModel != VolatilityModel.Null)
                {
                    // start: this is a work around to maintain retro compatibility
                    // did not want to add IVolatilityModel.SetSubscriptionDataConfigProvider
                    // to prevent breaking existing user models.
                    var baseType = security.VolatilityModel as BaseVolatilityModel;
                    baseType?.SetSubscriptionDataConfigProvider(
                        algorithm.SubscriptionManager.SubscriptionDataConfigService);
                    // end

                    var historyReq = security.VolatilityModel.GetHistoryRequirements(security, algorithm.UtcTime);

                    if (historyReq != null && algorithm.HistoryProvider != null)
                    {
                        var history = algorithm.HistoryProvider.GetHistory(historyReq, algorithm.TimeZone);
                        if (history != null)
                        {
                            foreach (var slice in history)
                            {
                                if (slice.Bars.ContainsKey(security.Symbol))
                                    security.VolatilityModel.Update(security, slice.Bars[security.Symbol]);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Adds a method invoker if the method exists to the method invokers dictionary
        /// </summary>
        /// <typeparam name="T">The data type to check for 'OnData(T data)</typeparam>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="methodInvokers">The dictionary of method invokers</param>
        /// <param name="methodName">The name of the method to search for</param>
        /// <returns>True if the method existed and was added to the collection</returns>
        private bool AddMethodInvoker<T>(IAlgorithm algorithm, Dictionary<Type, MethodInvoker> methodInvokers, string methodName = "OnData")
        {
            var newSplitMethodInfo = algorithm.GetType().GetMethod(methodName, new[] { typeof(T) });
            if (newSplitMethodInfo != null)
            {
                methodInvokers.Add(typeof(T), newSplitMethodInfo.DelegateForCallMethod());
                return true;
            }
            return false;
        }


        /// <summary>
        /// Performs delisting logic for the securities specified in <paramref name="newDelistings"/> that are marked as <see cref="DelistingType.Delisted"/>.
        /// </summary>
        private static void HandleDelistedSymbols(
            IAlgorithm algorithm,
            Delistings newDelistings,
            ISet<Delisting> delistings)
        {
            foreach (var delisting in newDelistings.Values)
            {
                // submit an order to liquidate on market close
                if (delisting.Type == DelistingType.Warning)
                {
                    //if (!delistings.Any(x => x.symbol == delisting.symbol && x.Type == delisting.Type))
                    if (!delistings.Contains(delisting))
                    {
                        delistings.Add(delisting);
                        //Log.Trace($"AlgorithmManager.Run(): Security delisting warning: {delisting.Symbol.Value}, UtcTime: {algorithm.UtcTime}, DelistingTime: {delisting.Time}");
                    }
                }
                else
                {
                    // mark security as no longer tradable
                    var security = algorithm.Securities[delisting.Symbol];
                    if (security.IsDelisted)
                    {
                        continue;
                    }
                    SymbolDelisted(algorithm, security, delisting);
                }
            }

            foreach (var delisting in delistings.ToArray())
            {
                if (algorithm.UtcTime >= delisting.DelistedUtcTime)
                {
                    var security = algorithm.Securities[delisting.Symbol];
                    SymbolDelisted(algorithm, security, delisting);
                    delistings.Remove(delisting);
                }
            }
        }

        private static void SymbolDelisted(IAlgorithm algorithm, Security security, Delisting delisting)
        {
            security.IsTradable = false;
            security.IsDelisted = true;

            // remove security from all universes
            foreach (var pair in algorithm.UniverseManager)
            {
                var universe = pair.Value;
                if (universe.ContainsMember(security.Symbol))
                {
                    universe.RemoveMember(algorithm.UtcTime, security);
                }
            }

            //Log.Trace($"AlgorithmManager.Run(): Security delisted: {delisting.Symbol.Value}, UtcTime: {algorithm.UtcTime}, DelistingTime: {delisting.Time}");
            var cancelledOrders = algorithm.Transactions.CancelOpenOrders(delisting.Symbol);
            foreach (var cancelledOrder in cancelledOrders)
            {
                Log.Trace("AlgorithmManager.SymbolDelisted(): " + cancelledOrder);
            }

            //record delisted symbol
            //writer hetao
            //removings.Add(delisting);
            algorithm.Securities.Remove(delisting.Symbol);
            Log.Trace($"Remove delisted {delisting.Symbol}");
        }

        /// <summary>
        /// Performs actual delisting of the contracts in delistingList collection
        /// </summary>
        private static void ProcessDelistingSymbols(IAlgorithm algorithm, ITransactionHandler transaction, ICollection<Delisting> delistingList)
        {
            foreach (var delisting in delistingList.ToArray())
            {
                // check if we are holding position
                var security = algorithm.Securities[delisting.symbol];
                if (security.holdings.Quantity == 0
                    && security.longHoldings.Quantity == 0
                    && security.shortHoldings.Quantity == 0)
                {
                    continue;
                }

                if (algorithm.UtcTime < delisting.DelistedUtcTime)
                {
                    continue;
                }

                // submit an order to liquidate on market close or exercise (for options)
                var requestList = new List<SubmitOrderRequest>();

                if (security.Type == SecurityType.Option)
                {
                    var option = (Option)security;

                    if (security.holdings.Quantity > 0)
                    {
                        requestList.Add(new SubmitOrderRequest(
                            OrderType.OptionExercise, security.Type, security.symbol,
                            security.holdings.Quantity, 0, 0, algorithm.UtcTime, "Automatic option exercise on expiration"));
                    }
                    if (security.longHoldings.Quantity > 0)
                    {
                        requestList.Add(new SubmitOrderRequest(
                            OrderType.OptionExercise, security.Type, security.Symbol,
                            security.LongHoldings.Quantity, 0, 0, algorithm.UtcTime, "Automatic option exercise on expiration", offset: OrderOffset.Close));
                    }
                    if (security.holdings.Quantity < 0)
                    {
                        var message = option.GetPayOff(option.Underlying.Price) > 0
                            ? "Automatic option assignment on expiration"
                            : "Option expiration";

                        requestList.Add(new SubmitOrderRequest(OrderType.OptionExercise, security.Type, security.symbol,
                            security.holdings.Quantity, 0, 0, algorithm.UtcTime, message));
                    }
                    if (security.shortHoldings.Quantity < 0)
                    {
                        var message = option.GetPayOff(option.Underlying.Price) > 0
                            ? "Automatic option assignment on expiration"
                            : "Option expiration";

                        requestList.Add(new SubmitOrderRequest(OrderType.OptionExercise, security.Type, security.symbol,
                            security.shortHoldings.Quantity, 0, 0, algorithm.UtcTime, message, offset: OrderOffset.Close));
                    }
                }
                else
                {
                    requestList.Add(new SubmitOrderRequest(OrderType.Market, security.Type, security.symbol,
                        -security.holdings.Quantity, 0, 0, algorithm.UtcTime, "Liquidate from delisting"));
                }
                foreach (var request in requestList)
                {
                    algorithm.Transactions.ProcessRequest(request);
                }
                SymbolDelisted(algorithm, security, delisting);
                delistingList.Remove(delisting);
            }
        }

        /// <summary>
        /// Keeps track of split warnings so we can later liquidate option contracts
        /// </summary>
        private void HandleSplitSymbols(Splits newSplits, List<Split> splitWarnings)
        {
            foreach (var split in newSplits.Values)
            {
                if (split.Type != SplitType.Warning)
                {
                    Log.Trace($"AlgorithmManager.HandleSplitSymbols(): {_algorithm.Time} - Security split occurred: Split Factor: {split} Reference Price: {split.ReferencePrice}");
                    continue;
                }

                Log.Trace($"AlgorithmManager.HandleSplitSymbols(): {_algorithm.Time} - Security split warning: {split}");

                if (!splitWarnings.Any(x => x.Symbol == split.Symbol && x.Type == SplitType.Warning))
                {
                    splitWarnings.Add(split);
                }
            }
        }

        /// <summary>
        /// Liquidate option contact holdings who's underlying security has split
        /// </summary>
        private void ProcessSplitSymbols(IAlgorithm algorithm, List<Split> splitWarnings)
        {
            // NOTE: This method assumes option contracts have the same core trading hours as their underlying contract
            //       This is a small performance optimization to prevent scanning every contract on every time step,
            //       instead we scan just the underlying, thereby reducing the time footprint of this methods by a factor
            //       of N, the number of derivative subscriptions
            for (var i = splitWarnings.Count - 1; i >= 0; i--)
            {
                var split = splitWarnings[i];
                var security = algorithm.Securities[split.Symbol];

                if (!security.IsTradable
                    && !algorithm.UniverseManager.ActiveSecurities.Keys.Contains(split.Symbol))
                {
                    Log.Debug($"AlgorithmManager.ProcessSplitSymbols(): {_algorithm.Time} - Removing split warning for {security.Symbol}");

                    // remove the warning from out list
                    splitWarnings.RemoveAt(i);
                    // Since we are storing the split warnings for a loop
                    // we need to check if the security was removed.
                    // When removed, it will be marked as non tradable but just in case
                    // we expect it not to be an active security either
                    continue;
                }

                var nextMarketClose = security.Exchange.Hours.GetNextMarketClose(security.LocalTime, false);

                // determine the latest possible time we can submit a MOC order
                var configs = algorithm.SubscriptionManager.SubscriptionDataConfigService
                    .GetSubscriptionDataConfigs(security.Symbol);

                if (configs.Count == 0)
                {
                    // should never happen at this point, if it does let's give some extra info
                    throw new Exception(
                        $"AlgorithmManager.ProcessSplitSymbols(): {_algorithm.Time} - No subscriptions found for {security.Symbol}" +
                        $", IsTradable: {security.IsTradable}" +
                        $", Active: {algorithm.UniverseManager.ActiveSecurities.Keys.Contains(split.Symbol)}");
                }

                var latestMarketOnCloseTimeRoundedDownByResolution = nextMarketClose.Subtract(MarketOnCloseOrder.DefaultSubmissionTimeBuffer)
                    .RoundDownInTimeZone(configs.GetHighestResolution().ToTimeSpan(), security.Exchange.TimeZone, configs.First().DataTimeZone);

                // we don't need to do anything until the market closes
                if (security.LocalTime < latestMarketOnCloseTimeRoundedDownByResolution) continue;

                // fetch all option derivatives of the underlying with holdings (excluding the canonical security)
                var derivatives = algorithm.Securities.Where(kvp => kvp.Key.HasUnderlying &&
                    kvp.Key.SecurityType == SecurityType.Option &&
                    kvp.Key.Underlying == security.Symbol &&
                    !kvp.Key.Underlying.IsCanonical() &&
                    kvp.Value.HoldStock
                );

                foreach (var kvp in derivatives)
                {
                    var optionContractSymbol = kvp.Key;
                    var optionContractSecurity = (Option)kvp.Value;

                    // close any open orders
                    algorithm.Transactions.CancelOpenOrders(optionContractSymbol, "Canceled due to impending split. Separate MarketOnClose order submitted to liquidate position.");

                    var request = new SubmitOrderRequest(OrderType.MarketOnClose, optionContractSecurity.Type, optionContractSymbol,
                        -optionContractSecurity.Holdings.Quantity, 0, 0, algorithm.UtcTime,
                        "Liquidated due to impending split. Option splits are not currently supported."
                    );

                    // send MOC order to liquidate option contract holdings
                    algorithm.Transactions.AddOrder(request);

                    // mark option contract as not tradable
                    optionContractSecurity.IsTradable = false;

                    algorithm.Debug($"MarketOnClose order submitted for option contract '{optionContractSymbol}' due to impending {split.Symbol.Value} split event. "
                        + "Option splits are not currently supported.");
                }

                // remove the warning from out list
                splitWarnings.RemoveAt(i);
            }
        }

        /// <summary>
        /// Samples the benchmark in a  try/catch block
        /// </summary>
        private void SampleBenchmark(IAlgorithm algorithm, IResultHandler results, DateTime time)
        {
            try
            {
                // backtest mode, sample benchmark on day changes
                results.SampleBenchmark(time, algorithm.Benchmark.Evaluate(time).SmartRounding());
            }
            catch (Exception err)
            {
                algorithm.RunTimeError = err;
                _algorithm.Status = AlgorithmStatus.RuntimeError;
                Log.Error(err);
            }
        }

        /// <summary>
        /// Determines if a data point is in it's native, configured resolution
        /// </summary>
        private static bool EndTimeIsInNativeResolution(SubscriptionDataConfig config, DateTime dataPointEndTime)
        {
            if (config.Resolution == Resolution.Tick
                ||
                // time zones don't change seconds or milliseconds so we can
                // shortcut timezone conversions
                (config.Resolution == Resolution.Second
                || config.Resolution == Resolution.Minute)
                && dataPointEndTime.Ticks % config.Increment.Ticks == 0)
            {
                return true;
            }

            var roundedDataPointEndTime = dataPointEndTime.RoundDownInTimeZone(config.Increment, config.ExchangeTimeZone, config.DataTimeZone);
            return dataPointEndTime == roundedDataPointEndTime;
        }
    }
}
