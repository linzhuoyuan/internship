using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using QuantConnect.Logging;
using QuantConnect.Packets;
using QuantConnect.Configuration;
using QuantConnect.Securities;
using QuantConnect.Interfaces;
using QuantConnect.Util;

namespace QuantConnect.Parameters
{
    public enum PerformanceAnalysisFrequency
    {
        None,
        Weekly,
        Daily,
        Custom
    }

    /// <summary>
    /// Specifies a field or property is a parameter that can be set
    /// from an <see cref="AlgorithmNodePacket.Parameters"/> dictionary
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PerformanceAnalyserAttribute : Attribute
    {
        /// <summary>
        /// The invoke time of day to perform the daily backtesting analysis (UTC time)
        /// </summary>
        private readonly TimeSpan _dailyInvokeTime = new TimeSpan(0, 0, 0);
        /// <summary>
        /// Flag that whether the snapshots are taken or not
        /// </summary>
        private bool _isSnapshotTaken = false;
        /// <summary>
        /// The live algorithm name that the performance analyser will be applied to
        /// </summary>
        public string LiveAlgorithmID {get; set; }
        /// <summary>
        /// The name of this benchmark backtesting algorithm
        /// </summary>
        public string BenchmarkBacktest { get; private set; }

        /// <summary>
        /// Start date of the expected period of this performance analysis
        /// </summary>
        /// <Note>
        /// Because Attribute can only take plain types, we need to use a string instead of a DateTime object here.
        /// </Note>
        public DateTime? StartDate{ get; private set; }

        /// <summary>
        /// End date of the expected period of this performance analysis
        /// </summary>
        /// <Note>
        /// Because Attribute can only take plain types, we need to use a string instead of a DateTime object here.
        /// </Note>
        public DateTime? EndDate{ get; private set; }

        /// <summary>
        /// The desstination output directory for the performance analysis results
        /// </summary>
        public String DestOutputFolder{ get; set; }

        /// <summary>
        /// The desstination output file name based on the start and end date
        /// </summary>
        public String DestOutputFileName{ get; set; }

        public CashBook InitialCashBook { get;  set; }
        public Dictionary<string, Holding> InitialHoldings { get;  set; }

        public PerformanceAnalysisFrequency Frequency{ get; private set;}

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceAnalyserAttribute"/> class
        /// </summary>
        /// <param name="StartDate"> the specified start date of this analysis job</param>
        /// <param name="EndDate"> the specified end date of this analysis job</param>
        /// <param name="BenchmarkBacktest"> the target backtesting algorithm name to compare with in this analysis job</param>
        /// <param name="DestOutputFolder"> the destination output folder for live results of this analysis job</param>
        /// <param name="LiveAlgorithmID"> the live algorithm name that need an analysis</param>
        public PerformanceAnalyserAttribute(DateTime StartDate, DateTime EndDate, string BenchmarkBacktest = "", string DestOutputFolder = "", string LiveAlgorithmID = "")
        {
            this.BenchmarkBacktest = BenchmarkBacktest;
            this.StartDate = StartDate;
            this.EndDate = EndDate;
            this.Frequency = PerformanceAnalysisFrequency.None;
            if (DestOutputFolder == "")
            {
                this.DestOutputFolder = Config.Get("perfanalyzer-dest-folder", "perfanalyser");
            }
            else
            {
                this.DestOutputFolder = DestOutputFolder;
            }
            this.LiveAlgorithmID = LiveAlgorithmID;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PerformanceAnalyserAttribute"/> class
        /// </summary>
        /// <param name="BenchmarkBacktest"> the target backtesting algorithm name to compare with in this analysis job</param>
        /// <param name="Frequency"> the frequency of this performance analysis job</param>
        /// <param name="DestOutputFolder"> the destination output folder for live results of this analysis job</param>
        /// <param name="LiveAlgorithmID"> the live algorithm name that need an analysis</param>
        public PerformanceAnalyserAttribute(string BenchmarkBacktest = "", PerformanceAnalysisFrequency Frequency = PerformanceAnalysisFrequency.None, string DestOutputFolder = "", string LiveAlgorithmID = "")
        {
            this.BenchmarkBacktest = BenchmarkBacktest;
            this.Frequency = Frequency;
            this.StartDate = null;
            this.EndDate = null;
            if (DestOutputFolder == "")
            {
                this.DestOutputFolder = Config.Get("perfanalyzer-dest-folder", "./perfanalyser");
            }
            else
            {
                this.DestOutputFolder = DestOutputFolder;
            }
            this.LiveAlgorithmID = LiveAlgorithmID;
        }

        public bool IsInvokingBacktest()
        {
            if (this.EndDate == null )
            {
              throw new Exception("EndDate is not specified");
            }

            return DateTime.Now > this.EndDate;
        }

        public void UpdateRolloverPeriod()
        {
            switch (this.Frequency)
            {
                case PerformanceAnalysisFrequency.None:
                    break;
                case PerformanceAnalysisFrequency.Daily:
                    if ((this.StartDate == null || this.EndDate == null) || 
                        (DateTime.UtcNow > this.EndDate))
                    {
                        UpdatePeriodImpl();
                    }
                    break;
                default:
                    throw new NotImplementedException("PerformanceAnalysisFrequency not implemented");
            }       
        }

        public void InitRolloverPeriod()
        {
          _isSnapshotTaken = false;  
          if (this.StartDate != null || this.EndDate != null)
          {
              Log.Debug($"PerformanceAnalyserAttribute.InitRolloverPeriod(): start date {this.StartDate} and end date {this.EndDate} are already specified!");
              return;
          }

          switch (this.Frequency)
            {
                case PerformanceAnalysisFrequency.None:
                    break;
                case PerformanceAnalysisFrequency.Daily:
                    this.StartDate = DateTime.UtcNow;
                    this.EndDate = DateTime.UtcNow.Date.AddDays(1).Date.Add(this._dailyInvokeTime);
                    this.DestOutputFileName = this.LiveAlgorithmID + "-vs-" + this.BenchmarkBacktest + "-" + this.StartDate.Value.ToString("yyyyMMdd_HHmmss") + "-" + this.EndDate.Value.ToString("yyyyMMdd_HHmmss") + ".json";
                    break;
                default:
                    throw new NotImplementedException("PerformanceAnalysisFrequency not implemented");
            } 

        }

        public void TakePortfolioSnapshot(IAlgorithm algorithm)
        {
            if (this._isSnapshotTaken)
            {
                Log.Trace("PerformanceAnalyserAttribute.TakePortfolioSnapshot(): Snapshot has already been taken!");
                return;
            }

            this.InitialCashBook = new CashBook();
            foreach (var kvp in algorithm.Portfolio.CashBook)
            {
                var initialCash = kvp.Value;
                Cash clonedCash = new Cash(initialCash.Symbol, initialCash.Amount, initialCash.ConversionRate, initialCash.CurrencySymbol);
                this.InitialCashBook.Add(kvp.Key, clonedCash);
            }

            var initialHoldings =  new Dictionary<string, Holding>();
            foreach (var kvp in algorithm.Securities
            // we send non internal, non canonical and tradable securities. When securities are removed they are marked as non tradable
            .Where(pair => pair.Value.IsTradable && !pair.Value.IsInternalFeed() && !pair.Key.IsCanonical())
            .OrderBy(x => x.Key.Value))
            {
                var security = kvp.Value;
                if (!initialHoldings.ContainsKey(security.Symbol.Value))
                {
                    initialHoldings.Add( security.Symbol.Value, new Holding(security));
                }
            }
            this.InitialHoldings = initialHoldings;
            this._isSnapshotTaken = true;
            Log.Trace($"PerformanceAnalyserAttribute.TakePortfolioSnapshot(): {this.BenchmarkBacktest} comparing job is taking the portfolio snapshot.");
        }

        // TODO: Need a round down and up method of datetime to avoid rounding issues.
        private void UpdatePeriodImpl()
        {
            this.StartDate = DateTime.UtcNow.Date.Add(this._dailyInvokeTime);
            this.EndDate = DateTime.UtcNow.AddDays(1).Date.Add(this._dailyInvokeTime);
            this.DestOutputFileName = this.LiveAlgorithmID + "-vs-" + this.BenchmarkBacktest + "-" + this.StartDate.Value.ToString("yyyyMMdd_HHmmss") + "-" + this.EndDate.Value.ToString("yyyyMMdd_HHmmss") + ".json";
            if (!this._isSnapshotTaken)
            {
                Log.Error("PerformanceAnalyserAttribute.UpdatePeriodImpl(): Snapshot has not been taken yet!");
            }
            this._isSnapshotTaken = false;
        }
    }
}