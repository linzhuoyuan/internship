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
using System.Linq;
using NodaTime;
using Python.Runtime;
using QuantConnect.Data;
using QuantConnect.Orders.Fees;
using QuantConnect.Orders.Fills;
using QuantConnect.Orders.Slippage;
using QuantConnect.Securities.Equity;
using QuantConnect.Securities.Forex;
using QuantConnect.Securities.Interfaces;
using QuantConnect.Data.Market;
using QuantConnect.Python;
using QuantConnect.Data.Fundamental;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;

namespace QuantConnect.Securities
{
    /// <summary>
    /// A base vehicle properties class for providing a common interface to all assets in QuantConnect.
    /// </summary>
    /// <remarks>
    /// Security object is intended to hold properties of the specific security asset. These properties can include trade start-stop dates,
    /// price, market hours, resolution of the security, the holdings information for this security and the specific fill model.
    /// </remarks>
    public class Security : ISecurityPrice
    {
        protected readonly ConcurrentBag<SubscriptionDataConfig> SubscriptionsBag;
        private readonly ICurrencyConverter _currencyConverter;
        private LocalTimeKeeper _localTimeKeeper;
        
        /// <summary>
        /// A null security leverage value
        /// </summary>
        /// <remarks>This value is used to determine when the
        /// <see cref="SecurityInitializer"/> leverage is used</remarks>
        public const decimal NullLeverage = 0;

        // using concurrent bag to avoid list enumeration threading issues
        internal SecurityHolding holdings;
        internal SecurityHolding longHoldings;
        internal SecurityHolding shortHoldings;
        internal SecurityExchange exchange;
        internal IFeeModel feeModel;
        internal IFillModel fillModel;
        internal ISlippageModel slippageModel;
        internal ISecurityPortfolioModel portfolioModel;
        internal IBuyingPowerModel buyingPowerModel;
        internal ISettlementModel settlementModel;
        internal IVolatilityModel volatilityModel;
        internal ISecurityDataFilter dataFilter;
        internal IPriceVariationModel priceVariationModel;
        internal Symbol symbol;
        internal SecurityCache cache;
        internal bool isTradable;
        internal bool isDelisted;
        internal readonly Cash quoteCurrency;
        internal readonly SymbolProperties symbolProperties;

        /// <summary>
        /// Gets all the subscriptions for this security
        /// </summary>
        public IEnumerable<SubscriptionDataConfig> Subscriptions => SubscriptionsBag;

        /// <summary>
        /// <see cref="Symbol"/> for the asset.
        /// </summary>
        public Symbol Symbol => symbol;
        
        public DateTimeZone ExchangeTimeZone => symbol.ExchangeTimeZone;

        /// <summary>
        /// Gets the Cash object used for converting the quote currency to the account currency
        /// </summary>
        public Cash QuoteCurrency => quoteCurrency;

        /// <summary>
        /// Gets the symbol properties for this security
        /// </summary>
        public SymbolProperties SymbolProperties => symbolProperties;

        /// <summary>
        /// Type of the security.
        /// </summary>
        /// <remarks>
        /// QuantConnect currently only supports Equities and Forex
        /// </remarks>
        public SecurityType Type => symbol.id.SecurityType;

        /// <summary>
        /// Resolution of data requested for this security.
        /// </summary>
        /// <remarks>Tick, second or minute resolution for QuantConnect assets.</remarks>
        [Obsolete("This property is obsolete. Use the 'SubscriptionDataConfig' exposed by 'SubscriptionManager'")]
        public Resolution Resolution { get; private set; }

        /// <summary>
        /// Indicates the data will use previous bars when there was no trading in this time period. This was a configurable data stream setting set in initialization.
        /// </summary>
        [Obsolete("This property is obsolete. Use the 'SubscriptionDataConfig' exposed by 'SubscriptionManager'")]
        public bool IsFillDataForward { get; private set; }

        /// <summary>
        /// Indicates the security will continue feeding data after the primary market hours have closed. This was a configurable setting set in initialization.
        /// </summary>
        [Obsolete("This property is obsolete. Use the 'SubscriptionDataConfig' exposed by 'SubscriptionManager'")]
        public bool IsExtendedMarketHours { get; private set; }

        /// <summary>
        /// Gets the data normalization mode used for this security
        /// </summary>
        [Obsolete("This property is obsolete. Use the 'SubscriptionDataConfig' exposed by 'SubscriptionManager'")]
        public DataNormalizationMode DataNormalizationMode { get; private set; }

        /// <summary>
        /// Gets the subscription configuration for this security
        /// </summary>
        [Obsolete("This property returns only the first subscription. Use the 'Subscriptions' property for all of this security's subscriptions.")]
        public SubscriptionDataConfig SubscriptionDataConfig => SubscriptionsBag.FirstOrDefault();

        /// <summary>
        /// There has been at least one data point since our algorithm started running for us to determine price.
        /// </summary>
        public bool HasData => GetLastData() != null;

        /// <summary>
        /// Gets or sets whether or not this security should be considered tradable
        /// </summary>
        public bool IsTradable
        {
            get => isTradable;
            set => isTradable = value;
        }

        /// <summary>
        /// True if the security has been delisted from exchanges and is no longer tradable
        /// </summary>
        public bool IsDelisted
        {
            get => isDelisted;
            set => isDelisted = value;
        }

        /// <summary>
        /// Data cache for the security to store previous price information.
        /// </summary>
        /// <seealso cref="EquityCache"/>
        /// <seealso cref="ForexCache"/>
        public SecurityCache Cache
        {
            get => cache;
            set => cache = value;
        }

        /// <summary>
        /// Holdings class contains the portfolio, cash and processes order fills.
        /// </summary>
        /// <seealso cref="EquityHolding"/>
        /// <seealso cref="ForexHolding"/>
        public SecurityHolding Holdings
        {
            get => holdings;
            set => holdings = value;
        }

        /// <summary>
        /// LongHoldings class contains the portfolio, cash and processes order fills.
        /// </summary>
        /// <seealso cref="EquityHolding"/>
        /// <seealso cref="ForexHolding"/>
        public SecurityHolding LongHoldings
        {
            get => longHoldings;
            set => longHoldings = value;
        }

        /// <summary>
        /// ShortHoldings class contains the portfolio, cash and processes order fills.
        /// </summary>
        /// <seealso cref="EquityHolding"/>
        /// <seealso cref="ForexHolding"/>
        public SecurityHolding ShortHoldings
        {
            get => shortHoldings;
            set => shortHoldings = value;
        }

        /// <summary>
        /// Exchange class contains the market opening hours, along with pre-post market hours.
        /// </summary>
        /// <seealso cref="EquityExchange"/>
        /// <seealso cref="ForexExchange"/>
        public SecurityExchange Exchange
        {
            get => exchange;
            set => exchange = value;
        }

        /// <summary>
        /// Fee model used to compute order fees for this security
        /// </summary>
        public IFeeModel FeeModel
        {
            get => feeModel;
            set => feeModel = value;
        }

        /// <summary>
        /// Fill model used to produce fill events for this security
        /// </summary>
        public IFillModel FillModel
        {
            get => fillModel;
            set => fillModel = value;
        }

        /// <summary>
        /// Slippage model use to compute slippage of market orders
        /// </summary>
        public ISlippageModel SlippageModel
        {
            get => slippageModel;
            set => slippageModel = value;
        }

        /// <summary>
        /// Gets the portfolio model used by this security
        /// </summary>
        public ISecurityPortfolioModel PortfolioModel
        {
            get => portfolioModel;
            set => portfolioModel = value;
        }

        /// <summary>
        /// Gets the buying power model used for this security
        /// </summary>
        public IBuyingPowerModel BuyingPowerModel
        {
            get => buyingPowerModel;
            set => buyingPowerModel = value;
        }

        /// <summary>
        /// Gets the buying power model used for this security, an alias for <see cref="BuyingPowerModel"/>
        /// </summary>
        public IBuyingPowerModel MarginModel
        {
            get => buyingPowerModel;
            set => buyingPowerModel = value;
        }

        /// <summary>
        /// Gets the settlement model used for this security
        /// </summary>
        public ISettlementModel SettlementModel
        {
            get => settlementModel;
            set => settlementModel = value;
        }

        /// <summary>
        /// Gets the volatility model used for this security
        /// </summary>
        public IVolatilityModel VolatilityModel
        {
            get => volatilityModel;
            set => volatilityModel = value;
        }

        /// <summary>
        /// Customizable data filter to filter outlier ticks before they are passed into user event handlers.
        /// By default all ticks are passed into the user algorithms.
        /// </summary>
        /// <remarks>TradeBars (seconds and minute bars) are prefilter-ed to ensure the ticks which build the bars are realistically trade-able</remarks>
        /// <seealso cref="EquityDataFilter"/>
        /// <seealso cref="ForexDataFilter"/>
        public ISecurityDataFilter DataFilter
        {
            get => dataFilter;
            set => dataFilter = value;
        }

        /// <summary>
        /// Customizable price variation model used to define the minimum price variation of this security.
        /// By default minimum price variation is a constant find in the symbol-properties-database.
        /// </summary>
        /// <seealso cref="AdjustedPriceVariationModel"/>
        /// <seealso cref="SecurityPriceVariationModel"/>
        /// <seealso cref="EquityPriceVariationModel"/>
        public IPriceVariationModel PriceVariationModel
        {
            get => priceVariationModel;
            set => priceVariationModel = value;
        }

        /// <summary>
        /// Construct a new security vehicle based on the user options.
        /// </summary>
        public Security(SecurityExchangeHours exchangeHours, SubscriptionDataConfig config, Cash quoteCurrency, SymbolProperties symbolProperties, ICurrencyConverter currencyConverter)
            : this(config,
                quoteCurrency,
                symbolProperties,
                new SecurityExchange(exchangeHours),
                new SecurityCache(),
                new SecurityPortfolioModel(),
                new ImmediateFillModel(),
                new InteractiveBrokersFeeModel(),
                new ConstantSlippageModel(0),
                new ImmediateSettlementModel(),
                Securities.VolatilityModel.Null,
                new SecurityMarginModel(),
                new SecurityDataFilter(),
                new SecurityPriceVariationModel(),
                currencyConverter)
        {
        }

        /// <summary>
        /// Construct a new security vehicle based on the user options.
        /// </summary>
        public Security(Symbol symbol, SecurityExchangeHours exchangeHours, Cash quoteCurrency, SymbolProperties symbolProperties, ICurrencyConverter currencyConverter)
            : this(symbol,
                quoteCurrency,
                symbolProperties,
                new SecurityExchange(exchangeHours),
                new SecurityCache(),
                new SecurityPortfolioModel(),
                new ImmediateFillModel(),
                new InteractiveBrokersFeeModel(),
                new ConstantSlippageModel(0),
                new ImmediateSettlementModel(),
                Securities.VolatilityModel.Null,
                new SecurityMarginModel(),
                new SecurityDataFilter(),
                new SecurityPriceVariationModel(),
                currencyConverter
                )
        {
        }

        /// <summary>
        /// Construct a new security vehicle based on the user options.
        /// </summary>
        protected Security(Symbol symbol,
            Cash quoteCurrency,
            SymbolProperties symbolProperties,
            SecurityExchange exchange,
            SecurityCache cache,
            ISecurityPortfolioModel portfolioModel,
            IFillModel fillModel,
            IFeeModel feeModel,
            ISlippageModel slippageModel,
            ISettlementModel settlementModel,
            IVolatilityModel volatilityModel,
            IBuyingPowerModel buyingPowerModel,
            ISecurityDataFilter dataFilter,
            IPriceVariationModel priceVariationModel,
            ICurrencyConverter currencyConverter)
        {
            if (symbolProperties == null)
            {
                throw new ArgumentNullException(
                    nameof(symbolProperties),
                    "Security requires a valid SymbolProperties instance.");
            }

            if (symbolProperties.QuoteCurrency != quoteCurrency.Symbol)
            {
                throw new ArgumentException(
                    "symbolProperties.QuoteCurrency must match the quoteCurrency.Symbol");
            }

            this._currencyConverter = currencyConverter;

            this.symbol = symbol;
            SubscriptionsBag = new ConcurrentBag<SubscriptionDataConfig>();
            this.quoteCurrency = quoteCurrency;
            this.symbolProperties = symbolProperties;
            IsTradable = true;
            Cache = cache;
            Exchange = exchange;
            DataFilter = dataFilter;
            PriceVariationModel = priceVariationModel;
            PortfolioModel = portfolioModel;
            BuyingPowerModel = buyingPowerModel;
            FillModel = fillModel;
            FeeModel = feeModel;
            SlippageModel = slippageModel;
            SettlementModel = settlementModel;
            VolatilityModel = volatilityModel;
            Holdings = new SecurityHolding(this, currencyConverter);
            LongHoldings = new SecurityHolding(this, currencyConverter, SecurityHoldingType.Long);
            ShortHoldings = new SecurityHolding(this, currencyConverter, SecurityHoldingType.Short);

            UpdateSubscriptionProperties();
        }


        /// <summary>
        /// Temporary convenience constructor
        /// </summary>
        protected Security(SubscriptionDataConfig config,
            Cash quoteCurrency,
            SymbolProperties symbolProperties,
            SecurityExchange exchange,
            SecurityCache cache,
            ISecurityPortfolioModel portfolioModel,
            IFillModel fillModel,
            IFeeModel feeModel,
            ISlippageModel slippageModel,
            ISettlementModel settlementModel,
            IVolatilityModel volatilityModel,
            IBuyingPowerModel buyingPowerModel,
            ISecurityDataFilter dataFilter,
            IPriceVariationModel priceVariationModel,
            ICurrencyConverter currencyConverter)
            : this(config.Symbol,
                quoteCurrency,
                symbolProperties,
                exchange,
                cache,
                portfolioModel,
                fillModel,
                feeModel,
                slippageModel,
                settlementModel,
                volatilityModel,
                buyingPowerModel,
                dataFilter,
                priceVariationModel,
                currencyConverter)
        {
            SubscriptionsBag.Add(config);
            UpdateSubscriptionProperties();
        }

        /// <summary>
        /// Read only property that checks if we currently own stock in the company.
        /// </summary>
        public virtual bool HoldStock => holdings.AbsoluteQuantity > 0
                                         || longHoldings.AbsoluteQuantity > 0
                                         || shortHoldings.AbsoluteQuantity > 0;

        /// <summary>
        /// Alias for HoldStock - Do we have any of this security
        /// </summary>
        public virtual bool Invested => HoldStock;

        /// <summary>
        /// Local time for this market
        /// </summary>
        public virtual DateTime LocalTime
        {
            get
            {
                if (_localTimeKeeper == null)
                {
                    throw new Exception("Security.SetLocalTimeKeeper(LocalTimeKeeper) must be called in order to use the LocalTime property.");
                }
                return _localTimeKeeper.LocalTime;
            }
        }

        /// <summary>
        /// Get the current value of the security.
        /// </summary>
        public virtual decimal Price => cache.price;

        /// <summary>
        /// Leverage for this Security.
        /// </summary>
        public virtual decimal Leverage => holdings.Leverage;

        /// <summary>
        /// If this uses tradebar data, return the most recent high.
        /// </summary>
        public virtual decimal High => Cache.high == 0 ? Cache.price : Cache.high;

        /// <summary>
        /// If this uses tradebar data, return the most recent low.
        /// </summary>
        public virtual decimal Low => Cache.Low == 0 ? Cache.price : Cache.Low;

        /// <summary>
        /// If this uses tradebar data, return the most recent close.
        /// </summary>
        public virtual decimal Close => Cache.Close == 0 ? Cache.price : Cache.Close;

        /// <summary>
        /// If this uses tradebar data, return the most recent open.
        /// </summary>
        public virtual decimal Open => Cache.open == 0 ? Cache.price : Cache.open;

        /// <summary>
        /// Access to the volume of the equity today
        /// </summary>
        public virtual decimal Volume => Cache.volume;

        /// <summary>
        /// Gets the most recent bid price if available
        /// </summary>
        public virtual decimal BidPrice => Cache.bidPrice == 0 ? Cache.price : Cache.bidPrice;

        /// <summary>
        /// Gets the most recent bid size if available
        /// </summary>
        public virtual decimal BidSize => Cache.bidSize;

        /// <summary>
        /// Gets the most recent ask price if available
        /// </summary>
        public virtual decimal AskPrice => Cache.askPrice == 0 ? Cache.price : Cache.askPrice;

        /// <summary>
        /// Gets the most recent ask size if available
        /// </summary>
        public virtual decimal AskSize => Cache.askSize;

        /// <summary>
        /// Access to the open interest of the security today
        /// </summary>
        public virtual long OpenInterest => Cache.openInterest;

        /// <summary>
        /// 上一交易日收盘价
        /// </summary>
        public virtual decimal PreDayClose => Cache.preDayClose;

        /// <summary>
        /// Gets the fundamental data associated with the security if there is any, otherwise null.
        /// </summary>
        public Fundamentals Fundamentals
        {
            get
            {
                if (cache.GetData<Fundamentals>() != null)
                {
                    return cache.GetData<Fundamentals>();
                }

                var coarse = cache.GetData<CoarseFundamental>();
                if (coarse == null)
                {
                    return null;
                }

                return new Fundamentals
                {
                    symbol = Symbol,
                    value = coarse.Value,
                    endTime = coarse.EndTime,
                    dollarVolume = coarse.DollarVolume,
                    dataType = coarse.DataType,
                    market = coarse.Market,
                    volume = coarse.Volume
                };
            }
        }

        /// <summary>
        /// Get the last price update set to the security.
        /// </summary>
        /// <returns>BaseData object for this security</returns>
        public BaseData GetLastData()
        {
            return cache.GetData();
        }

        public void RemoveLocalTimeKeeper()
        {
            _localTimeKeeper.TimeUpdated -= OnTimeUpdated;
        }

        /// <summary>
        /// Sets the <see cref="LocalTimeKeeper"/> to be used for this <see cref="Security"/>.
        /// This is the source of this instance's time.
        /// </summary>
        /// <param name="localTimeKeeper">The source of this <see cref="Security"/>'s time.</param>
        public void SetLocalTimeKeeper(LocalTimeKeeper localTimeKeeper)
        {
            _localTimeKeeper = localTimeKeeper;
            exchange.SetLocalDateTimeFrontier(localTimeKeeper.LocalTime);
            _localTimeKeeper.TimeUpdated += OnTimeUpdated;
        }

        private void OnTimeUpdated(object sender, TimeUpdatedEventArgs args)
        {
            //Update the Exchange/Timer:
            exchange.SetLocalDateTimeFrontier(args.Time);
        }

        /// <summary>
        /// Update any security properties based on the latest market data and time
        /// </summary>
        /// <param name="data">New data packet from LEAN</param>
        public void SetMarketPrice(BaseData data)
        {
            //Add new point to cache:
            if (data == null) return;
            cache.AddData(data);

            if (data is OpenInterest || data.value == 0m) return;

            if (data.Symbol.ID.Market == Market.Deribit &&
                data.Symbol.SecurityType is SecurityType.Future or SecurityType.Option)
            {
                var price = cache.markPrice > 0 ? cache.markPrice : cache.price;
                holdings.UpdateMarketPrice(price);
                volatilityModel.Update(this, data);
                shortHoldings.UpdateMarketPrice(price);
                longHoldings.UpdateMarketPrice(price);
                return;
            }


            holdings.UpdateMarketPrice(cache.price);
            volatilityModel.Update(this, data);
            shortHoldings.UpdateMarketPrice(cache.price);
            longHoldings.UpdateMarketPrice(cache.price);
        }

        /// <summary>
        /// Update any security properties based on the latest realtime data and time
        /// </summary>
        /// <param name="data">New data packet from LEAN</param>
        public void SetRealTimePrice(BaseData data)
        {
            //Add new point to cache:
            if (data == null) return;
            cache.AddData(data);

            if (data is OpenInterest || data.value == 0m) return;

            if (data.Symbol.ID.Market == Market.Deribit &&
                (data.Symbol.SecurityType == SecurityType.Future ||
                 data.Symbol.SecurityType == SecurityType.Option))
            {
                var price = cache.markPrice > 0 ? cache.markPrice : cache.price;
                holdings.UpdateMarketPrice(price);
                volatilityModel.Update(this, data);
                shortHoldings.UpdateMarketPrice(price);
                longHoldings.UpdateMarketPrice(price);
                return;
            }


            holdings.UpdateMarketPrice(cache.price);
            volatilityModel.Update(this, data);
            shortHoldings.UpdateMarketPrice(cache.price);
            longHoldings.UpdateMarketPrice(cache.price);
        }

        /// <summary>
        /// Returns true if the security contains at least one subscription that represents custom data
        /// </summary>
        [Obsolete("This method is obsolete. Use the 'SubscriptionDataConfig' exposed by" +
            " 'SubscriptionManager' and the 'IsCustomData()' extension method")]
        public bool IsCustomData()
        {
            if (SubscriptionsBag == null || !SubscriptionsBag.Any())
            {
                return false;
            }

            return SubscriptionsBag.Any(x => x.IsCustomData);
        }

        /// <summary>
        /// Set the leverage parameter for this security
        /// </summary>
        /// <param name="leverage">Leverage for this asset</param>
        public void SetLeverage(decimal leverage)
        {
            if (symbol.id.SecurityType is SecurityType.Future or SecurityType.Option)
                return;

            buyingPowerModel.SetLeverage(this, leverage);
        }

        /// <summary>
        /// Sets the data normalization mode to be used by this security
        /// </summary>
        [Obsolete("This method is obsolete. Use the 'SubscriptionDataConfig' exposed by" +
            " 'SubscriptionManager' and the 'SetDataNormalizationMode()' extension method")]
        public virtual void SetDataNormalizationMode(DataNormalizationMode mode)
        {
            foreach (var subscription in SubscriptionsBag)
            {
                subscription.DataNormalizationMode = mode;
            }
            UpdateSubscriptionProperties();
        }

        /// <summary>
        /// This method will refresh the value of the <see cref="DataNormalizationMode"/> property.
        /// This is required for backward-compatibility.
        /// TODO: to be deleted with the DataNormalizationMode property
        /// </summary>
        public void RefreshDataNormalizationModeProperty()
        {
            DataNormalizationMode = SubscriptionsBag
                .Select(x => x.DataNormalizationMode)
                .DefaultIfEmpty(DataNormalizationMode.Adjusted)
                .FirstOrDefault();
        }

        /// <summary>
        /// Sets the fee model
        /// </summary>
        /// <param name="feeModel">Model that represents a fee model</param>
        public void SetFeeModel(IFeeModel feeModel)
        {
            this.feeModel = feeModel;
        }

        /// <summary>
        /// Sets the fee model
        /// </summary>
        /// <param name="feeModel">Model that represents a fee model</param>
        public void SetFeeModel(PyObject feeModel)
        {
            this.feeModel = new FeeModelPythonWrapper(feeModel);
        }

        /// <summary>
        /// Sets the fill model
        /// </summary>
        /// <param name="fillModel">Model that represents a fill model</param>
        public void SetFillModel(IFillModel fillModel)
        {
            this.fillModel = fillModel;
        }

        /// <summary>
        /// Sets the fill model
        /// </summary>
        /// <param name="fillModel">Model that represents a fill model</param>
        public void SetFillModel(PyObject fillModel)
        {
            this.fillModel = new FillModelPythonWrapper(fillModel);
        }

        /// <summary>
        /// Sets the slippage model
        /// </summary>
        /// <param name="slippageModel">Model that represents a slippage model</param>
        public void SetSlippageModel(ISlippageModel slippageModel)
        {
            this.slippageModel = slippageModel;
        }

        /// <summary>
        /// Sets the slippage model
        /// </summary>
        /// <param name="slippageModel">Model that represents a slippage model</param>
        public void SetSlippageModel(PyObject slippageModel)
        {
            SlippageModel = new SlippageModelPythonWrapper(slippageModel);
        }

        /// <summary>
        /// Sets the volatility model
        /// </summary>
        /// <param name="volatilityModel">Model that represents a volatility model</param>
        public void SetVolatilityModel(IVolatilityModel volatilityModel)
        {
            this.volatilityModel = volatilityModel;
        }

        /// <summary>
        /// Sets the volatility model
        /// </summary>
        /// <param name="volatilityModel">Model that represents a volatility model</param>
        public void SetVolatilityModel(PyObject volatilityModel)
        {
            this.volatilityModel = new VolatilityModelPythonWrapper(volatilityModel);
        }

        /// <summary>
        /// Sets the buying power model
        /// </summary>
        /// <param name="buyingPowerModel">Model that represents a security's model of buying power</param>
        public void SetBuyingPowerModel(IBuyingPowerModel buyingPowerModel)
        {
            this.buyingPowerModel = buyingPowerModel;
        }

        /// <summary>
        /// Sets the buying power model
        /// </summary>
        /// <param name="pyObject">Model that represents a security's model of buying power</param>
        public void SetBuyingPowerModel(PyObject pyObject)
        {
            SetBuyingPowerModel(new BuyingPowerModelPythonWrapper(pyObject));
        }

        /// <summary>
        /// Sets the margin model
        /// </summary>
        /// <param name="marginModel">Model that represents a security's model of buying power</param>
        public void SetMarginModel(IBuyingPowerModel marginModel)
        {
            buyingPowerModel = marginModel;
        }

        /// <summary>
        /// Sets the margin model
        /// </summary>
        /// <param name="pyObject">Model that represents a security's model of buying power</param>
        public void SetMarginModel(PyObject pyObject)
        {
            SetMarginModel(new BuyingPowerModelPythonWrapper(pyObject));
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return symbol.ToString();
        }

        /// <summary>
        /// Adds the specified data subscription to this security.
        /// </summary>
        /// <param name="subscription">The subscription configuration to add. The Symbol and ExchangeTimeZone properties must match the existing Security object</param>
        internal void AddData(SubscriptionDataConfig subscription)
        {
            if (subscription.symbol != symbol)
                throw new ArgumentException("Symbols must match.", "subscription.Symbol");
            if (!subscription.ExchangeTimeZone.Equals(Exchange.TimeZone))
                throw new ArgumentException("ExchangeTimeZones must match.", "subscription.ExchangeTimeZone");
            SubscriptionsBag.Add(subscription);
            UpdateSubscriptionProperties();
        }

        /// <summary>
        /// Adds the specified data subscriptions to this security.
        /// </summary>
        /// <param name="subscriptions">The subscription configuration to add. The Symbol and ExchangeTimeZone properties must match the existing Security object</param>
        internal void AddData(SubscriptionDataConfigList subscriptions)
        {
            foreach (var subscription in subscriptions)
            {
                if (subscription.Symbol != Symbol)
                    throw new ArgumentException("Symbols must match.", "subscription.Symbol");
                if (!subscription.ExchangeTimeZone.Equals(Exchange.TimeZone))
                    throw new ArgumentException(
                        "ExchangeTimeZones must match.",
                        "subscription.ExchangeTimeZone");
                SubscriptionsBag.Add(subscription);
            }
            UpdateSubscriptionProperties();
        }

        private void UpdateSubscriptionProperties()
        {
            Resolution = SubscriptionsBag
                .Select(x => x.Resolution)
                .DefaultIfEmpty(Resolution.Daily)
                .Min();
            IsFillDataForward = SubscriptionsBag.Any(x => x.FillDataForward);
            IsExtendedMarketHours = SubscriptionsBag.Any(x => x.ExtendedMarketHours);
            RefreshDataNormalizationModeProperty();
        }
    }
}