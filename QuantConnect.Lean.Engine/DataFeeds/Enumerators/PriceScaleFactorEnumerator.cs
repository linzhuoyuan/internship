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
using System.Collections;
using System.Collections.Generic;
using QuantConnect.Data;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Data.Market;

namespace QuantConnect.Lean.Engine.DataFeeds.Enumerators
{
    /// <summary>
    /// This enumerator will update the <see cref="SubscriptionDataConfig.PriceScaleFactor"/> when required
    /// and adjust the raw <see cref="BaseData"/> prices based on the provided <see cref="SubscriptionDataConfig"/>.
    /// Assumes the prices of the provided <see cref="IEnumerator"/> are in raw mode.
    /// </summary>
    public sealed class PriceScaleFactorEnumerator : IEnumerator<BaseData>
    {
        private readonly IEnumerator<BaseData> _rawDataEnumerator;
        private readonly SubscriptionDataConfig _config;
        private readonly Lazy<FactorFile> _factorFile;
        private DateTime _lastTradableDate;
        internal BaseData current;

        /// <summary>
        /// Explicit interface implementation for <see cref="Current"/>
        /// </summary>
        object IEnumerator.Current => current;

        /// <summary>
        /// Last read <see cref="BaseData"/> object from this type and source
        /// </summary>
        public BaseData Current
        {
            get => current;
            private set => current = value;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="PriceScaleFactorEnumerator"/>.
        /// </summary>
        /// <param name="rawDataEnumerator">The underlying raw data enumerator</param>
        /// <param name="config">The <see cref="SubscriptionDataConfig"/> to enumerate for.
        /// Will determine the <see cref="DataNormalizationMode"/> to use.</param>
        /// <param name="factorFile">The <see cref="FactorFile"/> instance to use</param>
        public PriceScaleFactorEnumerator(
            IEnumerator<BaseData> rawDataEnumerator,
            SubscriptionDataConfig config,
            Lazy<FactorFile> factorFile)
        {
            _lastTradableDate = DateTime.MinValue;
            _config = config;
            _rawDataEnumerator = rawDataEnumerator;
            _factorFile = factorFile;
        }

        /// <summary>
        /// Dispose of the underlying enumerator.
        /// </summary>
        public void Dispose()
        {
            _rawDataEnumerator.Dispose();
        }

        /// <summary>
        /// Advances the enumerator to the next element of the collection.
        /// </summary>
        /// <returns>
        /// True if the enumerator was successfully advanced to the next element;
        /// False if the enumerator has passed the end of the collection.
        /// </returns>
        public bool MoveNext()
        {
            var underlyingReturnValue = _rawDataEnumerator.MoveNext();
            current = _rawDataEnumerator.Current;

            if (underlyingReturnValue
                && current != null
                && _factorFile != null)
            {
                if (current.Time.Date > _lastTradableDate)
                {
                    _lastTradableDate = current.time.Date;
                    UpdateScaleFactor(_lastTradableDate);
                }

                var securityType = current.symbol.id.SecurityType;
                switch (current.DataType)
                {
                    case MarketDataType.TradeBar:
                        if (current is TradeBar tradeBar)
                        {
                            tradeBar.open = _config.GetNormalizedPrice(tradeBar.open);
                            tradeBar.high = _config.GetNormalizedPrice(tradeBar.high);
                            tradeBar.low = _config.GetNormalizedPrice(tradeBar.low);
                            tradeBar.value = _config.GetNormalizedPrice(tradeBar.value);
                        }
                        break;
                    case MarketDataType.Tick:
                        if (current is Tick tick)
                        {
                            if (securityType == SecurityType.Equity)
                            {
                                tick.Value = _config.GetNormalizedPrice(tick.Value);
                            }
                            if (securityType == SecurityType.Option
                                || securityType == SecurityType.Future)
                            {
                                if (tick.TickType == TickType.Trade)
                                {
                                    tick.Value = _config.GetNormalizedPrice(tick.Value);
                                }
                                else if (tick.TickType != TickType.OpenInterest)
                                {
                                    tick.BidPrice = tick.BidPrice != 0 ? _config.GetNormalizedPrice(tick.BidPrice) : 0;
                                    tick.AskPrice = tick.AskPrice != 0 ?_config.GetNormalizedPrice(tick.AskPrice) : 0;

                                    if (tick.BidPrice != 0)
                                    {
                                        if (tick.AskPrice != 0)
                                        {
                                            tick.Value = (tick.BidPrice + tick.AskPrice) / 2m;
                                        }
                                        else
                                        {
                                            tick.Value = tick.BidPrice;
                                        }
                                    }
                                    else
                                    {
                                        tick.Value = tick.AskPrice;
                                    }
                                }
                            }
                        }
                        break;
                    case MarketDataType.QuoteBar:
                        if (current is QuoteBar quoteBar)
                        {
                            if (quoteBar.ask != null)
                            {
                                quoteBar.ask.open = _config.GetNormalizedPrice(quoteBar.ask.open);
                                quoteBar.ask.high = _config.GetNormalizedPrice(quoteBar.ask.high);
                                quoteBar.ask.low = _config.GetNormalizedPrice(quoteBar.ask.low);
                                quoteBar.ask.close = _config.GetNormalizedPrice(quoteBar.ask.close);
                            }
                            if (quoteBar.bid != null)
                            {
                                quoteBar.bid.open = _config.GetNormalizedPrice(quoteBar.bid.open);
                                quoteBar.bid.high = _config.GetNormalizedPrice(quoteBar.bid.high);
                                quoteBar.bid.low = _config.GetNormalizedPrice(quoteBar.bid.low);
                                quoteBar.bid.close = _config.GetNormalizedPrice(quoteBar.bid.close);
                            }
                            quoteBar.UpdateItem();
                            quoteBar.value = quoteBar.close;
                        }
                        break;
                    case MarketDataType.Auxiliary:
                    case MarketDataType.Base:
                    case MarketDataType.OptionChain:
                    case MarketDataType.FuturesChain:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return underlyingReturnValue;
        }

        /// <summary>
        /// Reset the IEnumeration
        /// </summary>
        /// <remarks>Not used</remarks>
        public void Reset()
        {
            throw new NotImplementedException("Reset method not implemented. Assumes loop will only be used once.");
        }

        private void UpdateScaleFactor(DateTime date)
        {
            switch (_config.DataNormalizationMode)
            {
                case DataNormalizationMode.Raw:
                    return;

                case DataNormalizationMode.TotalReturn:
                case DataNormalizationMode.SplitAdjusted:
                    _config.PriceScaleFactor = _factorFile.Value.GetSplitFactor(date);
                    break;

                case DataNormalizationMode.Adjusted:
                    _config.PriceScaleFactor = _factorFile.Value.GetPriceScaleFactor(date);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
