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
using System.Globalization;
using System.Runtime.CompilerServices;
using NodaTime;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// QuoteBar class for second and minute resolution data:
    /// An OHLC implementation of the QuantConnect BaseData class with parameters for candles.
    /// </summary>
    public class QuoteBar : BaseData, IBaseDataBar
    {
        /// <summary>
        /// Average bid size
        /// </summary>
        public decimal LastBidSize;

        /// <summary>
        /// Average ask size
        /// </summary>
        public decimal LastAskSize;

        /// <summary>
        /// Bid OHLC
        /// </summary>
        public Bar Bid
        {
            get => bid;
            set
            {
                bid = value;
                UpdateItem();
            }
        }

        /// <summary>
        /// Ask OHLC
        /// </summary>
        public Bar Ask
        {
            get => ask;
            set
            {
                ask = value;
                UpdateItem();
            }
        }
        
        /// <summary>
        /// AskIV
        /// </summary>
        public decimal AskIV
        {
            get => ask_iv;
            private set => ask_iv = value;
        }

        /// <summary>
        /// BidIV
        /// </summary>
        public decimal BidIV
        {
            get => bid_iv;
            private set => bid_iv = value;
        }

        /// <summary>
        /// The mark price for the instrument
        /// </summary>
        public decimal MarkPrice
        {
            get => markPrice;
            private set => markPrice = value;
        }

        public decimal MarkIV
        {
            get => mark_iv;
            private set => mark_iv = value;
        }
        
        public decimal MarkFittedIV
        {
            get => mark_fitted_iv;
            private set => mark_fitted_iv = value;
        }

        public decimal Delta
        {
            get => delta;
            private set => delta = value;
        }

        public decimal Vega
        {
            get => vega;
            private set => vega = value;
        }

        public decimal Rho
        {
            get => rho;
            private set => rho = value;
        }

        public decimal Theta
        {
            get => theta;
            private set => theta = value;
        }

        public decimal Gamma
        {
            get => gamma;
            private set => gamma = value;
        }

        /// <summary>
        /// Opening price of the bar: Defined as the price at the start of the time period.
        /// </summary>
        public decimal Open
        {
            get => open;
            set => open = value;
        }

        public decimal GetOpen()
        {
            if (bid != null && ask != null)
            {
                if (bid.open != 0m && ask.open != 0m)
                    return (bid.open + ask.open) / 2m;

                if (bid.open != 0)
                    return bid.open;

                if (ask.open != 0)
                    return ask.open;

                return 0m;
            }
            if (bid != null)
            {
                return bid.open;
            }
            if (ask != null)
            {
                return ask.open;
            }
            return 0m;
        }

        /// <summary>
        /// High price of the QuoteBar during the time period.
        /// </summary>
        public decimal High
        {
            get => high;
            set => high = value;
        }

        public decimal GetHigh()
        {
            if (bid != null && ask != null)
            {
                if (bid.high != 0m && ask.high != 0m)
                    return (bid.high + ask.high) / 2m;

                if (bid.high != 0)
                    return bid.high;

                if (ask.high != 0)
                    return ask.high;

                return 0m;
            }
            if (bid != null)
            {
                return bid.high;
            }
            if (ask != null)
            {
                return ask.high;
            }
            return 0m;
        }

        /// <summary>
        /// Low price of the QuoteBar during the time period.
        /// </summary>
        public decimal Low
        {
            get => low;
            set => low = value;
        }

        public decimal GetLow()
        {
            if (bid != null && ask != null)
            {
                if (bid.low != 0m && ask.low != 0m)
                    return (bid.low + ask.low) / 2m;

                if (bid.low != 0)
                    return bid.low;

                if (ask.low != 0)
                    return ask.low;

                return 0m;
            }
            if (bid != null)
            {
                return bid.low;
            }
            if (ask != null)
            {
                return ask.low;
            }
            return 0m;
        }

        /// <summary>
        /// Closing price of the QuoteBar. Defined as the price at Start Time + TimeSpan.
        /// </summary>
        public decimal Close
        {
            get => close;
            set => close = value;
        }

        public decimal ForwardAdjustFactor => forwardAdjustFactor;

        public decimal BackwardAdjustFactor => backwardAdjustFactor;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (decimal open, decimal high, decimal low, decimal close) GetData()
        {
            return (open, high, low, close);
        }

        public decimal GetClose()
        {
            if (bid != null && ask != null)
            {
                if (bid.close != 0m && ask.close != 0m)
                    return (bid.close + ask.close) / 2m;

                if (bid.close != 0)
                    return bid.close;

                if (ask.close != 0)
                    return ask.close;

                return 0m;
            }
            if (bid != null)
            {
                return bid.close;
            }
            if (ask != null)
            {
                return ask.close;
            }
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override void SetEndTime(DateTime dateTime)
        {
            endTime = dateTime;
            period = endTime - time;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected sealed override void SetTime(DateTime dateTime)
        {
            time = dateTime;
            endTime = time + period;
        }

        //public override DateTime EndTime
        //{
        //    get => time + period;
        //    set => period = value - time;
        //}

        /// <summary>
        /// The period of this quote bar, (second, minute, daily, ect...)
        /// </summary>
        public TimeSpan Period
        {
            get => period;
            set
            {
                if (period != value)
                {
                    period = value;
                    SetTime(time);
                }
            }
        }

        /// <summary>
        /// Default initializer to setup an empty quote bar.
        /// </summary>
        public QuoteBar()
        {
            bid = new Bar();
            ask = new Bar();
            value = 0;
            symbol = Symbol.Empty;
            period = TimeSpan.FromMinutes(1);
            SetTime(new DateTime());
            dataType = MarketDataType.QuoteBar;
        }

        /// <summary>
        /// Initialize Quote Bar with Bid(OHLC) and Ask(OHLC) Values:
        /// </summary>
        /// <param name="time">DateTime Timestamp of the bar</param>
        /// <param name="symbol">Market MarketType Symbol</param>
        /// <param name="bid">Bid OLHC bar</param>
        /// <param name="lastBidSize">Average bid size over period</param>
        /// <param name="ask">Ask OLHC bar</param>
        /// <param name="lastAskSize">Average ask size over period</param>
        /// <param name="period">The period of this bar, specify null for default of 1 minute</param>
        public QuoteBar(
            DateTime time, Symbol symbol, IBar bid, decimal lastBidSize, IBar ask, decimal lastAskSize, TimeSpan? period = null)
        {
            this.symbol = symbol;
            if (bid != null)
            {
                var bidData = bid.GetData();
                this.bid = new Bar(bidData.open, bidData.high, bidData.low, bidData.close);
            }
            else
            {
                this.bid = null;
            }
            //Bid = bid == null ? null : new Bar(bid.Open, bid.High, bid.Low, bid.Close);

            if (ask != null)
            {
                var askData = ask.GetData();
                this.ask = new Bar(askData.open, askData.high, askData.low, askData.close);
            }
            else
            {
                this.ask = null;
            }
            //Ask = ask == null ? null : new Bar(ask.Open, ask.High, ask.Low, ask.Close);

            if (bid != null) LastBidSize = lastBidSize;
            if (ask != null) LastAskSize = lastAskSize;
            UpdateItem();
            value = close;
            this.period = period ?? TimeSpan.FromMinutes(1);
            SetTime(time);
            dataType = MarketDataType.QuoteBar;
        }

        internal void UpdateItem()
        {
            open = GetOpen();
            high = GetHigh();
            low = GetLow();
            close = GetClose();
        }

        /// <summary>
        /// Update the quote bar - build the bar from this pricing information:
        /// </summary>
        /// <param name="lastTrade">The last trade price</param>
        /// <param name="bidPrice">Current bid price</param>
        /// <param name="askPrice">Current asking price</param>
        /// <param name="volume">Volume of this trade</param>
        /// <param name="bidSize">The size of the current bid, if available, if not, pass 0</param>
        /// <param name="askSize">The size of the current ask, if available, if not, pass 0</param>
        public override void Update(decimal lastTrade, decimal bidPrice, decimal askPrice, decimal volume, decimal bidSize, decimal askSize)
        {
            // update our bid and ask bars - handle null values, this is to give good values for midpoint OHLC
            if (bid == null && bidPrice != 0) bid = new Bar();
            bid?.Update(bidPrice);

            if (ask == null && askPrice != 0) ask = new Bar();
            ask?.Update(askPrice);

            if (bidSize > 0)
            {
                LastBidSize = bidSize;
            }

            if (askSize > 0)
            {
                LastAskSize = askSize;
            }

            // be prepared for updates without trades
            if (lastTrade != 0) value = lastTrade;
            else if (askPrice != 0) value = askPrice;
            else if (bidPrice != 0) value = bidPrice;
            UpdateItem();
        }

        /// <summary>
        /// QuoteBar Reader: Fetch the data from the QC storage and feed it line by line into the engine.
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">Date of this reader request</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>Enumerable iterator for returning each line of the required data.</returns>
        public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            try
            {
                switch (config.SecurityType)
                {
                    case SecurityType.Equity:
                        return ParseEquity(config, line, date);

                    case SecurityType.Forex:
                    case SecurityType.Crypto:
                        return ParseForex(config, line, date);

                    case SecurityType.Cfd:
                        return ParseCfd(config, line, date);

                    case SecurityType.Option:
                        return ParseOption(config, line, date);

                    case SecurityType.Future:
                        return ParseFuture(config, line, date);

                }
            }
            catch (Exception err)
            {
                Log.Error("QuoteBar.Reader(): Error parsing line: '{0}', Symbol: {1}, SecurityType: {2}, Resolution: {3}, Date: {4}, Message: {5}",
                    line, config.Symbol.Value, config.SecurityType, config.Resolution, date.ToString("yyyy-MM-dd"), err);
            }

            // if we couldn't parse it above return a default instance
            return new QuoteBar { symbol = config.Symbol, period = config.Increment };
        }

        private static bool _hasShownWarning;
        internal decimal close;
        internal decimal low;
        internal decimal high;
        internal decimal open;
        internal decimal markPrice;
        internal decimal ask_iv;
        internal decimal bid_iv;
        internal decimal mark_iv;
        internal decimal mark_fitted_iv;
        internal decimal delta;
        internal decimal vega;
        internal decimal rho;
        internal decimal gamma;
        internal decimal theta;
        internal TimeSpan period;
        internal Bar ask;
        internal Bar bid;
        internal decimal forwardAdjustFactor;
        internal decimal backwardAdjustFactor;

        /// <summary>
        /// "Scaffold" code - If the data being read is formatted as a TradeBar, use this method to deserialize it
        /// TODO: Once all Forex data refactored to use QuoteBar formatted data, remove this method
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">Date of this reader request</param>
        /// <returns><see cref="QuoteBar"/> with the bid/ask prices set to same values</returns>
        [Obsolete("All Forex data should use Quotes instead of Trades.")]
        private QuoteBar ParseTradeAsQuoteBar(SubscriptionDataConfig config, DateTime date, string line)
        {
            if (!_hasShownWarning)
            {
                Logging.Log.Error("QuoteBar.ParseTradeAsQuoteBar(): Data formatted as Trade when Quote format was expected.  Support for this will disappear June 2017.");
                _hasShownWarning = true;
            }

            var quoteBar = new QuoteBar
            {
                period = config.Increment,
                symbol = config.Symbol
            };

            var csv = line.ToCsv(5);
            if (config.Resolution == Resolution.Daily || config.Resolution == Resolution.Hour)
            {
                // hourly and daily have different time format, and can use slow, robust c# parser.
                quoteBar.time = DateTime.ParseExact(csv[0], DateFormat.TwelveCharacter, CultureInfo.InvariantCulture).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }
            else
            {
                //Fast decimal conversion
                quoteBar.time = date.Date.AddMilliseconds(csv[0].ToInt32()).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }

            var barBid = new Bar
            {
                open = csv[1].ToDecimal(),
                high = csv[2].ToDecimal(),
                low = csv[3].ToDecimal(),
                close = csv[4].ToDecimal()
            };
            var barAsk = new Bar
            {
                open = csv[1].ToDecimal(),
                high = csv[2].ToDecimal(),
                low = csv[3].ToDecimal(),
                close = csv[4].ToDecimal()
            };
            quoteBar.ask = barAsk;
            quoteBar.bid = barBid;
            UpdateItem();
            quoteBar.value = quoteBar.close;

            return quoteBar;
        }

        /// <summary>
        /// Parse a quote bar representing a future with a scaling factor
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType</param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">Date of this reader request</param>
        /// <returns><see cref="QuoteBar"/> with the bid/ask set to same values</returns>
        public QuoteBar ParseFuture(SubscriptionDataConfig config, string line, DateTime date)
        {
            return ParseQuote(config, date, line);
        }

        /// <summary>
        /// Parse a quote bar representing an option with a scaling factor
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType</param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">Date of this reader request</param>
        /// <returns><see cref="QuoteBar"/> with the bid/ask set to same values</returns>
        public QuoteBar ParseOption(SubscriptionDataConfig config, string line, DateTime date)
        {
            return ParseQuote(config, date, line);
        }

        /// <summary>
        /// Parse a quote bar representing a cfd without a scaling factor
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType</param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">Date of this reader request</param>
        /// <returns><see cref="QuoteBar"/> with the bid/ask set to same values</returns>
        public QuoteBar ParseCfd(SubscriptionDataConfig config, string line, DateTime date)
        {
            return ParseQuote(config, date, line);
        }

        /// <summary>
        /// Parse a quote bar representing a forex without a scaling factor
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType</param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">Date of this reader request</param>
        /// <returns><see cref="QuoteBar"/> with the bid/ask set to same values</returns>
        public QuoteBar ParseForex(SubscriptionDataConfig config, string line, DateTime date)
        {
            return ParseQuote(config, date, line);
        }

        /// <summary>
        /// Parse a quote bar representing an equity with a scaling factor
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType</param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">Date of this reader request</param>
        /// <returns><see cref="QuoteBar"/> with the bid/ask set to same values</returns>
        public QuoteBar ParseEquity(SubscriptionDataConfig config, string line, DateTime date)
        {
            return ParseQuote(config, date, line);
        }


        /// <summary>
        /// "Scaffold" code - If the data being read is formatted as a QuoteBar, use this method to deserialize it
        /// TODO: Once all Forex data refactored to use QuoteBar formatted data, use only this method
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">Date of this reader request</param>
        /// <returns><see cref="QuoteBar"/> with the bid/ask prices set appropriately</returns>
        private static QuoteBar ParseQuote(SubscriptionDataConfig config, DateTime date, string line)
        {
            var scaleFactor = config.PriceScaleFactor;

            var quoteBar = new QuoteBar
            {
                period = config.Increment,
                symbol = config.Symbol,
                forwardAdjustFactor = config.ForwardAdjustFactor,
                backwardAdjustFactor = config.BackwardAdjustFactor,
            };

            var csv = line.ToCsv(15);
            DateTime endTime;
            if (config.Resolution is Resolution.Daily or Resolution.Hour)
            {
                // hourly and daily have different time format, and can use slow, robust c# parser.
                endTime = DateTime.ParseExact(csv[0], DateFormat.TwelveCharacter, CultureInfo.InvariantCulture).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }
            else
            {
                // Using custom "ToDecimal" conversion for speed on high resolution data.
                endTime = date.Date.AddMilliseconds((double)csv[0].ToDecimal()).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }

            //if (config.ExchangeTimeZone != DateTimeZone.Utc)
            //{
            //    endTime = endTime.ConvertTo(config.ExchangeTimeZone, DateTimeZone.Utc);
            //}
            //endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            
            quoteBar.SetTime(endTime - config.Increment);
            // only create the bid if it exists in the file
            if (csv[1].Length != 0 || csv[2].Length != 0 || csv[3].Length != 0 || csv[4].Length != 0)
            {
                quoteBar.bid = new Bar
                {
                    open = csv[1].ToDecimal() * scaleFactor,
                    high = csv[2].ToDecimal() * scaleFactor,
                    low = csv[3].ToDecimal() * scaleFactor,
                    close = csv[4].ToDecimal() * scaleFactor
                };
                quoteBar.LastBidSize = csv[5].ToDecimal();
                if (quoteBar.bid.open == 0)
                {
                    quoteBar.bid = null;
                    quoteBar.LastBidSize = 0;
                }
            }
            else
            {
                quoteBar.bid = null;
            }

            // only create the ask if it exists in the file
            if (csv[6].Length != 0 || csv[7].Length != 0 || csv[8].Length != 0 || csv[9].Length != 0)
            {
                quoteBar.ask = new Bar
                {
                    open = csv[6].ToDecimal() * scaleFactor,
                    high = csv[7].ToDecimal() * scaleFactor,
                    low = csv[8].ToDecimal() * scaleFactor,
                    close = csv[9].ToDecimal() * scaleFactor
                };
                quoteBar.LastAskSize = csv[10].ToDecimal();
                if (quoteBar.ask.open == 0)
                {
                    quoteBar.ask = null;
                    quoteBar.LastAskSize = 0;
                }
            }
            else
            {
                quoteBar.ask = null;
            }

            quoteBar.UpdateItem();
            quoteBar.value = quoteBar.close;

            if (csv.Count is 11 or 13)
            //if (csv.Count == 11)
            {
                if (csv.Count == 13 && csv[11].Length != 0)
                {
                    quoteBar.markPrice = csv[11].ToDecimal() * scaleFactor;
                }
                else
                {
                    if (quoteBar.ask != null && quoteBar.bid != null)
                    {
                        var askLast = quoteBar.ask.close;
                        var bidLast = quoteBar.bid.close;
                        quoteBar.markPrice = (bidLast + askLast) / 2;
                    }
                    else
                    {
                        if (quoteBar.ask != null)
                        {
                            quoteBar.markPrice = quoteBar.ask.close;
                        }
                        else if (quoteBar.bid != null)
                        {
                            quoteBar.markPrice = quoteBar.bid.close;
                        }
                        else
                        {
                            quoteBar.markPrice = quoteBar.close;
                        }
                    }
                }
            }

            if (csv.Count == 17)
            {
                var index = 10;
                quoteBar.markPrice = csv[++index].ToDecimal() * scaleFactor;
                //skip flag field
                ++index;
                quoteBar.mark_iv = csv[++index].ToDecimal();
                quoteBar.mark_fitted_iv = csv[++index].ToDecimal();
                quoteBar.delta = csv[++index].ToDecimal();
                quoteBar.vega = csv[++index].ToDecimal();
            }

            if (csv.Count == 21)
            {
                var index = 10;
                quoteBar.markPrice = csv[++index].ToDecimal() * scaleFactor;
                quoteBar.bid_iv = csv[++index].ToDecimal();
                quoteBar.ask_iv = csv[++index].ToDecimal();
                quoteBar.mark_iv = csv[++index].ToDecimal();
                quoteBar.delta = csv[++index].ToDecimal();
                quoteBar.gamma = csv[++index].ToDecimal();
                quoteBar.rho = csv[++index].ToDecimal();
                quoteBar.theta = csv[++index].ToDecimal();
                quoteBar.vega = csv[++index].ToDecimal();
            }

            return quoteBar;
        }

        /// <summary>
        /// Get Source for Custom Data File
        /// >> What source file location would you prefer for each type of usage:
        /// </summary>
        /// <param name="config">Configuration object</param>
        /// <param name="date">Date of this source request if source spread across multiple files</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>String source location of the file</returns>
        public override SubscriptionDataSource GetSource(SubscriptionDataConfig config, DateTime date, bool isLiveMode)
        {
            if (isLiveMode)
            {
                return new SubscriptionDataSource(string.Empty, SubscriptionTransportMedium.LocalFile);
            }

            var source = LeanData.GenerateZipFilePath(Globals.DataFolder, config.Symbol, date, config.Resolution, config.TickType);
            if (config.SecurityType is SecurityType.Option or SecurityType.Future)
            {
                source += "#" + LeanData.GenerateZipEntryName(config.Symbol, date, config.Resolution, config.TickType);
            }
            return new SubscriptionDataSource(source, SubscriptionTransportMedium.LocalFile, FileFormat.Csv);
        }

        /// <summary>
        /// Return a new instance clone of this quote bar, used in fill forward
        /// </summary>
        /// <returns>A clone of the current quote bar</returns>
        public override BaseData Clone()
        {
            return new QuoteBar
            {
                ask = ask?.Clone(),
                bid = bid?.Clone(),
                LastAskSize = LastAskSize,
                LastBidSize = LastBidSize,
                symbol = symbol,
                open = open,
                high = high,
                low = low,
                close = close,
                time = time,
                endTime = endTime,
                period = period,
                value = value,
                dataType = dataType,
                ask_iv = ask_iv,
                bid_iv = bid_iv,
                markPrice = markPrice,
                mark_iv = mark_iv,
                mark_fitted_iv = mark_fitted_iv,
                delta = delta,
                vega = vega,
                rho = rho,
                theta = theta,
                gamma = gamma
            };
        }

        /// <summary>
        /// Collapses QuoteBars into TradeBars object when
        ///  algorithm requires FX data, but calls OnData(<see cref="TradeBars"/>)
        /// TODO: (2017) Remove this method in favor of using OnData(<see cref="Slice"/>)
        /// </summary>
        /// <returns><see cref="TradeBars"/></returns>
        public TradeBar Collapse()
        {
            return new TradeBar(time, symbol, open, high, low, close, 0)
            {
                Period = period
            };
        }
    }
}
