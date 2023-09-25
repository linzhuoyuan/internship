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
using System.Linq;
using System.Threading;
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Statistics;
using QuantConnect.Util;
using System.IO;
using QuantConnect.Lean.Engine.Alphas;
using QuantConnect.Securities;
using QuantConnect.Messaging;
using QuantConnect.Data.Market;
using Newtonsoft.Json;
using QuantConnect.Securities.Option;

namespace QuantConnect.Lean.Engine.Results
{
    /// <summary>
    /// Backtesting result handler passes messages back from the Lean to the User.
    /// </summary>
    public class BacktestingResultHandler : BaseResultsHandler, IResultHandler
    {
        private const string BtcChartName = "StrategyEquity2";

        // used for resetting out/error upon completion
        private static readonly TextWriter StandardOut = Console.Out;
        private static readonly TextWriter StandardError = Console.Error;

        private bool _exitTriggered;
        private BacktestNodePacket _job;
        private int _jobDays;
        private string _compileId = "";
        private string _backtestId = "";
        private DateTime _nextUpdate;
        private DateTime _nextS3Update;
        private DateTime _lastUpdate;
        private DateTime _lastUpdateTime;
        private readonly List<string> _log = new List<string>();
        private string _errorMessage = "";
        private readonly object _chartLock = new object();
        private readonly object _customerChartLock = new object();
        private readonly object _greeksChartLock = new object();
        private readonly object _greeksGnlChartLock = new object();
        private readonly object _runtimeLock = new object();
        private readonly Dictionary<string, string> _runtimeStatistics = new Dictionary<string, string>();
        private double _daysProcessed;
        private double _daysProcessedFrontier;
        private double _minutesProcessed;
        private double _minutesProcessedFrontier;
        private bool _processingFinalPacket;
        private readonly HashSet<string> _chartSeriesExceededDataPoints = new HashSet<string>();

        //Processing Time:
        private readonly DateTime _startTime = DateTime.UtcNow;
        private DateTime _nextSample;
        private IMessagingHandler _messagingHandler;
        //private IMessagingHandler _messagingHandlerOptionPrice;
        private ITransactionHandler _transactionHandler;
        private ISetupHandler _setupHandler;
        private string _algorithmId;
        private int _projectId;

        private const double Samples = 4000;
        private const double MinimumSamplePeriod = 4;

        private ConcurrentDictionary<long, long> _tradesCache = new ConcurrentDictionary<long, long>();

        /// <summary>
        /// Packeting message queue to temporarily store packets and then pull for processing.
        /// </summary>
        public ConcurrentQueue<Packet> Messages { get; set; } = new ConcurrentQueue<Packet>();

        /// <summary>
        /// 
        /// </summary>
        //public ConcurrentQueue<Packet> OptionPriceMessages { get; set; } = new ConcurrentQueue<Packet>();
        //public ConcurrentDictionary<Symbol, Tuple<decimal, decimal>> OptionPriceMap { get; set; } = new ConcurrentDictionary<Symbol, Tuple<decimal, decimal>>();
        public ConcurrentDictionary<Symbol, OptionPriceMarketData> OptionPriceMap = new ConcurrentDictionary<Symbol, OptionPriceMarketData>();

        /// <summary>
        /// Local object access to the algorithm for the underlying Debug and Error messaging.
        /// </summary>
        public IAlgorithm Algorithm { get; set; }

        /// <summary>
        /// Charts collection for storing the master copy of user charting data.
        /// </summary>
        public ConcurrentDictionary<string, Chart> Charts { get; set; } = new ConcurrentDictionary<string, Chart>();

        /// <summary>
        /// EchartCharts collection for storing the master copy of user echart  data.
        /// </summary>
        public ConcurrentDictionary<DateTime, EchartJson> Echarts { get; set; } = new ConcurrentDictionary<DateTime, EchartJson>();

        /// <summary>
        /// customer chart collection for storing the master copy of customer chart  data.
        /// </summary>
        public ConcurrentDictionary<string, List<CustomerChartData>> CustomerCharts { get; set; } = new ConcurrentDictionary<string, List<CustomerChartData>>();

        /// <summary>
        /// 
        /// </summary>
        public ConcurrentDictionary<string, GreeksChartData> GreeksCharts { get; set; } = new ConcurrentDictionary<string, GreeksChartData>();

        /// <summary>
        /// 
        /// </summary>
        public ConcurrentDictionary<string, GreeksPnlChartData> GreeksPnlCharts { get; set; } = new ConcurrentDictionary<string, GreeksPnlChartData>();

        /// <summary>
        /// Boolean flag indicating the result hander thread is completely finished and ready to dispose.
        /// </summary>
        public bool IsActive { get; private set; } = true;

        /// <summary>
        /// Sampling period for timespans between resamples of the charting equity.
        /// </summary>
        /// <remarks>Specifically critical for backtesting since with such long timeframes the sampled data can get extreme.</remarks>
        public TimeSpan ResamplePeriod { get; private set; } = TimeSpan.FromMinutes(1);


        /// <summary>
        /// How frequently the backtests push messages to the browser.
        /// </summary>
        /// <remarks>Update frequency of notification packets</remarks>
        public TimeSpan NotificationPeriod { get; } = TimeSpan.FromSeconds(2);

        /// <summary>
        /// A dictionary containing summary statistics
        /// </summary>
        public Dictionary<string, string> FinalStatistics { get; private set; }

        /// <summary>
        /// Default initializer for
        /// </summary>
        public BacktestingResultHandler()
        {
            // Delay uploading first packet
            _nextS3Update = _startTime.AddSeconds(30);

            //Default charts:
            Charts.AddOrUpdate("Strategy Equity", new Chart("Strategy Equity"));
            Charts["Strategy Equity"].Series.Add("Equity", new Series("Equity", SeriesType.Candle, 0, "$"));
            Charts["Strategy Equity"].Series.Add("Daily Performance", new Series("Daily Performance", SeriesType.Bar, 1, "%"));

            //Charts.AddOrUpdate("Strategy Equity In BTC", new Chart("Strategy Equity In BTC"));
            //Charts["Strategy Equity In BTC"].Series.Add("Equity", new Series("Equity", SeriesType.Candle, 0, "฿"));
            //Charts["Strategy Equity In BTC"].Series.Add("Daily Performance", new Series("Daily Performance", SeriesType.Bar, 1, "%"));
        }

        /// <summary>
        /// Initialize the result handler with this result packet.
        /// </summary>
        /// <param name="job">Algorithm job packet for this result handler</param>
        /// <param name="messagingHandler">The handler responsible for communicating messages to listeners</param>
        /// <param name="api">The api instance used for handling logs</param>
        /// <param name="setupHandler"></param>
        /// <param name="transactionHandler"></param>
        public virtual void Initialize(
            AlgorithmNodePacket job,
            IMessagingHandler messagingHandler,
            IApi api,
            ISetupHandler setupHandler,
            ITransactionHandler transactionHandler)
        {
            _algorithmId = job.AlgorithmId;
            _projectId = job.ProjectId;
            _messagingHandler = messagingHandler;
            _transactionHandler = transactionHandler;
            _setupHandler = setupHandler;
            _job = (BacktestNodePacket)job;
            if (_job == null) throw new Exception("BacktestingResultHandler.Constructor(): Submitted Job type invalid.");
            _compileId = _job.CompileId;
            _backtestId = _job.BacktestId;
        }

        /// <summary>
        /// The main processing method steps through the messaging queue and processes the messages one by one.
        /// </summary>
        public void Run()
        {
            //Setup minimum result arrays:
            //SampleEquity(job.periodStart, job.startingCapital);
            //SamplePerformance(job.periodStart, 0);

            try
            {
                while (!(_exitTriggered && Messages.Count == 0))
                {
                    //While there's no work to do, go back to the algorithm:
                    if (Messages.Count == 0)
                    {
                        ExitEvent.WaitOne(50);
                    }
                    else
                    {
                        //1. Process Simple Messages in Queue
                        if (Messages.TryDequeue(out var packet))
                        {
                            _messagingHandler.Send(packet);
                        }
                    }

                    //2. Update the packet scanner:
                    Update();

                } // While !End.
            }
            catch (Exception err)
            {
                // unexpected error, we need to close down shop
                Log.Error(err);
                // quit the algorithm due to error
                Algorithm.RunTimeError = err;
            }

            Log.Trace("BacktestingResultHandler.Run(): Ending Thread...");
            IsActive = false;

            // reset standard out/error
            Console.SetOut(StandardOut);
            Console.SetError(StandardError);
        } // End Run();

        /*
        public void RunOptionPrice()
        {
            try
            {
                var host = Config.Get("option-price-output-host", "localhost");
                var port = Config.Get("option-price-output-port", "8808");
                _messagingHandlerOptionPrice = new StreamingMessageHandler(host,port);
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
                            Log.Trace("11111");
                        }
                    }

                } // While !End.
            }
            catch (Exception err)
            {
                // unexpected error, we need to close down shop
                Log.Error(err);
                // quit the algorithm due to error
                Algorithm.RunTimeError = err;
            }
            Log.Trace("BacktestingResultHandler.RunEchart(): Ending Thread...");
            IsActive = false;
        }
        */


        private void CustomerChartDataSend()
        {
            if (CustomerCharts.Count > 0)
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
                }
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

        private void GreeksPnlChartDataSend()
        {
            if (GreeksPnlCharts.Count > 0)
            {
                var charts = new List<GreeksPnlChartData>();
                lock (_greeksGnlChartLock)
                {
                    foreach (var kvp in GreeksPnlCharts)
                    {
                        charts.Add(kvp.Value);
                    }

                    GreeksPnlCharts.Clear();
                }

                if (charts.Count > 0)
                {
                    GreeksGnlChartDataPacket packet = new GreeksGnlChartDataPacket(charts);
                    _messagingHandler.Send(packet);
                }
            }
        }

        private void GreeksChartDataSend()
        {
            if (GreeksCharts.Count > 0)
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
                }
            }
        }

        private Dictionary<string, Holding> SummerHoldings()
        {
            var holdings = new Dictionary<string, Holding>();

            var totalHolding = new Holding();
            totalHolding.Symbol = new Symbol(SecurityIdentifier.Empty, "Total");
            // Only send holdings updates when we have changes in orders, except for first time, then we want to send all
            var time = Algorithm.Time;
            foreach (var kvp in Algorithm.Securities.ItemOrderBy(x => x.Key.Value))
            {
                var security = kvp.Value;

                if (!security.IsInternalFeed() && !security.Symbol.IsCanonical() && security.Invested)
                {
                    if (security.Holdings.Invested)
                    {
                        var holding = new Holding(security, SecurityHoldingType.Net);
                        holding.Time = time;
                        DictionarySafeAdd(holdings, security.Symbol.Value + "Net", holding, "holdings");

                        totalHolding.Quantity += holding.Quantity;
                        totalHolding.MarketValue += holding.MarketValue;
                        totalHolding.UnrealizedPnL += holding.UnrealizedPnL;
                        totalHolding.RealizedPnL += holding.RealizedPnL;
                        totalHolding.ExercisePnL += holding.ExercisePnL;
                    }

                    if (security.LongHoldings.Invested)
                    {
                        var holding = new Holding(security, SecurityHoldingType.Long);
                        holding.Time = time;
                        DictionarySafeAdd(holdings, security.Symbol.Value + "Long", holding, "holdings");

                        totalHolding.Quantity += holding.Quantity;
                        totalHolding.MarketValue += holding.MarketValue;
                        totalHolding.UnrealizedPnL += holding.UnrealizedPnL;
                        totalHolding.RealizedPnL += holding.RealizedPnL;
                        totalHolding.ExercisePnL += holding.ExercisePnL;
                    }

                    if (security.ShortHoldings.Invested)
                    {
                        var holding = new Holding(security, SecurityHoldingType.Short);
                        holding.Time = time;
                        DictionarySafeAdd(holdings, security.Symbol.Value + "Short", holding, "holdings");

                        totalHolding.Quantity += holding.Quantity;
                        totalHolding.MarketValue += holding.MarketValue;
                        totalHolding.UnrealizedPnL += holding.UnrealizedPnL;
                        totalHolding.RealizedPnL += holding.RealizedPnL;
                        totalHolding.ExercisePnL += holding.ExercisePnL;
                    }
                }
            }

            holdings["Total"] = totalHolding;
            return holdings;
        }

        private TradeRecord ConverToTradeRecord(Order order)
        {
            var trade = new TradeRecord();
            trade.Symbol = order.Symbol;
            trade.TradeId = order.Id.ToString();
            trade.OrderId = order.Id.ToString();
            trade.Status = order.Status;
            trade.Direction = order.Direction;
            trade.Time = order.FillTime;
            trade.Amount = order.FillQuantity;
            trade.Price = order.AverageFillPrice;
            trade.Tag = order.Tag;

            return trade;
        }

        /// <summary>
        /// Send a backtest update to the browser taking a latest snapshot of the charting data.
        /// </summary>
        public void Update()
        {
            try
            {
                //Sometimes don't run the update, if not ready or we're ending.
                if (Algorithm?.Transactions == null || _processingFinalPacket)
                {
                    return;
                }

                CustomerChartDataSend();
                OptionPriceMarketDataSend();
                GreeksChartDataSend();
                GreeksPnlChartDataSend();


                if (DateTime.UtcNow <= _nextUpdate || _daysProcessed < _daysProcessedFrontier) return;
                //if (DateTime.UtcNow <= _nextUpdate || _minutesProcessed < _minutesProcessedFrontier) return;

                //Log.Trace($" -----------Update ------{Algorithm.Time.ToString("yyyy-MM-dd hh:mm:ss")}");

                //Extract the orders since last update
                var deltaOrders = new Dictionary<string, Order>();

                try
                {
                    deltaOrders = (from order in _transactionHandler.Orders
                                   where order.Value.Time.Date >= _lastUpdate && order.Value.Status == OrderStatus.Filled
                                   select order).ToDictionary(t => t.Key.ToString(), t => t.Value);
                }
                catch (Exception err)
                {
                    Log.Error(err, "Transactions");
                }

                //Extract the holdings
                var holdings = SummerHoldings();


                //Limit length of orders we pass back dynamically to avoid flooding.
                //if (deltaOrders.Count > 50) {
                //    deltaOrders.Clear();
                //}                

                //Reset loop variables:
                try
                {
                    _lastUpdate = Algorithm.UtcTime.Date;
                    _lastUpdateTime = Algorithm.UtcTime; //额外添加
                    _daysProcessedFrontier = _daysProcessed + 1;
                    _nextUpdate = DateTime.UtcNow.AddSeconds(2);
                    //_nextUpdate = DateTime.UtcNow.AddMinutes(10);
                    _minutesProcessedFrontier = _minutesProcessed + 120;
                }
                catch (Exception err)
                {
                    Log.Error(err, "Can't update variables");
                }

                var deltaCharts = new Dictionary<string, Chart>();
                lock (_chartLock)
                {
                    //Get the updates since the last chart
                    foreach (var kvp in Charts)
                    {
                        var chart = kvp.Value;

                        deltaCharts.Add(chart.Name, chart.GetUpdates());
                    }
                }

                //Get the runtime statistics from the user algorithm:
                var runtimeStatistics = new Dictionary<string, string>();
                lock (_runtimeLock)
                {
                    foreach (var pair in _runtimeStatistics)
                    {
                        runtimeStatistics.Add(pair.Key, pair.Value);
                    }
                }
                runtimeStatistics.Add("Unrealized", "$" + Algorithm.Portfolio.TotalUnrealizedProfit.ToString("N2"));
                runtimeStatistics.Add("Fees", "-$" + Algorithm.Portfolio.TotalFees.ToString("N2"));
                runtimeStatistics.Add("Net Profit", "$" + (Algorithm.Portfolio.TotalProfit - Algorithm.Portfolio.TotalFees).ToString("N2"));
                runtimeStatistics.Add("Option Value", "$" + Algorithm.Portfolio.GetMarketValue(holdings, QuantConnect.SecurityType.Option).ToString("N2"));
                runtimeStatistics.Add("Equity Value", "$" + Algorithm.Portfolio.GetMarketValue(holdings, QuantConnect.SecurityType.Equity).ToString("N2"));
                runtimeStatistics.Add("Future PnL", "$" + Algorithm.Portfolio.GetTotalFutureUnrealisedProfit(holdings, QuantConnect.SecurityType.Future).ToString("N2"));
                if (_setupHandler.StartingPortfolioValue != 0)
                {
                    runtimeStatistics.Add("Return", ((Algorithm.Portfolio.TotalPortfolioValue - _setupHandler.StartingPortfolioValue) / _setupHandler.StartingPortfolioValue).ToString("P"));
                }
                runtimeStatistics.Add("Equity", "$" + Algorithm.Portfolio.TotalPortfolioValue.ToString("N2"));
                runtimeStatistics.Add("Available Marigin", "$" + Algorithm.Portfolio.GetMarginRemaining(0m).ToString("N2"));
                runtimeStatistics.Add("Used Marigin", "$" + Algorithm.Portfolio.TotalMarginUsed.ToString("N2"));
                runtimeStatistics.Add("Unsettled Cash", "$" + Algorithm.Portfolio.UnsettledCash.ToString("N2"));

                //Profit Loss Changes:
                decimal progress = 0;
                if (_jobDays != 0)
                {
                    progress = Convert.ToDecimal(_daysProcessed / _jobDays);
                    if (progress > 0.999m) progress = 0.999m;
                }

                //1. Cloud Upload -> Upload the whole packet to S3  Immediately:
                if (DateTime.UtcNow > _nextS3Update)
                {
                    // For intermediate backtesting results, we truncate the order list to include only the last 100 orders
                    // The final packet will contain the full list of orders.
                    const int maxOrders = 100;
                    var orderCount = _transactionHandler.Orders.Count;
                    var orders = orderCount > maxOrders
                        ? _transactionHandler.Orders.Skip(orderCount - maxOrders).ToDictionary()
                        : _transactionHandler.Orders.ToDictionary();
                    var so = orders.ToDictionary(t => t.Key.ToString(), t => t.Value);

                    var completeResult = new BacktestResult(
                        Charts,
                        //orderCount > maxOrders ? _transactionHandler.Orders.Skip(orderCount - maxOrders).ToDictionary() : _transactionHandler.Orders.ToDictionary(),
                        so,
                        holdings,
                        Algorithm.Transactions.TransactionRecord,
                        new Dictionary<string, string>(),
                        runtimeStatistics,
                        new Dictionary<string, AlgorithmPerformance>());

                    StoreResult(new BacktestResultPacket(_job, completeResult, Algorithm.EndDate, Algorithm.StartDate, progress));

                    _nextS3Update = DateTime.UtcNow.AddSeconds(30);
                }

                //2. Backtest Update -> Send the truncated packet to the backtester:
                var splitPackets = SplitPackets(deltaCharts, deltaOrders, holdings, runtimeStatistics, progress);

                foreach (var backtestingPacket in splitPackets)
                {
                    _messagingHandler.Send(backtestingPacket);
                }

                var os = deltaOrders.OrderBy(x => x.Value.Id).Select(x => x.Value).ToList();
                foreach (var item in os)
                {
                    if (_tradesCache.ContainsKey(item.Id))
                    {
                        continue;
                    }

                    _tradesCache.TryAdd(item.Id, item.Id);
                    OnTrade(ConverToTradeRecord(item));
                }
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

        /// <summary>
        /// Run over all the data and break it into smaller packets to ensure they all arrive at the terminal
        /// </summary>
        public IEnumerable<BacktestResultPacket> SplitPackets(Dictionary<string, Chart> deltaCharts, Dictionary<string, Order> deltaOrders, Dictionary<string, Holding> deltaHoldings, Dictionary<string, string> runtimeStatistics, decimal progress)
        {
            // break the charts into groups
            var splitPackets = new List<BacktestResultPacket>();
            foreach (var chart in deltaCharts.Values)
            {
                //Don't add packet if the series is empty:
                if (chart.Series.Values.Aggregate(0, (i, x) => i + x.Values.Count) == 0)
                {
                    continue;
                }

                splitPackets.Add(new BacktestResultPacket(
                    _job,
                    new BacktestResult { Charts = new Dictionary<string, Chart> { { chart.Name, chart } } },
                    Algorithm.EndDate,
                    Algorithm.StartDate,
                    progress));
            }

            // Send alpha run time statistics
            splitPackets.Add(new BacktestResultPacket(
                _job,
                new BacktestResult { AlphaRuntimeStatistics = AlphaRuntimeStatistics },
                Algorithm.EndDate,
                Algorithm.StartDate,
                progress));

            // Add the orders into the charting packet:
            splitPackets.Add(new BacktestResultPacket(
                _job,
                new BacktestResult { Orders = deltaOrders },
                Algorithm.EndDate,
                Algorithm.StartDate,
                progress));

            // Add the orders into the charting packet:
            splitPackets.Add(new BacktestResultPacket(
                _job,
                new BacktestResult { Holdings = deltaHoldings },
                Algorithm.EndDate,
                Algorithm.StartDate,
                progress));

            //Add any user runtime statistics into the backtest.
            splitPackets.Add(new BacktestResultPacket(
                _job,
                new BacktestResult { RuntimeStatistics = runtimeStatistics },
                Algorithm.EndDate,
                Algorithm.StartDate,
                progress));

            return splitPackets;
        }

        /// <summary>
        /// Save the snapshot of the total results to storage.
        /// </summary>
        /// <param name="packet">Packet to store.</param>
        /// <param name="async">Store the packet asyncronously to speed up the thread.</param>
        /// <remarks>Async creates crashes in Mono 3.10 if the thread disappears before the upload is complete so it is disabled for now.</remarks>
        public void StoreResult(Packet packet, bool async = false)
        {
            try
            {
                // Make sure this is the right type of packet:
                if (packet.Type != PacketType.BacktestResult) return;

                // Port to packet format:
                if (packet is BacktestResultPacket result)
                {
                    // Get Storage Location:
                    var key = _job.BacktestId + ".json";

                    BacktestResult results;
                    lock (_chartLock)
                    {
                        results = new BacktestResult(
                            result.Results.Charts.ToDictionary(x => x.Key, x => x.Value.Clone()),
                            result.Results.Orders,
                            result.Results.Holdings,
                            result.Results.ProfitLoss,
                            result.Results.Statistics,
                            result.Results.RuntimeStatistics,
                            result.Results.RollingWindow,
                            result.Results.TotalPerformance
                        )
                        // Set Alpha Runtime Statistics
                        { AlphaRuntimeStatistics = result.Results.AlphaRuntimeStatistics };
                    }
                    // Save results
                    SaveResults(key, results);
                }
                else
                {
                    Log.Error("BacktestingResultHandler.StoreResult(): Result Null.");
                }
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

        /// <summary>
        /// Send a final analysis result back to the IDE.
        /// </summary>
        /// <param name="job">Lean AlgorithmJob task</param>
        /// <param name="orders">Collection of orders from the algorithm</param>
        /// <param name="profitLoss">Collection of time-profit values for the algorithm</param>
        /// <param name="holdings">Current holdings state for the algorithm</param>
        /// <param name="cashbook">Cashbook for the holdings</param>
        /// <param name="statisticsResults">Statistics information for the algorithm (empty if not finished)</param>
        /// <param name="banner">Runtime statistics banner information</param>
        public void SendFinalResult(
            AlgorithmNodePacket job,
            Dictionary<string, Order> orders,
            Dictionary<DateTime, decimal> profitLoss,
            Dictionary<string, Holding> holdings,
            CashBook cashbook,
            StatisticsResults statisticsResults,
            Dictionary<string, string> banner)
        {
            try
            {
                FinalStatistics = statisticsResults.Summary;

                //Convert local dictionary:
                var charts = new Dictionary<string, Chart>(Charts);
                _processingFinalPacket = true;

                // clear the trades collection before placing inside the backtest result
                foreach (var ap in statisticsResults.RollingPerformances.Values)
                {
                    ap.ClosedTrades.Clear();
                }

                //Create a result packet to send to the browser.
                var result = new BacktestResultPacket(
                    (BacktestNodePacket)job,
                    new BacktestResult(charts, orders, holdings, profitLoss, statisticsResults.Summary, banner, statisticsResults.RollingPerformances, statisticsResults.TotalPerformance, AlphaRuntimeStatistics),
                    Algorithm.EndDate, Algorithm.StartDate)
                {
                    ProcessingTime = (DateTime.UtcNow - _startTime).TotalSeconds,
                    DateFinished = DateTime.Now,
                    Progress = 1
                };

                //Place result into storage.
                StoreResult(result);

                //Second, send the truncated packet:
                _messagingHandler.Send(result);

                Log.Trace("BacktestingResultHandler.SendAnalysisResult(): Processed final packet");
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

        /// <summary>
        /// Set the Algorithm instance for ths result.
        /// </summary>
        /// <param name="algorithm">Algorithm we're working on.</param>
        /// <remarks>While setting the algorithm the backtest result handler.</remarks>
        public virtual void SetAlgorithm(IAlgorithm algorithm)
        {
            Algorithm = algorithm;

            if (Algorithm.PortfolioManagerName == "deribit")
            {
                Charts.AddOrUpdate(BtcChartName, new Chart(BtcChartName));
                Charts[BtcChartName].Series.Add("Equity", new Series("Equity", SeriesType.Candle, 0, "฿"));
                Charts[BtcChartName].Series.Add("Daily Performance", new Series("Daily Performance", SeriesType.Bar, 1, "%"));
            }

            //Get the resample period:
            var totalMinutes = (algorithm.EndDate - algorithm.StartDate).TotalMinutes;
            var resampleMinutes = totalMinutes < MinimumSamplePeriod * Samples ? MinimumSamplePeriod : totalMinutes / Samples; // Space out the sampling every
            ResamplePeriod = TimeSpan.FromMinutes(resampleMinutes);
            Log.Trace("BacktestingResultHandler(): Sample Period Set: " + resampleMinutes.ToString("00.00"));

            //Setup the sampling periods:
            _jobDays = Algorithm.Securities.Count > 0
                ? Time.TradeableDates(Algorithm.Securities.Values, algorithm.StartDate, algorithm.EndDate)
                : Convert.ToInt32((algorithm.StartDate.Date - algorithm.EndDate.Date).TotalDays) + 1;

            //Set the security / market types.
            var types = new List<SecurityType>();
            foreach (var kvp in Algorithm.Securities)
            {
                var security = kvp.Value;

                if (!types.Contains(security.Type)) types.Add(security.Type);
            }
            SecurityType(types);

            ConfigureConsoleTextWriter(algorithm);
        }

        /// <summary>
        /// Configures the <see cref="Console.Out"/> and <see cref="Console.Error"/> <see cref="TextWriter"/>
        /// instances. By default, we forward <see cref="Console.WriteLine(string)"/> to <see cref="IAlgorithm.Debug"/>.
        /// This is perfect for running in the cloud, but since they're processed asynchronously, the ordering of these
        /// messages with respect to <see cref="Log"/> messages is broken. This can lead to differences in regression
        /// test logs based solely on the ordering of messages. To disable this forwarding, set <code>"forward-console-messages"</code>
        /// to <code>false</code> in the configuration.
        /// </summary>
        protected virtual void ConfigureConsoleTextWriter(IAlgorithm algorithm)
        {
            if (Config.GetBool("forward-console-messages", true))
            {
                // we need to forward Console.Write messages to the algorithm's Debug function
                Console.SetOut(new FuncTextWriter(algorithm.Debug));
                Console.SetError(new FuncTextWriter(algorithm.Error));
            }
            else
            {
                // we need to forward Console.Write messages to the standard Log functions
                Console.SetOut(new FuncTextWriter(msg => Log.Trace(msg)));
                Console.SetError(new FuncTextWriter(msg => Log.Error(msg)));
            }
        }

        /// <summary>
        /// Send a debug message back to the browser console.
        /// </summary>
        /// <param name="message">Message we'd like shown in console.</param>
        public virtual void DebugMessage(string message)
        {
            Messages.Enqueue(new DebugPacket(_projectId, _backtestId, _compileId, message));

            //Save last message sent:
            if (Algorithm != null)
            {
                _log.Add(Algorithm.Time.ToString(DateFormat.UI) + " " + message);
            }
        }

        /// <summary>
        /// Send a system debug message back to the browser console.
        /// </summary>
        /// <param name="message">Message we'd like shown in console.</param>
        public virtual void SystemDebugMessage(string message)
        {
            Messages.Enqueue(new SystemDebugPacket(_projectId, _backtestId, _compileId, message));

            //Save last message sent:
            if (Algorithm != null)
            {
                _log.Add(Algorithm.Time.ToString(DateFormat.UI) + " " + message);
            }
        }

        /// <summary>
        /// Send a logging message to the log list for storage.
        /// </summary>
        /// <param name="message">Message we'd in the log.</param>
        public virtual void LogMessage(string message)
        {
            Messages.Enqueue(new LogPacket(_backtestId, message));

            if (Algorithm != null)
            {
                _log.Add(Algorithm.Time.ToString(DateFormat.UI) + " " + message);
            }
        }

        /// <summary>
        /// Send list of security asset types the algortihm uses to browser.
        /// </summary>
        public virtual void SecurityType(List<SecurityType> types)
        {
            var packet = new SecurityTypesPacket
            {
                Types = types
            };
            Messages.Enqueue(packet);
        }

        /// <summary>
        /// Send an error message back to the browser highlighted in red with a stacktrace.
        /// </summary>
        /// <param name="message">Error message we'd like shown in console.</param>
        /// <param name="stacktrace">Stacktrace information string</param>
        public virtual void ErrorMessage(string message, string stacktrace = "")
        {
            if (message == _errorMessage) return;
            if (Messages.Count > 500) return;
            Messages.Enqueue(new HandledErrorPacket(_backtestId, message, stacktrace));
            _errorMessage = message;
        }

        /// <summary>
        /// Send a runtime error message back to the browser highlighted with in red
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="stacktrace">Stacktrace information string</param>
        public virtual void RuntimeError(string message, string stacktrace = "")
        {
            PurgeQueue();
            Messages.Enqueue(new RuntimeErrorPacket(_job.UserId, _backtestId, message, stacktrace));
            _errorMessage = message;
        }


        public bool IsPushOptionPrice { get; set; } = false;

        public string AlgorithmId => _algorithmId;

        public void OptionPrice(OptionContract contract)
        {
            if ((contract.BidPrice == 0 && contract.AskPrice == 0) || contract.UnderlyingLastPrice == 0)
            {
                return;
            }
            if (Algorithm?.Portfolio == null ||
                !Algorithm.Portfolio.Securities.ContainsKey(contract.Symbol))
            {
                return;
            }

            var security = Algorithm.Portfolio.Securities[contract.Symbol];
            decimal holding = security.Holdings.AbsoluteQuantity
                              + security.LongHoldings.AbsoluteQuantity
                              + security.ShortHoldings.AbsoluteQuantity;
            if (holding == 0) return;

            int count = 0;
            decimal price = 0;
            if (contract.BidPrice > 0)
            {
                count++;
                price += contract.BidPrice;
            }
            if (contract.AskPrice > 0)
            {
                count++;
                price += contract.AskPrice;
            }
            price = price / count;

            if (OptionPriceMap.ContainsKey(contract.Symbol))
            {
                var v = OptionPriceMap[contract.Symbol];
                if (v.Price == price &&
                    v.UnderlyingPrice == contract.UnderlyingLastPrice &&
                    v.Holding == holding)
                {
                    return;
                }
                //时间间隔
                var timeSpan = (contract.Time - v.Time).TotalMinutes;
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
        /// <param name="seriesIndex">Type of chart we should create if it doesn't already exist.</param>
        /// <param name="seriesName">Series name for the chart.</param>
        /// <param name="seriesType">Series type for the chart.</param>
        /// <param name="time">Time for the sample</param>
        /// <param name="unit">Unit of the sample</param>
        /// <param name="value">Value for the chart sample.</param>
        public void Sample(string chartName, string seriesName, int seriesIndex, SeriesType seriesType, DateTime time, decimal value, string unit = "$")
        {
            // Sampling during warming up period skews statistics
            if (Algorithm.IsWarmingUp)
            {
                return;
            }

            lock (_chartLock)
            {
                //Add a copy locally:
                if (!Charts.TryGetValue(chartName, out var chart))
                {
                    chart = new Chart(chartName);
                    Charts.AddOrUpdate(chartName, chart);
                }

                //Add the sample to our chart:
                if (!chart.Series.TryGetValue(seriesName, out var series))
                {
                    series = new Series(seriesName, seriesType, seriesIndex, unit);
                    chart.Series.Add(seriesName, series);
                }

                //Add our value:
                if (series.Values.Count == 0 || time > Time.UnixTimeStampToDateTime(series.Values[series.Values.Count - 1].x))
                {
                    series.Values.Add(new ChartPoint(time, value));
                }
            }
        }

        /// <summary>
        /// Sample the current equity of the strategy directly with time-value pair.
        /// </summary>
        /// <param name="time">Current backtest time.</param>
        /// <param name="value">Current equity value.</param>
        public void SampleEquity(DateTime time, decimal value)
        {
            //Sample the Equity Value:
            Sample("Strategy Equity", "Equity", 0, SeriesType.Candle, time, value);

            //Recalculate the days processed:
            _daysProcessed = (time - Algorithm.StartDate).TotalDays;

            _minutesProcessed = (time - Algorithm.StartDate).TotalMinutes;
        }

        /// <summary>
        /// Sample the current equity of the strategy directly with time-value pair.
        /// </summary>
        /// <param name="time">Current backtest time.</param>
        /// <param name="value">Current equity value.</param>
        public void SampleEquityInBTC(DateTime time, decimal value)
        {
            //Sample the Equity Value:
            Sample(BtcChartName, "Equity", 0, SeriesType.Candle, time, value);

            //Recalculate the days processed:
            _daysProcessed = (time - Algorithm.StartDate).TotalDays;
            _minutesProcessed = (time - Algorithm.StartDate).TotalMinutes;
        }

        /// <summary>
        /// Sample the current daily performance directly with a time-value pair.
        /// </summary>
        /// <param name="time">Current backtest date.</param>
        /// <param name="value">Current daily performance value.</param>
        public virtual void SamplePerformance(DateTime time, decimal value)
        {
            //Added a second chart to equity plot - daily perforamnce:
            Sample("Strategy Equity", "Daily Performance", 1, SeriesType.Bar, time, value, "%");
        }

        /// <summary>
        /// Sample the current benchmark performance directly with a time-value pair.
        /// </summary>
        /// <param name="time">Current backtest date.</param>
        /// <param name="value">Current benchmark value.</param>
        /// <seealso cref="IResultHandler.Sample"/>
        public void SampleBenchmark(DateTime time, decimal value)
        {
            Sample("Benchmark", "Benchmark", 0, SeriesType.Line, time, value);
        }

        /// <summary>
        /// Add a range of samples from the users algorithms to the end of our current list.
        /// </summary>
        /// <param name="updates">Chart updates since the last request.</param>
        public void SampleRange(List<Chart> updates)
        {
            lock (_chartLock)
            {
                foreach (var update in updates)
                {
                    //Create the chart if it doesn't exist already:
                    if (!Charts.TryGetValue(update.Name, out var chart))
                    {
                        chart = new Chart(update.Name);
                        chart.Json = update.Json;
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
                                var values = thisSeries.Values;
                                if ((values.Count + series.Values.Count) <= _job.Controls.MaximumDataPointsPerChartSeries) // check chart data point limit first
                                {
                                    //We already have this record, so just the new samples to the end:
                                    values.AddRange(series.Values);
                                }
                                else if (!_chartSeriesExceededDataPoints.Contains(chart.Name + series.Name))
                                {
                                    _chartSeriesExceededDataPoints.Add(chart.Name + series.Name);
                                    DebugMessage($"Exceeded maximum data points per series, chart update skipped. Chart Name {update.Name}. Series name {series.Name}. " +
                                                 $"Limit is currently set at {_job.Controls.MaximumDataPointsPerChartSeries}");
                                }
                            }
                        }
                    }
                }
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        public void SampleRangeGreeksPnlChart(List<GreeksPnlChartData> data)
        {
            lock (_greeksGnlChartLock)
            {
                foreach (var chart in data)
                {
                    GreeksPnlCharts.AddOrUpdate(chart.DataTime.ToString(), chart);
                }
            }
        }

        /// <summary>
        /// Terminate the result thread and apply any required exit procedures.
        /// </summary>
        public virtual void Exit()
        {
            // Only process the logs once
            if (!_exitTriggered)
            {
                ProcessSynchronousEvents(true);
                var logLocation = SaveLogs(AlgorithmId, _log);
                SystemDebugMessage("Your log was successfully created and can be retrieved from: " + logLocation);
            }

            //Set exit flag, and wait for the messages to send:
            _exitTriggered = true;
            ExitEvent.Set();
        }

        /// <summary>
        /// Send a new order event to the browser.
        /// </summary>
        /// <remarks>In backtesting the order events are not sent because it would generate a high load of messaging.</remarks>
        /// <param name="newEvent">New order event details</param>
        public virtual void OrderEvent(OrderEvent newEvent)
        {
            // NOP. Don't do any order event processing for results in backtest mode.
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="trade"></param>
        public virtual void OnTrade(TradeRecord trade)
        {
            //Log.Trace("BacktestingResultHandler.OnTrade(): " + trade, true);
            Messages.Enqueue(new TradeRecordPacket(Algorithm.Name, trade));
        }

        /// <summary>
        /// Send an algorithm status update to the browser.
        /// </summary>
        /// <param name="status">Status enum value.</param>
        /// <param name="message">Additional optional status message.</param>
        public virtual void SendStatusUpdate(AlgorithmStatus status, string message = "")
        {
            var statusPacket = new AlgorithmStatusPacket(AlgorithmId, _projectId, status, message);
            _messagingHandler.Send(statusPacket);
        }

        /// <summary>
        /// Sample the asset prices to generate plots.
        /// </summary>
        /// <param name="symbol">Symbol we're sampling.</param>
        /// <param name="time">Time of sample</param>
        /// <param name="value">Value of the asset price</param>
        public void SampleAssetPrices(Symbol symbol, DateTime time, decimal value)
        {
            //NOP. Don't sample asset prices in console.
        }

        /// <summary>
        /// Purge/clear any outstanding messages in message queue.
        /// </summary>
        public void PurgeQueue()
        {
            Messages.Clear();
        }

        /// <summary>
        /// Set the current runtime statistics of the algorithm.
        /// These are banner/title statistics which show at the top of the live trading results.
        /// </summary>
        /// <param name="key">Runtime headline statistic name</param>
        /// <param name="value">Runtime headline statistic value</param>
        public virtual void RuntimeStatistic(string key, string value)
        {
            lock (_runtimeLock)
            {
                _runtimeStatistics[key] = value;
            }
        }

        /// <summary>
        /// Set the chart subscription we want data for. Not used in backtesting.
        /// </summary>
        public void SetChartSubscription(string symbol)
        {
            //NOP.
        }

        /// <summary>
        /// Process the synchronous result events, sampling and message reading.
        /// This method is triggered from the algorithm manager thread.
        /// </summary>
        /// <remarks>Prime candidate for putting into a base class. Is identical across all result handlers.</remarks>
        public virtual void ProcessSynchronousEvents(bool forceProcess = false)
        {
            if (Algorithm == null) return;

            var time = Algorithm.UtcTime;

            SampleRangeCustomerChart(Algorithm.GetCustomerChartDataAdds());

            SampleRangeGreeksChart(Algorithm.GetGreeksChartData());

            SampleRangeGreeksPnlChart(Algorithm.GetGreeksPnlChartData());


            if (time > _nextSample || forceProcess)
            {
                //Set next sample time: 4000 samples per backtest
                _nextSample = time.Add(ResamplePeriod);

                //Sample the portfolio value over time for chart.
                SampleEquity(time, Math.Round(Algorithm.Portfolio.TotalPortfolioValue, 4));

                if (Algorithm.Portfolio.CashBook.ContainsKey("BTC"))
                {
                    var rate = Algorithm.Portfolio.CashBook["BTC"].ConversionRate;
                    if (rate != 0)
                    {
                        var value = Algorithm.Portfolio.GetTotalPortfolioValueForCurrency("BTC");
                        var result = value / rate;
                        SampleEquityInBTC(time, Math.Round(result, 4));
                    }
                }

                SampleBenchmark(time, Algorithm.Benchmark.Evaluate(time).SmartRounding());


                //Also add the user samples / plots to the result handler tracking:
                SampleRange(Algorithm.GetChartUpdates());

                //Sample the asset pricing:
                foreach (var kvp in Algorithm.Securities)
                {
                    var security = kvp.Value;

                    SampleAssetPrices(security.Symbol, time, security.Price);
                }
            }

            long endTime;
            // avoid calling utcNow if not required
            if (Algorithm.DebugMessages.Count > 0)
            {
                //Send out the debug messages:
                endTime = DateTime.UtcNow.AddMilliseconds(250).Ticks;
                while (Algorithm.DebugMessages.Count > 0 && DateTime.UtcNow.Ticks < endTime)
                {
                    if (Algorithm.DebugMessages.TryDequeue(out var message))
                    {
                        DebugMessage(message);
                    }
                }
            }

            // avoid calling utcNow if not required
            if (Algorithm.ErrorMessages.Count > 0)
            {
                //Send out the error messages:
                endTime = DateTime.UtcNow.AddMilliseconds(250).Ticks;
                while (Algorithm.ErrorMessages.Count > 0 && DateTime.UtcNow.Ticks < endTime)
                {
                    if (Algorithm.ErrorMessages.TryDequeue(out var message))
                    {
                        ErrorMessage(message);
                    }
                }
            }

            // avoid calling utcNow if not required
            if (Algorithm.LogMessages.Count > 0)
            {
                //Send out the log messages:
                endTime = DateTime.UtcNow.AddMilliseconds(250).Ticks;
                while (Algorithm.LogMessages.Count > 0 && DateTime.UtcNow.Ticks < endTime)
                {
                    if (Algorithm.LogMessages.TryDequeue(out var message))
                    {
                        LogMessage(message);
                    }
                }
            }

            //Set the running statistics:
            foreach (var pair in Algorithm.RuntimeStatistics)
            {
                RuntimeStatistic(pair.Key, pair.Value);
            }
        }


        private static void DictionarySafeAdd<T>(Dictionary<string, T> dictionary, string key, T value, string dictionaryName)
        {
            if (dictionary.ContainsKey(key))
            {
                Log.Error($"BacktestingResultHandler.DictionarySafeAdd(): dictionary {dictionaryName} already contains key {key}");
            }
            else
            {
                dictionary.Add(key, value);
            }
        }
    }
}
