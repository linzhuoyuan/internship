using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using QuantConnect.Configuration;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Lean.Engine.DataFeeds;

namespace QuantConnect.Lean.Engine.Results
{
    /// <summary>
    /// Provides base functionality to the implementations of <see cref="IResultHandler"/>
    /// </summary>
    public class BaseResultsHandler
    {
        protected IDataFeedSubscriptionManager DataManager;

        /// <summary>
        /// Gets or sets the current alpha runtime statistics
        /// </summary>
        protected AlphaRuntimeStatistics AlphaRuntimeStatistics { get; set; }

        /// <summary>
        /// Directory location to store results
        /// </summary>
        protected string ResultsDestinationFolder;

        /// <summary>
        /// Event set when exit is triggered
        /// </summary>
        protected ManualResetEvent ExitEvent { get; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        protected BaseResultsHandler()
        {
            ExitEvent = new ManualResetEvent(false);
            ResultsDestinationFolder = Config.Get("results-destination-folder", Directory.GetCurrentDirectory());
        }

        /// <summary>
        /// Returns the location of the logs
        /// </summary>
        /// <param name="id">Id that will be incorporated into the algorithm log name</param>
        /// <param name="logs">The logs to save</param>
        /// <returns>The path to the logs</returns>
        public virtual string SaveLogs(string id, IEnumerable<string> logs)
        {
            var filename = $"{id}-log.txt";
            var path = GetResultsPath(filename);
            File.WriteAllLines(path, logs);
            return path;
        }

        /// <summary>
        /// Save the results for performance analysis purpose 
        /// </summary>
        /// <param name="name">The name of the results</param>
        /// <param name="result">The results to save</param>
        public virtual void SavePerformanceAnalyser(string full_path, Result result)
        {
            File.WriteAllText(full_path, JsonConvert.SerializeObject(result, Formatting.Indented));
        }

        /// <summary>
        /// Gets the full path for a results file
        /// </summary>
        /// <param name="filename">The filename to add to the path</param>
        /// <returns>The full path, including the filename</returns>
        protected string GetResultsPath(string filename)
        {
            return Path.Combine(ResultsDestinationFolder, filename);
        }

        /// <summary>
        /// Save the results to disk
        /// </summary>
        /// <param name="name">The name of the results</param>
        /// <param name="result">The results to save</param>
        public virtual void SaveResults(string name, Result result)
        {
            File.WriteAllText(GetResultsPath(name), JsonConvert.SerializeObject(result, Formatting.Indented));
        }

        /// <summary>
        /// Sets the current alpha runtime statistics
        /// </summary>
        /// <param name="statistics">The current alpha runtime statistics</param>
        public virtual void SetAlphaRuntimeStatistics(AlphaRuntimeStatistics statistics)
        {
            AlphaRuntimeStatistics = statistics;
        }

        /// <summary>
        /// Sets the current Data Manager instance
        /// </summary>
        public virtual void SetDataManager(IDataFeedSubscriptionManager dataManager)
        {
            DataManager = dataManager;
        }

        /// <summary>
        /// Event fired each time that we add/remove securities from the data feed
        /// </summary>
        public virtual void OnSecuritiesChanged(SecurityChanges changes)
        {
        }
    }
}
