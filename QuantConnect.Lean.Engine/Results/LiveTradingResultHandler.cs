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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using QuantConnect.Algorithm;
using QuantConnect.Configuration;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.Alphas;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Logging;
using QuantConnect.Messaging;
using QuantConnect.Notifications;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Statistics;
using QuantConnect.Util;
using QuantConnect.Parameters;

namespace QuantConnect.Lean.Engine.Results
{
    /// <summary>
    /// Live trading result handler implementation passes the messages to the QC live trading interface.
    /// </summary>
    /// <remarks>Live trading result handler is quite busy. It sends constant price updates, equity updates and order/holdings updates.</remarks>
    public class LiveTradingResultHandler : BaseResultsHandler, IResultHandler
    {
        private readonly DateTime _launchTimeUtc = DateTime.UtcNow;

        // Required properties for the cloud app.
        private string _compileId;
        private string _deployId;
        private LiveNodePacket _job;
        private readonly ConcurrentQueue<OrderEvent> _orderEvents = new ConcurrentQueue<OrderEvent>();
        private IAlgorithm _algorithm;
        private IEnumerable<PerformanceAnalyserAttribute> _analysisAttribs = Enumerable.Empty<PerformanceAnalyserAttribute>();
        private volatile bool _exitTriggered;
        private readonly DateTime _startTime = DateTime.UtcNow;
        private readonly Dictionary<string, string> _runtimeStatistics = new Dictionary<string, string>();

        //Update loop:
        private DateTime _nextUpdate;
        private DateTime _nextChartsUpdate;
        private DateTime _nextChartTrimming;
        private DateTime _nextLogStoreUpdate;
        private DateTime _nextStatisticsUpdate;
        private DateTime _nextAnalyserOutput;
        private long _lastOrderId;
        private readonly object _chartLock = new object();
        private readonly object _customerChartLock = new object();
        private readonly object _greeksChartLock = new object();
        private readonly object _runtimeLock = new object();
        private string _subscription = "Strategy Equity";

        //Log Message Store:
        private readonly object _logStoreLock = new object();
        private List<LogEntry> _logStore = new List<LogEntry>();
        private DateTime _nextSample;
        private IMessagingHandler _messagingHandler;
        //private IMessagingHandler _messagingHandlerOptionPrice;
        private IApi _api;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private ISetupHandler _setupHandler;
        private ITransactionHandler _transactionHandler;
        private bool _storeResult = false;


        private long _sendCount = 0;
        private long _sendMessageCount=0;
        private long _sendResultCount = 0;
        private long _sendCustomerChartCount = 0;
        private long _sendGreeksChartCount = 0;
        private DateTime _lastTime = DateTime.Now;
        // private bool _conversionRateReady = false;

        /// <summary>
        /// Live packet messaging queue. Queue the messages here and send when the result queue is ready.
        /// </summary>
        public ConcurrentQueue<Packet> Messages { get; set; } = new ConcurrentQueue<Packet>();


        /// <summary>
        /// 
        /// </summary>
        //public ConcurrentQueue<Packet> OptionPriceMessages { get; set; } = new ConcurrentQueue<Packet>();
        //public ConcurrentDictionary<Symbol, Tuple<decimal, decimal>> OptionPriceMap { get; set; } = new ConcurrentDictionary<Symbol, Tuple<decimal, decimal>>();
        public ConcurrentDictionary<Symbol, OptionPriceMarketData> OptionPriceMap = new ConcurrentDictionary<Symbol, OptionPriceMarketData>();


        /// <summary>
        /// Storage for the price and equity charts of the live results.
        /// </summary>
        /// <remarks>
        ///     Potential memory leak when the algorithm has been running for a long time. Infinitely storing the results isn't wise.
        ///     The results should be stored to disk daily, and then the caches reset.
        /// </remarks>
        public ConcurrentDictionary<string, Chart> Charts { get; set; } = new ConcurrentDictionary<string, Chart>();

        /// <summary>
        /// customer chart collection for storing the master copy of customer chart  data.
        /// </summary>
        public ConcurrentDictionary<string, List<CustomerChartData>> CustomerCharts { get; set; } = new ConcurrentDictionary<string, List<CustomerChartData>>();

        /// <summary>
        /// 
        /// </summary>
        public ConcurrentDictionary<string, GreeksChartData> GreeksCharts { get; set; } = new ConcurrentDictionary<string, GreeksChartData>();

        /// <summary>
        /// Boolean flag indicating the thread is still active.
        /// </summary>
        public bool IsActive { get; private set; } = true;

        /// <summary>
        /// Equity resampling period for the charting.
        /// </summary>
        /// <remarks>Live trading can resample at much higher frequencies (every 1-2 seconds)</remarks>
        public TimeSpan ResamplePeriod { get; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Sampling period for timespans between resamples of the echart.
        /// </summary>
        /// <remarks>Specifically critical for backtesting since with such long timeframes the sampled data can get extreme.</remarks>
        public TimeSpan ResamplePeriodEchart { get; private set; } = TimeSpan.FromSeconds(Double.Parse(Config.Get("echart-sample","5")));

        /// <summary>
        /// Notification periods set how frequently we push updates to the browser.
        /// </summary>
        /// <remarks>Live trading resamples - sends updates at high frequencies(every 1-2 seconds)</remarks>
        public TimeSpan NotificationPeriod { get; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Initialize the result handler with this result packet.
        /// </summary>
        /// <param name="job">Algorithm job packet for this result handler</param>
        /// <param name="messagingHandler"></param>
        /// <param name="api"></param>
        /// <param name="setupHandler"></param>
        /// <param name="transactionHandler"></param>
        public virtual void Initialize(AlgorithmNodePacket job, IMessagingHandler messagingHandler, IApi api, ISetupHandler setupHandler, ITransactionHandler transactionHandler)
        {
            _api = api;
            _messagingHandler = messagingHandler;
            _setupHandler = setupHandler;
            _transactionHandler = transactionHandler;
            _job = (LiveNodePacket)job;
            if (_job == null) throw new Exception("LiveResultHandler.Constructor(): Submitted Job type invalid.");
            _deployId = _job.DeployId;
            _compileId = _job.CompileId;
            _storeResult = Config.GetBool("store-result", false);
        }

        /// <summary>
        /// Live trading result handler thread.
        /// </summary>
        public void Run()
        {
            // give the algorithm time to initialize, else we will log an error right away
            ExitEvent.WaitOne(3000);

            // -> 1. Run Primary Sender Loop: Continually process messages from queue as soon as they arrive.
            while (!(_exitTriggered && Messages.Count == 0))
            {
                try
                {
                    //1. Process Simple Messages in Queue
                    Packet packet;
                    if (Messages.TryDequeue(out packet))
                    {
                        if (packet.Type == PacketType.TradeRecord)
                        {
                            Log.Trace($"Send TradeRecord:{((TradeRecordPacket)packet).Trade}");
                        }
                        _messagingHandler.Send(packet);
                        _sendMessageCount++;
                        _sendCount++;
                    }

                    //2. Update the packet scanner:
                    Update();

                    if (Messages.Count == 0)
                    {
                        // prevent thread lock/tight loop when there's no work to be done
                        ExitEvent.WaitOne(100);
                    }

                    // //测试数据包发送
                    // var t = DateTime.Now;
                    // if ((t - _lastTime).TotalSeconds > 60)
                    // {
                    //     _lastTime = t;
                    //     Log.Trace($"Packet Send: SendCount:{_sendCount} ResultCount:{_sendResultCount} MessageCount:{_sendMessageCount} CustomerChartCount:{_sendCustomerChartCount} GreeksChartCount:{_sendGreeksChartCount}");
                    // }
                }
                catch (Exception err)
                {
                    Log.Error(err);
                }
            } // While !End.

            Log.Trace("LiveTradingResultHandler.Run(): Ending Thread...");
            IsActive = false;
        } // End Run();

        // private bool ConversionRateReady()
        // {
        //     if (!_conversionRateReady)
        //     {
        //         foreach (var item in _algorithm.Portfolio.CashBook)
        //         {
        //             if (item.Value.ConversionRate == 0)
        //             {
        //                 return false;
        //             }
        //         }
        //     }
        //     _conversionRateReady = true;
        //     return _conversionRateReady;
        // }
        /*
        public void RunOptionPrice()
        {
            try
            {
                var host = Config.Get("option-price-output-host", "localhost");
                var port = Config.Get("option-price-output-port", "8808");
                _messagingHandlerOptionPrice = new StreamingMessageHandler(host, port);
                while (!(_exitTriggered && OptionPriceMessages.Count == 0))
                {
                    
                    //While there's no work to do, go back to the algorithm:
                    if (OptionPriceMessages.Count == 0)
                    {
                        Thread.Sleep(50);
                    }
                    else
                    {
                        //1. Process Simple Messages in Queue
                        Packet packet;
                        if (OptionPriceMessages.TryDequeue(out packet))
                        {
                            _messagingHandlerOptionPrice.Send(packet);
                        }
                    }

                } // While !End.
            }
            catch (Exception err)
            {
                // unexpected error, we need to close down shop
                Log.Error(err);
            }
            Log.Trace("BacktestingResultHandler.RunEchart(): Ending Thread...");
            IsActive = false;
        }
        */

        private void CustomerChartDataSend()
        {
            var customerCharts = new List<CustomerChartData>();
            lock (_customerChartLock)
            {
                //Get the updates since the last chart
                customerCharts = CustomerCharts.SelectMany(x => x.Value).ToList();
                CustomerCharts.Clear();
            }
            if (customerCharts.Count > 0)
            {
                CustomerChartDataPacket customerChartDataPacket = new CustomerChartDataPacket(customerCharts);
                _messagingHandler.Send(customerChartDataPacket);
                _sendCustomerChartCount++;
                _sendCount++;
            }
        }


        private void GreeksChartDataSend()
        {
            var charts = new List<GreeksChartData>();
            lock (_greeksChartLock)
            {
                //Get the updates since the last chart
                foreach (var kvp in GreeksCharts)
                {
                    charts.Add(kvp.Value);
                }
                GreeksCharts.Clear();
            }
            if (charts.Count > 0)
            {
                GreeksChartDataPacket packet = new GreeksChartDataPacket(charts);
                _messagingHandler.Send(packet);
                _sendGreeksChartCount++;
                _sendCount++;
            }
        }

        private void OptionPriceMarketDataSend()
        {
            //添加发送期权行情
            if (OptionPriceMap.Count > 0)
            {
                var optionPrices = new List<OptionPriceMarketData>();
                foreach (var i in OptionPriceMap)
                {
                    if (i.Value.Updated)
                    {
                        optionPrices.Add(i.Value);
                    }
                }

                if (optionPrices.Count > 0)
                {
                    OptionPriceMarketDataPacket packet = new OptionPriceMarketDataPacket(optionPrices);
                    _messagingHandler.Send(packet);
                }
            }
        }

        // private Dictionary<string, Holding> SummerHoldings()
        // {
        //     var holdings = new Dictionary<string, Holding>();

        //     var totalHolding = new Holding();
        //     totalHolding.Symbol = new Symbol(SecurityIdentifier.Empty, "Total");
        //     // Only send holdings updates when we have changes in orders, except for first time, then we want to send all
        //     try
        //     {
        //         var time = _algorithm.Time;
        //         foreach (var kvp in _algorithm.Securities.ItemOrderBy(x => x.Key.Value))
        //         {
        //             var security = kvp.Value;

        //             if (!security.IsInternalFeed() && !security.Symbol.IsCanonical() && security.Invested)
        //             {
        //                 if (security.Holdings.Invested)
        //                 {
        //                     var holding = new Holding(security, SecurityHoldingType.Net);
        //                     holding.Time = time;
        //                     DictionarySafeAdd(holdings, security.Symbol.Value + security.symbol.SecurityType + "Net", holding, "holdings");

        //                     totalHolding.Quantity += holding.Quantity;
        //                     totalHolding.MarketValue += holding.MarketValue;
        //                     totalHolding.UnrealizedPnL += holding.UnrealizedPnL;
        //                     totalHolding.RealizedPnL += holding.RealizedPnL;
        //                     totalHolding.ExercisePnL += holding.ExercisePnL;
        //                 }

        //                 if (security.LongHoldings.Invested)
        //                 {
        //                     var holding = new Holding(security, SecurityHoldingType.Long);
        //                     holding.Time = time;
        //                     DictionarySafeAdd(holdings, security.Symbol.Value + security.symbol.SecurityType + "Long", holding, "holdings");

        //                     totalHolding.Quantity += holding.Quantity;
        //                     totalHolding.MarketValue += holding.MarketValue;
        //                     totalHolding.UnrealizedPnL += holding.UnrealizedPnL;
        //                     totalHolding.RealizedPnL += holding.RealizedPnL;
        //                     totalHolding.ExercisePnL += holding.ExercisePnL;
        //                 }

        //                 if (security.ShortHoldings.Invested)
        //                 {
        //                     var holding = new Holding(security, SecurityHoldingType.Short);
        //                     holding.Time = time;
        //                     DictionarySafeAdd(holdings, security.Symbol.Value + security.symbol.SecurityType + "Short", holding, "holdings");

        //                     totalHolding.Quantity += holding.Quantity;
        //                     totalHolding.MarketValue += holding.MarketValue;
        //                     totalHolding.UnrealizedPnL += holding.UnrealizedPnL;
        //                     totalHolding.RealizedPnL += holding.RealizedPnL;
        //                     totalHolding.ExercisePnL += holding.ExercisePnL;
        //                 }
        //             }
        //         }
        //     }
        //     catch (Exception e)
        //     {
        //         Log.Error(e, "LiveTradingResultHandler().SummerHoldings(): ", true);
        //     }

        //     holdings["Total"] = totalHolding;
        //     return holdings;
        // }

        /// <summary>
        /// Every so often send an update to the browser with the current state of the algorithm.
        /// </summary>
        public void Update()
        {
            //Error checks if the algorithm & threads have not loaded yet, or are closing down.
            if (_algorithm?.Transactions == null || _transactionHandler.Orders == null || !_algorithm.GetLocked())
            {
                Log.Error("LiveTradingResultHandler.Update(): Algorithm not yet initialized.");
                return;
            }

            TakePerformanceSnapshot();

            var utcNow = DateTime.UtcNow;
            if (utcNow > _nextUpdate)
            {
                try
                {
                    //Extract the orders created since last update
                    var deltaOrders = new Dictionary<string, Order>();

                    var stopwatch = Stopwatch.StartNew();
                    //while (_orderEvents.TryDequeue(out orderEvent) && stopwatch.ElapsedMilliseconds < 15)
                    //{
                    //    var order = _algorithm.Transactions.GetOrderById(orderEvent.OrderId);
                    //    deltaOrders[orderEvent.OrderId] = order.Clone();
                    //}

                    while (_orderEvents.TryDequeue(out var orderEvent) && stopwatch.ElapsedMilliseconds < 15)
                    {
                        if (orderEvent.BrokerId.Count > 0)
                        {
                            var brokerId = orderEvent.BrokerId[0];
                            if (_algorithm.OrderRecords.ContainsKey(orderEvent.BrokerId[0]))
                            {
                                var orderRecord = _algorithm.OrderRecords[orderEvent.BrokerId[0]];
                                deltaOrders[brokerId] = orderRecord.order.Clone();
                            }
                        }
                    }



                    //For charting convert to UTC
                    foreach (var order in deltaOrders)
                    {
                        order.Value.Price = order.Value.Price.SmartRounding();
                        order.Value.Time = order.Value.Time.ToUniversalTime();
                    }

                    //Reset loop variables:
                    _lastOrderId = (from order in deltaOrders.Values select order.Id).DefaultIfEmpty(_lastOrderId).Max();

                    //Limit length of orders we pass back dynamically to avoid flooding.
                    //if (deltaOrders.Count > 50) deltaOrders.Clear();

                    //Create and send back the changes in chart since the algorithm started.
                    var deltaCharts = new Dictionary<string, Chart>();
                    //Log.Debug("LiveTradingResultHandler.Update(): Build delta charts");
                    lock (_chartLock)
                    {
                        //Get the updates since the last chart
                        foreach (var chart in Charts)
                        {
                            // remove directory pathing characters from chart names
                            var safeName = chart.Value.Name.Replace('/', '-');
                            DictionarySafeAdd(deltaCharts, safeName, chart.Value.GetUpdates(), "deltaCharts");
                        }
                    }
                    Log.Debug("LiveTradingResultHandler.Update(): End build delta charts");
                    // if(!ConversionRateReady())
                    // {
                    //     return;
                    // }

                    //Profit loss changes, get the banner statistics, summary information on the performance for the headers.
                    var holdings =  new Dictionary<string, Holding>();
                    var deltaStatistics = new Dictionary<string, string>();
                    var runtimeStatistics = new Dictionary<string, string>();
                    var serverStatistics = OS.GetServerStatistics();
                    var upTime = utcNow - _launchTimeUtc;
                    serverStatistics["Up Time"] = $"{upTime.Days}d {upTime:hh\\:mm\\:ss}";
                    serverStatistics["Total RAM (MB)"] = _job.Controls.RamAllocation.ToString();
                    decimal netReturn = 0;

                    foreach (var kvp in _algorithm.Securities
                        // we send non internal, non canonical and tradable securities. When securities are removed they are marked as non tradable
                        .Where(pair => pair.Value.IsTradable && !pair.Value.IsInternalFeed() && !pair.Key.IsCanonical())
                        .OrderBy(x => x.Key.Value))
                    {
                        var security = kvp.Value;
                        DictionarySafeAdd(holdings, security.Symbol.ToString(), new Holding(security), "holdings");
                    }

                    //Add the algorithm statistics first.
                    //Log.Debug("LiveTradingResultHandler.Update(): Build run time stats");
                    lock (_runtimeLock)
                    {
                        foreach (var pair in _runtimeStatistics)
                        {
                            runtimeStatistics.Add(pair.Key, pair.Value);
                        }
                    }
                    //Log.Debug("LiveTradingResultHandler.Update(): End build run time stats");
                    try
                    {
                        //Some users have $0 in their brokerage account / starting cash of $0. Prevent divide by zero errors
                        netReturn = _setupHandler.StartingPortfolioValue > 0
                            ? (_algorithm.Portfolio.TotalPortfolioValue - _setupHandler.StartingPortfolioValue) /
                              _setupHandler.StartingPortfolioValue
                            : 0;

                        //Add other fixed parameters.
                        DictionarySafeAdd(
                            runtimeStatistics,
                            "Unrealized:",
                            "$" + _algorithm.Portfolio.TotalUnrealizedProfit.ToString("N2"),
                            "runtimeStatistics"
                        );
                        DictionarySafeAdd(
                            runtimeStatistics,
                            "Fees:",
                            "-$" + _algorithm.Portfolio.TotalFees.ToString("N2"),
                            "runtimeStatistics"
                        );
                        DictionarySafeAdd(
                            runtimeStatistics,
                            "Net Profit:",
                            "$" + (_algorithm.Portfolio.TotalProfit - _algorithm.Portfolio.TotalFees).ToString("N2"),
                            "runtimeStatistics"
                        );


                        ////增加
                        //DictionarySafeAdd(runtimeStatistics, "Option Value:", "$" + (_algorithm.Portfolio.GetMarketValue(holdings, QuantConnect.SecurityType.Option)).ToString("N2"), "runtimeStatistics");
                        //DictionarySafeAdd(runtimeStatistics, "Equity Value:", "$" + (_algorithm.Portfolio.GetMarketValue(holdings, QuantConnect.SecurityType.Equity)).ToString("N2"), "runtimeStatistics");
                        //DictionarySafeAdd(runtimeStatistics, "Future PnL:", "$" + (_algorithm.Portfolio.GetTotalFutureUnrealisedProfit(holdings, QuantConnect.SecurityType.Future)).ToString("N2"), "runtimeStatistics");



                        DictionarySafeAdd(runtimeStatistics, "Return:", netReturn.ToString("P"), "runtimeStatistics");
                        DictionarySafeAdd(
                            runtimeStatistics,
                            "Equity:",
                            "$" + _algorithm.Portfolio.TotalPortfolioValue.ToString("N2"),
                            "runtimeStatistics"
                        );

                        //暂时删除
                        DictionarySafeAdd(
                            runtimeStatistics,
                            "Holdings:",
                            "$" + _algorithm.Portfolio.TotalHoldingsValue.ToString("N2"),
                            "runtimeStatistics"
                        );
                        DictionarySafeAdd(
                            runtimeStatistics,
                            "Volume:",
                            "$" + _algorithm.Portfolio.TotalSaleVolume.ToString("N2"),
                            "runtimeStatistics"
                        );

                        //增加
                        //DictionarySafeAdd(runtimeStatistics, "Available Marigin:", "$" + _algorithm.Portfolio.TotalPortfolioValue.ToString("N2"), "runtimeStatistics");
                        //DictionarySafeAdd(runtimeStatistics, "Used Marigin:", "$" + _algorithm.Portfolio.TotalMarginUsed.ToString("N2"), "runtimeStatistics");
                        //DictionarySafeAdd(runtimeStatistics, "Unsettled Cash:", "$" + _algorithm.Portfolio.UnsettledCash.ToString("N2"), "runtimeStatistics");


                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "LiveTradingResultHandler().Update(): runtimeStatistics", true);
                    }

                    // since we're sending multiple packets, let's do it async and forget about it
                    // chart data can get big so let's break them up into groups
                    var splitPackets = SplitPackets(deltaCharts, deltaOrders, holdings, _algorithm.Portfolio.CashBook, deltaStatistics, runtimeStatistics, serverStatistics);

                    foreach (var liveResultPacket in splitPackets)
                    {
                        _messagingHandler.Send(liveResultPacket);
                        _sendResultCount++;
                        _sendCount++;
                    }

                    //Send full packet to storage.
                    if (utcNow > _nextChartsUpdate)
                    {
                        Log.Debug("LiveTradingResultHandler.Update(): Pre-store result");
                        var chartComplete = new Dictionary<string, Chart>();
                        lock (_chartLock)
                        {
                            foreach (var chart in Charts)
                            {
                                // remove directory pathing characters from chart names
                                var safeName = chart.Value.Name.Replace('/', '-');
                                DictionarySafeAdd(chartComplete, safeName, chart.Value.Clone(), "chartComplete");
                            }
                        }
                        //var orders = new Dictionary<int, Order>(_transactionHandler.Orders);
                        var orders = _transactionHandler.Orders.ToDictionary(t => t.Key.ToString(), t => t.Value);
                        var complete = new LiveResultPacket(_job, new LiveResult(chartComplete, orders, _algorithm.Transactions.TransactionRecord, holdings, _algorithm.Portfolio.CashBook, deltaStatistics, runtimeStatistics, serverStatistics));
                        if (_storeResult)
                        {
                            StoreResult(complete);
                        }
                        _nextChartsUpdate = DateTime.UtcNow.AddMinutes(1);
                        Log.Debug("LiveTradingResultHandler.Update(): End-store result");
                    }

                    // Upload the logs every 1-2 minutes; this can be a heavy operation depending on amount of live logging and should probably be done asynchronously.
                    if (utcNow > _nextLogStoreUpdate)
                    {
                        List<LogEntry> logs;
                        Log.Debug("LiveTradingResultHandler.Update(): Storing log...");
                        lock (_logStoreLock)
                        {
                            var timeLimitUtc = utcNow.RoundDown(TimeSpan.FromHours(1));
                            logs = (from log in _logStore
                                    where log.Time >= timeLimitUtc
                                    select log).ToList();
                            //Override the log master to delete the old entries and prevent memory creep.
                            _logStore = logs;
                            // we need a new container instance so we can store the logs outside the lock
                            logs = new List<LogEntry>(logs);
                        }
                        StoreLog(logs);
                        _nextLogStoreUpdate = DateTime.UtcNow.AddMinutes(2);
                        Log.Debug("LiveTradingResultHandler.Update(): Finished storing log");
                    }

                    // Every minute send usage statistics:
                    if (utcNow > _nextStatisticsUpdate)
                    {
                        try
                        {
                            _api.SendStatistics(
                                _job.AlgorithmId,
                                _algorithm.Portfolio.TotalUnrealizedProfit,
                                _algorithm.Portfolio.TotalFees,
                                _algorithm.Portfolio.TotalProfit,
                                _algorithm.Portfolio.TotalHoldingsValue,
                                _algorithm.Portfolio.TotalPortfolioValue,
                                netReturn,
                                _algorithm.Portfolio.TotalSaleVolume,
                                _lastOrderId, 0);
                        }
                        catch (Exception err)
                        {
                            Log.Error(err, "Error sending statistics:");
                        }
                        _nextStatisticsUpdate = utcNow.AddMinutes(1);
                    }

                    if (utcNow > _nextChartTrimming)
                    {
                        Log.Debug("LiveTradingResultHandler.Update(): Trimming charts");
                        var timeLimitUtc = Time.DateTimeToUnixTimeStamp(utcNow.AddDays(-2));
                        lock (_chartLock)
                        {
                            foreach (var chart in Charts)
                            {
                                foreach (var series in chart.Value.Series)
                                {
                                    // trim data that's older than 2 days
                                    series.Value.Values =
                                        (from v in series.Value.Values
                                         where v.x > timeLimitUtc
                                         select v).ToList();
                                }
                            }
                        }
                        _nextChartTrimming = DateTime.UtcNow.AddMinutes(10);
                        Log.Debug("LiveTradingResultHandler.Update(): Finished trimming charts");
                    }

                    // Store the output for performance analysers
                    if (utcNow > _nextAnalyserOutput)
                    {
                        Log.Debug("LiveTradingResultHandler.Update(): Storing performance analysers' output");
                        var chartComplete = new Dictionary<string, Chart>();
                        lock (_chartLock)
                        {
                            foreach (var chart in Charts)
                            {
                                // remove directory pathing characters from chart names
                                var safeName = chart.Value.Name.Replace('/', '-');
                                DictionarySafeAdd(chartComplete, safeName, chart.Value.Clone(), "chartComplete");
                            }
                        }
                        //var orders = new Dictionary<int, Order>(_transactionHandler.Orders);
                        var orders = _transactionHandler.Orders.ToDictionary(t => t.Key.ToString(), t => t.Value);
                        var complete = new LivePerformanceFragmentPacket(_job, new LivePerformanceFragmentResult(chartComplete, orders, _algorithm.Transactions.TransactionRecord, holdings, _algorithm.Portfolio.CashBook, deltaStatistics, runtimeStatistics));  
                        
                        foreach (var analyser in _analysisAttribs)
                        {
                            StorePerformanceAnalyserOutput(complete, analyser);
                        }
                        _nextAnalyserOutput = DateTime.UtcNow.AddMinutes(1);
                        Log.Debug("LiveTradingResultHandler.Update(): Finished storing performance analysers' output");
                    }
                    
                }
                catch (Exception err)
                {
                    Log.Error(err, "LiveTradingResultHandler().Update(): ", true);
                }

                //Set the new update time after we've finished processing.
                // The processing can takes time depending on how large the packets are.
                _nextUpdate = DateTime.UtcNow.AddSeconds(3);
            } // End Update Charts:
        }

        private void TakePerformanceSnapshot()
        {
            var utcNow = DateTime.UtcNow;
            foreach (var analyserAttribute in _analysisAttribs)
            {
                analyserAttribute.UpdateRolloverPeriod();
                if (utcNow > analyserAttribute.StartDate && utcNow - analyserAttribute.StartDate <= TimeSpan.FromSeconds(3))
                {
                    analyserAttribute.TakePortfolioSnapshot(_algorithm);
                }
            }
        }


        /// <summary>
        /// Run over all the data and break it into smaller packets to ensure they all arrive at the terminal
        /// </summary>
        private IEnumerable<LiveResultPacket> SplitPackets(Dictionary<string, Chart> deltaCharts,
            Dictionary<string, Order> deltaOrders,
            Dictionary<string, Holding> holdings,
            CashBook cashbook,
            Dictionary<string, string> deltaStatistics,
            Dictionary<string, string> runtimeStatistics,
            Dictionary<string, string> serverStatistics)
        {
            // break the charts into groups

            var groupSize = 3;
            var current = new Dictionary<string, Chart>();
            var chartPackets = new List<LiveResultPacket>();

            // First add send charts

            // Loop through all the charts, add them to packets to be sent.
            // Group three charts to a packets, and add in the data to the chart depending on the subscription.

            foreach (var deltaChart in deltaCharts.Values)
            {
                var chart = new Chart(deltaChart.Name);
                current.Add(deltaChart.Name, chart);

                if (deltaChart.Name == _subscription || (_subscription == "*" && deltaChart.Name == "Strategy Equity"))
                {
                    chart.Series = deltaChart.Series;
                }

                // If there is room left in the group. add the subscription
                // to the packet unless it is a wildcard subscription
                if (current.Count >= groupSize && _subscription != "*")
                {
                    // Add the micro packet to transport.
                    chartPackets.Add(new LiveResultPacket(_job, new LiveResult { Charts = current }));
                    // Reset the carrier variable.
                    current = new Dictionary<string, Chart>();
                }
            }

            // Add whatever is left over here too
            // unless it is a wildcard subscription
            if (current.Count > 0 && _subscription != "*")
            {
                chartPackets.Add(new LiveResultPacket(_job, new LiveResult { Charts = current}));
            }

            // these are easier to split up, not as big as the chart objects
            var packets = new[]
            {
                new LiveResultPacket(_job, new LiveResult { Orders = deltaOrders}),
                new LiveResultPacket(_job, new LiveResult { Holdings = holdings, Cash = cashbook}),
                new LiveResultPacket(_job, new LiveResult
                {
                    Statistics = deltaStatistics,
                    RuntimeStatistics = runtimeStatistics,
                    ServerStatistics = serverStatistics,
                    AlphaRuntimeStatistics = AlphaRuntimeStatistics
                })
            };

            return packets.Concat(chartPackets);
        }


        /// <summary>
        /// Send a live trading debug message to the live console.
        /// </summary>
        /// <param name="message">Message we'd like shown in console.</param>
        /// <remarks>When there are already 500 messages in the queue it stops adding new messages.</remarks>
        public void DebugMessage(string message)
        {
            if (Messages.Count > 500) return; //if too many in the queue already skip the logging.
            Messages.Enqueue(new DebugPacket(_job.ProjectId, _deployId, _compileId, message));
            AddToLogStore(message);
        }

        /// <summary>
        /// Send a live trading system debug message to the live console.
        /// </summary>
        /// <param name="message">Message we'd like shown in console.</param>
        public void SystemDebugMessage(string message)
        {
            Messages.Enqueue(new SystemDebugPacket(_job.ProjectId, _deployId, _compileId, message));
            AddToLogStore(message);
        }


        /// <summary>
        /// Log string messages and send them to the console.
        /// </summary>
        /// <param name="message">String message wed like logged.</param>
        /// <remarks>When there are already 500 messages in the queue it stops adding new messages.</remarks>
        public void LogMessage(string message)
        {
            //Send the logging messages out immediately for live trading:
            if (Messages.Count > 500) return;
            Messages.Enqueue(new LogPacket(_deployId, message));
            AddToLogStore(message);
        }

        /// <summary>
        /// Save an algorithm message to the log store. Uses a different timestamped method of adding messaging to interweve debug and logging messages.
        /// </summary>
        /// <param name="message">String message to send to browser.</param>
        private void AddToLogStore(string message)
        {
            Log.Debug("LiveTradingResultHandler.AddToLogStore(): Adding");
            lock (_logStoreLock)
            {
                _logStore.Add(new LogEntry(DateTime.Now.ToString(DateFormat.UI) + " " + message));
            }
            Log.Debug("LiveTradingResultHandler.AddToLogStore(): Finished adding");
        }

        /// <summary>
        /// Send an error message back to the browser console and highlight it read.
        /// </summary>
        /// <param name="message">Message we'd like shown in console.</param>
        /// <param name="stacktrace">Stacktrace to show in the console.</param>
        public void ErrorMessage(string message, string stacktrace = "")
        {
            if (Messages.Count > 500) return;
            Messages.Enqueue(new HandledErrorPacket(_deployId, message, stacktrace));
            AddToLogStore(message + (!string.IsNullOrEmpty(stacktrace) ? ": StackTrace: " + stacktrace : string.Empty));
        }

        /// <summary>
        /// Send a list of secutity types that the algorithm trades to the browser to show the market clock - is this market open or closed!
        /// </summary>
        /// <param name="types">List of security types</param>
        public void SecurityType(List<SecurityType> types)
        {
            var packet = new SecurityTypesPacket { Types = types };
            Messages.Enqueue(packet);
        }

        /// <summary>
        /// Send a runtime error back to the users browser and highlight it red.
        /// </summary>
        /// <param name="message">Runtime error message</param>
        /// <param name="stacktrace">Associated error stack trace.</param>
        public void RuntimeError(string message, string stacktrace = "")
        {
            Messages.Enqueue(new RuntimeErrorPacket(_job.UserId, _deployId, message, stacktrace));
            AddToLogStore(message + (!string.IsNullOrEmpty(stacktrace) ? ": StackTrace: " + stacktrace : string.Empty));
        }

        /// <summary>
        /// 
        /// </summary>
        public bool IsPushOptionPrice { get; set; } = false;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contract"></param>
        public void OptionPrice(OptionContract contract)
        {
            if ((contract.BidPrice == 0 && contract.AskPrice == 0) || contract.UnderlyingLastPrice == 0)
            {
                return;
            }
            if (_algorithm?.Portfolio == null ||
                !_algorithm.Portfolio.Securities.ContainsKey(contract.Symbol))
            {
                return;
            }

            var security = _algorithm.Portfolio.Securities[contract.Symbol];

            decimal holding = security.Holdings.AbsoluteQuantity
                              + security.LongHoldings.AbsoluteQuantity
                              + security.ShortHoldings.AbsoluteQuantity;

            if(holding ==0) return;

            if (OptionPriceMap.ContainsKey(contract.Symbol))
            {
                var v = OptionPriceMap[contract.Symbol];
                if (v.Price == contract.LastPrice &&
                    v.UnderlyingPrice == contract.UnderlyingLastPrice &&
                    v.Holding == holding)
                {
                    return;
                }
                //时间间隔
                var timeSpan = (contract.Time - v.Time).TotalSeconds;
                if (timeSpan < 30) return;
            }

            var data = new OptionPriceMarketData(contract);
            data.Holding = holding;
            data.ContractMultiplier = security.SymbolProperties.ContractMultiplier;
            OptionPriceMap[contract.Symbol] = data;
        }

        /// <summary>   
        /// Add a sample to the chart specified by the chartName, and seriesName.
        /// </summary>
        /// <param name="chartName">String chart name to place the sample.</param>
        /// <param name="seriesName">Series name for the chart.</param>
        /// <param name="seriesIndex">Series chart index - which chart should this series belong</param>
        /// <param name="seriesType">Series type for the chart.</param>
        /// <param name="time">Time for the sample</param>
        /// <param name="value">Value for the chart sample.</param>
        /// <param name="unit">Unit for the chart axis</param>
        /// <remarks>Sample can be used to create new charts or sample equity - daily performance.</remarks>
        public void Sample(string chartName, string seriesName, int seriesIndex, SeriesType seriesType, DateTime time, decimal value, string unit = "$")
        {
            // Sampling during warming up period skews statistics
            if (_algorithm.IsWarmingUp)
            {
                return;
            }

            Log.Debug("LiveTradingResultHandler.Sample(): Sampling " + chartName + "." + seriesName);
            lock (_chartLock)
            {
                //Add a copy locally:
                if (!Charts.ContainsKey(chartName))
                {
                    Charts.AddOrUpdate(chartName, new Chart(chartName));
                }

                //Add the sample to our chart:
                if (!Charts[chartName].Series.ContainsKey(seriesName))
                {
                    Charts[chartName].Series.Add(seriesName, new Series(seriesName, seriesType, seriesIndex, unit));
                }

                //Add our value:
                Charts[chartName].Series[seriesName].Values.Add(new ChartPoint(time, value));
            }
            Log.Debug("LiveTradingResultHandler.Sample(): Done sampling " + chartName + "." + seriesName);
        }

        /// <summary>
        /// Wrapper methond on sample to create the equity chart.
        /// </summary>
        /// <param name="time">Time of the sample.</param>
        /// <param name="value">Equity value at this moment in time.</param>
        /// <seealso cref="Sample(string,string,int,SeriesType,DateTime,decimal,string)"/>
        public void SampleEquity(DateTime time, decimal value)
        {
            if (value > 0)
            {
                Log.Debug("LiveTradingResultHandler.SampleEquity(): " + time.ToShortTimeString() + " >" + value);
                Sample("Strategy Equity", "Equity", 0, SeriesType.Candle, time, value);
            }
        }

        /// <summary>
        /// Sample the asset prices to generate plots.
        /// </summary>
        /// <param name="symbol">Symbol we're sampling.</param>
        /// <param name="time">Time of sample</param>
        /// <param name="value">Value of the asset price</param>
        /// <seealso cref="Sample(string,string,int,SeriesType,DateTime,decimal,string)"/>
        public virtual void SampleAssetPrices(Symbol symbol, DateTime time, decimal value)
        {
            // don't send stockplots for internal feeds
            Security security;
            if (_algorithm.Securities.TryGetValue(symbol, out security) && !security.IsInternalFeed() && value > 0)
            {
                var now = DateTime.UtcNow.ConvertFromUtc(security.Exchange.TimeZone);
                if (security.Exchange.Hours.IsOpen(now, security.IsExtendedMarketHours))
                {
                    Sample("Stockplot: " + symbol.Value, "Stockplot: " + symbol.Value, 0, SeriesType.Line, time, value);
                }
            }
        }

        /// <summary>
        /// Sample the current daily performance directly with a time-value pair.
        /// </summary>
        /// <param name="time">Current backtest date.</param>
        /// <param name="value">Current daily performance value.</param>
        /// <seealso cref="Sample(string,string,int,SeriesType,DateTime,decimal,string)"/>
        public void SamplePerformance(DateTime time, decimal value)
        {
            //No "daily performance" sampling for live trading yet.
            //Log.Debug("LiveTradingResultHandler.SamplePerformance(): " + time.ToShortTimeString() + " >" + value);
            //Sample("Strategy Equity", ChartType.Overlay, "Daily Performance", SeriesType.Line, time, value, "%");
        }

        /// <summary>
        /// Sample the current benchmark performance directly with a time-value pair.
        /// </summary>
        /// <param name="time">Current backtest date.</param>
        /// <param name="value">Current benchmark value.</param>
        /// <seealso cref="IResultHandler.Sample"/>
        public virtual void SampleBenchmark(DateTime time, decimal value)
        {
            Sample("Benchmark", "Benchmark", 0, SeriesType.Line, time, value);
        }

        /// <summary>
        /// Add a range of samples from the users algorithms to the end of our current list.
        /// </summary>
        /// <param name="updates">Chart updates since the last request.</param>
        /// <seealso cref="Sample(string,string,int,SeriesType,DateTime,decimal,string)"/>
        public void SampleRange(List<Chart> updates)
        {
            Log.Debug("LiveTradingResultHandler.SampleRange(): Begin sampling");
            lock (_chartLock)
            {
                foreach (var update in updates)
                {
                    //Create the chart if it doesn't exist already:
                    Chart chart;
                    if (!Charts.TryGetValue(update.Name, out chart))
                    {
                        chart = new Chart(update.Name);
                        Charts.AddOrUpdate(update.Name, chart);
                    }

                    // for alpha assets chart, we always create a new series instance (step on previous value)
                    var forceNewSeries = update.Name == ChartingInsightManagerExtension.AlphaAssets;

                    //Add these samples to this chart.
                    foreach (var series in update.Series.Values)
                    {
                        if (series.Values.Count > 0)
                        {
                            var thisSeries = chart.TryAddAndGetSeries(series.Name, series.SeriesType, series.Index,
                                series.Unit, series.Color, series.ScatterMarkerSymbol,
                                forceNewSeries);
                            if (series.SeriesType == SeriesType.Pie)
                            {
                                var dataPoint = series.ConsolidateChartPoints();
                                if (dataPoint != null)
                                {
                                    thisSeries.AddPoint(dataPoint);
                                }
                            }
                            else
                            {
                                //We already have this record, so just the new samples to the end:
                                thisSeries.Values.AddRange(series.Values);
                            }
                        }
                    }
                }
            }
            Log.Debug("LiveTradingResultHandler.SampleRange(): Finished sampling");
        }

        /// <summary>
        /// Set the algorithm of the result handler after its been initialized.
        /// </summary>
        /// <param name="algorithm">Algorithm object matching IAlgorithm interface</param>
        public void SetAlgorithm(IAlgorithm algorithm)
        {
            _algorithm = algorithm;

            var types = new List<SecurityType>();
            foreach (var kvp in _algorithm.Securities)
            {
                var security = kvp.Value;

                if (!types.Contains(security.Type)) types.Add(security.Type);
            }
            SecurityType(types);

            // we need to forward Console.Write messages to the algorithm's Debug function
            var debug = new FuncTextWriter(algorithm.Debug);
            var error = new FuncTextWriter(algorithm.Error);
            Console.SetOut(debug);
            Console.SetError(error);

            UpdateAlgorithmStatus();

            // Setup the performanceAnalyserAttribute for this algorithm when its set
            _analysisAttribs = 
            algorithm.GetType().GetCustomAttributes(typeof(PerformanceAnalyserAttribute), true).Cast<PerformanceAnalyserAttribute>();

            String uniqueId = "";
            var accountNameMembers = algorithm.GetType().GetMember("AccountName");
            if (accountNameMembers.Any())
            {   
                var accountNameMember = accountNameMembers.FirstOrDefault();
                switch (accountNameMember.MemberType)
                {
                    case MemberTypes.Field:
                        uniqueId = ((FieldInfo)accountNameMember).GetValue(algorithm).ToString();
                        break;
                    case MemberTypes.Property:
                        uniqueId = ((PropertyInfo)accountNameMember).GetValue(algorithm).ToString();
                        break;
                    default:
                        uniqueId = "";
                        break;
                }
            }

            foreach (var analyserAttribute in _analysisAttribs)
            {
                string prefix = "";
                if (!string.IsNullOrEmpty(uniqueId))
                {
                    prefix += uniqueId;
                }
                
                // This is not a bug. We just want to use the account name as folder name to identify the results.
                // analyserAttribute.LiveAlgorithmID = prefix + algorithm.GetType().Name;
                analyserAttribute.LiveAlgorithmID = algorithm.GetType().Name;
                analyserAttribute.DestOutputFolder = Path.Combine(analyserAttribute.DestOutputFolder, prefix);
                analyserAttribute.InitRolloverPeriod();
            }
        }


        /// <summary>
        /// Send a algorithm status update to the user of the algorithms running state.
        /// </summary>
        /// <param name="status">Status enum of the algorithm.</param>
        /// <param name="message">Optional string message describing reason for status change.</param>
        public void SendStatusUpdate(AlgorithmStatus status, string message = "")
        {
            var msg = status + (string.IsNullOrEmpty(message) ? string.Empty : " " + message);
            Log.Trace("LiveTradingResultHandler.SendStatusUpdate(): " + msg);
            var packet = new AlgorithmStatusPacket(_job.AlgorithmId, _job.ProjectId, status, message);
            Messages.Enqueue(packet);
        }


        /// <summary>
        /// Set a dynamic runtime statistic to show in the (live) algorithm header
        /// </summary>
        /// <param name="key">Runtime headline statistic name</param>
        /// <param name="value">Runtime headline statistic value</param>
        public void RuntimeStatistic(string key, string value)
        {
            Log.Debug("LiveTradingResultHandler.RuntimeStatistic(): Begin setting statistic");
            lock (_runtimeLock)
            {
                if (!_runtimeStatistics.ContainsKey(key))
                {
                    _runtimeStatistics.Add(key, value);
                }
                _runtimeStatistics[key] = value;
            }
            Log.Debug("LiveTradingResultHandler.RuntimeStatistic(): End setting statistic");
        }

        /// <summary>
        /// Send a final analysis result back to the IDE.
        /// </summary>
        /// <param name="job">Lean AlgorithmJob task</param>
        /// <param name="orders">Collection of orders from the algorithm</param>
        /// <param name="profitLoss">Collection of time-profit values for the algorithm</param>
        /// <param name="holdings">Current holdings state for the algorithm</param>
        /// <param name="cashbook">Cashbook of the current cash of the algorithm</param>
        /// <param name="statisticsResults">Statistics information for the algorithm (empty if not finished)</param>
        /// <param name="runtime">Runtime statistics banner information</param>
        public void SendFinalResult(AlgorithmNodePacket job, Dictionary<string, Order> orders, Dictionary<DateTime, decimal> profitLoss, Dictionary<string, Holding> holdings, CashBook cashbook, StatisticsResults statisticsResults, Dictionary<string, string> runtime)
        {
            Log.Trace("LiveTradingResultHandler.SendFinalResult(): Starting...");
            try
            {
                //Convert local dictionary:
                var charts = new Dictionary<string, Chart>();
                lock (_chartLock)
                {
                    foreach (var kvp in Charts)
                    {
                        charts.Add(kvp.Key, kvp.Value.Clone());
                    }
                }

                //Create a packet:
                var result = new LiveResultPacket((LiveNodePacket) job,
                    new LiveResult(charts, orders, profitLoss, holdings, cashbook, statisticsResults.Summary, runtime))
                {
                    ProcessingTime = (DateTime.UtcNow - _startTime).TotalSeconds
                };

                //Save the processing time:

                //Store to S3:
                if (_storeResult)
                {
                    StoreResult(result, false);
                }
                Log.Trace("LiveTradingResultHandler.SendFinalResult(): Finished storing results. Start sending...");
                //Truncate packet to fit within 32kb:
                result.Results = new LiveResult();

                //Send the truncated packet:
                _messagingHandler.Send(result);
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
            Log.Trace("LiveTradingResultHandler.SendFinalResult(): Ended");
        }


        /// <summary>
        /// Process the log entries and save it to permanent storage
        /// </summary>
        /// <param name="logs">Log list</param>
        public void StoreLog(IEnumerable<LogEntry> logs)
        {
            try
            {
                SaveLogs(_job.DeployId, logs.Select(x => x.Message));
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

        /// <summary>
        /// Save the snapshot of the total results to storage.
        /// </summary>
        /// <param name="packet">Packet to store.</param>
        /// <param name="async">Store the packet asyncronously to speed up the thread.</param>
        /// <remarks>
        ///     Async creates crashes in Mono 3.10 if the thread disappears before the upload is complete so it is disabled for now.
        ///     For live trading we're making assumption its a long running task and safe to async save large files.
        /// </remarks>
        public void StoreResult(Packet packet, bool async = true)
        {
            try
            {
                Log.Debug("LiveTradingResultHandler.StoreResult(): Begin store result sampling");

                // Make sure this is the right type of packet:
                if (packet.Type != PacketType.LiveResult) return;

                // Port to packet format:
                var live = packet as LiveResultPacket;

                if (live != null)
                {
                    live.Results.AlphaRuntimeStatistics = AlphaRuntimeStatistics;

                    // we need to down sample
                    var start = DateTime.UtcNow.Date;
                    var stop = start.AddDays(1);

                    // truncate to just today, we don't need more than this for anyone
                    Truncate(live.Results, start, stop);

                    var highResolutionCharts = new Dictionary<string, Chart>(live.Results.Charts);

                    // minute resolution data, save today
                    var minuteSampler = new SeriesSampler(TimeSpan.FromMinutes(1));
                    var minuteCharts = minuteSampler.SampleCharts(live.Results.Charts, start, stop);

                    // swap out our charts with the sampled data
                    live.Results.Charts = minuteCharts;
                    SaveResults(CreateKey("minute"), live.Results);

                    // 10 minute resolution data, save today
                    var tenminuteSampler = new SeriesSampler(TimeSpan.FromMinutes(10));
                    var tenminuteCharts = tenminuteSampler.SampleCharts(live.Results.Charts, start, stop);

                    live.Results.Charts = tenminuteCharts;
                    SaveResults(CreateKey("10minute"), live.Results);

                    // high resolution data, we only want to save an hour
                    live.Results.Charts = highResolutionCharts;
                    start = DateTime.UtcNow.RoundDown(TimeSpan.FromHours(1));
                    stop = DateTime.UtcNow.RoundUp(TimeSpan.FromHours(1));

                    Truncate(live.Results, start, stop);

                    foreach (var name in live.Results.Charts.Keys)
                    {
                        var result = new LiveResult
                        {
                            Orders = new Dictionary<string, Order>(live.Results.Orders),
                            Holdings = new Dictionary<string, Holding>(live.Results.Holdings),
                            Charts = new Dictionary<string, Chart> {{name, live.Results.Charts[name]}}
                        };

                        SaveResults(CreateKey("second_" + CreateSafeChartName(name), "yyyy-MM-dd-HH"), result);
                    }
                }
                else
                {
                    Log.Error("LiveResultHandler.StoreResult(): Result Null.");
                }

                Log.Debug("LiveTradingResultHandler.StoreResult(): End store result sampling");
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

        /// <summary>
        /// Save the snapshot of the total results for performance analyser attributes
        /// </summary>
        /// <param name="packet">Packet to store.</param>
        public void StorePerformanceAnalyserOutput(Packet packet, PerformanceAnalyserAttribute analyser)
        {
            try
            {
                Log.Debug("LiveTradingResultHandler.StorePerformanceAnalyserOutput(): Begin store result sampling");

                // Make sure this is the right type of packet:
                if (packet.Type != PacketType.LivePerformanceFragment) return;

                // Port to packet format:
                var livePerformance = packet as LivePerformanceFragmentPacket;

                if (livePerformance != null)
                {
                    livePerformance.Results.AlphaRuntimeStatistics = AlphaRuntimeStatistics;

                    // we need to down sample
                    var start = analyser.StartDate.GetValueOrDefault();
                    var stop = analyser.EndDate.GetValueOrDefault();

                    // truncate to just today, we don't need more than this for anyone
                    Truncate(ref livePerformance.Results.Charts, ref livePerformance.Results.Orders, start, stop);

                    // TODO: Since the backtesting now can only support minute resolution, we need to truncate the results to the nearest minute
                    var minuteSampler = new SeriesSampler(TimeSpan.FromMinutes(1));
                    var minuteCharts = minuteSampler.SampleCharts(livePerformance.Results.Charts, start, stop);

                    // swap out our charts with the sampled data
                    livePerformance.Results.Charts = minuteCharts;
                    livePerformance.Results.Cash = analyser.InitialCashBook;
                    livePerformance.Results.Holdings = analyser.InitialHoldings;
                    var qc_algo = _algorithm as QCAlgorithm;
                    if (qc_algo != null)
                    {
                        var BindingFlags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
                        var members = qc_algo.GetType().GetFields(BindingFlags).Concat<MemberInfo>(qc_algo.GetType().GetProperties(BindingFlags));
                        var parameters = new Dictionary<string, string>();
                        foreach (var memberInfo in members)
                        {
                            if (memberInfo.GetCustomAttribute<ParameterAttribute>() != null)
                            {
                                var fieldInfo = memberInfo as FieldInfo;
                                var propertyInfo = memberInfo as PropertyInfo;
                                var parameterValue = "";
                                if (fieldInfo != null)
                                {
                                    parameterValue = fieldInfo.GetValue(qc_algo).ToString();
                                }
                                else
                                {
                                    parameterValue = propertyInfo.GetValue(qc_algo).ToString();
                                }
                                var attribute = memberInfo.GetCustomAttribute<ParameterAttribute>();
                                parameters.Add(attribute.Name ?? memberInfo.Name, parameterValue);
                            }
                        }
                        livePerformance.Results.Parameters = parameters;
                    }
                    
                    if (!System.IO.Directory.Exists(analyser.DestOutputFolder))
                    {
                        System.IO.Directory.CreateDirectory(analyser.DestOutputFolder);
                    }
                    SavePerformanceAnalyser(Path.Combine(analyser.DestOutputFolder, analyser.DestOutputFileName), livePerformance.Results);
                }
                else
                {
                    Log.Error("LiveResultHandler.StorePerformanceAnalyserOutput(): Result Null.");
                }

                Log.Debug("LiveTradingResultHandler.StorePerformanceAnalyserOutput(): End store result sampling");
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

        /// <summary>
        /// New order event for the algorithm backtest: send event to browser.
        /// </summary>
        /// <param name="newEvent">New event details</param>
        public void OrderEvent(OrderEvent newEvent)
        {
            // we'll pull these out for the deltaOrders
            _orderEvents.Enqueue(newEvent);

            //Send the message to frontend as packet:
            Log.Trace("LiveTradingResultHandler.OrderEvent(): " + newEvent, true);
            Messages.Enqueue(new OrderEventPacket(_deployId, newEvent));

            var message = "New Order Event: " + newEvent;
            DebugMessage(message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="trade"></param>
        public void OnTrade(TradeRecord trade)
        {
            Log.Trace("LiveTradingResultHandler.OnTrade(): " + trade, true);
            Messages.Enqueue(new TradeRecordPacket(_deployId, trade));
        }

        /// <summary>
        /// Terminate the result thread and apply any required exit procedures.
        /// </summary>
        public void Exit()
        {
            if (!_exitTriggered)
            {
                _exitTriggered = true;
                _cancellationTokenSource.Cancel();

                if (_algorithm != null)
                {
                    ProcessSynchronousEvents(true);
                }

                ExitEvent.Set();

                lock (_logStoreLock)
                {
                    StoreLog(_logStore);
                }
            }
        }

        /// <summary>
        /// Purge/clear any outstanding messages in message queue.
        /// </summary>
        public void PurgeQueue()
        {
            Messages.Clear();
        }

        /// <summary>
        /// Truncates the chart and order data in the result packet to within the specified time frame
        /// </summary>
        private static void Truncate(LiveResult result, DateTime start, DateTime stop)
        {
            var unixDateStart = Time.DateTimeToUnixTimeStamp(start);
            var unixDateStop = Time.DateTimeToUnixTimeStamp(stop);

            //Log.Trace("LiveTradingResultHandler.Truncate: Start: " + start.ToString("u") + " Stop : " + stop.ToString("u"));
            //Log.Trace("LiveTradingResultHandler.Truncate: Truncate Delta: " + (unixDateStop - unixDateStart) + " Incoming Points: " + result.Charts["Strategy Equity"].Series["Equity"].Values.Count);

            var charts = new Dictionary<string, Chart>();
            foreach (var kvp in result.Charts)
            {
                var chart = kvp.Value;
                var newChart = new Chart(chart.Name, chart.ChartType);
                charts.Add(kvp.Key, newChart);
                foreach (var series in chart.Series.Values)
                {
                    var newSeries = new Series(series.Name, series.SeriesType);
                    newSeries.Values.AddRange(series.Values.Where(chartPoint => chartPoint.x >= unixDateStart && chartPoint.x <= unixDateStop));
                    newChart.AddSeries(newSeries);
                }
            }
            result.Charts = charts;
            result.Orders = result.Orders.Values.Where(x => x.Time >= start && x.Time <= stop).ToDictionary(x => x.Id.ToString());

            //Log.Trace("LiveTradingResultHandler.Truncate: Truncate Outgoing: " + result.Charts["Strategy Equity"].Series["Equity"].Values.Count);

            //For live charting convert to UTC
            foreach (var order in result.Orders)
            {
                order.Value.Time = order.Value.Time.ToUniversalTime();
            }
        }

        /// <summary>
        /// Truncates the chart and order data in the result packet to within the specified time frame
        /// </summary>
        private static void Truncate(ref IDictionary<string, Chart> charts, ref IDictionary<string, Order> orders, DateTime start, DateTime stop)
        {
            var unixDateStart = Time.DateTimeToUnixTimeStamp(start);
            var unixDateStop = Time.DateTimeToUnixTimeStamp(stop);

            var newCharts = new Dictionary<string, Chart>();
            foreach (var kvp in charts)
            {
                var chart = kvp.Value;
                var newChart = new Chart(chart.Name);
                foreach (var series in chart.Series.Values)
                {
                    var newSeries = new Series(series.Name, series.SeriesType);
                    newSeries.Values.AddRange(series.Values.Where(chartPoint => chartPoint.x >= unixDateStart && chartPoint.x <= unixDateStop));
                    newChart.AddSeries(newSeries);
                }
                newCharts.Add(kvp.Key, newChart);
            }
            charts = newCharts;
            orders = orders.Values.Where(x => x.Time >= start && x.Time <= stop).ToDictionary(x => x.Id.ToString());

            //Log.Trace("LiveTradingResultHandler.Truncate: Truncate Outgoing: " + result.Charts["Strategy Equity"].Series["Equity"].Values.Count);

            //For live charting convert to UTC
            foreach (var order in orders)
            {
                order.Value.Time = order.Value.Time.ToUniversalTime();
            }
        }

        private string CreateKey(string suffix, string dateFormat = "yyyy-MM-dd")
        {
            return $"{_job.DeployId}-{DateTime.UtcNow.ToString(dateFormat)}_{suffix}.json";
        }

        /// <summary>
        /// Escape the chartname so that it can be saved to a file system
        /// </summary>
        /// <param name="chartName">The name of a chart</param>
        /// <returns>The name of the chart will all escape all characters except RFC 2396 unreserved characters</returns>
        protected virtual string CreateSafeChartName(string chartName)
        {
            return Uri.EscapeDataString(chartName);
        }


        /// <summary>
        /// Set the chart name that we want data from.
        /// </summary>
        public void SetChartSubscription(string symbol)
        {
            _subscription = symbol;
        }

        /// <summary>
        /// Process the synchronous result events, sampling and message reading.
        /// This method is triggered from the algorithm manager thread.
        /// </summary>
        /// <remarks>Prime candidate for putting into a base class. Is identical across all result handlers.</remarks>
        public void ProcessSynchronousEvents(bool forceProcess = false)
        {
            var time = _algorithm.SimulationMode ? _algorithm.Time : DateTime.UtcNow;

            SampleRangeCustomerChart(_algorithm.GetCustomerChartDataAdds());
            SampleRangeGreeksChart(_algorithm.GetGreeksChartData());

            if (time > _nextSample || forceProcess)
            {
                Log.Debug("LiveTradingResultHandler.ProcessSynchronousEvents(): Enter");

                //Set next sample time: 4000 samples per backtest
                _nextSample = time.Add(ResamplePeriod);

                //Update the asset prices to take a real time sample of the market price even though we're using minute bars
                if (DataManager != null)
                {
                    foreach (var subscription in DataManager.DataFeedSubscriptions)
                    {
                        var symbol = subscription.Configuration.Symbol;
                        var tickType = subscription.Configuration.TickType;

                        // OI subscription doesn't contain asset market prices
                        if (tickType == TickType.OpenInterest)
                            continue;

                        Security security;
                        if (_algorithm.Securities.TryGetValue(symbol, out security))
                        {
                            //Sample Portfolio Value:
                            var price = subscription.RealtimePrice;

                            var last = security.GetLastData();
                            if (last != null && price > 0)
                            {
                                // Prevents changes in previous bar
                                last = last.Clone(last.IsFillForward);

                                last.Value = price;
                                security.SetRealTimePrice(last);

                                // Update CashBook for Forex securities
                                var cash = (from c in _algorithm.Portfolio.CashBook
                                    where c.Value.SecuritySymbol == last.Symbol
                                    select c.Value).SingleOrDefault();

                                cash?.Update(last);
                            }
                            else
                            {
                                // we haven't gotten data yet so just spoof a tick to push through the system to start with
                                if (price > 0)
                                {
                                    security.SetMarketPrice(new Tick(time, symbol, price, price) { TickType = tickType });
                                }
                            }

                            //Sample Asset Pricing: DO NOT SAMPLE ASSET PRICING FOR IT COSTING TOO MUCH RESOURCES!!!
                            //SampleAssetPrices(symbol, time, price);
                        }
                    }
                }

                //Sample the portfolio value over time for chart.
                SampleEquity(time, Math.Round(_algorithm.Portfolio.TotalPortfolioValue, 4));

                //Also add the user samples / plots to the result handler tracking:
                SampleRange(_algorithm.GetChartUpdates(true));
            }

            //Send out the debug messages:
            var debugStopWatch = Stopwatch.StartNew();
            while (_algorithm.DebugMessages.Count > 0 && debugStopWatch.ElapsedMilliseconds < 250)
            {
                if (_algorithm.DebugMessages.TryDequeue(out var message))
                {
                    DebugMessage(message);
                }
            }

            //Send out the error messages:
            var errorStopWatch = Stopwatch.StartNew();
            while (_algorithm.ErrorMessages.Count > 0 && errorStopWatch.ElapsedMilliseconds < 250)
            {
                if (_algorithm.ErrorMessages.TryDequeue(out var message))
                {
                    ErrorMessage(message);
                }
            }

            //Send out the log messages:
            var logStopWatch = Stopwatch.StartNew();
            while (_algorithm.LogMessages.Count > 0 && logStopWatch.ElapsedMilliseconds < 250)
            {
                if (_algorithm.LogMessages.TryDequeue(out var message))
                {
                    LogMessage(message);
                }
            }

            //Set the running statistics:
            foreach (var pair in _algorithm.RuntimeStatistics)
            {
                RuntimeStatistic(pair.Key, pair.Value);
            }

            //Send all the notification messages but timeout within a second, or if this is a force process, wait till its done.
            var start = DateTime.UtcNow;
            while (_algorithm.Notify.Messages.Count > 0 && (DateTime.UtcNow < start.AddSeconds(1) || forceProcess))
            {
                if (_algorithm.Notify.Messages.TryDequeue(out var message))
                {
                    //Process the notification messages:
                    Log.Trace("LiveTradingResultHandler.ProcessSynchronousEvents(): Processing Notification...");
                    try
                    {
                        _messagingHandler.SendNotification(message);
                    }
                    catch (Exception err)
                    {
                        Log.Error(err, "Sending notification: " + message.GetType().FullName);
                    }
                }
            }

            Log.Debug("LiveTradingResultHandler.ProcessSynchronousEvents(): Exit");
        }

        private static void DictionarySafeAdd<T>(Dictionary<string, T> dictionary, string key, T value, string dictionaryName)
        {
            if (dictionary.ContainsKey(key))
            {
                Log.Error($"LiveTradingResultHandler.DictionarySafeAdd(): dictionary {dictionaryName} already contains key {key}");
            }
            else
            {
                dictionary.Add(key, value);
            }
        }

        /// <summary>
        /// Will launch a task which will call the API and update the algorithm status every minute
        /// </summary>
        private void UpdateAlgorithmStatus()
        {
            if (!_exitTriggered
                && !_cancellationTokenSource.IsCancellationRequested) // just in case
            {
                // wait until after we're warmed up to start sending running status each minute
                if (!_algorithm.IsWarmingUp)
                {
                    _api.SetAlgorithmStatus(_job.AlgorithmId, AlgorithmStatus.Running);
                }
                Task.Delay(TimeSpan.FromMinutes(1), _cancellationTokenSource.Token).ContinueWith(_ => UpdateAlgorithmStatus());
            }
        }

        /// <summary>
        /// Add a range of samples from the users algorithms to the end of our current list.
        /// </summary>
        /// <param name="customerChartDatas">customerChartDatas add since the last request.</param>
        public void SampleRangeCustomerChart(List<CustomerChartData> customerChartDatas)
        {
            lock (_customerChartLock)
            {
                foreach (var chart in customerChartDatas)
                {
                    if (!CustomerCharts.ContainsKey(chart.Name))
                    {
                        CustomerCharts[chart.Name] = new List<CustomerChartData>();
                    }
                    CustomerCharts[chart.Name].Add(chart);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        public void SampleRangeGreeksChart(List<GreeksChartData> data)
        {
            lock (_greeksChartLock)
            {
                foreach (var chart in data)
                {
                    GreeksCharts.AddOrUpdate(chart.DataTime.ToString(), chart);
                }
            }
        }
    }
}
