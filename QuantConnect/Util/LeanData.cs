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
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NodaTime;
using QLNet;
using QuantConnect.Data;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Data.Market;
using QuantConnect.Logging;
using QuantConnect.Securities;
using QuantConnect.Securities.Future;
using QuantConnect.Securities.Option;
using Path = System.IO.Path;

namespace QuantConnect.Util
{
    /// <summary>
    /// Provides methods for generating lean data file content
    /// </summary>
    public static class LeanData
    {
        /// <summary>
        /// Converts the specified base data instance into a lean data file csv line.
        /// This method takes into account the fake that base data instances typically
        /// are time stamped in the exchange time zone, but need to be written to disk
        /// in the data time zone.
        /// </summary>
        public static string GenerateLine(IBaseData data, Resolution resolution, DateTimeZone exchangeTimeZone, DateTimeZone dataTimeZone)
        {
            var clone = data.Clone();
            clone.Time = data.Time.ConvertTo(exchangeTimeZone, dataTimeZone);
            return GenerateLine(clone, clone.Symbol.ID.SecurityType, resolution);
        }

        /// <summary>
        /// Converts the specified base data instance into a lean data file csv line
        /// </summary>
        public static string GenerateLine(IBaseData data, SecurityType securityType, Resolution resolution)
        {
            var milliseconds = data.Time.TimeOfDay.TotalMilliseconds.ToString(CultureInfo.InvariantCulture);
            var longTime = data.Time.ToString(DateFormat.TwelveCharacter);

            switch (securityType)
            {
                case SecurityType.Equity:
                    switch (resolution)
                    {
                        case Resolution.Tick:
                            var tick = (Tick)data;
                            return ToCsv(milliseconds, Scale(tick.LastPrice), tick.Quantity, tick.Exchange, tick.SaleCondition, tick.Suspicious ? "1" : "0");

                        case Resolution.Minute:
                        case Resolution.Second:
                            var bar = (TradeBar)data;
                            return ToCsv(milliseconds, Scale(bar.Open), Scale(bar.High), Scale(bar.Low), Scale(bar.Close), bar.Volume);

                        case Resolution.Hour:
                        case Resolution.Daily:
                            var bigBar = (TradeBar)data;
                            return ToCsv(longTime, Scale(bigBar.Open), Scale(bigBar.High), Scale(bigBar.Low), Scale(bigBar.Close), bigBar.Volume);
                    }
                    break;

                case SecurityType.Crypto:
                    switch (resolution)
                    {
                        case Resolution.Tick:
                            if (data is not Tick tick) throw new NullReferenceException("Cryto tick could not be created");

                            if (tick.TickType == TickType.Trade)
                            {
                                return ToCsv(milliseconds, tick.LastPrice, tick.Quantity);
                            }
                            if (tick.TickType == TickType.Quote)
                            {
                                return ToCsv(milliseconds, tick.BidPrice, tick.BidSize, tick.AskPrice, tick.AskSize);
                            }
                            throw new ArgumentException("Cryto tick could not be created");
                        case Resolution.Second:
                        case Resolution.Minute:
                            if (data is QuoteBar quoteBar)
                            {
                                return ToCsv(milliseconds,
                                    ToNonScaledCsv(quoteBar.Bid), quoteBar.LastBidSize,
                                    ToNonScaledCsv(quoteBar.Ask), quoteBar.LastAskSize);
                            }

                            if (data is TradeBar tradeBar)
                            {
                                return ToCsv(milliseconds, tradeBar.Open, tradeBar.High, tradeBar.Low, tradeBar.Close, tradeBar.Volume);
                            }
                            throw new NullReferenceException("Cryto minute/second bar could not be created");

                        case Resolution.Hour:
                        case Resolution.Daily:
                            if (data is QuoteBar bigQuoteBar)
                            {
                                return ToCsv(longTime,
                                    ToNonScaledCsv(bigQuoteBar.Bid), bigQuoteBar.LastBidSize,
                                    ToNonScaledCsv(bigQuoteBar.Ask), bigQuoteBar.LastAskSize);
                            }

                            if (data is TradeBar bigTradeBar)
                            {
                                return ToCsv(longTime,
                                             bigTradeBar.Open,
                                             bigTradeBar.High,
                                             bigTradeBar.Low,
                                             bigTradeBar.Close,
                                             bigTradeBar.Volume);
                            }
                            throw new NullReferenceException("Cryto hour/daily bar could not be created");
                    }
                    break;
                case SecurityType.Forex:
                case SecurityType.Cfd:
                    switch (resolution)
                    {
                        case Resolution.Tick:
                            if (data is not Tick tick) throw new NullReferenceException("tick");
                            return ToCsv(milliseconds, tick.BidPrice, tick.AskPrice);

                        case Resolution.Second:
                        case Resolution.Minute:
                            if (data is not QuoteBar bar) throw new NullReferenceException("bar");
                            return ToCsv(milliseconds,
                                ToNonScaledCsv(bar.Bid), bar.LastBidSize,
                                ToNonScaledCsv(bar.Ask), bar.LastAskSize);

                        case Resolution.Hour:
                        case Resolution.Daily:
                            if (data is not QuoteBar bigBar) throw new NullReferenceException("big bar");
                            return ToCsv(longTime,
                                ToNonScaledCsv(bigBar.Bid), bigBar.LastBidSize,
                                ToNonScaledCsv(bigBar.Ask), bigBar.LastAskSize);
                    }
                    break;

                case SecurityType.Option:
                    switch (resolution)
                    {
                        case Resolution.Tick:
                            var tick = (Tick)data;
                            if (tick.TickType == TickType.Trade)
                            {
                                return ToCsv(milliseconds,
                                    Scale(tick.LastPrice), tick.Quantity, tick.Exchange, tick.SaleCondition, tick.Suspicious ? "1" : "0");
                            }
                            if (tick.TickType == TickType.Quote)
                            {
                                return ToCsv(milliseconds,
                                    Scale(tick.BidPrice), tick.BidSize, Scale(tick.AskPrice), tick.AskSize, tick.Exchange, tick.Suspicious ? "1" : "0");
                            }
                            if (tick.TickType == TickType.OpenInterest)
                            {
                                return ToCsv(milliseconds, tick.Value);
                            }
                            break;

                        case Resolution.Second:
                        case Resolution.Minute:
                            // option and future data can be quote or trade bars
                            if (data is QuoteBar quoteBar)
                            {
                                return ToCsv(milliseconds,
                                    ToScaledCsv(quoteBar.Bid), quoteBar.LastBidSize,
                                    ToScaledCsv(quoteBar.Ask), quoteBar.LastAskSize);
                            }

                            if (data is TradeBar tradeBar)
                            {
                                return ToCsv(milliseconds,
                                    Scale(tradeBar.Open), Scale(tradeBar.High), Scale(tradeBar.Low), Scale(tradeBar.Close), tradeBar.Volume);
                            }

                            if (data is OpenInterest openInterest)
                            {
                                return ToCsv(milliseconds, openInterest.Value);
                            }
                            break;

                        case Resolution.Hour:
                        case Resolution.Daily:
                            // option and future data can be quote or trade bars
                            if (data is QuoteBar bigQuoteBar)
                            {
                                return ToCsv(longTime,
                                    ToScaledCsv(bigQuoteBar.Bid), bigQuoteBar.LastBidSize,
                                    ToScaledCsv(bigQuoteBar.Ask), bigQuoteBar.LastAskSize);
                            }

                            if (data is TradeBar bigTradeBar)
                            {
                                return ToCsv(longTime, ToScaledCsv(bigTradeBar), bigTradeBar.Volume);
                            }

                            if (data is OpenInterest bigOpenInterest)
                            {
                                return ToCsv(milliseconds, bigOpenInterest.Value);
                            }
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
                    }
                    break;
                case SecurityType.Future:
                    switch (resolution)
                    {
                        case Resolution.Tick:
                            var tick = (Tick)data;
                            if (tick.TickType == TickType.Trade)
                            {
                                return ToCsv(milliseconds,
                                             tick.LastPrice, tick.Quantity, tick.Exchange, tick.SaleCondition, tick.Suspicious ? "1" : "0");
                            }
                            if (tick.TickType == TickType.Quote)
                            {
                                return ToCsv(milliseconds,
                                             tick.BidPrice, tick.BidSize, tick.AskPrice, tick.AskSize, tick.Exchange, tick.Suspicious ? "1" : "0");
                            }
                            if (tick.TickType == TickType.OpenInterest)
                            {
                                return ToCsv(milliseconds, tick.Value);
                            }
                            break;

                        case Resolution.Second:
                        case Resolution.Minute:
                            // option and future data can be quote or trade bars
                            if (data is QuoteBar quoteBar)
                            {
                                return ToCsv(milliseconds,
                                    ToNonScaledCsv(quoteBar.Bid), quoteBar.LastBidSize,
                                    ToNonScaledCsv(quoteBar.Ask), quoteBar.LastAskSize);
                            }

                            if (data is TradeBar tradeBar)
                            {
                                return ToCsv(milliseconds,
                                             tradeBar.Open, tradeBar.High, tradeBar.Low, tradeBar.Close, tradeBar.Volume);
                            }

                            if (data is OpenInterest openInterest)
                            {
                                return ToCsv(milliseconds, openInterest.Value);
                            }
                            break;

                        case Resolution.Hour:
                        case Resolution.Daily:
                            // option and future data can be quote or trade bars
                            if (data is QuoteBar bigQuoteBar)
                            {
                                return ToCsv(longTime,
                                    ToNonScaledCsv(bigQuoteBar.Bid), bigQuoteBar.LastBidSize,
                                    ToNonScaledCsv(bigQuoteBar.Ask), bigQuoteBar.LastAskSize);
                            }

                            if (data is TradeBar bigTradeBar)
                            {
                                return ToCsv(longTime, ToNonScaledCsv(bigTradeBar), bigTradeBar.Volume);
                            }

                            if (data is OpenInterest bigOpenInterest)
                            {
                                return ToCsv(longTime, bigOpenInterest.Value);
                            }
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(securityType), securityType, null);
            }

            throw new NotImplementedException("LeanData.GenerateLine has not yet been implemented for security type: " + securityType + " at resolution: " + resolution);
        }

        /// <summary>
        /// Gets the data type required for the specified combination of resolution and tick type
        /// </summary>
        /// <param name="resolution">The resolution, if Tick, the Type returned is always Tick</param>
        /// <param name="tickType">The <see cref="TickType"/> that primarily dictates the type returned</param>
        /// <returns>The Type used to create a subscription</returns>
        public static Type GetDataType(Resolution resolution, TickType tickType)
        {
            if (resolution == Resolution.Tick) return typeof(Tick);
            if (tickType == TickType.OpenInterest) return typeof(OpenInterest);
            if (tickType == TickType.Quote) return typeof(QuoteBar);
            return typeof(TradeBar);
        }


        /// <summary>
        /// Determines if the Type is a 'common' type used throughout lean
        /// This method is helpful in creating <see cref="SubscriptionDataConfig"/>
        /// </summary>
        /// <param name="baseDataType">The Type to check</param>
        /// <returns>A bool indicating whether the type is of type <see cref="TradeBar"/>
        ///  <see cref="QuoteBar"/> or <see cref="OpenInterest"/></returns>
        public static bool IsCommonLeanDataType(Type baseDataType)
        {
            if (baseDataType == typeof(TradeBar) ||
                baseDataType == typeof(QuoteBar) ||
                baseDataType == typeof(OpenInterest))
            {
                return true;
            }

            return false;
        }


        /// <summary>
        /// Generates the full zip file path rooted in the <paramref name="dataDirectory"/>
        /// </summary>
        public static string GenerateZipFilePath(string dataDirectory, Symbol symbol, DateTime date, Resolution resolution, TickType tickType)
        {
            return Path.Combine(dataDirectory, GenerateRelativeZipFilePath(symbol, date, resolution, tickType));
        }

        /// <summary>
        /// Generates the full zip file path rooted in the <paramref name="dataDirectory"/>
        /// </summary>
        public static string GenerateZipFilePath(string dataDirectory, string symbol, SecurityType securityType, string market, DateTime date, Resolution resolution)
        {
            return Path.Combine(dataDirectory, GenerateRelativeZipFilePath(symbol, securityType, market, date, resolution));
        }

        /// <summary>
        /// Generates the relative zip directory for the specified symbol/resolution
        /// </summary>
        public static string GenerateRelativeZipFileDirectory(Symbol symbol, Resolution resolution)
        {
            var isHourOrDaily = resolution is Resolution.Hour or Resolution.Daily;
            var securityType = symbol.ID.SecurityType.SecurityTypeToLower();
            var market = symbol.ID.Market.ToLower();
            var res = resolution.ResolutionToLower();
            var directory = Path.Combine(securityType, market, res);
            switch (symbol.ID.SecurityType)
            {
                case SecurityType.Base:
                case SecurityType.Equity:
                case SecurityType.Forex:
                case SecurityType.Cfd:
                case SecurityType.Crypto:
                    return !isHourOrDaily ? Path.Combine(directory, symbol.Value.ToLower()) : directory;

                case SecurityType.Option:
                    // options uses the underlying symbol for pathing
                    return !isHourOrDaily ? Path.Combine(directory, symbol.Underlying.Value.ToLower()) : directory;

                case SecurityType.Future:
                    return !isHourOrDaily ? Path.Combine(directory, symbol.ID.Symbol.ToLower()) : directory;

                case SecurityType.Commodity:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Generates relative factor file paths for equities
        /// </summary>
        public static string GenerateRelativeFactorFilePath(Symbol symbol)
        {
            return Path.Combine(Globals.DataFolder,
                                        "equity",
                                        symbol.ID.Market,
                                        "factor_files",
                                        symbol.Value.ToLower() + ".csv");
        }

        /// <summary>
        /// Generates the relative zip file path rooted in the /Data directory
        /// </summary>
        public static string GenerateRelativeZipFilePath(Symbol symbol, DateTime date, Resolution resolution, TickType tickType)
        {
            return Path.Combine(GenerateRelativeZipFileDirectory(symbol, resolution), GenerateZipFileName(symbol, date, resolution, tickType));
        }

        /// <summary>
        /// Generates the relative zip file path rooted in the /Data directory
        /// </summary>
        public static string GenerateRelativeZipFilePath(string symbol, SecurityType securityType, string market, DateTime date, Resolution resolution, string tickType = null)
        {
            var directory = Path.Combine(securityType.SecurityTypeToLower(), market.ToLower(), resolution.ResolutionToLower());
            if (resolution != Resolution.Daily && resolution != Resolution.Hour)
            {
                directory = Path.Combine(directory, symbol.ToLower());
            }

            tickType ??= (securityType is SecurityType.Forex or SecurityType.Cfd
                ? TickType.Quote
                : TickType.Trade).TickTypeToLower();
            return Path.Combine(directory, GenerateZipFileName(symbol, securityType, date, resolution, tickType));
        }

        /// <summary>
        /// Generates the zip entry name to hold the specified data.
        /// </summary>
        public static string GenerateZipEntryName(Symbol symbol, DateTime date, Resolution resolution, TickType tickType)
        {
            var formattedDate = date.ToString(DateFormat.EightCharacter);
            var isHourOrDaily = resolution is Resolution.Hour or Resolution.Daily;

            switch (symbol.ID.SecurityType)
            {
                case SecurityType.Base:
                case SecurityType.Equity:
                case SecurityType.Forex:
                case SecurityType.Cfd:
                case SecurityType.Crypto:
                    if (resolution == Resolution.Tick && symbol.SecurityType == SecurityType.Equity)
                    {
                        return string.Format("{0}_{1}_{2}_{3}.csv",
                            formattedDate,
                            symbol.Value.ToLower(),
                            tickType,
                            resolution
                        );
                    }

                    if (isHourOrDaily)
                    {
                        return string.Format("{0}.csv",
                            symbol.Value.ToLower()
                            );
                    }

                    return string.Format("{0}_{1}_{2}_{3}.csv",
                        formattedDate,
                        symbol.Value.ToLower(),
                        resolution.ResolutionToLower(),
                        tickType.TickTypeToLower()
                        );

                case SecurityType.Option:

                    //if (symbol.ID.contractId != null)
                    //{
                    //    return symbol.ID.contractId + ".csv";
                    //}
                    if (symbol.IsChinaMarket())
                    {
                        var contractId = symbol.ID.contractId;
                        if (SymbolCache.TryGetSymbol(contractId + "A", out var exists))
                        {
                            symbol = exists;
                        }

                        return string.Join("_",
                            formattedDate,
                            symbol.Underlying.Value.ToLower()[2..],
                            symbol.ID.OptionRight.ToLower(),
                            Scale(symbol.ID.StrikePrice),
                            symbol.ID.Date.ToString(DateFormat.EightCharacter),
                            symbol.SymbolProperties.ContractMultiplier,
                            contractId,
                            resolution.ResolutionToLower(),
                            tickType.TickTypeToLower()
                            ) + ".csv";
                    }
                    if (isHourOrDaily)
                    {
                        return string.Join("_",
                            symbol.Underlying.Value.ToLower(), // underlying
                            tickType.TickTypeToLower(),
                            symbol.ID.OptionStyle.ToLower(),
                            symbol.ID.OptionRight.ToLower(),
                            Scale(symbol.ID.StrikePrice),
                            symbol.ID.Date.ToString(DateFormat.EightCharacter)
                        ) + ".csv";
                    }

                    return string.Join("_",
                        formattedDate,
                        symbol.Underlying.Value.ToLower(), // underlying
                        resolution.ResolutionToLower(),
                        tickType.TickTypeToLower(),
                        symbol.ID.OptionStyle.ToLower(),
                        symbol.ID.OptionRight.ToLower(),
                        Scale(symbol.ID.StrikePrice),
                        symbol.ID.Date.ToString(DateFormat.EightCharacter)
                    ) + ".csv";

                case SecurityType.Future:
                    var expiryDate = symbol.ID.Date;
                    if (symbol.ID.Date == SecurityIdentifier.PerpetualExpiration)
                    {
                        expiryDate = new DateTime(2030, 12, 31);
                    }
                    var contractYearMonth = FuturesExpiryUtilityFunctions.ExpiresInPreviousMonth(symbol.ID.Symbol)
                        ? expiryDate.AddMonths(1).ToString(DateFormat.YearMonth)
                        : expiryDate.ToString(DateFormat.YearMonth);

                    if (isHourOrDaily)
                    {
                        return string.Join("_",
                            symbol.ID.Symbol.ToLower(),
                            tickType.TickTypeToLower(),
                            contractYearMonth,
                            expiryDate.ToString(DateFormat.EightCharacter)
                            ) + ".csv";
                    }

                    return string.Join("_",
                        formattedDate,
                        symbol.ID.Symbol.ToLower(),
                        resolution.ResolutionToLower(),
                        tickType.TickTypeToLower(),
                        contractYearMonth,
                        expiryDate.ToString(DateFormat.EightCharacter)
                        ) + ".csv";

                case SecurityType.Commodity:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Generates the zip file name for the specified date of data.
        /// </summary>
        public static string GenerateZipFileName(Symbol symbol, DateTime date, Resolution resolution, TickType tickType)
        {
            var tickTypeString = tickType.TickTypeToLower();
            var formattedDate = date.ToString(DateFormat.EightCharacter);
            var isHourOrDaily = resolution is Resolution.Hour or Resolution.Daily;

            switch (symbol.ID.SecurityType)
            {
                case SecurityType.Base:
                case SecurityType.Equity:
                case SecurityType.Forex:
                case SecurityType.Cfd:
                    if (isHourOrDaily)
                    {
                        return string.Format("{0}.zip",
                            symbol.Value.ToLower()
                            );
                    }

                    return string.Format("{0}_{1}.zip",
                        formattedDate,
                        tickTypeString
                        );
                case SecurityType.Crypto:
                    if (isHourOrDaily)
                    {
                        return string.Format("{0}_{1}.zip",
                            symbol.Value.ToLower(),
                            tickTypeString
                        );
                    }

                    return string.Format("{0}_{1}.zip",
                        formattedDate,
                        tickTypeString
                    );
                case SecurityType.Option:
                    if (isHourOrDaily)
                    {
                        return string.Format("{0}_{1}_{2}.zip",
                            symbol.Underlying.Value.ToLower(), // underlying
                            tickTypeString,
                            symbol.ID.OptionStyle.ToLower()
                            );
                    }

                    return string.Format("{0}_{1}_{2}.zip",
                        formattedDate,
                        tickTypeString,
                        symbol.ID.OptionStyle.ToLower()
                        );

                case SecurityType.Future:
                    if (isHourOrDaily)
                    {
                        return string.Format("{0}_{1}.zip",
                            symbol.ID.Symbol.ToLower(),
                            tickTypeString);
                    }

                    return string.Format("{0}_{1}.zip",
                        formattedDate,
                        tickTypeString);

                case SecurityType.Commodity:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Creates the zip file name for a QC zip data file
        /// </summary>
        public static string GenerateZipFileName(string symbol, SecurityType securityType, DateTime date, Resolution resolution, string tickType)
        {
            if (resolution is Resolution.Hour or Resolution.Daily)
            {
                return symbol.ToLower() + ".zip";
            }

            var zipFileName = date.ToString(DateFormat.EightCharacter);
            //tickType ??= (securityType == SecurityType.Forex || securityType == SecurityType.Cfd ? TickType.Quote : TickType.Trade);
            var suffix = $"_{tickType}.zip";
            return zipFileName + suffix;
        }

        /// <summary>
        /// Gets the tick type most commonly associated with the specified security type
        /// </summary>
        /// <param name="securityType">The security type</param>
        /// <returns>The most common tick type for the specified security type</returns>
        public static TickType GetCommonTickType(SecurityType securityType)
        {
            if (securityType is SecurityType.Forex or SecurityType.Cfd or SecurityType.Crypto)
            {
                return TickType.Quote;
            }
            return TickType.Trade;
        }

        /// <summary>
        /// Creates a symbol from the specified zip entry name
        /// </summary>
        /// <param name="symbol">The root symbol of the output symbol</param>
        /// <param name="resolution">The resolution of the data source producing the zip entry name</param>
        /// <param name="zipEntryName">The zip entry name to be parsed</param>
        /// <returns>A new symbol representing the zip entry name</returns>
        public static Symbol ReadSymbolFromZipEntry(Symbol symbol, Resolution resolution, string zipEntryName)
        {
            var isHourlyOrDaily = resolution is Resolution.Hour or Resolution.Daily;
            var parts = zipEntryName.Replace(".csv", string.Empty).Split('_');
            switch (symbol.ID.SecurityType)
            {
                case SecurityType.Option:
                    if (Market.IsChinaMarket(symbol))
                    {
                        var style = OptionStyle.European;
                        var right = (OptionRight)Enum.Parse(typeof(OptionRight), parts[2], true);
                        var strike = parts[3].ToDecimal() / 10000m;
                        var expiry = DateTime.ParseExact(parts[4], DateFormat.EightCharacter, null);
                        expiry = expiry.AddHours(15);
                        var optionSymbol = Symbol.CreateOption(symbol.Underlying, symbol.ID.Market, style, right, strike, expiry, parts[6]);
                        optionSymbol.SymbolProperties = new SymbolProperties(
                            string.Empty,
                            Currencies.USD,
                            parts[5].ToDecimal(),
                            0.0001m,
                            1);
                        return optionSymbol;
                    }

                    if (isHourlyOrDaily)
                    {
                        var style = (OptionStyle)Enum.Parse(typeof(OptionStyle), parts[2], true);
                        var right = (OptionRight)Enum.Parse(typeof(OptionRight), parts[3], true);
                        var strike = decimal.Parse(parts[4]) / 10000m;
                        var expiry = DateTime.ParseExact(parts[5], DateFormat.EightCharacter, null);
                        return Symbol.CreateOption(symbol.Underlying, symbol.ID.Market, style, right, strike, expiry);
                    }
                    else
                    {
                        var style = (OptionStyle)Enum.Parse(typeof(OptionStyle), parts[4], true);
                        var right = (OptionRight)Enum.Parse(typeof(OptionRight), parts[5], true);
                        var strike = decimal.Parse(parts[6]) / 10000m;
                        var expiry = DateTime.ParseExact(parts[7], DateFormat.EightCharacter, null);
                        if (symbol.ID.Market == Market.Deribit)
                        {
                            expiry = DateTime.SpecifyKind(expiry, DateTimeKind.Utc).AddHours(8);
                        }
                        return Symbol.CreateOption(symbol.Underlying, symbol.ID.Market, style, right, strike, expiry);
                    }

                case SecurityType.Future:
                    if (isHourlyOrDaily)
                    {
                        var expiryYearMonth = DateTime.ParseExact(parts[2], DateFormat.YearMonth, null);
                        var futureExpiryFunc = FuturesExpiryFunctions.FuturesExpiryFunction(parts[1]);
                        var futureExpiry = futureExpiryFunc(expiryYearMonth);
                        return Symbol.CreateFuture(parts[0], symbol.ID.Market, futureExpiry);
                    }
                    else
                    {
                        var expiryYearMonth = DateTime.ParseExact(parts[4], DateFormat.YearMonth, null);
                        var futureExpiryFunc = FuturesExpiryFunctions.FuturesExpiryFunction(parts[1]);
                        var futureExpiry = futureExpiryFunc(expiryYearMonth);
                        return Symbol.CreateFuture(parts[1], symbol.ID.Market, futureExpiry);
                    }

                default:
                    throw new NotImplementedException($"ReadSymbolFromZipEntry is not implemented for {symbol.ID.SecurityType} {symbol.ID.Market} {resolution}");
            }
        }

        /// <summary>
        /// Creates a symbol from the specified zip entry name
        /// </summary>
        /// <param name="symbol">The root symbol of the output symbol</param>
        /// <param name="resolution">The resolution of the data source producing the zip entry name</param>
        /// <param name="zipEntryName">The zip entry name to be parsed</param>
        /// <param name="date">tradeDate</param>
        /// <returns>A new symbol representing the zip entry name</returns>
        public static Symbol ReadSymbolFromZipEntry(Symbol symbol, Resolution resolution, string zipEntryName, DateTime date)
        {
            var isHourlyOrDaily = resolution is Resolution.Hour or Resolution.Daily;
            var parts = zipEntryName.Replace(".csv", string.Empty).Split('_');
            switch (symbol.ID.SecurityType)
            {
                case SecurityType.Option:
                    if (Market.IsChinaMarket(symbol))
                    {
                        var contractId = parts[6];
                        var strike = parts[3].ToDecimal() / 10000m;
                        var multiplier = parts[5].ToDecimal();

                        if (SymbolCache.TryGetSymbol(contractId, out var exists) && exists.ID.StrikePrice == strike)
                        {
                            return exists;
                        }

                        const OptionStyle style = OptionStyle.European;
                        var right = (OptionRight)Enum.Parse(typeof(OptionRight), parts[2], true);
                        var expiry = DateTime.ParseExact(parts[4], DateFormat.EightCharacter, null);
                        expiry = expiry.AddHours(15);
                        var optionSymbol = Symbol.CreateOption(symbol.Underlying, symbol.ID.Market, style, right, strike, expiry, contractId);
                        optionSymbol.SymbolProperties = new SymbolProperties(
                            string.Empty,
                            Currencies.USD,
                            multiplier,
                            0.0001m,
                            1);

                        if (exists != null)
                        {
                            contractId = $"{contractId}A";
                        }
                        SymbolCache.Set(contractId, optionSymbol, contractId.EndsWith("A"));
                        return exists != null ? exists : optionSymbol;
                    }

                    if (isHourlyOrDaily)
                    {
                        var style = (OptionStyle)Enum.Parse(typeof(OptionStyle), parts[2], true);
                        var right = (OptionRight)Enum.Parse(typeof(OptionRight), parts[3], true);
                        var strike = decimal.Parse(parts[4]) / 10000m;
                        var expiry = DateTime.ParseExact(parts[5], DateFormat.EightCharacter, null);
                        return Symbol.CreateOption(symbol.Underlying, symbol.ID.Market, style, right, strike, expiry);
                    }
                    else
                    {
                        var style = (OptionStyle)Enum.Parse(typeof(OptionStyle), parts[4], true);
                        var right = (OptionRight)Enum.Parse(typeof(OptionRight), parts[5], true);
                        var strike = decimal.Parse(parts[6]) / 10000m;
                        // TODO: This is a hack to get around the fact that the day of expiry date is not always have a leading zero padding.
                        var expiryDate = parts[7];
                        if (expiryDate.Length < 8)
                        {
                            expiryDate = expiryDate.Insert(expiryDate.Length - 1, "0");
                        }
                        var expiry = DateTime.ParseExact(expiryDate, DateFormat.EightCharacter, null);
                        if (symbol.ID.Market == Market.Deribit)
                        {
                            expiry = DateTime.SpecifyKind(expiry, DateTimeKind.Unspecified).AddHours(8);
                        }

                        var alias =
                            $"{symbol.Underlying.Value[..3]}-" +
                            $"{expiry.ToString("dMMMyy", CultureInfo.GetCultureInfo("en-US"))}-" +
                            $"{strike:F0}-" +
                            $"{(right == OptionRight.Call ? "C" : "P")}";
                        alias = alias.ToUpper();
                        var optionSymbol = Symbol.CreateOption(symbol.Underlying, symbol.ID.Market, style, right, strike, expiry, alias);
                        SymbolCache.Set(alias, optionSymbol);
                        return optionSymbol;
                    }

                case SecurityType.Future:
                    if (isHourlyOrDaily)
                    {
                        var expiryYearMonth = DateTime.ParseExact(parts[2], DateFormat.YearMonth, null);
                        var futureExpiryFunc = FuturesExpiryFunctions.FuturesExpiryFunction(parts[1]);
                        var futureExpiry = futureExpiryFunc(expiryYearMonth);
                        return Symbol.CreateFuture(parts[0], symbol.ID.Market, futureExpiry);
                    }
                    else
                    {
                        var expiryYearMonth = DateTime.ParseExact(parts[4], DateFormat.YearMonth, null);
                        var futureExpiryFunc = FuturesExpiryFunctions.FuturesExpiryFunction(parts[1]);
                        var futureExpiry = futureExpiryFunc(expiryYearMonth);
                        //if (parts[1] == Futures.Currencies.BTC_PERPETUAL || parts[1] == Futures.Currencies.ETH_PERPETUAL)
                        var key = parts[1].ToUpper();
                        if (key.Contains("PERPETUAL"))
                        {
                            return Symbol.CreateFuture(key, symbol.ID.Market, futureExpiry, key);
                        }

                        return Symbol.CreateFuture(key, symbol.ID.Market, futureExpiry);
                    }

                default:
                    throw new NotImplementedException($"ReadSymbolFromZipEntry is not implemented for {symbol.ID.SecurityType} {symbol.ID.Market} {resolution}");
            }
        }

        /// <summary>
        /// Scale and convert the resulting number to deci-cents int.
        /// </summary>
        private static long Scale(decimal value)
        {
            return (long)(value * 10000);
        }

        /// <summary>
        /// Create a csv line from the specified arguments
        /// </summary>
        private static string ToCsv(params object[] args)
        {
            // use culture neutral formatting for decimals
            for (var i = 0; i < args.Length; i++)
            {
                var value = args[i];
                if (value is decimal)
                {
                    args[i] = ((decimal)value).Normalize().ToString(CultureInfo.InvariantCulture);
                }
            }

            return string.Join(",", args);
        }

        /// <summary>
        /// Creates a scaled csv line for the bar, if null fills in empty strings
        /// </summary>
        private static string ToScaledCsv(IBar bar)
        {
            if (bar == null)
            {
                return ToCsv(string.Empty, string.Empty, string.Empty, string.Empty);
            }

            return ToCsv(Scale(bar.Open), Scale(bar.High), Scale(bar.Low), Scale(bar.Close));
        }


        /// <summary>
        /// Creates a non scaled csv line for the bar, if null fills in empty strings
        /// </summary>
        private static string ToNonScaledCsv(IBar bar)
        {
            if (bar == null)
            {
                return ToCsv(string.Empty, string.Empty, string.Empty, string.Empty);
            }

            return ToCsv(bar.Open, bar.High, bar.Low, bar.Close);
        }

        /// <summary>
        /// Get the <see cref="TickType"/> for common Lean data types.
        /// If not a Lean common data type, return a TickType of Trade.
        /// </summary>
        /// <param name="type">A Type used to determine the TickType</param>
        /// <param name="securityType">The SecurityType used to determine the TickType</param>
        /// <returns>A TickType corresponding to the type</returns>
        public static TickType GetCommonTickTypeForCommonDataTypes(Type type, SecurityType securityType)
        {
            if (type == typeof(TradeBar))
            {
                return TickType.Trade;
            }
            if (type == typeof(QuoteBar))
            {
                return TickType.Quote;
            }
            if (type == typeof(OpenInterest))
            {
                return TickType.OpenInterest;
            }
            if (type == typeof(ZipEntryName))
            {
                return TickType.Quote;
            }
            if (type == typeof(Tick))
            {
                if (securityType is SecurityType.Forex or SecurityType.Cfd or SecurityType.Crypto)
                {
                    return TickType.Quote;
                }
            }

            return TickType.Trade;
        }

        /// <summary>
        /// Parses file name into a <see cref="Security"/> and DateTime
        /// </summary>
        /// <param name="fileName">File name to be parsed</param>
        /// <param name="symbol">The symbol as parsed from the fileName</param>
        /// <param name="date">Date of data in the file path. Only returned if the resolution is lower than Hourly</param>
        /// <param name="resolution">The resolution of the symbol as parsed from the filePath</param>
        public static bool TryParsePath(string fileName, out Symbol symbol, out DateTime date, out Resolution resolution)
        {
            symbol = null;
            resolution = Resolution.Daily;
            date = default(DateTime);

            var pathSeparators = new[] { '/', '\\' };
            var securityTypes = Enum.GetNames(typeof(SecurityType)).Select(x => x.ToLower()).ToList();

            try
            {
                // Removes file extension
                fileName = fileName.Replace(fileName.GetExtension(), "");

                // remove any relative file path
                while (fileName.First() == '.' || pathSeparators.Any(x => x == fileName.First()))
                {
                    fileName = fileName.Remove(0, 1);
                }

                // split path into components
                var info = fileName.Split(pathSeparators, StringSplitOptions.RemoveEmptyEntries).ToList();

                // find where the useful part of the path starts - i.e. the securityType
                var startIndex = info.FindIndex(x => securityTypes.Contains(x.ToLower()));

                // Gather components useed to create the security
                var market = info[startIndex + 1];
                var ticker = info[startIndex + 3];
                resolution = (Resolution)Enum.Parse(typeof(Resolution), info[startIndex + 2], true);
                var securityType = (SecurityType)Enum.Parse(typeof(SecurityType), info[startIndex], true);

                // If resolution is Daily or Hour, we do not need to set the date and tick type
                if (resolution < Resolution.Hour)
                {
                    date = DateTime.ParseExact(info[startIndex + 4].Substring(0, 8), DateFormat.EightCharacter, null);
                }

                if (securityType == SecurityType.Crypto)
                {
                    ticker = ticker.Split('_').First();
                }

                symbol = Symbol.Create(ticker, securityType, market);
            }
            catch (Exception ex)
            {
                Log.Error("LeanData.TryParsePath(): Error encountered while parsing the path {0}. Error: {1}", fileName, ex.GetBaseException());
                return false;
            }

            return true;
        }
    }
}
