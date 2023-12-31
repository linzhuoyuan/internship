﻿/*
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
using System.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using QuantConnect.Algorithm;
using QuantConnect.AlgorithmFactory;
using QuantConnect.Brokerages.Backtesting;
using QuantConnect.Configuration;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Packets;
using QuantConnect.Data;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Securities;
using QuantConnect.Util;
using Newtonsoft.Json;

namespace QuantConnect.Lean.Engine.Setup
{
    /// <summary>
    /// Backtesting setup handler processes the algorithm initialize method and sets up the internal state of the algorithm class.
    /// </summary>
    public class BacktestingSetupHandler : ISetupHandler
    {
        private TimeSpan _maxRuntime = TimeSpan.FromSeconds(300);
        private int _maxOrders = 0;
        private DateTime _startingDate = new(1998, 01, 01);

        /// <summary>
        /// The worker thread instance the setup handler should use
        /// </summary>
        public WorkerThread WorkerThread { get; set; }

        /// <summary>
        /// Internal errors list from running the setup procedures.
        /// </summary>
        public List<Exception> Errors
        {
            get;
            set;
        }

        /// <summary>
        /// Maximum runtime of the algorithm in seconds.
        /// </summary>
        /// <remarks>Maximum runtime is a formula based on the number and resolution of symbols requested, and the days backtesting</remarks>
        public TimeSpan MaximumRuntime => _maxRuntime;

        /// <summary>
        /// Starting capital according to the users initialize routine.
        /// </summary>
        /// <remarks>Set from the user code.</remarks>
        /// <seealso cref="QCAlgorithm.SetCash(decimal)"/>
        public decimal StartingPortfolioValue { get; private set; } = 0;

        /// <summary>
        /// Start date for analysis loops to search for data.
        /// </summary>
        /// <seealso cref="QCAlgorithm.SetStartDate(DateTime)"/>
        public DateTime StartingDate => _startingDate;

        /// <summary>
        /// Maximum number of orders for this backtest.
        /// </summary>
        /// <remarks>To stop algorithm flooding the backtesting system with hundreds of megabytes of order data we limit it to 100 per day</remarks>
        public int MaxOrders => _maxOrders;

        /// <summary>
        /// Initialize the backtest setup handler.
        /// </summary>
        public BacktestingSetupHandler()
        {
            Errors = new List<Exception>();
        }

        /// <summary>
        /// Create a new instance of an algorithm from a physical dll path.
        /// </summary>
        /// <param name="assemblyPath">The path to the assembly's location</param>
        /// <param name="algorithmNodePacket">Details of the task required</param>
        /// <param name="loadTimeLimit">loadTimeLimit</param>
        /// <returns>A new instance of IAlgorithm, or throws an exception if there was an error</returns>
        public virtual IAlgorithm CreateAlgorithmInstance(AlgorithmNodePacket algorithmNodePacket, string assemblyPath, int loadTimeLimit = 60)
        {
            // limit load times to 60 seconds and force the assembly to have exactly one derived type
            var loader = new Loader(algorithmNodePacket.Language, TimeSpan.FromSeconds(60), names => names.SingleOrAlgorithmTypeName(Config.Get("algorithm-type-name")), WorkerThread);
            var complete = loader.TryCreateAlgorithmInstanceWithIsolator(assemblyPath, algorithmNodePacket.RamAllocation, out var algorithm, out var error);
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
        /// Setup the algorithm cash, dates and data subscriptions as desired.
        /// </summary>
        /// <param name="parameters">The parameters object to use</param>
        /// <returns>Boolean true on successfully initializing the algorithm</returns>
        public bool Setup(SetupHandlerParameters parameters)
        {
            var algorithm = parameters.Algorithm;
            var job = parameters.AlgorithmNodePacket as BacktestNodePacket;
            if (job == null)
            {
                throw new ArgumentException("Expected BacktestNodePacket but received " + parameters.AlgorithmNodePacket.GetType().Name);
            }

            Log.Trace(string.Format("BacktestingSetupHandler.Setup(): Setting up job: Plan: {0}, UID: {1}, PID: {2}, Version: {3}, Source: {4}", job.UserPlan, job.UserId, job.ProjectId, job.Version, job.RequestSource));

            if (algorithm == null)
            {
                Errors.Add(new AlgorithmSetupException("Could not create instance of algorithm"));
                return false;
            }

            algorithm.Name = job.GetAlgorithmName();

            //Make sure the algorithm start date ok.
            if (job.PeriodStart == default && algorithm.StartDate == default)
            {
                Errors.Add(new AlgorithmSetupException("Algorithm start date was never set"));
                return false;
            }

            var controls = job.Controls;
            var isolator = new Isolator();
            var initializeComplete = isolator.ExecuteWithTimeLimit(TimeSpan.FromMinutes(5), () =>
            {
                try
                {
                    parameters.ResultHandler.SendStatusUpdate(AlgorithmStatus.Initializing, "Initializing algorithm...");

                    //Set our parameters
                    algorithm.SetParameters(job.Parameters);

                    //Algorithm is backtesting, not live:
                    algorithm.SetLiveMode(false);

                    //Set the source impl for the event scheduling
                    algorithm.Schedule.SetEventSchedule(parameters.RealTimeHandler);

                    // set the option chain provider
                    algorithm.SetOptionChainProvider(new CachingOptionChainProvider(new BacktestingOptionChainProvider()));

                    // set the future chain provider
                    algorithm.SetFutureChainProvider(new CachingFutureChainProvider(new BacktestingFutureChainProvider()));

                    // before we call initialize
                    BaseSetupHandler.LoadBacktestJobAccountCurrency(algorithm, job);

                    //Initialise the algorithm, get the required data:
                    algorithm.Initialize();

                    if (Config.GetBool("is-comparing-live-job"))
                    {
                        // Clear the original start and end date setup in the backtesting job.
                        ((QCAlgorithm)algorithm).SetStartDate(new DateTime(1900, 1, 1));
                        ((QCAlgorithm)algorithm).SetEndDate(DateTime.MaxValue);
                        if (Config.Get("start-date", "") == "")
                        {
                            Errors.Add(new AlgorithmSetupException("When comparing live job, start date must be provided by config.json"));
                        }
                        else
                        {
                            ((QCAlgorithm)algorithm).SetStartDate(DateTime.ParseExact(Config.Get("start-date"), "yyyy-MM-dd HH:mm:ss",
                                            System.Globalization.CultureInfo.InvariantCulture));
                        }

                        if (Config.Get("end-date", "") == "")
                        {
                            Errors.Add(new AlgorithmSetupException("When comparing live job, end date must be provided by config.json"));
                        }
                        else
                        {
                            ((QCAlgorithm)algorithm).SetEndDate(DateTime.ParseExact(Config.Get("end-date"), "yyyy-MM-dd HH:mm:ss",
                                            System.Globalization.CultureInfo.InvariantCulture));
                        }

                        // set the algorithm's cashbook through live results json
                        RetrieveAndSetupCashBook(algorithm, Config.Get("live-results", ""));
                        RetrieveAndSetupHoldings(algorithm, Config.Get("live-results", ""));
                    }

                    // set start and end date if present in the job
                    if (job.PeriodStart != DateTime.MinValue)
                    {
                        algorithm.SetStartDate(job.PeriodStart);
                    }
                    if (job.PeriodFinish != DateTime.MinValue)
                    {
                        algorithm.SetEndDate(job.PeriodFinish);
                    }

                    // after we call initialize
                    BaseSetupHandler.LoadBacktestJobCashAmount(algorithm, job);

                    // finalize initialization
                    algorithm.PostInitialize();
                }
                catch (Exception err)
                {
                    Log.Error(err);
                    Errors.Add(new AlgorithmSetupException("During the algorithm initialization, the following exception has occurred: ", err));
                }
            }, controls.RamAllocation,
                sleepIntervalMillis: 50,  // entire system is waiting on this, so be as fast as possible
                workerThread: WorkerThread);

            //Before continuing, detect if this is ready:
            if (!initializeComplete) return false;

            // TODO: Refactor the BacktestResultHandler to use algorithm not job to set times
            job.PeriodStart = algorithm.StartDate;
            job.PeriodFinish = algorithm.EndDate;

            //Calculate the max runtime for the strategy
            _maxRuntime = GetMaximumRuntime(job.PeriodStart, job.PeriodFinish, algorithm.SubscriptionManager, algorithm.UniverseManager, parameters.AlgorithmNodePacket.Controls);

            // Python takes forever; lets give it 10x longer to finish.
            if (job.Language == Language.Python)
            {
                _maxRuntime = _maxRuntime.Add(TimeSpan.FromSeconds(_maxRuntime.TotalSeconds * 9));
            }

            BaseSetupHandler.SetupCurrencyConversions(algorithm, parameters.UniverseSelection);
            StartingPortfolioValue = algorithm.Portfolio.Cash;

            //Max Orders: 10k per backtest:
            if (job.UserPlan == UserPlan.Free)
            {
                _maxOrders = 10000;
            }
            else
            {
                _maxOrders = int.MaxValue;
                _maxRuntime += _maxRuntime;
            }

            //Set back to the algorithm,
            algorithm.SetMaximumOrders(_maxOrders);

            //Starting date of the algorithm:
            _startingDate = job.PeriodStart;

            //Put into log for debugging:
            Log.Trace("SetUp Backtesting: User: " + job.UserId + " ProjectId: " + job.ProjectId + " AlgoId: " + job.AlgorithmId);
            Log.Trace("Dates: Start: " + job.PeriodStart.ToShortDateString() + " End: " + job.PeriodFinish.ToShortDateString() + " Cash: " + StartingPortfolioValue.ToString("C"));

            if (Errors.Count > 0)
            {
                initializeComplete = false;
            }
            return initializeComplete;
        }

        /// <summary>
        /// Calculate the maximum runtime for this algorithm job.
        /// </summary>
        /// <param name="start">State date of the algorithm</param>
        /// <param name="finish">End date of the algorithm</param>
        /// <param name="subscriptionManager">Subscription Manager</param>
        /// <param name="universeManager">Universe manager containing configured universes</param>
        /// <param name="controls">Job controls instance</param>
        /// <returns>Timespan maximum run period</returns>
        private TimeSpan GetMaximumRuntime(DateTime start, DateTime finish, SubscriptionManager subscriptionManager, UniverseManager universeManager, Controls controls)
        {
            // option/futures chain subscriptions
            var derivativeSubscriptions = subscriptionManager.Subscriptions
                .Where(x => x.Symbol.IsCanonical())
                .Select(x => controls.GetLimit(x.Resolution))
                .Sum();

            // universe coarse/fine/custom subscriptions
            var universeSubscriptions = universeManager
                // use max limit for universes without explicitly added securities
                .Sum(u => u.Value.Members.Count == 0 ? controls.GetLimit(u.Value.UniverseSettings.Resolution) : u.Value.Members.Count);

            var subscriptionCount = derivativeSubscriptions + universeSubscriptions;

            double maxRunTime = 0;
            var jobDays = (finish - start).TotalDays;

            maxRunTime = 10 * subscriptionCount * jobDays;

            //Rationalize:
            if ((maxRunTime / 3600) > 12)
            {
                //12 hours maximum
                maxRunTime = 3600 * 12;
            }
            else if (maxRunTime < 60)
            {
                //If less than 60 seconds.
                maxRunTime = 60;
            }

            Log.Trace("BacktestingSetupHandler.GetMaxRunTime(): Job Days: " + jobDays + " Max Runtime: " + Math.Round(maxRunTime / 60) + " min");

            //Override for windows:
            if (OS.IsWindows)
            {
                maxRunTime = 24 * 60 * 60;
            }

            return TimeSpan.FromSeconds(maxRunTime);
        }

        private void RetrieveAndSetupCashBook(IAlgorithm? algorithm, string liveResultsJsonPath)
        {
            var liveResults = JsonConvert.DeserializeObject<LiveResult>(File.ReadAllText(liveResultsJsonPath));
            if (null == liveResults)
            {
                Log.Error("BacktestingSetupHandler.RetrieveAndSetupCashBook(): Unable to de-serialize: " + liveResultsJsonPath);
                return;
            }
            if (null == algorithm)
            {
                Log.Error("BacktestingSetupHandler.RetrieveAndSetupCashBook(): Algorithm is null.");
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
                Log.Error("BacktestingSetupHandler.RetrieveAndSetupCashBook(): Unable to de-serialize: " + liveResultsJsonPath);
                return;
            }
            if (null == algorithm)
            {
                Log.Error("BacktestingSetupHandler.RetrieveAndSetupCashBook(): Algorithm is null.");
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
                Log.Trace("BacktestingSetupHandler.RetrieveAndSetupHoldings(): Setup pre-existing holding: " + holding);

                // verify existing holding security type
                if (!supportedSecurityTypes.Contains(holding.Type))
                {
                    Log.Error("BacktestingSetupHandler.Setup(): Unsupported security type: " + holding.Type + "-" + holding.Symbol.Value);
                    Errors.Add(new AlgorithmSetupException("Found unsupported security type in existing brokerage holdings: " + holding.Type + ". " +
                        "QuantConnect currently supports the following security types: " + string.Join(",", supportedSecurityTypes)));

                    // keep aggregating these errors
                    continue;
                }

                //if (IsPerpetualSymbol(holding.Symbol))
                //{
                //    holding.Symbol = FixPerpetualSymbol(holding.Symbol, algorithm);
                //}

                AddUnrequestedSecurity(algorithm, holding.Symbol, minResolution.Value);


                SecurityHolding? sholding = null;
                if (holding.HoldingType == SecurityHoldingType.Long || holding.HoldingType == SecurityHoldingType.Short)
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
                Log.Trace("BacktestingSetupHandler.Setup(): Adding unrequested security: " + symbol.Value);

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
