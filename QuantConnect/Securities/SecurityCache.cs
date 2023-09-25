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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using QuantConnect.Data;
using QuantConnect.Data.Market;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Base class caching caching spot for security data and any other temporary properties.
    /// </summary>
    /// <remarks>
    /// This class is virtually unused and will soon be made obsolete.
    /// This comment made in a remark to prevent obsolete errors in all users algorithms
    /// </remarks>
    public class SecurityCache
    {
        private static readonly List<Type> BaseDataTypes = new List<Type>();

        static SecurityCache()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsSubclassOf(typeof(BaseData)))
                    {
                        BaseDataTypes.Add(type);
                    }
                }
            }
        }

        private BaseData _lastData;
        //private readonly ConcurrentDictionary<Type, BaseData> _dataByType = new ConcurrentDictionary<Type, BaseData>();

        private readonly Dictionary<Type, BaseData> _dataByType = new Dictionary<Type, BaseData>();
        internal long openInterest;
        internal decimal preDayClose;
        internal decimal volume;
        internal decimal price;
        internal decimal close;
        internal decimal open;
        internal decimal high;
        internal decimal low;
        internal decimal preDaySettlementPrice;
        internal decimal settlementPrice;
        internal decimal markPrice;
        internal decimal bidPrice;
        internal decimal askPrice;
        internal decimal bidSize;
        internal decimal askSize;
        internal decimal ask_iv;
        internal decimal bid_iv;
        internal decimal mark_iv;
        internal decimal mark_fitted_iv;
        internal decimal delta;
        internal decimal vega;
        internal decimal rho;
        internal decimal gamma;
        internal decimal theta;

        private DateTime _lastDataTime;
        // this is used to prefer quote bar data over the tradebar data
        private DateTime _lastQuoteBarUpdate;

        public SecurityCache()
        {
            foreach (var type in BaseDataTypes)
            {
                _dataByType[type] = null;
            }
        }

        /// <summary>
        /// Gets the most recent price submitted to this cache
        /// </summary>
        public decimal Price
        {
            get => price;
            private set => price = value;
        }

        /// <summary>
        /// Gets the most recent open submitted to this cache
        /// </summary>
        public decimal Open
        {
            get => open;
            private set => open = value;
        }

        /// <summary>
        /// Gets the most recent high submitted to this cache
        /// </summary>
        public decimal High
        {
            get => high;
            private set => high = value;
        }

        /// <summary>
        /// Gets the most recent low submitted to this cache
        /// </summary>
        public decimal Low
        {
            get => low;
            private set => low = value;
        }

        /// <summary>
        /// Gets the most recent close submitted to this cache
        /// </summary>
        public decimal Close
        {
            get => close;
            private set => close = value;
        }

        /// <summary>
        /// Gets the most recent bid submitted to this cache
        /// </summary>
        public decimal BidPrice
        {
            get => bidPrice;
            private set => bidPrice = value;
        }

        /// <summary>
        /// Gets the most recent ask submitted to this cache
        /// </summary>
        public decimal AskPrice
        {
            get => askPrice;
            private set => askPrice = value;
        }

        /// <summary>
        /// Gets the most recent bid size submitted to this cache
        /// </summary>
        public decimal BidSize
        {
            get => bidSize;
            private set => bidSize = value;
        }

        /// <summary>
        /// Gets the most recent ask size submitted to this cache
        /// </summary>
        public decimal AskSize
        {
            get => askSize;
            private set => askSize = value;
        }

        /// <summary>
        /// Gets the most recent volume submitted to this cache
        /// </summary>
        public decimal Volume
        {
            get => volume;
            private set => volume = value;
        }

        /// <summary>
        /// Gets the most recent open interest submitted to this cache
        /// </summary>
        public long OpenInterest
        {
            get => openInterest;
            private set => openInterest = value;
        }

        /// <summary>
        /// The pre-day close price for the instrument.
        /// </summary>
        public decimal PreDayClose
        {
            get => preDayClose;
            private set => preDayClose = value;
        }

        /// <summary>
        ///  The pre-day settlement price for the instrument.
        /// </summary>
        public decimal PreDaySettlementPrice
        {
            get => preDaySettlementPrice;
            private set => preDaySettlementPrice = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public decimal SettlementPrice
        {
            get => settlementPrice;
            private set => settlementPrice = value;
        }

        /// <summary>
        /// The mark price for the instrument
        /// </summary>
        public decimal MarkPrice
        {
            get => markPrice;
            private set => markPrice = value;
        }

        /// <summary>
        /// The ask implied volatility for the instrument
        /// </summary>
        public decimal AskIV
        {
            get => ask_iv;
            private set => ask_iv = value;
        }

        /// <summary>
        /// The bid implied volatility for the instrument
        /// </summary>
        public decimal BidIV
        {
            get => bid_iv;
            private set => bid_iv = value;
        }

        /// <summary>
        /// The mark implied volatility for the instrument
        /// </summary>
        public decimal MarkIV
        {
            get => mark_iv;
            private set => mark_iv = value;
        }

        /// <summary>
        /// The mark fitted implied volatility for the instrument
        /// </summary>
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

        public decimal Gamma
        {
            get => gamma;
            private set => gamma = value;
        }

        public decimal Theta
        {
            get => theta;
            private set => theta = value;
        }

        public DateTime LastDataTime
        {
            set => _lastDataTime = value;
            get => _lastDataTime;
        }

        public void Clear()
        {
            Reset();
            _lastData = null;
            _lastDataTime = DateTime.MinValue;
            _lastQuoteBarUpdate = DateTime.MinValue;
            price = 0m;
            volume = 0m;
            askPrice = 0m;
            askSize = 0m;
            bidPrice = 0m;
            bidSize = 0m;
            markPrice = 0m;
            settlementPrice = 0m;
            preDaySettlementPrice = 0m;
            preDayClose = 0m;
            openInterest = 0;
            open = 0m;
            high = 0m;
            low = 0m;
            close = 0m;
            ask_iv = 0m;
            bid_iv = 0m;
            mark_iv = 0m;
            mark_fitted_iv = 0m;
            delta = 0m;
            vega = 0m;
            gamma = 0m;
            theta = 0m;
            rho = 0m;
        }

        /// <summary>
        /// Add a new market data point to the local security cache for the current market price.
        /// Rules:
        ///     Don't cache fill forward data.
        ///     Always return the last observation.
        ///     If two consecutive data has the same time stamp and one is Quotebars and the other Tradebar, prioritize the Quotebar.
        /// </summary>
        public void AddData(BaseData data)
        {
            _lastDataTime = data.Time;

            if (data is OpenInterest openInterest)
            {
                this.openInterest = (long)openInterest.value;
                preDayClose = openInterest.PreDayClose;
                preDaySettlementPrice = openInterest.PreDaySettlementPrice;
                if (openInterest.MarkPrice != 0)
                    markPrice = openInterest.MarkPrice;
                return;
            }

            var tick = data as Tick;
            if (tick?.TickType == TickType.OpenInterest)
            {
                this.openInterest = (long)tick.value;
                if (tick.MarkPrice != 0)
                    markPrice = tick.MarkPrice;
                return;
            }

            // Only cache non fill-forward data.
            if (data.isFillForward) return;

            // Always keep track of the last observation
            _dataByType[data.GetType()] = data;

            // don't set _lastData if receive quotebar then tradebar w/ same end time. this
            // was implemented to grant preference towards using quote data in the fill
            // models and provide a level of determinism on the values exposed via the cache.
            if (_lastData == null
              || _lastQuoteBarUpdate != data.endTime
              || data.dataType != MarketDataType.TradeBar)
            {
                _lastData = data;
            }

            if (tick != null)
            {
                _lastData = data;

                if (tick.MarkPrice != 0)
                    markPrice = tick.MarkPrice;
                if (tick.SettlementPrice != 0)
                    settlementPrice = tick.SettlementPrice;
                if (tick.Value != 0)
                    price = tick.Value;
                if (tick.AskIV != 0)
                    ask_iv = tick.AskIV;
                if (tick.BidIV != 0)
                    bid_iv = tick.BidIV;
                if (tick.MarkIV != 0)
                    mark_iv = tick.MarkIV;
                if (tick.Delta != 0)
                    delta = tick.Delta;
                if (tick.Vega != 0)
                    vega = tick.Vega;
                if (tick.Gamma != 0)
                    gamma = tick.Gamma;
                if (tick.Rho != 0)
                    rho = tick.Rho;
                if (tick.Theta != 0)
                    theta = tick.Theta;

                if (tick.TickType == TickType.Trade && tick.Quantity != 0)
                {
                    volume = tick.Quantity;
                }
                //if (tick.TickType == TickType.Quote)
                {
                    if (tick.BidPrice != 0)
                    {
                        bidPrice = tick.BidPrice;
                    }
                    bidSize = tick.BidSize;
                    if (tick.AskPrice != 0)
                    {
                        askPrice = tick.AskPrice;
                    }
                    askSize = tick.AskSize;
                }
            }
            else if (data is IBar bar)
            {
                if (_lastQuoteBarUpdate != data.endTime)
                {
                    var barData = bar.GetData();
                    if (barData.open != 0) open = barData.open;
                    if (barData.high != 0) high = barData.high;
                    if (barData.low != 0) low = barData.low;
                    if (barData.close != 0)
                    {
                        price = barData.close;
                        close = barData.close;
                    }
                }

                if (bar is TradeBar tradeBar)
                {
                    if (tradeBar.volume != 0) volume = tradeBar.volume;
                }
                else if (bar is QuoteBar quoteBar)
                {
                    _lastQuoteBarUpdate = quoteBar.endTime;
                    if (quoteBar.ask != null && quoteBar.ask.close != 0)
                    {
                        askPrice = quoteBar.ask.close;
                    }
                    if (quoteBar.bid != null && quoteBar.bid.close != 0)
                    {
                        bidPrice = quoteBar.bid.close;
                    }
                    bidSize = quoteBar.LastBidSize;
                    askSize = quoteBar.LastAskSize;
                    if (quoteBar.markPrice != 0)
                        markPrice = quoteBar.markPrice;
                    if (quoteBar.ask_iv != 0)
                        ask_iv = quoteBar.ask_iv;
                    if (quoteBar.bid_iv != 0)
                        bid_iv = quoteBar.bid_iv;
                    if (quoteBar.mark_iv != 0)
                        mark_iv = quoteBar.mark_iv;
                    if (quoteBar.mark_fitted_iv != 0)
                        mark_fitted_iv = quoteBar.mark_fitted_iv;
                    if (quoteBar.delta != 0)
                        delta = quoteBar.delta;
                    if (quoteBar.vega != 0)
                        vega = quoteBar.vega;
                    if (quoteBar.gamma != 0)
                        gamma = quoteBar.gamma;
                    if (quoteBar.rho != 0)
                        rho = quoteBar.rho;
                    if (quoteBar.theta != 0)
                        theta = quoteBar.theta;

                }
            }
            else if (data.DataType != MarketDataType.Auxiliary)
            {
                price = data.value;
            }
        }

        /// <summary>
        /// Stores the specified data instance in the cache WITHOUT updating any of the cache properties, such as Price
        /// </summary>
        /// <param name="data"></param>
        public void StoreData(BaseData data)
        {
            _dataByType[data.GetType()] = data;
        }

        /// <summary>
        /// Get last data packet received for this security
        /// </summary>
        /// <returns>BaseData type of the security</returns>
        public BaseData GetData()
        {
            return _lastData;
        }

        /// <summary>
        /// Get last data packet received for this security of the specified ty[e
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <returns>The last data packet, null if none received of type</returns>
        public T GetData<T>()
            where T : BaseData
        {
            _dataByType.TryGetValue(typeof(T), out var data);
            return data as T;
        }

        /// <summary>
        /// Reset cache storage and free memory
        /// </summary>
        public void Reset()
        {
            foreach (var type in BaseDataTypes)
            {
                _dataByType[type] = null;
            }
            //_dataByType.Clear();
        }
    }
}
