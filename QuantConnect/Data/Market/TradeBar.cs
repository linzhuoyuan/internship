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
using System.Threading;
using NodaTime;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// TradeBar class for second and minute resolution data:
    /// An OHLC implementation of the QuantConnect BaseData class with parameters for candles.
    /// </summary>
    public class TradeBar : BaseData, IBaseDataBar
    {
        // scale factor used in QC equity/forex data files
        private const decimal ScaleFactor = 1 / 10000m;

        private int _initialized;
        internal decimal open;
        internal decimal high;
        internal decimal low;
        internal decimal volume;
        internal TimeSpan period;
        internal decimal forwardAdjustFactor = 1m;
        internal decimal backwardAdjustFactor = 1m;

        /// <summary>
        /// Volume:
        /// </summary>
        public decimal Volume
        {
            get => volume;
            set => volume = value;
        }

        /// <summary>
        /// Opening price of the bar: Defined as the price at the start of the time period.
        /// </summary>
        public decimal Open
        {
            get => open;
            set
            {
                Initialize(value);
                open = value;
            }
        }

        /// <summary>
        /// High price of the TradeBar during the time period.
        /// </summary>
        public decimal High
        {
            get => high;
            set
            {
                Initialize(value);
                high = value;
            }
        }

        /// <summary>
        /// Low price of the TradeBar during the time period.
        /// </summary>
        public decimal Low
        {
            get => low;
            set
            {
                Initialize(value);
                low = value;
            }
        }

        /// <summary>
        /// Closing price of the TradeBar. Defined as the price at Start Time + TimeSpan.
        /// </summary>
        public decimal Close
        {
            get => value;
            set
            {
                Initialize(value);
                this.value = value;
            }
        }

        public decimal ForwardAdjustFactor => forwardAdjustFactor;

        public decimal BackwardAdjustFactor => backwardAdjustFactor;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public (decimal open, decimal high, decimal low, decimal close) GetData()
        {
            return (open, high, low, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected sealed override void SetEndTime(DateTime dateTime)
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

        /// <summary>
        /// The period of this trade bar, (second, minute, daily, ect...)
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

        //In Base Class: Alias of Closing:
        //public decimal Price;

        //Symbol of Asset.
        //In Base Class: public Symbol Symbol;

        //In Base Class: DateTime Of this TradeBar
        //public DateTime Time;

        /// <summary>
        /// Default initializer to setup an empty trade bar.
        /// </summary>
        public TradeBar()
        {
            symbol = Symbol.Empty;
            dataType = MarketDataType.TradeBar;
            period = TimeSpan.FromMinutes(1);
            SetTime(time);
        }

        /// <summary>
        /// Clone constructor for implementing fill forward.
        /// Return a new instance with the same values as this original.
        /// </summary>
        /// <param name="original">Original trade bar object we seek to clone</param>
        public TradeBar(TradeBar original)
        {
            dataType = MarketDataType.TradeBar;
            symbol = original.symbol;
            value = original.volume;
            open = original.open;
            high = original.high;
            low = original.low;
            volume = original.volume;
            period = original.period;
            backwardAdjustFactor = original.backwardAdjustFactor;
            forwardAdjustFactor = original.forwardAdjustFactor;

            SetTime(new DateTime(original.time.Ticks));
            _initialized = 1;
        }

        /// <summary>
        /// Initialize Trade Bar with OHLC Values:
        /// </summary>
        /// <param name="time">DateTime Timestamp of the bar</param>
        /// <param name="symbol">Market MarketType Symbol</param>
        /// <param name="open">Decimal Opening Price</param>
        /// <param name="high">Decimal High Price of this bar</param>
        /// <param name="low">Decimal Low Price of this bar</param>
        /// <param name="close">Decimal Close price of this bar</param>
        /// <param name="volume">Volume sum over day</param>
        /// <param name="period">The period of this bar, specify null for default of 1 minute</param>
        public TradeBar(DateTime time, Symbol symbol, decimal open, decimal high, decimal low, decimal close, decimal volume, TimeSpan? period = null)
        {
            this.symbol = symbol;
            this.value = close;
            this.open = open;
            this.high = high;
            this.low = low;
            this.volume = volume;
            this.period = period ?? TimeSpan.FromMinutes(1);
            SetTime(time);
            this.dataType = MarketDataType.TradeBar;
            _initialized = 1;
        }

        /// <summary>
        /// TradeBar Reader: Fetch the data from the QC storage and feed it line by line into the engine.
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">Date of this reader request</param>
        /// <param name="isLiveMode">true if we're in live mode, false for backtesting mode</param>
        /// <returns>Enumerable iterator for returning each line of the required data.</returns>
        public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            //Handle end of file:
            if (line == null)
            {
                return null;
            }

            if (isLiveMode)
            {
                return new TradeBar();
            }

            try
            {
                switch (config.SecurityType)
                {
                    //Equity File Data Format:
                    case SecurityType.Equity:
                        return ParseEquity(config, line, date);

                    //FOREX has a different data file format:
                    case SecurityType.Forex:
                        return ParseForex(config, line, date);

                    case SecurityType.Crypto:
                        return ParseCrypto(config, line, date);

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
                Log.Error("TradeBar.Reader(): Error parsing line: '{0}', Symbol: {1}, SecurityType: {2}, Resolution: {3}, Date: {4}, Message: {5}",
                    line, config.Symbol.Value, config.SecurityType, config.Resolution, date.ToString("yyyy-MM-dd"), err);
            }

            // if we couldn't parse it above return a default instance
            return new TradeBar { symbol = config.Symbol, period = config.Increment };
        }

        private static TradeBar ParseTrade(SubscriptionDataConfig config, string line, DateTime date)
        {
            var tradeBar = new TradeBar
            {
                period = config.Increment,
                symbol = config.Symbol
            };

            var csv = line.ToCsv(6);
            var endTime = config.Resolution is Resolution.Daily or Resolution.Hour 
                ? DateTime.ParseExact(csv[0], DateFormat.TwelveCharacter, CultureInfo.InvariantCulture)
                : date.Date.AddMilliseconds(csv[0].ToInt32());
            endTime = endTime.ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);

            //if (config.ExchangeTimeZone != DateTimeZone.Utc)
            //{
            //    endTime = endTime.ConvertTo(config.ExchangeTimeZone, DateTimeZone.Utc);
            //}
            //endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);
            tradeBar.SetTime(endTime - config.Increment);
            tradeBar._initialized = 1;
            var scaleFactor = config.PriceScaleFactor;
            tradeBar.forwardAdjustFactor = config.ForwardAdjustFactor;
            tradeBar.backwardAdjustFactor = config.BackwardAdjustFactor;
            tradeBar.open = csv[1].ToDecimal() * scaleFactor;
            tradeBar.high = csv[2].ToDecimal() * scaleFactor;
            tradeBar.low = csv[3].ToDecimal() * scaleFactor;
            tradeBar.value = csv[4].ToDecimal() * scaleFactor;
            if (csv.Count > 4)
            {
                tradeBar.volume = csv[5].ToDecimal();
            }

            if (tradeBar.open == 0 || tradeBar.high == 0 || tradeBar.low == 0 || tradeBar.value == 0)
            {
                return null;
            }
            return tradeBar;
        }

        /// <summary>
        /// Parses the trade bar data line assuming QC data formats
        /// </summary>
        public static TradeBar Parse(SubscriptionDataConfig config, string line, DateTime baseDate)
        {
            switch (config.SecurityType)
            {
                case SecurityType.Equity:
                    return ParseEquity(config, line, baseDate);

                case SecurityType.Forex:
                case SecurityType.Crypto:
                    return ParseForex(config, line, baseDate);

                case SecurityType.Cfd:
                    return ParseCfd(config, line, baseDate);
            }

            return null;
        }

        /// <summary>
        /// Parses equity trade bar data into the specified tradebar type, useful for custom types with OHLCV data deriving from TradeBar
        /// </summary>
        /// <typeparam name="T">The requested output type, must derive from TradeBar</typeparam>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">Date of this reader request</param>
        /// <returns></returns>
        public static T ParseEquity<T>(SubscriptionDataConfig config, string line, DateTime date)
            where T : TradeBar, new()
        {
            var tradeBar = new T
            {
                symbol = config.Symbol,
                period = config.Increment
            };

            ParseEquity(tradeBar, config, line, date);

            return tradeBar;
        }

        private static void ParseEquity(TradeBar tradeBar, SubscriptionDataConfig config, string line, DateTime date)
        {
            var csv = line.ToCsv(6);
            if (config.Resolution is Resolution.Daily or Resolution.Hour)
            {
                // hourly and daily have different time format, and can use slow, robust c# parser.
                tradeBar.time = DateTime.ParseExact(csv[0], DateFormat.TwelveCharacter, CultureInfo.InvariantCulture).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }
            else
            {
                // Using custom "ToDecimal" conversion for speed on high resolution data.
                tradeBar.time = date.Date.AddMilliseconds(csv[0].ToInt32()).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }

            tradeBar.SetTime(tradeBar.time - config.Increment);
            tradeBar._initialized = 1;
            tradeBar.forwardAdjustFactor = config.ForwardAdjustFactor;
            tradeBar.backwardAdjustFactor = config.BackwardAdjustFactor;
            var scaleFactor = config.PriceScaleFactor;
            tradeBar.open = csv[1].ToDecimal() * scaleFactor;
            tradeBar.high = csv[2].ToDecimal() * scaleFactor;
            tradeBar.low = csv[3].ToDecimal() * scaleFactor;
            tradeBar.value = csv[4].ToDecimal() * scaleFactor;
            tradeBar.volume = csv[5].ToDecimal();
        }

        /// <summary>
        /// Parses equity trade bar data into the specified tradebar type, useful for custom types with OHLCV data deriving from TradeBar
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">Date of this reader request</param>
        /// <returns></returns>
        public static TradeBar ParseEquity(SubscriptionDataConfig config, string line, DateTime date)
        {
            return ParseTrade(config, line, date);
        }

        /// <summary>
        /// Parses forex trade bar data into the specified tradebar type, useful for custom types with OHLCV data deriving from TradeBar
        /// </summary>
        /// <typeparam name="T">The requested output type, must derive from TradeBar</typeparam>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">The base data used to compute the time of the bar since the line specifies a milliseconds since midnight</param>
        /// <returns></returns>
        public static T ParseForex<T>(SubscriptionDataConfig config, string line, DateTime date)
            where T : TradeBar, new()
        {
            var tradeBar = new T
            {
                symbol = config.Symbol,
                period = config.Increment
            };
            ParseForex(tradeBar, config, line, date);

            return tradeBar;
        }

        private static void ParseForex(TradeBar tradeBar, SubscriptionDataConfig config, string line, DateTime date)
        {
            var csv = line.ToCsv(5);
            if (config.Resolution is Resolution.Daily or Resolution.Hour)
            {
                // hourly and daily have different time format, and can use slow, robust c# parser.
                tradeBar.time = DateTime.ParseExact(csv[0], DateFormat.TwelveCharacter, CultureInfo.InvariantCulture).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }
            else
            {
                //Fast decimal conversion
                tradeBar.time = date.Date.AddMilliseconds(csv[0].ToInt32()).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }

            tradeBar.SetTime(tradeBar.time - config.Increment);
            tradeBar._initialized = 1;
            tradeBar.open = csv[1].ToDecimal();
            tradeBar.high = csv[2].ToDecimal();
            tradeBar.low = csv[3].ToDecimal();
            tradeBar.value = csv[4].ToDecimal();
        }

        /// <summary>
        /// Parses crypto trade bar data into the specified tradebar type, useful for custom types with OHLCV data deriving from TradeBar
        /// </summary>
        /// <typeparam name="T">The requested output type, must derive from TradeBar</typeparam>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">The base data used to compute the time of the bar since the line specifies a milliseconds since midnight</param>
        public static T ParseCrypto<T>(SubscriptionDataConfig config, string line, DateTime date)
            where T : TradeBar, new()
        {
            var tradeBar = new T
            {
                symbol = config.Symbol,
                period = config.Increment
            };
            ParseCrypto(tradeBar, config, line, date);
            if (tradeBar.open == 0 || tradeBar.high == 0 || tradeBar.low == 0 || tradeBar.value == 0)
            {
                return null;
            }
            return tradeBar;
        }

        /// <summary>
        /// Parses crypto trade bar data into the specified tradebar type, useful for custom types with OHLCV data deriving from TradeBar
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">The base data used to compute the time of the bar since the line specifies a milliseconds since midnight</param>
        public static TradeBar ParseCrypto(SubscriptionDataConfig config, string line, DateTime date)
        {
            return ParseTrade(config, line, date);
        }

        private static void ParseCrypto(TradeBar tradeBar, SubscriptionDataConfig config, string line, DateTime date)
        {
            var csv = line.ToCsv(6);
            if (config.Resolution is Resolution.Daily or Resolution.Hour)
            {
                // hourly and daily have different time format, and can use slow, robust c# parser.
                tradeBar.time = DateTime.ParseExact(csv[0], DateFormat.TwelveCharacter, CultureInfo.InvariantCulture).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }
            else
            {
                //Fast decimal conversion
                tradeBar.time = date.Date.AddMilliseconds(csv[0].ToInt32()).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }

            //数据文件时间为bar的结束时间。
            //writer :lh
            //tradeBar.time -= config.Increment;
            tradeBar.SetTime(tradeBar.time - config.Increment);
            tradeBar._initialized = 1;
            tradeBar.open = csv[1].ToDecimal();
            tradeBar.high = csv[2].ToDecimal();
            tradeBar.low = csv[3].ToDecimal();
            tradeBar.value = csv[4].ToDecimal();
            tradeBar.volume = csv[5].ToDecimal();
        }

        /// <summary>
        /// Parses forex trade bar data into the specified tradebar type, useful for custom types with OHLCV data deriving from TradeBar
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">The base data used to compute the time of the bar since the line specifies a milliseconds since midnight</param>
        /// <returns></returns>
        public static TradeBar ParseForex(SubscriptionDataConfig config, string line, DateTime date)
        {
            return ParseTrade(config, line, date);
        }

        /// <summary>
        /// Parses CFD trade bar data into the specified tradebar type, useful for custom types with OHLCV data deriving from TradeBar
        /// </summary>
        /// <typeparam name="T">The requested output type, must derive from TradeBar</typeparam>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">The base data used to compute the time of the bar since the line specifies a milliseconds since midnight</param>
        /// <returns></returns>
        public static T ParseCfd<T>(SubscriptionDataConfig config, string line, DateTime date)
            where T : TradeBar, new()
        {
            // CFD has the same data format as Forex
            return ParseForex<T>(config, line, date);
        }

        /// <summary>
        /// Parses CFD trade bar data into the specified tradebar type, useful for custom types with OHLCV data deriving from TradeBar
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">The base data used to compute the time of the bar since the line specifies a milliseconds since midnight</param>
        /// <returns></returns>
        public static TradeBar ParseCfd(SubscriptionDataConfig config, string line, DateTime date)
        {
            // CFD has the same data format as Forex
            return ParseForex(config, line, date);
        }

        /// <summary>
        /// Parses Option trade bar data into the specified tradebar type, useful for custom types with OHLCV data deriving from TradeBar
        /// </summary>
        /// <typeparam name="T">The requested output type, must derive from TradeBar</typeparam>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">The base data used to compute the time of the bar since the line specifies a milliseconds since midnight</param>
        /// <returns></returns>
        public static T ParseOption<T>(SubscriptionDataConfig config, string line, DateTime date)
            where T : TradeBar, new()
        {
            if (line.Trim().StartsWith("ms,"))
            {
                return null;
            }

            var tradeBar = new T
            {
                period = config.Increment,
                symbol = config.Symbol
            };

            var csv = line.ToCsv(6);
            if (config.Resolution is Resolution.Daily or Resolution.Hour)
            {
                // hourly and daily have different time format, and can use slow, robust c# parser.
                tradeBar.time = DateTime.ParseExact(csv[0], DateFormat.TwelveCharacter, CultureInfo.InvariantCulture).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }
            else
            {
                // Using custom "ToDecimal" conversion for speed on high resolution data.
                tradeBar.time = date.Date.AddMilliseconds(csv[0].ToInt32()).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }

            //数据文件时间为bar的结束时间。
            //writer :lh
            //tradeBar.time -= config.Increment;
            tradeBar.SetTime(tradeBar.time - config.Increment);
            tradeBar._initialized = 1;
            tradeBar.open = csv[1].ToDecimal() * config.PriceScaleFactor;
            tradeBar.high = csv[2].ToDecimal() * config.PriceScaleFactor;
            tradeBar.low = csv[3].ToDecimal() * config.PriceScaleFactor;
            tradeBar.value = csv[4].ToDecimal() * config.PriceScaleFactor;
            tradeBar.volume = csv[5].ToDecimal();

            if (tradeBar.open == 0 || tradeBar.high == 0 || tradeBar.low == 0 || tradeBar.value == 0)
            {
                return null;
            }
            return tradeBar;
        }

        /// <summary>
        /// Parses Future trade bar data into the specified tradebar type, useful for custom types with OHLCV data deriving from TradeBar
        /// </summary>
        /// <typeparam name="T">The requested output type, must derive from TradeBar</typeparam>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">The base data used to compute the time of the bar since the line specifies a milliseconds since midnight</param>
        /// <returns></returns>
        public static T ParseFuture<T>(SubscriptionDataConfig config, string line, DateTime date)
            where T : TradeBar, new()
        {
            var tradeBar = new T
            {
                period = config.Increment,
                symbol = config.Symbol
            };

            var csv = line.ToCsv(6);
            if (config.Resolution is Resolution.Daily or Resolution.Hour)
            {
                // hourly and daily have different time format, and can use slow, robust c# parser.
                tradeBar.time = DateTime.ParseExact(csv[0], DateFormat.TwelveCharacter, CultureInfo.InvariantCulture).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }
            else
            {
                // Using custom "ToDecimal" conversion for speed on high resolution data.
                tradeBar.time = date.Date.AddMilliseconds(csv[0].ToInt32()).ConvertTo(config.DataTimeZone, config.ExchangeTimeZone);
            }

            //数据文件时间为bar的结束时间。
            //writer :lh
            //tradeBar.time -= config.Increment;
            tradeBar.SetTime(tradeBar.time - config.Increment);
            tradeBar._initialized = 1;
            tradeBar.open = csv[1].ToDecimal() * config.PriceScaleFactor;
            tradeBar.high = csv[2].ToDecimal() * config.PriceScaleFactor;
            tradeBar.low = csv[3].ToDecimal() * config.PriceScaleFactor;
            tradeBar.value = csv[4].ToDecimal() * config.PriceScaleFactor;
            tradeBar.volume = csv[5].ToDecimal();

            if (tradeBar.open == 0 || tradeBar.high == 0 || tradeBar.low == 0 || tradeBar.value == 0)
            {
                return null;
            }

            return tradeBar;
        }


        /// <summary>
        /// Parses Option trade bar data into the specified tradebar type, useful for custom types with OHLCV data deriving from TradeBar
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">The base data used to compute the time of the bar since the line specifies a milliseconds since midnight</param>
        /// <returns></returns>
        public static TradeBar ParseOption(SubscriptionDataConfig config, string line, DateTime date)
        {
            return ParseTrade(config, line, date);
        }


        /// <summary>
        /// Parses Future trade bar data into the specified tradebar type, useful for custom types with OHLCV data deriving from TradeBar
        /// </summary>
        /// <param name="config">Symbols, Resolution, DataType, </param>
        /// <param name="line">Line from the data file requested</param>
        /// <param name="date">The base data used to compute the time of the bar since the line specifies a milliseconds since midnight</param>
        /// <returns></returns>
        public static TradeBar ParseFuture(SubscriptionDataConfig config, string line, DateTime date)
        {
            return ParseTrade(config, line, date);
        }

        /// <summary>
        /// Update the tradebar - build the bar from this pricing information:
        /// </summary>
        /// <param name="lastTrade">This trade price</param>
        /// <param name="bidPrice">Current bid price (not used) </param>
        /// <param name="askPrice">Current asking price (not used) </param>
        /// <param name="volume">Volume of this trade</param>
        /// <param name="bidSize">The size of the current bid, if available</param>
        /// <param name="askSize">The size of the current ask, if available</param>
        public override void Update(decimal lastTrade, decimal bidPrice, decimal askPrice, decimal volume, decimal bidSize, decimal askSize)
        {
            Initialize(lastTrade);
            if (lastTrade > high) high = lastTrade;
            if (lastTrade < low) low = lastTrade;
            //Volume is the total summed volume of trades in this bar:
            this.volume += volume;
            //Always set the closing price;
            this.value = lastTrade;
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
        /// Return a new instance clone of this object, used in fill forward
        /// </summary>
        /// <param name="fillForward">True if this is a fill forward clone</param>
        /// <returns>A clone of the current object</returns>
        public override BaseData Clone(bool fillForward)
        {
            var clone = base.Clone(fillForward);

            if (fillForward)
            {
                // zero volume out, since it would skew calculations in volume-based indicators
                ((TradeBar)clone).volume = 0;
            }

            return clone;
        }

        /// <summary>
        /// Return a new instance clone of this object
        /// </summary>
        public override BaseData Clone()
        {
            return (BaseData)MemberwiseClone();
        }

        /// <summary>
        /// Initializes this bar with a first data point
        /// </summary>
        /// <param name="value">The seed value for this bar</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Initialize(decimal value)
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 0)
            {
                open = value;
                low = value;
                high = value;
            }
        }
    }
}
