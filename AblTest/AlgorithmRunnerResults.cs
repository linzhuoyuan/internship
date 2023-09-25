using QuantConnect;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Packets;

namespace AblTest;

/// <summary>
/// Container class for results generated during an algorithm's execution in <see cref="AlgorithmRunner"/>
/// </summary>
public class AlgorithmRunnerResults
{
    /// <summary>
    /// Algorithm name
    /// </summary>
    public readonly string Algorithm;

    /// <summary>
    /// Algorithm language (C#, Python)
    /// </summary>
    public readonly Language Language;

    /// <summary>
    /// AlgorithmManager instance that is used to run the algorithm
    /// </summary>
    public readonly AlgorithmManager? AlgorithmManager;

    /// <summary>
    /// Algorithm results containing all of the sampled series
    /// </summary>
    public readonly BacktestingResultHandler? BacktestingResults;

    /// <summary>
    /// Algorithm results containing all of the sampled series
    /// </summary>
    public readonly LiveTradingResultHandler? LiveResults;

    public AlgorithmRunnerResults(
        string algorithm,
        Language language,
        AlgorithmManager? manager,
        BacktestingResultHandler? backtestingResults = null)
    {
        Algorithm = algorithm;
        Language = language;
        AlgorithmManager = manager;
        BacktestingResults = backtestingResults;
    }

    public AlgorithmRunnerResults(
        string algorithm,
        Language language,
        AlgorithmManager? manager,
        LiveTradingResultHandler? liveResults = null)
    {
        Algorithm = algorithm;
        Language = language;
        AlgorithmManager = manager;
        LiveResults = liveResults;
    }
}