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
using NodaTime;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Lean.Engine.Results;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// Instance base class that will provide methods for creating new <see cref="TimeSlice"/>
    /// </summary>
    public class TimeSliceFactory
    {
        private readonly DateTimeZone _timeZone;

        // performance: these collections are not always used so keep a reference to an empty
        // instance to use and avoid unnecessary constructors and allocations
        private readonly List<UpdateData<ISecurityPrice>> _emptyCustom = new List<UpdateData<ISecurityPrice>>();
        private readonly TradeBars _emptyTradeBars = new TradeBars();
        private readonly QuoteBars _emptyQuoteBars = new QuoteBars();
        private readonly Ticks _emptyTicks = new Ticks();
        private readonly Splits _emptySplits = new Splits();
        private readonly Dividends _emptyDividends = new Dividends();
        private readonly Delistings _emptyDelistings = new Delistings();
        private readonly OptionChains _emptyOptionChains = new OptionChains();
        private readonly FuturesChains _emptyFuturesChains = new FuturesChains();
        private readonly SymbolChangedEvents _emptySymbolChangedEvents = new SymbolChangedEvents();
        private readonly SymbolBonusEvents _emptySymbolBonusEvents = new SymbolBonusEvents();
        private readonly Dictionary<string, Symbol> _options = new Dictionary<string, Symbol>();
        /// <summary>
        /// For security matrix of option price add by pang
        /// </summary>
        public IResultHandler ResultHandler { get; set; }

        /// <summary>
        /// Creates a new instance
        /// </summary>
        /// <param name="timeZone">The time zone required for computing algorithm and slice time</param>
        public TimeSliceFactory(DateTimeZone timeZone)
        {
            _timeZone = timeZone;
        }

        /// <summary>
        /// Creates a new <see cref="TimeSlice"/> for the specified time using the specified data
        /// </summary>
        /// <param name="utcDateTime">The UTC frontier date time</param>
        /// <param name="data">The data in this <see cref="TimeSlice"/></param>
        /// <param name="changes">The new changes that are seen in this time slice as a result of universe selection</param>
        /// <param name="universeData"></param>
        /// <returns>A new <see cref="TimeSlice"/> containing the specified data</returns>
        public TimeSlice Create(
            DateTime utcDateTime,
            List<DataFeedPacket> data,
            SecurityChanges changes,
            Dictionary<Universe, BaseDataCollection> universeData)
        {
            var count = 0;
            var securityUpdates = new List<UpdateData<ISecurityPrice>>(data.Count);
            List<UpdateData<ISecurityPrice>> custom = null;
            var consolidator = new List<UpdateData<SubscriptionDataConfig>>(data.Count);
            var allDataForAlgorithm = new List<BaseData>(data.Count);
            var optionUnderlyingUpdates = new Dictionary<Symbol, BaseData>();

            // we need to be able to reference the slice being created in order to define the
            // evaluation of option price models, so we define a 'future' that can be referenced
            // in the option price model evaluation delegates for each contract
            Slice slice = null;
            var sliceFuture = new Lazy<Slice>(() => slice);

            var algorithmTime = utcDateTime.ConvertFromUtc(_timeZone);
            SpecifyTimeZoneKind(ref algorithmTime, _timeZone);
            TradeBars tradeBars = null;
            QuoteBars quoteBars = null;
            Ticks ticks = null;
            Splits splits = null;
            Dividends dividends = null;
            Delistings delistings = null;
            OptionChains optionChains = null;
            FuturesChains futuresChains = null;
            SymbolChangedEvents symbolChanges = null;
            SymbolBonusEvents bonusEvents = null;

            UpdateEmptyCollections(algorithmTime);

            if (universeData.Count > 0)
            {
                // count universe data
                foreach (var kvp in universeData)
                {
                    count += kvp.Value.Data.Count;
                }
            }

            // ensure we read equity data before option data, so we can set the current underlying price
            foreach (var packet in data)
            {
                // filter out packets for removed subscriptions
                if (packet.IsSubscriptionRemoved)
                {
                    continue;
                }

                var list = packet.data;
                var symbol = packet.configuration.symbol;

                if (list.Count == 0) continue;

                // keep count of all data points
                if (list.Count == 1 && list[0] is BaseDataCollection)
                {
                    var baseDataCollectionCount = ((BaseDataCollection)list[0]).Data.Count;
                    if (baseDataCollectionCount == 0)
                    {
                        continue;
                    }
                    count += baseDataCollectionCount;
                }
                else
                {
                    count += list.Count;
                }

                if (!packet.configuration.IsInternalFeed && packet.configuration.IsCustomData)
                {
                    if (custom == null)
                    {
                        custom = new List<UpdateData<ISecurityPrice>>(1);
                    }
                    // This is all the custom data
                    custom.Add(new UpdateData<ISecurityPrice>(
                        packet.security, packet.configuration.Type, list));
                }

                var securityUpdate = new List<BaseData>(list.Count);
                var consolidatorUpdate = new List<BaseData>(list.Count);
                foreach (var baseData in list)
                {
                    if (!packet.configuration.IsInternalFeed)
                    {
                        // this is all the data that goes into the algorithm
                        allDataForAlgorithm.Add(baseData);
                    }
                    // don't add internal feed data to ticks/bars objects
                    if (baseData.dataType != MarketDataType.Auxiliary)
                    {
                        var tick = baseData as Tick;

                        if (!packet.configuration.IsInternalFeed)
                        {
                            // populate data dictionaries
                            switch (baseData.dataType)
                            {
                                case MarketDataType.Tick:
                                    if (ticks == null)
                                    {
                                        ticks = new Ticks(algorithmTime);
                                    }
                                    ticks.Add(baseData.symbol, (Tick)baseData);
                                    break;

                                case MarketDataType.TradeBar:
                                    if (tradeBars == null)
                                    {
                                        tradeBars = new TradeBars(algorithmTime);
                                    }
                                    tradeBars[baseData.symbol] = (TradeBar)baseData;
                                    break;

                                case MarketDataType.QuoteBar:
                                    if (quoteBars == null)
                                    {
                                        quoteBars = new QuoteBars(algorithmTime);
                                    }
                                    quoteBars[baseData.symbol] = (QuoteBar)baseData;
                                    break;

                                case MarketDataType.OptionChain:
                                    if (optionChains == null)
                                    {
                                        optionChains = new OptionChains(algorithmTime);
                                    }
                                    optionChains[baseData.symbol] = (OptionChain)baseData;
                                    break;

                                case MarketDataType.FuturesChain:
                                    if (futuresChains == null)
                                    {
                                        futuresChains = new FuturesChains(algorithmTime);
                                    }
                                    futuresChains[baseData.symbol] = (FuturesChain)baseData;
                                    break;
                            }

                            // special handling of options data to build the option chain
                            if (symbol.id.SecurityType == SecurityType.Option)
                            {
                                if (optionChains == null)
                                {
                                    optionChains = new OptionChains(algorithmTime);
                                }

                                if (baseData.dataType == MarketDataType.OptionChain)
                                {
                                    optionChains[baseData.symbol] = (OptionChain)baseData;
                                }
                                else if (!HandleOptionData(
                                    algorithmTime,
                                    baseData,
                                    optionChains,
                                    packet.security,
                                    sliceFuture,
                                    optionUnderlyingUpdates))
                                {
                                    continue;
                                }
                            }

                            // special handling of futures data to build the futures chain
                            if (symbol.id.SecurityType == SecurityType.Future)
                            {
                                if (futuresChains == null)
                                {
                                    futuresChains = new FuturesChains(algorithmTime);
                                }
                                if (baseData.dataType == MarketDataType.FuturesChain)
                                {
                                    futuresChains[baseData.symbol] = (FuturesChain)baseData;
                                }
                                else if (!HandleFuturesData(
                                    algorithmTime, baseData, futuresChains, packet.Security))
                                {
                                    continue;
                                }
                            }

                            // this is data used to update consolidators
                            // do not add it if it is a Suspicious tick
                            if (tick == null || !tick.Suspicious)
                            {
                                consolidatorUpdate.Add(baseData);
                            }
                        }

                        // this is the data used set market prices
                        // do not add it if it is a Suspicious tick
                        if (tick != null && tick.Suspicious) continue;

                        securityUpdate.Add(baseData);

                        // option underlying security update
                        // supports equity options and crypto options.
                        // writer: fifi
                        if (symbol.id.SecurityType == SecurityType.Equity
                            || symbol.id.SecurityType == SecurityType.Crypto)
                        {
                            optionUnderlyingUpdates[symbol] = baseData;
                        }
                    }
                    // include checks for various aux types so we don't have to construct the dictionaries in Slice
                    else
                    {
                        Delisting delisting;
                        if ((delisting = baseData as Delisting) != null)
                        {
                            if (delistings == null)
                            {
                                delistings = new Delistings(algorithmTime);
                            }
                            delistings[symbol] = delisting;
                        }
                        else
                        {
                            Dividend dividend;
                            if ((dividend = baseData as Dividend) != null)
                            {
                                if (dividends == null)
                                {
                                    dividends = new Dividends(algorithmTime);
                                }
                                dividends[symbol] = dividend;
                            }
                            else
                            {
                                Split split;
                                if ((split = baseData as Split) != null)
                                {
                                    if (splits == null)
                                    {
                                        splits = new Splits(algorithmTime);
                                    }
                                    splits[symbol] = split;
                                }
                                else
                                {
                                    SymbolChangedEvent symbolChangedEvent;
                                    if ((symbolChangedEvent = baseData as SymbolChangedEvent) != null)
                                    {
                                        if (symbolChanges == null)
                                        {
                                            symbolChanges = new SymbolChangedEvents(algorithmTime);
                                        }
                                        // symbol changes is keyed by the requested symbol
                                        symbolChanges[packet.Configuration.Symbol] = symbolChangedEvent;
                                    }
                                    else
                                    {
                                        SymbolBonusEvent bonusEvent;
                                        if ((bonusEvent = baseData as SymbolBonusEvent) != null)
                                        {
                                            if (bonusEvents == null)
                                            {
                                                bonusEvents = new SymbolBonusEvents(algorithmTime);
                                            }
                                            bonusEvents[packet.Configuration.Symbol] = bonusEvent;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (securityUpdate.Count > 0)
                {
                    securityUpdates.Add(new UpdateData<ISecurityPrice>(
                        packet.security, packet.configuration.Type, securityUpdate));
                }
                if (consolidatorUpdate.Count > 0)
                {
                    consolidator.Add(new UpdateData<SubscriptionDataConfig>(
                        packet.configuration, packet.configuration.Type, consolidatorUpdate));
                }
            }

            slice = new Slice(
                algorithmTime,
                allDataForAlgorithm,
                tradeBars ?? _emptyTradeBars,
                quoteBars ?? _emptyQuoteBars,
                ticks ?? _emptyTicks,
                optionChains ?? _emptyOptionChains,
                futuresChains ?? _emptyFuturesChains,
                splits ?? _emptySplits,
                dividends ?? _emptyDividends,
                delistings ?? _emptyDelistings,
                symbolChanges ?? _emptySymbolChangedEvents,
                bonusEvents ?? _emptySymbolBonusEvents,
                allDataForAlgorithm.Count > 0);

            return new TimeSlice(
                utcDateTime,
                count,
                slice,
                data,
                securityUpdates,
                consolidator,
                custom ?? _emptyCustom,
                changes,
                universeData);
        }

        private void UpdateEmptyCollections(DateTime algorithmTime)
        {
            // just in case
            _emptyTradeBars.Clear();
            _emptyQuoteBars.Clear();
            _emptyTicks.Clear();
            _emptySplits.Clear();
            _emptyDividends.Clear();
            _emptyDelistings.Clear();
            _emptyOptionChains.Clear();
            _emptyFuturesChains.Clear();
            _emptySymbolChangedEvents.Clear();
            _emptySymbolBonusEvents.Clear();

            _emptyTradeBars.Time
                = _emptyQuoteBars.Time
                = _emptyTicks.Time
                = _emptySplits.Time
                = _emptyDividends.Time
                = _emptyDelistings.Time
                = _emptyOptionChains.Time
                = _emptyFuturesChains.Time
                = _emptySymbolBonusEvents.Time
                = _emptySymbolChangedEvents.Time = algorithmTime;
        }

        private bool HandleOptionData(
            DateTime algorithmTime,
            BaseData baseData,
            OptionChains optionChains,
            ISecurityPrice securityPrice,
            Lazy<Slice> sliceFuture,
            IReadOnlyDictionary<Symbol, BaseData> optionUnderlyingUpdates)
        {
            var symbol = baseData.symbol;

            //lyq 20191006
            /*
            var canonical = symbol.ID.Market == Market.Deribit 
                ? Symbol.CreateOption(symbol.Underlying, symbol.ID.Market, symbol.ID.OptionStyle, symbol.ID.OptionRight, symbol.ID.StrikePrice, symbol.ID.Date, symbol.Value)
                : Symbol.CreateOption(symbol.Underlying, symbol.ID.Market, symbol.ID.OptionStyle, default(OptionRight), 0, SecurityIdentifier.DefaultDate); ;
            */
            //  var canonical = Symbol.CreateOption(symbol.Underlying, symbol.ID.Market, symbol.ID.OptionStyle, default(OptionRight), 0, SecurityIdentifier.DefaultDate); ;

            var id = symbol.underlying.identify;

            if (!_options.TryGetValue(id, out var canonical))
            {
                canonical = Symbol.CreateOption(
                    symbol.underlying,
                    symbol.id.Market,
                    symbol.id.OptionStyle,
                    default,
                    0,
                    SecurityIdentifier.DefaultDate);
                _options.Add(id, canonical);
            }

            if (!optionChains.TryGetValue(canonical, out var chain))
            {
                chain = new OptionChain(canonical, algorithmTime);
                optionChains[canonical] = chain;
            }

            // set the underlying current data point in the option chain
            var optionPrice = securityPrice as IOptionPrice;
            if (optionPrice != null)
            {
                if (optionPrice.Underlying == null)
                {
                    Log.Error($"TimeSlice.HandleOptionData(): {algorithmTime}: Option underlying is null");
                    return false;
                }

                //add buy pangtongqing
                if (optionUnderlyingUpdates.Count == 0)
                    return false;

                if (!optionUnderlyingUpdates.TryGetValue(optionPrice.Underlying.Symbol, out var underlyingData))
                {
                    underlyingData = optionPrice.Underlying.GetLastData();
                }

                if (underlyingData == null)
                {
                    Log.Error($"TimeSlice.HandleOptionData(): {algorithmTime}: Option underlying({optionPrice.Underlying.Symbol}) GetLastData returned null");
                    return false;
                }
                chain.underlying = underlyingData;
            }

            if (baseData is OptionChainUniverseDataCollection universeData)
            {
                if (universeData.underlying != null)
                {
                    foreach (var addedContract in chain.contracts)
                    {
                        addedContract.Value.underlyingLastPrice = chain.underlying.value;
                    }
                }
                foreach (var contractSymbol in universeData.filteredContracts)
                {
                    chain.filteredContracts.Add(contractSymbol);
                }
                return false;
            }

            if (!chain.contracts.TryGetValue(baseData.symbol, out var contract))
            {
                var underlyingSymbol = baseData.symbol.underlying;
                contract = new OptionContract(baseData.symbol, underlyingSymbol) {
                    time = baseData.endTime,
                    //LastPrice = security.Close,
                    //contract行情不使用历史直接行情用baseData更新
                    //writer：lh
                    //Volume = security.Volume,
                    //BidPrice = security.BidPrice,
                    //BidSize = security.BidSize,
                    //AskPrice = security.AskPrice,
                    //AskSize = security.AskSize,
                    openInterest = securityPrice.OpenInterest,
                    underlyingLastPrice = chain.underlying.value
                };

                chain.contracts[baseData.symbol] = contract;

                if (optionPrice != null)
                {
                    contract.SetOptionPriceModel(() => optionPrice.EvaluatePriceModel(sliceFuture.Value, contract));
                }
            }


            // populate ticks and trade bars dictionaries with no aux data
            switch (baseData.dataType)
            {
                case MarketDataType.Tick:
                    var tick = (Tick)baseData;
                    chain.ticks.Add(tick.symbol, tick);
                    UpdateContract(contract, tick);
                    break;

                case MarketDataType.TradeBar:
                    var tradeBar = (TradeBar)baseData;
                    chain.tradeBars[symbol] = tradeBar;
                    UpdateContract(contract, tradeBar);
                    break;

                case MarketDataType.QuoteBar:
                    var quote = (QuoteBar)baseData;
                    chain.quoteBars[symbol] = quote;
                    UpdateContract(contract, quote);
                    break;

                case MarketDataType.Base:
                    chain.AddAuxData(baseData);
                    break;
            }

            //此处添加数据发送给敏感矩阵
            if (ResultHandler != null && ResultHandler.IsPushOptionPrice)
            {
                ResultHandler.OptionPrice(contract);
            }
            return true;
        }


        private static bool HandleFuturesData(
            DateTime algorithmTime,
            BaseData baseData,
            FuturesChains futuresChains,
            ISecurityPrice security)
        {
            var symbol = baseData.symbol;

            var canonical = Symbol.Create(symbol.id.symbol, SecurityType.Future, symbol.id.Market);
            if (!futuresChains.TryGetValue(canonical, out var chain))
            {
                chain = new FuturesChain(canonical, algorithmTime);
                futuresChains[canonical] = chain;
            }

            if (baseData is FuturesChainUniverseDataCollection universeData)
            {
                foreach (var contractSymbol in universeData.filteredContracts)
                {
                    chain.filteredContracts.Add(contractSymbol);
                }
                return false;
            }

            if (!chain.contracts.TryGetValue(baseData.symbol, out var contract))
            {
                var underlyingSymbol = baseData.symbol.underlying;
                contract = new FuturesContract(baseData.symbol, underlyingSymbol) {
                    time = baseData.endTime,
                    lastPrice = security.Close,
                    volume = security.Volume,
                    bidPrice = security.BidPrice,
                    bidSize = security.BidSize,
                    askPrice = security.AskPrice,
                    askSize = security.AskSize,
                    openInterest = security.OpenInterest
                };
                chain.contracts[baseData.symbol] = contract;
            }

            // populate ticks and trade bars dictionaries with no aux data
            switch (baseData.DataType)
            {
                case MarketDataType.Tick:
                    var tick = (Tick)baseData;
                    chain.ticks.Add(tick.symbol, tick);
                    UpdateContract(contract, tick);
                    break;

                case MarketDataType.TradeBar:
                    var tradeBar = (TradeBar)baseData;
                    chain.tradeBars[symbol] = tradeBar;
                    UpdateContract(contract, tradeBar);
                    break;

                case MarketDataType.QuoteBar:
                    var quote = (QuoteBar)baseData;
                    chain.quoteBars[symbol] = quote;
                    UpdateContract(contract, quote);
                    break;

                case MarketDataType.Base:
                    chain.AddAuxData(baseData);
                    break;
            }
            return true;
        }

        private static void UpdateContract(OptionContract contract, QuoteBar quote)
        {
            if (quote.ask != null && quote.ask.close != 0m)
            {
                contract.askPrice = quote.ask.close;
                contract.askSize = quote.LastAskSize;
            }
            if (quote.bid != null && quote.bid.close != 0m)
            {
                contract.bidPrice = quote.bid.close;
                contract.bidSize = quote.LastBidSize;
            }
        }

        private static void UpdateContract(OptionContract contract, Tick tick)
        {
            contract.lastPrice = tick.Price;
            contract.askPrice = tick.AskPrice;
            contract.askSize = tick.AskSize;
            contract.bidPrice = tick.BidPrice;
            contract.bidSize = tick.BidSize;
            contract.openInterest = tick.OpenInterest;
            contract.volume = tick.Volume;
            contract.time = tick.time;
            /*
            if (tick.TickType == TickType.Trade)
            {
                contract.LastPrice = tick.Price;
            }
            else if (tick.TickType == TickType.Quote)
            {
                // update by lyq   
                // 根据tick的档行情更新contract
                contract.AskPrice = tick.AskPrice;
                contract.AskSize =  tick.AskSize;
                contract.BidPrice = tick.BidPrice;
                contract.BidSize =  tick.BidSize;
            }
            else if (tick.TickType == TickType.OpenInterest)
            {
                if (tick.Value != 0m)
                {
                    contract.OpenInterest = tick.Value;
                }
            }
            */
        }

        private static void UpdateContract(OptionContract contract, TradeBar tradeBar)
        {
            if (tradeBar.value == 0m) return;
            contract.LastPrice = tradeBar.value;
            contract.volume = tradeBar.volume;
        }

        private static void UpdateContract(FuturesContract contract, QuoteBar quote)
        {
            if (quote.ask != null && quote.ask.close != 0m)
            {
                contract.askPrice = quote.ask.close;
                contract.askSize = quote.LastAskSize;
            }
            if (quote.bid != null && quote.bid.close != 0m)
            {
                contract.bidPrice = quote.bid.close;
                contract.bidSize = quote.LastBidSize;
            }
        }

        private static void UpdateContract(FuturesContract contract, Tick tick)
        {
            contract.LastPrice = tick.Price;
            contract.AskPrice = tick.AskPrice;
            contract.AskSize = tick.AskSize;
            contract.BidPrice = tick.BidPrice;
            contract.BidSize = tick.BidSize;
            contract.OpenInterest = tick.OpenInterest;
            contract.Volume = tick.Volume;
            contract.Time = tick.Time;

            /*
            if (tick.TickType == TickType.Trade)
            {
                contract.lastPrice = tick.value;
            }
            else if (tick.TickType == TickType.Quote)
            {
                if (tick.AskPrice != 0m)
                {
                    contract.askPrice = tick.AskPrice;
                    contract.askSize = tick.AskSize;
                }
                if (tick.BidPrice != 0m)
                {
                    contract.bidPrice = tick.BidPrice;
                    contract.bidSize = tick.BidSize;
                }
            }
            else if (tick.TickType == TickType.OpenInterest)
            {
                if (tick.value != 0m)
                {
                    contract.openInterest = tick.value;
                }
            }
            */
        }

        private static void UpdateContract(FuturesContract contract, TradeBar tradeBar)
        {
            if (tradeBar.value == 0m) return;
            contract.lastPrice = tradeBar.value;
            contract.volume = tradeBar.volume;
        }

        
        // TODO: This is a temporary implementation to make sure the inputTime.ToUniversalTime() works when the 
        //       inputTime is in UTC timezone. Otherwise the inputTime.Kind will be left as UnSpecified and it
        //       will performe as a local timezone kind, which is significantly wrong. PLEASE remove here if the 
        //       a new DateTime with timezone info class implemented.
        private static void SpecifyTimeZoneKind(ref DateTime inputTime, DateTimeZone targetTz)
        {
            if (inputTime.Kind != DateTimeKind.Unspecified)
            {
                Log.Debug($"TimeSliceFactory::SpecifyTimeZoneKind(DateTime inputTime): Timezone kind already specified: {inputTime.Kind}");
                return;
            }

            if (targetTz == DateTimeZone.Utc)
            {
                inputTime = DateTime.SpecifyKind(inputTime, DateTimeKind.Utc);
            }
        }
    }
}
