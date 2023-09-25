using System;
using System.Collections.Generic;
using QuantConnect;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Statistics;

namespace Monitor.Model
{
    public class ResultConverter : IResultConverter
    {
        /* QuantConnect results are either BacktestResult or LiveResult. 
         * They have common properties as well as specific properties.
         * However, the QC libary has no base class for them. For this tool, we do need a baseclass
         * This baseclass is 'Result' which remembers the actual result type, and has all possible fields to show in the UI
         */

        public Result FromBacktestResult(BacktestResult backtestResult)
        {
            return new Result
            {
                ResultType = ResultType.Backtest,
                Charts = new Dictionary<string, Charting.ChartDefinition>(backtestResult.Charts.MapToChartDefinitionDictionary()),
                Orders = new Dictionary<string, Order>(backtestResult.Orders),
                ProfitLoss = new Dictionary<DateTime, decimal>(backtestResult.ProfitLoss),
                Statistics = new Dictionary<string, string>(backtestResult.Statistics),
                RuntimeStatistics = new Dictionary<string, string>(backtestResult.RuntimeStatistics),
                RollingWindow = new Dictionary<string, AlgorithmPerformance>(backtestResult.RollingWindow)               
            };
        }

        public Result FromLiveResult(LiveResult liveResult)
        {
            return new Result
            {
                ResultType = ResultType.Live,                
                Charts = new Dictionary<string, Charting.ChartDefinition>(liveResult.Charts.MapToChartDefinitionDictionary()),
                Orders = new Dictionary<string, Order>(liveResult.Orders),
                ProfitLoss = new Dictionary<DateTime, decimal>(liveResult.ProfitLoss),
                Statistics = new Dictionary<string, string>(liveResult.Statistics),
                RuntimeStatistics = new Dictionary<string, string>(liveResult.RuntimeStatistics)
            };
        }

        public BacktestResult ToBacktestResult(Result result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.ResultType != ResultType.Backtest) throw new ArgumentException(@"Result is not of type Backtest", nameof(result));

            // Total performance is always null in the original data holder
            return new BacktestResult(result.Charts.MapToChartDictionary(), result.Orders, new Dictionary<string, Holding>(), result.ProfitLoss, result.Statistics, result.RuntimeStatistics, result.RollingWindow);
        }

        public LiveResult ToLiveResult(Result result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.ResultType != ResultType.Live) throw new ArgumentException(@"Result is not of type Live", nameof(result));

            // Holdings is not supported in the current result.
            // ServerStatistics is not supported in the current result.
            return new LiveResult(result.Charts.MapToChartDictionary(), result.Orders, result.ProfitLoss, null, null, result.Statistics, result.RuntimeStatistics);
        }
    }
}