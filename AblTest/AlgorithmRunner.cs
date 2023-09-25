﻿using System.Linq.Expressions;
using NodaTime;
using QuantConnect;
using QuantConnect.Algorithm;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Alphas;
using QuantConnect.Lean.Engine.HistoricalData;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Lean.Engine.Setup;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;
using HistoryRequest = QuantConnect.Data.HistoryRequest;

namespace AblTest;

/// <summary>
/// Provides methods for running an algorithm and testing it's performance metrics
/// </summary>
public static class AlgorithmRunner
{
    private const string DataFeeds =
        "{\"Option\":[\"Quote\"],\"Equity\":[\"Trade\"],\"Crypto\":[\"Trade\", \"Quote\"],\"Future\":[\"Quote\"]}";
    public static AlgorithmRunnerResults RunLocalSimulation(
        string algorithm,
        Language language,
        AlgorithmStatus expectedFinalStatus,
        string brokerage,
        string setupHandler = "SimulationSetupHandler")
    {
        Composer.Instance.Reset();
        SymbolCache.Clear();

        var logFile = $"./simulation/{algorithm}.{language.ToLower()}.log";
        Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
        File.Delete(logFile);

        AlgorithmManager? algorithmManager = null;
        LiveTradingResultHandler? results = null;
        try
        {
            // set the configuration up
            Config.Set("algorithm-type-name", algorithm);
            Config.Set("live-mode", "true");
            Config.Set("environment", "");
            Config.Set("security-data-feeds", DataFeeds);
            Config.Set("live-mode-brokerage", brokerage);
            Config.Set("data-queue-handler", brokerage);
            Config.Set("messaging-handler", "QuantConnect.Messaging.Messaging");
            Config.Set("job-queue-handler", "QuantConnect.Queues.JobQueue");
            Config.Set("setup-handler", setupHandler);
            Config.Set("history-provider", "SimulationHistoryProvider");
            Config.Set("api-handler", "QuantConnect.Api.Api");
            Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.LiveTradingResultHandler");
            Config.Set("transaction-handler", "QuantConnect.Lean.Engine.TransactionHandlers.BrokerageTransactionHandler");
            Config.Set("data-feed-handler", "QuantConnect.Lean.Engine.DataFeeds.LiveTradingDataFeed");
            Config.Set("real-time-handler", "QuantConnect.Lean.Engine.RealTime.LiveTradingRealTimeHandler");
            Config.Set("algorithm-language", language.ToString());
            Config.Set("algorithm-location",
                language == Language.Python
                    ? "../../../Algorithm.Python/" + algorithm + ".py"
                    : "QuantConnect.Algorithm." + language + ".dll");

            // Store initial log variables
            var initialLogHandler = Log.LogHandler;
            var initialDebugEnabled = Log.DebuggingEnabled;

            // Use our current test LogHandler and a FileLogHandler
            var newLogHandlers = new ILogHandler[] { new ConsoleLogHandler(), new FileLogHandler(logFile, false) };

            using (Log.LogHandler = new CompositeLogHandler(newLogHandlers))
            using (var algorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance))
            using (var systemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance))
            using (var workerThread = new TestWorkerThread())
            {
                Log.DebuggingEnabled = true;

                Log.Trace("");
                Log.Trace("{0}: Running " + algorithm + "...", DateTime.UtcNow);
                Log.Trace("");

                // run the algorithm in its own thread
                var engine = new QuantConnect.Lean.Engine.Engine(systemHandlers, algorithmHandlers, true);
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        var job = (LiveNodePacket)systemHandlers.JobQueue.NextJob(out var algorithmPath);
                        algorithmManager = new AlgorithmManager(true);
                        systemHandlers.LeanManager.Initialize(systemHandlers, algorithmHandlers, job, algorithmManager);
                        engine.Run(job, algorithmManager, algorithmPath, workerThread);
                    }
                    catch (Exception e)
                    {
                        Log.Trace($"Error in AlgorithmRunner task: {e}");
                    }
                }).Wait();

                // Reset settings to initial values
                Log.LogHandler = initialLogHandler;
                Log.DebuggingEnabled = initialDebugEnabled;
            }
        }
        catch (Exception ex)
        {
            if (expectedFinalStatus != AlgorithmStatus.RuntimeError)
            {
                Log.Error("{0} {1}", ex.Message, ex.StackTrace!);
            }
        }
        return new AlgorithmRunnerResults(algorithm, language, algorithmManager, results);
    }

    public static AlgorithmRunnerResults RunLocalBacktest(
        string algorithm,
        Dictionary<string, string> expectedStatistics,
        AlphaRuntimeStatistics expectedAlphaStatistics,
        Language language,
        AlgorithmStatus expectedFinalStatus,
        DateTime? startDate = null,
        DateTime? endDate = null,
        string setupHandler = "RegressionSetupHandlerWrapper",
        string? currency = null,
        decimal? initialCash = null)
    {
        AlgorithmManager? algorithmManager = null;
        var statistics = new Dictionary<string, string>();
        var alphaStatistics = new AlphaRuntimeStatistics(new TestAccountCurrencyProvider());
        BacktestingResultHandler? results = null;

        Composer.Instance.Reset();
        SymbolCache.Clear();
        MarketOnCloseOrder.SubmissionTimeBuffer = MarketOnCloseOrder.DefaultSubmissionTimeBuffer;

        var ordersLogFile = string.Empty;
        var logFile = $"./regression/{algorithm}.{language.ToLower()}.log";
        Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);
        File.Delete(logFile);

        try
        {
            // set the configuration up
            Config.Set("algorithm-type-name", algorithm);
            Config.Set("live-mode", "false");
            Config.Set("environment", "");
            Config.Set("security-data-feeds", DataFeeds);
            Config.Set("messaging-handler", "QuantConnect.Messaging.Messaging");
            Config.Set("job-queue-handler", "QuantConnect.Queues.JobQueue");
            Config.Set("setup-handler", setupHandler);
            Config.Set("history-provider", "RegressionHistoryProviderWrapper");
            Config.Set("api-handler", "QuantConnect.Api.Api");
            Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.RegressionResultHandler");
            Config.Set("algorithm-language", language.ToString());
            Config.Set("algorithm-location",
                language == Language.Python
                    ? "../../../Algorithm.Python/" + algorithm + ".py"
                    : "QuantConnect.Algorithm." + language + ".dll");

            // Store initial log variables
            var initialLogHandler = Log.LogHandler;
            var initialDebugEnabled = Log.DebuggingEnabled;

            // Use our current test LogHandler and a FileLogHandler
            var newLogHandlers = new ILogHandler[] { new ConsoleLogHandler(), new FileLogHandler(logFile, false) };

            using (Log.LogHandler = new CompositeLogHandler(newLogHandlers))
            using (var algorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance))
            using (var systemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance))
            using (var workerThread = new TestWorkerThread())
            {
                Log.DebuggingEnabled = true;

                Log.Trace("");
                Log.Trace("{0}: Running " + algorithm + "...", DateTime.UtcNow);
                Log.Trace("");

                // run the algorithm in its own thread
                var engine = new QuantConnect.Lean.Engine.Engine(systemHandlers, algorithmHandlers, false);
                Task.Factory.StartNew(() =>
                {
                    try
                    {
                        var job = (BacktestNodePacket)systemHandlers.JobQueue.NextJob(out var algorithmPath);
                        job.BacktestId = algorithm;
                        job.PeriodStart = startDate ?? DateTime.MinValue;
                        job.PeriodFinish = endDate ?? DateTime.MinValue;
                        if (initialCash.HasValue)
                        {
                            job.CashAmount = new CashAmount(initialCash.Value, currency ?? Currencies.USD);
                        }
                        algorithmManager = new AlgorithmManager(false);

                        systemHandlers.LeanManager.Initialize(systemHandlers, algorithmHandlers, job, algorithmManager);

                        engine.Run(job, algorithmManager, algorithmPath, workerThread);
                        ordersLogFile = ((RegressionResultHandler)algorithmHandlers.Results).LogFilePath;
                    }
                    catch (Exception e)
                    {
                        Log.Trace($"Error in AlgorithmRunner task: {e}");
                    }
                }).Wait();

                var backtestingResultHandler = (BacktestingResultHandler)algorithmHandlers.Results;
                results = backtestingResultHandler;
                statistics = backtestingResultHandler.FinalStatistics;

                var defaultAlphaHandler = (DefaultAlphaHandler)algorithmHandlers.Alphas;
                alphaStatistics = defaultAlphaHandler.RuntimeStatistics;
            }

            // Reset settings to initial values
            Log.LogHandler = initialLogHandler;
            Log.DebuggingEnabled = initialDebugEnabled;
        }
        catch (Exception ex)
        {
            if (expectedFinalStatus != AlgorithmStatus.RuntimeError)
            {
                Log.Error("{0} {1}", ex.Message, ex.StackTrace!);
            }
        }

        if (algorithmManager?.State != expectedFinalStatus)
        {
            Assert.Fail($"Algorithm state should be {expectedFinalStatus} and is: {algorithmManager?.State}, {algorithm}");
        }

        foreach (var expectedStat in expectedStatistics)
        {
            Assert.IsTrue(statistics.TryGetValue(expectedStat.Key, out var result), "Missing key: " + expectedStat.Key);

            // normalize -0 & 0, they are the same thing
            var expected = expectedStat.Value;
            if (expected == "-0")
            {
                expected = "0";
            }

            if (result == "-0")
            {
                result = "0";
            }

            Assert.AreEqual(expected, result, "Failed on " + expectedStat.Key);
        }

        if (expectedAlphaStatistics != null)
        {
            AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.MeanPopulationScore.Direction);
            AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.MeanPopulationScore.Magnitude);
            AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.RollingAveragedPopulationScore.Direction);
            AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.RollingAveragedPopulationScore.Magnitude);
            AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.LongShortRatio);
            AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.TotalInsightsClosed);
            AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.TotalInsightsGenerated);
            AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.TotalAccumulatedEstimatedAlphaValue);
            AssertAlphaStatistics(expectedAlphaStatistics, alphaStatistics, s => s.TotalInsightsAnalysisCompleted);
        }

        // we successfully passed the regression test, copy the log file so we don't have to continually
        // re-run master in order to compare against a passing run
        var passedFile = logFile.Replace("./regression/", "./passed/");
        Directory.CreateDirectory(Path.GetDirectoryName(passedFile)!);
        File.Delete(passedFile);
        File.Copy(logFile, passedFile);

        var passedOrderLogFile = ordersLogFile.Replace("./regression/", "./passed/");
        Directory.CreateDirectory(Path.GetDirectoryName(passedFile)!);
        File.Delete(passedOrderLogFile);
        if (File.Exists(ordersLogFile)) File.Copy(ordersLogFile, passedOrderLogFile);

        return new AlgorithmRunnerResults(algorithm, language, algorithmManager, results);
    }

    private static void AssertAlphaStatistics(AlphaRuntimeStatistics expected, AlphaRuntimeStatistics actual, Expression<Func<AlphaRuntimeStatistics, object>> selector)
    {
        // extract field name from expression
        var field = selector.AsEnumerable().OfType<MemberExpression>().First().ToString();
        field = field[(field.IndexOf('.') + 1)..];

        var func = selector.Compile();
        var expectedValue = func(expected);
        var actualValue = func(actual);
        if (expectedValue is double value)
        {
            Assert.AreEqual(value, (double)actualValue, 1e-4, "Failed on alpha statistics " + field);
        }
        else
        {
            Assert.AreEqual(expectedValue, actualValue, "Failed on alpha statistics " + field);
        }
    }

    public class SimulationSetupHandler : BrokerageSetupHandler
    {
        public static IAlgorithm? Algorithm { get; protected set; }
        public override IAlgorithm CreateAlgorithmInstance(AlgorithmNodePacket algorithmNodePacket, string assemblyPath, int loadTimeLimit = 60)
        {
            Algorithm = base.CreateAlgorithmInstance(algorithmNodePacket, assemblyPath, loadTimeLimit);
            if (Algorithm is QCAlgorithm framework)
            {
                framework.DebugMode = true;
            }
            return Algorithm;
        }
    }

    /// <summary>
    /// Used to perform checks against history requests for all regression algorithms
    /// </summary>
    public class SimulationHistoryProvider : SubscriptionDataReaderHistoryProvider
    {
        public override IEnumerable<Slice> GetHistory(IEnumerable<HistoryRequest> requests, DateTimeZone sliceTimeZone)
        {
            requests = requests.ToList();
            if (requests.Any(r => SimulationSetupHandler.Algorithm!.UniverseManager.ContainsKey(r.Symbol)))
            {
                throw new Exception("History requests should not be submitted for universe symbols");
            }
            return base.GetHistory(requests, sliceTimeZone);
        }
    }

    /// <summary>
    /// Used to intercept the algorithm instance to aid the <see cref="RegressionHistoryProviderWrapper"/>
    /// </summary>
    public class RegressionSetupHandlerWrapper : BacktestingSetupHandler
    {
        public static IAlgorithm? Algorithm { get; protected set; }
        public override IAlgorithm CreateAlgorithmInstance(AlgorithmNodePacket algorithmNodePacket, string assemblyPath, int loadTimeLimit = 60)
        {
            Algorithm = base.CreateAlgorithmInstance(algorithmNodePacket, assemblyPath, loadTimeLimit);
            if (Algorithm is QCAlgorithm framework)
            {
                framework.DebugMode = true;
            }
            return Algorithm;
        }
    }

    /// <summary>
    /// Used to perform checks against history requests for all regression algorithms
    /// </summary>
    public class RegressionHistoryProviderWrapper : SubscriptionDataReaderHistoryProvider
    {
        public override IEnumerable<Slice> GetHistory(IEnumerable<HistoryRequest> requests, DateTimeZone sliceTimeZone)
        {
            requests = requests.ToList();
            if (requests.Any(r => RegressionSetupHandlerWrapper.Algorithm!.UniverseManager.ContainsKey(r.Symbol)))
            {
                throw new Exception("History requests should not be submitted for universe symbols");
            }
            return base.GetHistory(requests, sliceTimeZone);
        }
    }

    public class TestWorkerThread : WorkerThread
    {
    }
}