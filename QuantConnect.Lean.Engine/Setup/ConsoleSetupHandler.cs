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
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using QuantConnect.AlgorithmFactory;
using QuantConnect.Brokerages.Backtesting;
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Packets;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Util;
using QuantConnect.Algorithm;
using QuantConnect.Securities;
using QuantConnect.Data.Market;

namespace QuantConnect.Lean.Engine.Setup
{
    /// <summary>
    /// Console setup handler to initialize and setup the Lean Engine properties for a local backtest
    /// </summary>
    public class ConsoleSetupHandler : ISetupHandler
    {
        /// <summary>
        /// The worker thread instance the setup handler should use
        /// </summary>
        public WorkerThread WorkerThread { get; set; }

        /// <summary>
        /// Error which occured during setup may appear here.
        /// </summary>
        public List<Exception> Errors { get; set; }

        /// <summary>
        /// Maximum runtime of the strategy. (Set to 10 years for local backtesting).
        /// </summary>
        public TimeSpan MaximumRuntime { get; }

        /// <summary>
        /// Starting capital for the algorithm (Loaded from the algorithm code).
        /// </summary>
        public decimal StartingPortfolioValue { get; private set; }

        /// <summary>
        /// Start date for the backtest.
        /// </summary>
        public DateTime StartingDate { get; private set; }

        /// <summary>
        /// Maximum number of orders for this backtest.
        /// </summary>
        public int MaxOrders { get; }

        /// <summary>
        /// Setup the algorithm data, cash, job start end date etc:
        /// </summary>
        public ConsoleSetupHandler()
        {
            MaxOrders = int.MaxValue;
            StartingPortfolioValue = 0;
            StartingDate = new DateTime(1998, 01, 01);
            MaximumRuntime = TimeSpan.FromDays(10 * 365);
            Errors = new List<Exception>();
        }

        /// <summary>
        /// Create a new instance of an algorithm from a physical dll path.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly's location</param>
        /// <param name="algorithmNodePacket">Details of the task required</param>
        /// <param name="loadTimeLimit">loadTimeLimit</param>
        /// <returns>A new instance of IAlgorithm, or throws an exception if there was an error</returns>
        public IAlgorithm CreateAlgorithmInstance(AlgorithmNodePacket algorithmNodePacket, string assemblyPath, int loadTimeLimit = 60)
        {
            string error;
            IAlgorithm algorithm;
            var algorithmName = Config.Get("algorithm-type-name");

            // don't force load times to be fast here since we're running locally, this allows us to debug
            // and step through some code that may take us longer than the default 10 seconds
            var loader = new Loader(algorithmNodePacket.Language, TimeSpan.FromHours(1), names => names.SingleOrDefault(name => MatchTypeName(name, algorithmName)), WorkerThread);
            var complete = loader.TryCreateAlgorithmInstanceWithIsolator(assemblyPath, algorithmNodePacket.RamAllocation, out algorithm, out error);
            if (!complete) throw new AlgorithmSetupException($"During the algorithm initialization, the following exception has occurred: {error}");

            return algorithm;
        }

        /// <summary>
        /// Creates a new <see cref="BacktestingBrokerage"/> instance
        /// </summary>
        /// <param name="algorithmNodePacket">Job packet</param>
        /// <param name="uninitializedAlgorithm">The algorithm instance before Initialize has been called</param>
        /// <param name="factory">The brokerage factory</param>
        /// <returns>The brokerage instance, or throws if error creating instance</returns>
        public IBrokerage CreateBrokerage(AlgorithmNodePacket algorithmNodePacket, IAlgorithm uninitializedAlgorithm, out IBrokerageFactory factory)
        {
            factory = new BacktestingBrokerageFactory();
            var optionMarketSimulation = new BasicOptionAssignmentSimulation();

            return new BacktestingBrokerage(uninitializedAlgorithm, optionMarketSimulation);
        }

        /// <summary>
        /// Setup the algorithm cash, dates and portfolio as desired.
        /// </summary>
        /// <param name="parameters">The parameters object to use</param>
        /// <returns>Boolean true on successfully setting up the console.</returns>
        public bool Setup(SetupHandlerParameters parameters)
        {
            var algorithm = parameters.Algorithm;
            var baseJob = parameters.AlgorithmNodePacket;
            var initializeComplete = false;
            try
            {
                //Set common variables for console programs:

                if (baseJob.Type == PacketType.BacktestNode)
                {
                    var backtestJob = baseJob as BacktestNodePacket;
                    if (backtestJob == null)
                    {
                        throw new ArgumentException("Expected BacktestNodePacket but received " + baseJob.GetType().Name);
                    }

                    algorithm.SetMaximumOrders(int.MaxValue);

                    // set our parameters
                    algorithm.SetParameters(baseJob.Parameters);
                    algorithm.SetLiveMode(false);
                    algorithm.SetAvailableDataTypes(GetConfiguredDataFeeds());

                    //Set the source impl for the event scheduling
                    algorithm.Schedule.SetEventSchedule(parameters.RealTimeHandler);

                    // set the option chain provider
                    algorithm.SetOptionChainProvider(new CachingOptionChainProvider(new BacktestingOptionChainProvider()));

                    // set the future chain provider
                    algorithm.SetFutureChainProvider(new CachingFutureChainProvider(new BacktestingFutureChainProvider()));

                    var isolator = new Isolator();
                    isolator.ExecuteWithTimeLimit(TimeSpan.FromMinutes(5),
                        () =>
                        {
                            //Setup Base Algorithm:
                            algorithm.Initialize();
                        }, baseJob.Controls.RamAllocation,
                        sleepIntervalMillis: 50,
                        workerThread: WorkerThread);

                    //Set the time frontier of the algorithm
                    algorithm.SetDateTime(algorithm.StartDate.ConvertToUtc(algorithm.TimeZone));

                    if (Config.GetBool("have-init-json", false))
                    {
                        // set the algorithm's cashbook through live results json
                        RetrieveAndSetupCashBook(algorithm, Config.Get("init-json", ""));
                        // RetrieveAndSetupHoldings(algorithm, Config.Get("init-json", ""));
                        algorithm.BacktestInitPositionAction = () => RetrieveAndSetupHoldings(algorithm, Config.Get("init-json", ""));
                    }
                    //Finalize Initialization
                    algorithm.PostInitialize();

                    //Construct the backtest job packet:
                    backtestJob.PeriodStart = algorithm.StartDate;
                    backtestJob.PeriodFinish = algorithm.EndDate;

                    //Backtest Specific Parameters:
                    StartingDate = backtestJob.PeriodStart;

                    BaseSetupHandler.SetupCurrencyConversions(algorithm, parameters.UniverseSelection);
                    StartingPortfolioValue = algorithm.Portfolio.Cash;
                }
                else
                {
                    throw new Exception("The ConsoleSetupHandler is for backtests only. Use the BrokerageSetupHandler.");
                }
            }
            catch (Exception err)
            {
                Log.Error(err);
                Errors.Add(new AlgorithmSetupException("During the algorithm initialization, the following exception has occurred: ", err));
            }

            if (Errors.Count == 0)
            {
                initializeComplete = true;
            }

            return initializeComplete;
        }

        /// <summary>
        /// Get the available data feeds from config.json,
        /// If none available, throw an error
        /// </summary>
        private static Dictionary<SecurityType, List<TickType>> GetConfiguredDataFeeds()
        {
            var dataFeedsConfigString = Config.Get("security-data-feeds");

            Dictionary<SecurityType, List<TickType>> dataFeeds = new Dictionary<SecurityType, List<TickType>>();
            if (dataFeedsConfigString != string.Empty)
            {
                dataFeeds = JsonConvert.DeserializeObject<Dictionary<SecurityType, List<TickType>>>(dataFeedsConfigString);
            }

            return dataFeeds;
        }

        /// <summary>
        /// Matches type names as namespace qualified or just the name
        /// If expectedTypeName is null or empty, this will always return true
        /// </summary>
        /// <param name="currentTypeFullName"></param>
        /// <param name="expectedTypeName"></param>
        /// <returns>True on matching the type name</returns>
        private static bool MatchTypeName(string currentTypeFullName, string expectedTypeName)
        {
            if (string.IsNullOrEmpty(expectedTypeName))
            {
                return true;
            }
            return currentTypeFullName == expectedTypeName
                || currentTypeFullName.Substring(currentTypeFullName.LastIndexOf('.') + 1) == expectedTypeName;
        }

        private void RetrieveAndSetupCashBook(IAlgorithm? algorithm, string liveResultsJsonPath)
        {
            var liveResults = JsonConvert.DeserializeObject<LiveResult>(File.ReadAllText(liveResultsJsonPath));
            if (null == liveResults)
            {
                Log.Error("ConsoleSetupHandler.RetrieveAndSetupCashBook(): Unable to de-serialize: " + liveResultsJsonPath);
                return;
            }
            if (null == algorithm)
            {
                Log.Error("ConsoleSetupHandler.RetrieveAndSetupCashBook(): Algorithm is null.");
                return;
            }

            // we need to clear any possible manually initialized cash from the algorithm initialization.
            algorithm.Portfolio.CashBook.Clear();

            var cashBook = liveResults.Cash;
            foreach (var cashEntry in cashBook.Keys)
            {
                if (cashBook[cashEntry].Amount == 0)
                {
                    //Log.Debug($"The {cashEntry} Has no amount. Omitting here.");
                    //continue;
                }
                algorithm.Portfolio.SetCash(cashEntry, cashBook[cashEntry].Amount, cashBook[cashEntry].ConversionRate);
            }
        }

        private void RetrieveAndSetupHoldings(IAlgorithm? algorithm, string liveResultsJsonPath)
        {
            var liveResults = JsonConvert.DeserializeObject<LiveResult>(File.ReadAllText(liveResultsJsonPath));
            if (null == liveResults)
            {
                Log.Error("ConsoleSetupHandler.RetrieveAndSetupCashBook(): Unable to de-serialize: " + liveResultsJsonPath);
                return;
            }
            if (null == algorithm)
            {
                Log.Error("ConsoleSetupHandler.RetrieveAndSetupCashBook(): Algorithm is null.");
                return;
            }

            var supportedSecurityTypes = new HashSet<SecurityType>
                {
                    SecurityType.Equity, SecurityType.Forex, SecurityType.Cfd, SecurityType.Option, SecurityType.Future, SecurityType.Crypto
                };
            var minResolution = new Lazy<Resolution>(() => algorithm.Securities.Select(x => x.Value.Resolution).DefaultIfEmpty(Resolution.Minute).Min());

            // populate the algorithm with the account's current holdings
            var holdings = liveResults.Holdings;
            // add options first to ensure raw data normalization mode is set on the equity underlyings
            foreach (var symbol in holdings.Keys)
            {
                var holding = holdings[symbol];
                Log.Trace("ConsoleSetupHandler.RetrieveAndSetupHoldings(): Setup pre-existing holding: " + holding);

                // verify existing holding security type
                if (!supportedSecurityTypes.Contains(holding.Type))
                {
                    Log.Error("ConsoleSetupHandler.Setup(): Unsupported security type: " + holding.Type + "-" + holding.Symbol.Value);
                    Errors.Add(new AlgorithmSetupException("Found unsupported security type in existing brokerage holdings: " + holding.Type + ". " +
                        "QuantConnect currently supports the following security types: " + string.Join(",", supportedSecurityTypes)));

                    // keep aggregating these errors
                    continue;
                }

                if (IsPerpetualSymbol(holding.Symbol))
                {
                    //holding.Symbol = FixPerpetualSymbol(holding.Symbol, algorithm);
                }

                AddUnrequestedSecurity(algorithm, holding.Symbol, minResolution.Value);


                SecurityHolding? sholding = null;
                if (holding.HoldingType is SecurityHoldingType.Long or SecurityHoldingType.Short)
                {
                    Errors.Add(new NotImplementedException($"The holding type of {holding.Symbol} is not supported. Please use SecurityHoldingType.Net instead"));
                    return;
                }

                sholding = algorithm.Securities[holding.Symbol].Holdings;

                sholding.SetHoldings(holding.AveragePrice, holding.Quantity, holding.QuantityT0);
                sholding.SetProfit(holding.RealizedPnL);
                sholding.SetTotalFee(holding.Commission);

                algorithm.Securities[holding.Symbol].SetMarketPrice(new TradeBar
                {
                    Time = DateTime.Now,
                    Open = holding.MarketPrice,
                    High = holding.MarketPrice,
                    Low = holding.MarketPrice,
                    Close = holding.MarketPrice,
                    Volume = 0,
                    Symbol = holding.Symbol,
                    DataType = MarketDataType.TradeBar
                });
            }
        }

        private static void AddUnrequestedSecurity(IAlgorithm algorithm, Symbol symbol, Resolution minResolution)
        {
            if (!algorithm.Portfolio.ContainsKey(symbol))
            {
                Log.Trace("ConsoleSetupHandler.Setup(): Adding unrequested security: " + symbol.Value);

                if (symbol.SecurityType == SecurityType.Option)
                {
                    // add current option contract to the system
                    algorithm.AddOptionContract(symbol, minResolution, true, 1.0m);
                }
                else if (symbol.SecurityType == SecurityType.Future)
                {
                    // add current future contract to the system
                    algorithm.AddFutureContract(symbol, minResolution, true, 1.0m);
                }
                else
                {
                    // for items not directly requested set leverage to 1 and at the min resolution
                    algorithm.AddSecurity(symbol.SecurityType, symbol.Value, minResolution, symbol.ID.Market, true, 1.0m, false);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPerpetualSymbol(Symbol candidateSymbol)
        {
            return candidateSymbol.ID.SecurityType == SecurityType.Future &&
                   candidateSymbol.Value.ToLower().Contains("perpetual");
        }

        private static Symbol FixPerpetualSymbol(Symbol symbol, IAlgorithm algorithm)
        {
            if (!IsPerpetualSymbol(symbol))
            {
                return Symbol.Empty;
            }

            Symbol fixedSymbol = Symbol.Empty;


            // Create the canonical symbol to find the correct future contract
            var alias = "/" + symbol.Value;
            if (!SymbolCache.TryGetSymbol(alias, out var canonicalSymbol) ||
                canonicalSymbol.ID.Market != symbol.ID.Market ||
                canonicalSymbol.SecurityType != SecurityType.Future)
            {
                canonicalSymbol = QuantConnect.Symbol.Create(symbol.Value, SecurityType.Future, symbol.ID.Market, alias);
            }

            try
            {
                var symbolFromData = algorithm.FutureChainProvider.GetFutureContractList(canonicalSymbol, ((QCAlgorithm)algorithm).StartDate).FirstOrDefault();
                fixedSymbol = new Symbol(symbolFromData.ID, symbol.Value);
            }
            catch (Exception ex)
            {
                Log.Trace("ConsoleSetupHandler.FixDeribitPerpetualSymbol(): Unable to use future chain to fix the symbol: " +
                        symbol.Value + "\n Now use a future expired at 2030-12-31 to fixe it");
                fixedSymbol = QuantConnect.Symbol.CreateFuture(symbol.Value, symbol.ID.Market, new DateTime(2030, 12, 31));
            }

            return fixedSymbol;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
        }

    } // End Result Handler Thread:

} // End Namespace
