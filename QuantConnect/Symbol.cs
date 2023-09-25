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
using Fasterflect;
using Newtonsoft.Json;
using NodaTime;
using QuantConnect.Securities;

namespace QuantConnect
{
    /// <summary>
    /// Represents a unique security identifier. This is made of two components,
    /// the unique SID and the Value. The value is the current ticker symbol while
    /// the SID is constant over the life of a security
    /// </summary>
    [JsonConverter(typeof(SymbolJsonConverter))]
    public sealed class Symbol : IEquatable<Symbol>, IComparable
    {
        internal readonly Symbol underlying;
        internal string identify;
        internal readonly SecurityIdentifier id;
        internal readonly string value;

        /// <summary>
        /// Represents an unassigned symbol. This is intended to be used as an
        /// uninitialized, default value
        /// </summary>
        public static readonly Symbol Empty = new(SecurityIdentifier.Empty, string.Empty);

        /// <summary>
        /// Provides a convince method for creating a Symbol for most security types.
        /// This method currently does not support Option, Commodity, and Future
        /// </summary>
        /// <param name="ticker">The string ticker symbol</param>
        /// <param name="securityType">The security type of the ticker. If securityType == Option, then a canonical symbol is created</param>
        /// <param name="market">The market the ticker resides in</param>
        /// <param name="alias">An alias to be used for the symbol cache. Required when
        ///     adding the same security from different markets</param>
        /// <param name="perpetual">whether or not this is a perpetual contract</param>
        /// <returns>A new Symbol object for the specified ticker</returns>
        public static Symbol Create(string ticker, SecurityType securityType, string market, string alias = null, bool perpetual = false)
        {
            SecurityIdentifier sid;

            switch (securityType)
            {
                case SecurityType.Base:
                    sid = SecurityIdentifier.GenerateBase(ticker, market);
                    break;

                case SecurityType.Equity:
                    sid = SecurityIdentifier.GenerateEquity(ticker, market);
                    break;

                case SecurityType.Forex:
                    sid = SecurityIdentifier.GenerateForex(ticker, market);
                    break;

                case SecurityType.Cfd:
                    sid = SecurityIdentifier.GenerateCfd(ticker, market);
                    break;

                case SecurityType.Option:
                    var optionStyle = default(OptionStyle);
                    switch (market)
                    {
                        case Market.SSE:
                        case Market.SZSE:
                            optionStyle = OptionStyle.European;
                            break;
                        case Market.Deribit:
                            optionStyle = OptionStyle.European;
                            break;
                        case Market.Okex:
                            optionStyle = OptionStyle.European;
                            break;

                    };
                    return CreateOption(
                        ticker,
                        market,
                        optionStyle,
                        default,
                        0,
                        SecurityIdentifier.DefaultDate);

                case SecurityType.Future:
                    sid = SecurityIdentifier.GenerateFuture(
                        perpetual ? SecurityIdentifier.PerpetualExpiration : SecurityIdentifier.DefaultDate, ticker, market);
                    break;

                case SecurityType.Crypto:
                    sid = SecurityIdentifier.GenerateCrypto(ticker, market);
                    break;

                case SecurityType.Commodity:
                default:
                    throw new NotImplementedException("The security type has not been implemented yet: " + securityType);
            }

            return new Symbol(sid, alias ?? ticker);
        }

        /// <summary>
        /// Provides a convenience method for creating an option Symbol.
        /// </summary>
        /// <param name="underlying">The underlying ticker</param>
        /// <param name="market">The market the underlying resides in</param>
        /// <param name="style">The option style (American, European, ect..)</param>
        /// <param name="right">The option right (Put/Call)</param>
        /// <param name="strike">The option strike price</param>
        /// <param name="expiry">The option expiry date</param>
        /// <param name="alias">An alias to be used for the symbol cache. Required when
        /// adding the same security from different markets</param>
        /// <param name="mapSymbol">Specifies if symbol should be mapped using map file provider</param>
        /// <returns>A new Symbol object for the specified option contract</returns>
        public static Symbol CreateOption(
            string underlying,
            string market,
            OptionStyle style,
            OptionRight right,
            decimal strike,
            DateTime expiry,
            string alias = null,
            bool mapSymbol = true)
        {
            var underlyingSid = market is Market.Deribit or Market.Okex
                ? SecurityIdentifier.GenerateCrypto(underlying, market)
                : SecurityIdentifier.GenerateEquity(underlying, market, mapSymbol);
            var underlyingSymbol = new Symbol(underlyingSid, underlying);

            return CreateOption(underlyingSymbol, market, style, right, strike, expiry, alias);
        }

        /// <summary>
        /// Provides a convenience method for creating an option Symbol using SecurityIdentifier.
        /// </summary>
        /// <param name="underlyingSymbol">The underlying security symbol</param>
        /// <param name="market">The market the underlying resides in</param>
        /// <param name="style">The option style (American, European, ect..)</param>
        /// <param name="right">The option right (Put/Call)</param>
        /// <param name="strike">The option strike price</param>
        /// <param name="expiry">The option expiry date</param>
        /// <param name="alias">An alias to be used for the symbol cache. Required when
        /// adding the same security from different markets</param>
        /// <returns>A new Symbol object for the specified option contract</returns>
        public static Symbol CreateOption(
            Symbol underlyingSymbol,
            string market,
            OptionStyle style,
            OptionRight right,
            decimal strike,
            DateTime expiry,
            string alias = null)
        {
            var sid = SecurityIdentifier.GenerateOption(expiry, underlyingSymbol.ID, market, strike, right, style, alias);

            if (expiry == SecurityIdentifier.DefaultDate)
            {
                alias ??= "?" + underlyingSymbol.Value.LazyToUpper();
            }
            else
            {
                var sym = underlyingSymbol.Value;
                if (sym.Length > 5) sym += " ";

                alias ??= SymbolRepresentation.GenerateOptionTickerOSI(sym, sid.OptionRight, sid.StrikePrice, sid.Date);
            }
            if (alias == "10000691")
            {
                
            }
            return new Symbol(sid, alias, underlyingSymbol);
        }

        /// <summary>
        /// Provides a convenience method for creating a future Symbol.
        /// </summary>
        /// <param name="ticker">The ticker</param>
        /// <param name="market">The market the future resides in</param>
        /// <param name="expiry">The future expiry date</param>
        /// <param name="alias">An alias to be used for the symbol cache. Required when
        /// adding the same security from different markets</param>
        /// <returns>A new Symbol object for the specified future contract</returns>
        public static Symbol CreateFuture(string ticker, string market, DateTime expiry, string alias = null)
        {
            var sid = SecurityIdentifier.GenerateFuture(expiry, ticker, market);

            if (expiry == SecurityIdentifier.DefaultDate)
            {
                alias ??= "/" + ticker.LazyToUpper();
            }
            else
            {
                alias ??= SymbolRepresentation.GenerateFutureTicker(sid.Symbol, sid.Date);
            }

            return new Symbol(sid, alias);
        }

        /// <summary>
        /// Method returns true, if symbol is a derivative canonical symbol
        /// </summary>
        /// <returns>true, if symbol is a derivative canonical symbol</returns>
        public bool IsCanonical()
        {
            return
                (id.SecurityType == SecurityType.Future || id.SecurityType == SecurityType.Option && HasUnderlying)
                && id.Date == SecurityIdentifier.DefaultDate;
        }

        /// <summary>
        /// Method returns true, if symbol is a perpetual future symbol
        /// </summary>
        /// <returns>true, if symbol is a perpetual future symbol</returns>
        public bool IsOption()
        {
            return id.SecurityType is SecurityType.Option or SecurityType.IndexOption or SecurityType.FutureOption;
        }

        /// <summary>
        /// Method returns true, if symbol is a perpetual future symbol
        /// </summary>
        /// <returns>true, if symbol is a perpetual future symbol</returns>
        public bool IsPerpetual()
        {
            return (id.SecurityType == SecurityType.Future
                    && id.Date == SecurityIdentifier.PerpetualExpiration);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsChinaMarket()
        {
            return Market.IsChinaMarket(this);
        }

        public DateTimeZone ExchangeTimeZone { get; private set; }

        /// <summary>
        /// Determines if the specified <paramref name="symbol"/> is an underlying of this symbol instance
        /// </summary>
        /// <param name="symbol">The underlying to check for</param>
        /// <returns>True if the specified <paramref name="symbol"/> is an underlying of this symbol instance</returns>
        public bool HasUnderlyingSymbol(Symbol symbol)
        {
            var current = this;
            while (current.HasUnderlying)
            {
                if (current.Underlying == symbol)
                {
                    return true;
                }

                current = current.Underlying;
            }

            return false;
        }

        #region Properties

        /// <summary>
        /// Gets the current symbol for this ticker
        /// </summary>
        public string Value => value;

        /// <summary>
        /// 
        /// </summary>
        public SymbolProperties SymbolProperties { get; set; }

        /// <summary>
        /// Gets the security identifier for this symbol
        /// </summary>
        public SecurityIdentifier ID => id;

        /// <summary>
        /// 
        /// </summary>
        public string Identify => identify;

        /// <summary>
        /// Gets whether or not this <see cref="Symbol"/> is a derivative,
        /// that is, it has a valid <see cref="Underlying"/> property
        /// </summary>
        public bool HasUnderlying => !(underlying is null);

        /// <summary>
        /// Gets the security underlying symbol, if any
        /// </summary>
        public Symbol Underlying => underlying;


        /// <summary>
        /// Gets the security type of the symbol
        /// </summary>
        public SecurityType SecurityType => id.SecurityType;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Symbol"/> class
        /// </summary>
        /// <param name="sid">The security identifier for this symbol</param>
        /// <param name="value">The current ticker symbol value</param>
        public Symbol(SecurityIdentifier sid, string value)
            : this(sid, value, null)
        {
        }

        /// <summary>
        /// Creates new symbol with updated mapped symbol. Symbol Mapping: When symbols change over time (e.g. CHASE-> JPM) need to update the symbol requested.
        /// Method returns newly created symbol
        /// </summary>
        public Symbol UpdateMappedSymbol(string mappedSymbol)
        {
            if (id.SecurityType == SecurityType.Option)
            {
                var underlyingSymbol = new Symbol(underlying.id, mappedSymbol, null);

                var alias = value;

                if (id.Date != SecurityIdentifier.DefaultDate)
                {
                    var sym = mappedSymbol;
                    alias = SymbolRepresentation.GenerateOptionTickerOSI(sym, id.OptionRight, id.StrikePrice, id.Date);
                }

                return new Symbol(id, alias, underlyingSymbol);
            }

            return new Symbol(id, mappedSymbol, Underlying);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="strikePrice"></param>
        /// <returns></returns>
        public Symbol UpdateStrikePriceSymbol(decimal strikePrice)
        {
            if (id.SecurityType == SecurityType.Option)
            {
                var underlyingSymbol = new Symbol(underlying.id, value, null);

                var alias = value;

                if (id.Date != SecurityIdentifier.DefaultDate)
                {
                    alias = SymbolRepresentation.GenerateOptionTickerOSI(
                        id.symbol, id.OptionRight, strikePrice, id.Date);
                }

                return new Symbol(id, alias, underlyingSymbol);
            }

            return new Symbol(id, id.symbol, underlying);
        }

        /// <summary>
        /// Private constructor initializes a new instance of the <see cref="Symbol"/> class with underlying
        /// </summary>
        /// <param name="sid">The security identifier for this symbol</param>
        /// <param name="value">The current ticker symbol value</param>
        /// <param name="underlying">The underlying symbol</param>
        internal Symbol(SecurityIdentifier sid, string value, Symbol underlying)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            id = sid;
            this.value = value.LazyToUpper();
            if (sid.SecurityType == SecurityType.Option 
                && Market.IsChinaMarket(sid.Market)
                && string.IsNullOrEmpty(id.contractId)
                && !string.IsNullOrEmpty(value)
                && !value.StartsWith("?"))
            {
                id.contractId = value;
            }

            this.underlying = underlying;
            identify = id.ToString();
            if (id.SecurityType != SecurityType.Base)
            {
                try
                {
                    ExchangeTimeZone = MarketHoursDatabase.FromDataFolder().GetDataTimeZone(sid.Market, this, id.SecurityType);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }
        #endregion

        #region Overrides of Object

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param><filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            // compare strings just as you would a symbol object
            if (obj is string sidString)
            {
                if (SecurityIdentifier.TryParse(sidString, out var sid))
                {
                    return id.Equals(sid);
                }
            }

            // compare a sid just as you would a symbol object
            if (obj is SecurityIdentifier identifier)
            {
                return id.Equals(identifier);
            }

            if (obj.GetType() != GetType())
                return false;
            return Equals((Symbol)obj);
        }

        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            // only SID is used for comparisons
            return id.hashCode;
        }

        /// <summary>
        /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
        /// </summary>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared. The return value has these meanings: Value Meaning Less than zero This instance precedes <paramref name="obj"/> in the sort order. Zero This instance occurs in the same position in the sort order as <paramref name="obj"/>. Greater than zero This instance follows <paramref name="obj"/> in the sort order.
        /// </returns>
        /// <param name="obj">An object to compare with this instance. </param><exception cref="T:System.ArgumentException"><paramref name="obj"/> is not the same type as this instance. </exception><filterpriority>2</filterpriority>
        public int CompareTo(object obj)
        {
            if (obj is string str)
            {
                return string.Compare(Value, str, StringComparison.OrdinalIgnoreCase);
            }
            var sym = obj as Symbol;
            if (sym != null)
            {
                return string.Compare(Value, sym.Value, StringComparison.OrdinalIgnoreCase);
            }

            throw new ArgumentException("Object must be of type Symbol or string.");
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
            return SymbolCache.GetTicker(this);
        }

        #endregion

        #region Equality members

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(Symbol other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            // only SID is used for comparisons
            return id.Equals(other.id);
        }

        /// <summary>
        /// Equals operator
        /// </summary>
        /// <param name="left">The left operand</param>
        /// <param name="right">The right operand</param>
        /// <returns>True if both symbols are equal, otherwise false</returns>
        public static bool operator ==(Symbol left, Symbol right)
        {
            if (ReferenceEquals(left, null) || left.Equals(Empty))
                return ReferenceEquals(right, null) || right.Equals(Empty);
            return left.Equals(right);
        }

        /// <summary>
        /// Not equals operator
        /// </summary>
        /// <param name="left">The left operand</param>
        /// <param name="right">The right operand</param>
        /// <returns>True if both symbols are not equal, otherwise false</returns>
        public static bool operator !=(Symbol left, Symbol right)
        {
            return !(left == right);
        }

        #endregion

        #region Implicit operators

        /// <summary>
        /// Returns the symbol's string ticker
        /// </summary>
        /// <param name="symbol">The symbol</param>
        /// <returns>The string ticker</returns>
        [Obsolete("Symbol implicit operator to string is provided for algorithm use only.")]
        public static implicit operator string(Symbol symbol)
        {
            return symbol.ToString();
        }

        /// <summary>
        /// Creates symbol using string as sid
        /// </summary>
        /// <param name="ticker">The string</param>
        /// <returns>The symbol</returns>
        [Obsolete("Symbol implicit operator from string is provided for algorithm use only.")]
        public static implicit operator Symbol(string ticker)
        {
            if (SymbolCache.TryGetSymbol(ticker, out var symbol))
            {
                return symbol;
            }

            if (SecurityIdentifier.TryParse(ticker, out var sid))
            {
                return new Symbol(sid, sid.Symbol);
            }

            return Empty;
        }

        #endregion

        #region String methods

        // in order to maintain better compile time backwards compatibility,
        // we'll redirect a few common string methods to Value, but mark obsolete
#pragma warning disable 1591
        [Obsolete("Symbol.Contains is a pass-through for Symbol.Value.Contains")]
        public bool Contains(string str) { return value.Contains(str); }
        [Obsolete("Symbol.EndsWith is a pass-through for Symbol.Value.EndsWith")]
        public bool EndsWith(string str) { return value.EndsWith(str); }
        [Obsolete("Symbol.StartsWith is a pass-through for Symbol.Value.StartsWith")]
        public bool StartsWith(string str) { return value.StartsWith(str); }
        [Obsolete("Symbol.ToLower is a pass-through for Symbol.Value.ToLower")]
        public string ToLower() { return value.ToLower(); }
        [Obsolete("Symbol.ToUpper is a pass-through for Symbol.Value.ToUpper")]
        public string ToUpper() { return value; }
#pragma warning restore 1591

        #endregion
    }
}
