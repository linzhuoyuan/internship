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
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using QuantConnect.Configuration;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect
{
    public class SecurityProperties : IEquatable<SecurityProperties>
    {
        private const string DateFormat = "yyyy-MM-ddTHH:mm:ss";

        #region Scales, Widths and Market Maps

        // these values define the structure of the 'otherData'
        // the constant width fields are used via modulus, so the width is the number of zeros specified,
        // {put/call:1}{oa-date:5}{style:1}{strike:6}{strike-scale:2}{market:3}{security-type:2}

        private const ulong SecurityTypeWidth = 100;
        private const ulong SecurityTypeOffset = 1;

        private const ulong MarketWidth = 1000;
        private const ulong MarketOffset = SecurityTypeOffset * SecurityTypeWidth;

        private const int StrikeDefaultScale = 4;
        private static readonly ulong StrikeDefaultScaleExpanded = Pow(10, StrikeDefaultScale);

        private const ulong StrikeScaleWidth = 100;
        private const ulong StrikeScaleOffset = MarketOffset * MarketWidth;

        private const ulong StrikeWidth = 1000000;
        private const ulong StrikeOffset = StrikeScaleOffset * StrikeScaleWidth;

        private const ulong OptionStyleWidth = 10;
        private const ulong OptionStyleOffset = StrikeOffset * StrikeWidth;

        private const ulong DaysWidth = 100000;
        private const ulong DaysOffset = OptionStyleOffset * OptionStyleWidth;

        private const ulong PutCallOffset = DaysOffset * DaysWidth;
        private const ulong PutCallWidth = 10;

        #endregion

        private static ulong Pow(uint x, int pow)
        {
            // don't use Math.Pow(double, double) due to precision issues
            return (ulong)BigInteger.Pow(x, pow);
        }

        /// <summary>
        /// Extracts the embedded value from _otherData
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ExtractFromProperties(ulong offset, ulong width, ulong properties)
        {
            return (properties / offset) % width;
        }

        /// <summary>
        /// Converts an upper case alpha numeric string into a long
        /// </summary>
        private static ulong DecodeBase36(string symbol)
        {
            var result = 0ul;
            var baseValue = 1ul;
            for (var i = symbol.Length - 1; i > -1; i--)
            {
                var c = symbol[i];

                // assumes alpha numeric upper case only strings
                var value = (uint)(c <= 57
                    ? c - '0'
                    : c - 'A' + 10);

                result += baseValue * value;
                baseValue *= 36;
            }

            return result;
        }

        /// <summary>
        /// Converts a long to an uppercase alpha numeric string
        /// </summary>
        private static string EncodeBase36(ulong data)
        {
            var stack = new Stack<char>();
            while (data != 0)
            {
                var value = data % 36;
                var c = value < 10
                    ? (char)(value + '0')
                    : (char)(value - 10 + 'A');

                stack.Push(c);
                data /= 36;
            }
            return new string(stack.ToArray());
        }

        public SecurityProperties(
            SecurityType securityType,
            string market,
            decimal strikePrice,
            OptionStyle optionStyle,
            OptionRight optionRight,
            DateTime? expired)
        {
            SecurityType = securityType;
            Market = market;
            StrikePrice = strikePrice;
            OptionStyle = optionStyle;
            OptionRight = optionRight;
            ExpiredDateTime = expired;
        }

        public SecurityProperties(ulong properties)
        {
            SecurityType = (SecurityType)ExtractFromProperties(SecurityTypeOffset, SecurityTypeWidth, properties);
            var marketCode = ExtractFromProperties(MarketOffset, MarketWidth, properties);
            // if we couldn't find it, send back the numeric representation
            Market = QuantConnect.Market.Decode((int)marketCode) ?? marketCode.ToString();

            if (SecurityType == SecurityType.Option)
            {
                var scale = ExtractFromProperties(StrikeScaleOffset, StrikeScaleWidth, properties);
                var unscaled = ExtractFromProperties(StrikeOffset, StrikeWidth, properties);
                var pow = Math.Pow(10, (int)scale - StrikeDefaultScale);
                StrikePrice = unscaled * (decimal)pow;
                OptionStyle = (OptionStyle)ExtractFromProperties(OptionStyleOffset, OptionStyleWidth, properties);
                OptionRight = (OptionRight)ExtractFromProperties(PutCallOffset, PutCallWidth, properties);
            }

            switch (SecurityType)
            {
                case SecurityType.Equity:
                case SecurityType.Crypto:
                case SecurityType.Option:
                case SecurityType.Future:
                    ExpiredDateTime = DateTime.SpecifyKind(DateTime.FromOADate(ExtractFromProperties(DaysOffset, DaysWidth, properties)), DateTimeKind.Utc);
                    break;
            }
        }

        public readonly SecurityType SecurityType;
        public readonly string Market;
        public decimal StrikePrice;
        public readonly OptionStyle OptionStyle;
        public readonly OptionRight OptionRight;
        public readonly DateTime? ExpiredDateTime;


        public string StringValue
        {
            get
            {
                if (SecurityType == SecurityType.Base && Market == "empty")
                {
                    return string.Empty;
                }

                switch (SecurityType)
                {
                    case SecurityType.Future:
                        return ExpiredDateTime == SecurityIdentifier.PerpetualExpiration
                            ? $"/Perpetual/{Market}"
                            : $"/{SecurityType}/{Market}/{ExpiredDateTime?.ToString(DateFormat)}";
                    case SecurityType.Option:
                        return $"/{SecurityType}/{Market}/{StrikePrice.NormalizeToStr()}/{OptionStyle}/{OptionRight}/{ExpiredDateTime?.ToString(DateFormat)}";
                    default:
                        return $"/{SecurityType}/{Market}";
                }
            }
        }

        public static SecurityProperties Parse(string value)
        {
            if (value.StartsWith("/"))
            {
                var items = value.Split("/");

                var securityType = items[1] == "Perpetual"
                    ? SecurityType.Future
                    : (SecurityType)Enum.Parse(typeof(SecurityType), items[1]);
                var market = items[2];

                var strikePrice = securityType == SecurityType.Option ? items[3].ToDecimal() : 0;

                var optionStyle = OptionStyle.American;
                if (securityType == SecurityType.Option && items[4].Length > 0)
                {
                    optionStyle = (OptionStyle)Enum.Parse(typeof(OptionStyle), items[4]);
                }

                var optionRight = OptionRight.Call;
                if (securityType == SecurityType.Option && items[5].Length > 0)
                {
                    optionRight = (OptionRight)Enum.Parse(typeof(OptionRight), items[5]);
                }

                var expired = DateTime.FromOADate(0);
                if (items[1] == "Perpetual")
                {
                    expired = SecurityIdentifier.PerpetualExpiration;
                }
                else if (securityType is SecurityType.Option or SecurityType.Future)
                {
                    var index = securityType == SecurityType.Future ? 3 : 6;
                    DateTime.TryParseExact(items[index], DateFormat, null, DateTimeStyles.None, out expired);
                }

                return new SecurityProperties(securityType, market, strikePrice, optionStyle, optionRight, expired);
            }

            return new SecurityProperties(DecodeBase36(value));
        }

        public static implicit operator SecurityProperties(ulong properties)
        {
            return new SecurityProperties(properties);
        }

        public bool Equals(SecurityProperties other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return SecurityType == other.SecurityType
                   && Market == other.Market
                   && StrikePrice == other.StrikePrice
                   && OptionStyle == other.OptionStyle
                   && OptionRight == other.OptionRight
                   && Nullable.Equals(ExpiredDateTime, other.ExpiredDateTime);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SecurityProperties)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)SecurityType, Market, StrikePrice, (int)OptionStyle, (int)OptionRight, ExpiredDateTime);
        }

        public static bool operator ==(SecurityProperties lhs, SecurityProperties rhs)
        {
            return lhs!.Equals(rhs);
        }

        public static bool operator !=(SecurityProperties lhs, SecurityProperties rhs)
        {
            return !(lhs == rhs);
        }
    }

    /// <summary>
    /// Defines a unique identifier for securities
    /// </summary>
    /// <remarks>
    /// The SecurityIdentifier contains information about a specific security.
    /// This includes the symbol and other data specific to the SecurityType.
    /// The symbol is limited to 12 characters
    /// </remarks>
    [JsonConverter(typeof(SecurityIdentifierJsonConverter))]
    public class SecurityIdentifier : IEquatable<SecurityIdentifier>
    {
        #region Empty, DefaultDate Fields

        private static readonly ConcurrentDictionary<string, SecurityIdentifier> SecurityIdentifierCache = new();
        private static readonly string MapFileProviderTypeName =
            Config.Get("map-file-provider", "LocalDiskMapFileProvider");
        private static readonly char[] InvalidCharacters = { '|', ' ' };
        private static IMapFileProvider _mapFileProvider;
        private static readonly object MapFileProviderLock = new();

        /// <summary>
        /// Gets an instance of <see cref="SecurityIdentifier"/> that is empty, that is, one with no symbol specified
        /// </summary>
        public static readonly SecurityIdentifier Empty = new(string.Empty, 0, null);

        /// <summary>
        /// Gets the date to be used when it does not apply.
        /// </summary>
        public static readonly DateTime DefaultDate = DateTime.FromOADate(0);

        public static readonly DateTime PerpetualExpiration = new(3000, 1, 1);

        /// <summary>
        /// Gets the set of invalids symbol characters
        /// </summary>
        public static readonly HashSet<char> InvalidSymbolCharacters = new(InvalidCharacters);

        #endregion

        #region Scales, Widths and Market Maps

        // these values define the structure of the 'otherData'
        // the constant width fields are used via modulus, so the width is the number of zeros specified,
        // {put/call:1}{oa-date:5}{style:1}{strike:6}{strike-scale:2}{market:3}{security-type:2}

        private const ulong SecurityTypeWidth = 100;

        #endregion

        #region Member variables
        private readonly SidBox _underlying;
        internal readonly SecurityProperties properties;
        internal string contractId;
        internal string stringValue;
        internal readonly int hashCode;
        internal readonly string symbol;
        //internal string market;
        //internal SecurityType securityType;
        //internal decimal strikePrice;
        //internal OptionStyle optionStyle;
        //internal OptionRight optionRight;
        #endregion

        #region Properties

        /// <summary>
        /// Gets whether or not this <see cref="SecurityIdentifier"/> is a derivative,
        /// that is, it has a valid <see cref="Underlying"/> property
        /// </summary>
        public string ContractId => contractId;

        public bool HasUnderlying => _underlying != null;

        /// <summary>
        /// Gets the underlying security identifier for this security identifier. When there is
        /// no underlying, this property will return a value of <see cref="Empty"/>.
        /// </summary>
        public SecurityIdentifier Underlying
        {
            get
            {
                if (_underlying == null)
                {
                    throw new InvalidOperationException("No underlying specified for this identifier. Check that HasUnderlying is true before accessing the Underlying property.");
                }
                return _underlying.SecurityIdentifier;
            }
        }

        /// <summary>
        /// Gets the date component of this identifier. For equities this
        /// is the first date the security traded. Technically speaking,
        /// in LEAN, this is the first date mentioned in the map_files.
        /// For options this is the expiry date. For futures this is the
        /// settlement date. For forex and cfds this property will throw an
        /// exception as the field is not specified.
        /// </summary>
        public DateTime Date
        {
            get
            {
                if (properties.ExpiredDateTime != null)
                {
                    return properties.ExpiredDateTime.Value;
                }
                throw new InvalidOperationException("Date is only defined for SecurityType.Equity, SecurityType.Option and SecurityType.Future");
            }
        }

        /// <summary>
        /// Gets the original symbol used to generate this security identifier.
        /// For equities, by convention this is the first ticker symbol for which
        /// the security traded
        /// </summary>
        public string Symbol => symbol;

        /// <summary>
        /// Gets the market component of this security identifier. If located in the
        /// internal mappings, the full string is returned. If the value is unknown,
        /// the integer value is returned as a string.
        /// </summary>
        public string Market => properties.Market;

        /// <summary>
        /// Gets the security type component of this security identifier.
        /// </summary>
        public SecurityType SecurityType => properties.SecurityType;

        /// <summary>
        /// Gets the option strike price. This only applies to SecurityType.Option
        /// and will thrown an exception if accessed otherwise.
        /// </summary>
        public decimal StrikePrice
        {
            get
            {
                if (properties.SecurityType != SecurityType.Option)
                {
                    throw new InvalidOperationException("OptionType is only defined for SecurityType.Option");
                }
                return properties.StrikePrice;
            }
            set
            {
                properties.StrikePrice = value;
                stringValue = GetStringValue();
            }
        }

        /// <summary>
        /// Gets the option type component of this security identifier. This
        /// only applies to SecurityType.Open and will throw an exception if
        /// accessed otherwise.
        /// </summary>
        public OptionRight OptionRight
        {
            get
            {
                if (properties.SecurityType != SecurityType.Option)
                {
                    throw new InvalidOperationException("OptionRight is only defined for SecurityType.Option");
                }

                return properties.OptionRight;
            }
        }

        /// <summary>
        /// Gets the option style component of this security identifier. This
        /// only applies to SecurityType.Open and will throw an exception if
        /// accessed otherwise.
        /// </summary>
        public OptionStyle OptionStyle
        {
            get
            {
                if (properties.SecurityType != SecurityType.Option)
                {
                    throw new InvalidOperationException("OptionStyle is only defined for SecurityType.Option");
                }

                return properties.OptionStyle;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityIdentifier"/> class
        /// </summary>
        /// <param name="symbol">The base36 string encoded as a long using alpha [0-9A-Z]</param>
        /// <param name="properties">Other data defining properties of the symbol including market,
        /// security type, listing or expiry date, strike/call/put/style for options, ect...</param>
        /// <param name="contractId"></param>
        public SecurityIdentifier(string symbol, SecurityProperties properties, string contractId)
        {
            if (symbol == null)
            {
                throw new ArgumentNullException(nameof(symbol), "SecurityIdentifier requires a non-null string 'symbol'");
            }
            if (symbol.IndexOfAny(InvalidCharacters) != -1)
            {
                throw new ArgumentException("symbol must not contain the characters '|' or ' '.", nameof(symbol));
            }
            this.symbol = symbol;
            this.properties = properties;
            this.contractId = contractId;
            _underlying = null;

            //if (this.contractId != null)
            //{
            //    hashCode = unchecked(symbol.GetHashCode() * 397) ^ this.contractId.GetHashCode();
            //}
            //else
            //{
            //    hashCode = unchecked(symbol.GetHashCode() * 397) ^ this.properties.GetHashCode();
            //}
            hashCode = unchecked(symbol.GetHashCode() * 397) ^ this.properties.GetHashCode();
            stringValue = GetStringValue();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityIdentifier"/> class
        /// </summary>
        /// <param name="symbol">The base36 string encoded as a long using alpha [0-9A-Z]</param>
        /// <param name="properties">Other data defining properties of the symbol including market,
        /// security type, listing or expiry date, strike/call/put/style for options, ect...</param>
        /// <param name="underlying">Specifies a <see cref="SecurityIdentifier"/> that represents the underlying security</param>
        /// <param name="contractId"></param>
        public SecurityIdentifier(
            string symbol,
            SecurityProperties properties,
            SecurityIdentifier underlying,
            string contractId)
            : this(symbol, properties, contractId)
        {
            this.symbol = symbol ?? throw new ArgumentNullException(
                nameof(symbol), "SecurityIdentifier requires a non-null string 'symbol'");
            // performance: directly call Equals(SecurityIdentifier other), shortcuts Equals(object other)
            if (!underlying.Equals(Empty))
            {
                _underlying = new SidBox(underlying);
            }
            stringValue = GetStringValue();
        }

        #endregion

        #region AddMarket, GetMarketCode, and Generate

        /// <summary>
        /// Generates a new <see cref="SecurityIdentifier"/> for an option
        /// </summary>
        /// <param name="expiry">The date the option expires</param>
        /// <param name="underlying">The underlying security's symbol</param>
        /// <param name="market">The market</param>
        /// <param name="strike">The strike price</param>
        /// <param name="optionRight">The option type, call or put</param>
        /// <param name="optionStyle">The option style, American or European</param>
        /// <param name="contractId">The option unique ID</param>
        /// <returns>A new <see cref="SecurityIdentifier"/> representing the specified option security</returns>
        public static SecurityIdentifier GenerateOption(
            DateTime expiry,
            SecurityIdentifier underlying,
            string market,
            decimal strike,
            OptionRight optionRight,
            OptionStyle optionStyle,
            string contractId = null)
        {
            return Generate(
                expiry,
                underlying.Symbol,
                SecurityType.Option,
                market,
                strike,
                optionRight,
                optionStyle,
                underlying,
                contractId);
        }

        /// <summary>
        /// Generates a new <see cref="SecurityIdentifier"/> for a future
        /// </summary>
        /// <param name="expiry">The date the future expires</param>
        /// <param name="symbol">The security's symbol</param>
        /// <param name="market">The market</param>
        /// <returns>A new <see cref="SecurityIdentifier"/> representing the specified futures security</returns>
        public static SecurityIdentifier GenerateFuture(
            DateTime expiry,
            string symbol,
            string market)
        {
            return Generate(expiry, symbol, SecurityType.Future, market);
        }

        /// <summary>
        /// Helper overload that will search the map files to resolve the first date. This implementation
        /// uses the configured <see cref="IMapFileProvider"/> via the <see cref="Composer.Instance"/>
        /// </summary>
        /// <param name="symbol">The symbol as it is known today</param>
        /// <param name="market">The market</param>
        /// <param name="mapSymbol">Specifies if symbol should be mapped using map file provider</param>
        /// <param name="mapFileProvider">Specifies the IMapFileProvider to use for resolving symbols, specify null to load from Composer</param>
        /// <returns>A new <see cref="SecurityIdentifier"/> representing the specified symbol today</returns>
        public static SecurityIdentifier GenerateEquity(
            string symbol,
            string market,
            bool mapSymbol = true,
            IMapFileProvider mapFileProvider = null)
        {
            if (mapSymbol)
            {
                MapFile mapFile;
                if (mapFileProvider == null)
                {
                    lock (MapFileProviderLock)
                    {
                        if (_mapFileProvider == null)
                        {
                            _mapFileProvider = Composer.Instance.GetExportedValueByTypeName<IMapFileProvider>(MapFileProviderTypeName);
                        }

                        mapFile = GetMapFile(_mapFileProvider, market, symbol);
                    }
                }
                else
                {
                    mapFile = GetMapFile(mapFileProvider, market, symbol);
                }

                var firstDate = mapFile.FirstDate;
                if (mapFile.Any())
                {
                    symbol = mapFile.FirstTicker;
                }

                return GenerateEquity(firstDate, symbol, market);
            }
            else
            {
                return GenerateEquity(DefaultDate, symbol, market);
            }

        }

        public static MapFile GetMapFile(IMapFileProvider mapFileProvider, string market, string symbol)
        {
            var resolver = mapFileProvider.Get(market);
            var mapFile = resolver.ResolveMapFile(symbol, DateTime.Today);
            return mapFile;
        }

        /// <summary>
        /// Generates a new <see cref="SecurityIdentifier"/> for an equity
        /// </summary>
        /// <param name="date">The first date this security traded (in LEAN this is the first date in the map_file</param>
        /// <param name="symbol">The ticker symbol this security traded under on the <paramref name="date"/></param>
        /// <param name="market">The security's market</param>
        /// <returns>A new <see cref="SecurityIdentifier"/> representing the specified equity security</returns>
        public static SecurityIdentifier GenerateEquity(DateTime date, string symbol, string market)
        {
            return Generate(date, symbol, SecurityType.Equity, market);
        }

        /// <summary>
        /// Generates a new <see cref="SecurityIdentifier"/> for a custom security
        /// </summary>
        /// <param name="symbol">The ticker symbol of this security</param>
        /// <param name="market">The security's market</param>
        /// <returns>A new <see cref="SecurityIdentifier"/> representing the specified base security</returns>
        public static SecurityIdentifier GenerateBase(string symbol, string market)
        {
            return Generate(DefaultDate, symbol, SecurityType.Base, market);
        }

        /// <summary>
        /// Generates a new <see cref="SecurityIdentifier"/> for a forex pair
        /// </summary>
        /// <param name="symbol">The currency pair in the format similar to: 'EURUSD'</param>
        /// <param name="market">The security's market</param>
        /// <returns>A new <see cref="SecurityIdentifier"/> representing the specified forex pair</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SecurityIdentifier GenerateForex(string symbol, string market)
        {
            return Generate(DefaultDate, symbol, SecurityType.Forex, market);
        }

        /// <summary>
        /// Generates a new <see cref="SecurityIdentifier"/> for a Crypto pair
        /// </summary>
        /// <param name="symbol">The currency pair in the format similar to: 'EURUSD'</param>
        /// <param name="market">The security's market</param>
        /// <returns>A new <see cref="SecurityIdentifier"/> representing the specified Crypto pair</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SecurityIdentifier GenerateCrypto(string symbol, string market)
        {
            return Generate(DefaultDate, symbol, SecurityType.Crypto, market);
        }

        /// <summary>
        /// Generates a new <see cref="SecurityIdentifier"/> for a CFD security
        /// </summary>
        /// <param name="symbol">The CFD contract symbol</param>
        /// <param name="market">The security's market</param>
        /// <returns>A new <see cref="SecurityIdentifier"/> representing the specified CFD security</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SecurityIdentifier GenerateCfd(string symbol, string market)
        {
            return Generate(DefaultDate, symbol, SecurityType.Cfd, market);
        }

        /// <summary>
        /// Generic generate method. This method should be used carefully as some parameters are not required and
        /// some parameters mean different things for different security types
        /// </summary>
        private static SecurityIdentifier Generate(
            DateTime date,
            string symbol,
            SecurityType securityType,
            string market,
            decimal strike = 0,
            OptionRight optionRight = 0,
            OptionStyle optionStyle = 0,
            SecurityIdentifier underlying = null,
            string contractId = null)
        {
            if ((ulong)securityType >= SecurityTypeWidth || securityType < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(securityType), "securityType must be between 0 and 99");
            }
            if ((int)optionRight > 1 || optionRight < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(optionRight), "optionType must be either 0 or 1");
            }

            // normalize input strings
            market = market.ToLower();
            symbol = symbol.LazyToUpper();

            var marketIdentifier = QuantConnect.Market.Encode(market);
            if (!marketIdentifier.HasValue)
            {
                throw new ArgumentOutOfRangeException(nameof(market),
                    $"The specified market wasn't found in the markets lookup. Requested: {market}. " +
                    "You can add markets by calling QuantConnect.Market.AddMarket(string,ushort)");
            }

            var properties = new SecurityProperties(securityType, market, strike, optionStyle, optionRight, date);
            return new SecurityIdentifier(symbol, properties, underlying ?? Empty, contractId);
        }

        #endregion

        #region Parsing routines

        /// <summary>
        /// Parses the specified string into a <see cref="SecurityIdentifier"/>
        /// The string must be a 40 digit number. The first 20 digits must be parseable
        /// to a 64 bit unsigned integer and contain ancillary data about the security.
        /// The second 20 digits must also be parseable as a 64 bit unsigned integer and
        /// contain the symbol encoded from base36, this provides for 12 alpha numeric case
        /// insensitive characters.
        /// </summary>
        /// <param name="value">The string value to be parsed</param>
        /// <returns>A new <see cref="SecurityIdentifier"/> instance if the <paramref name="value"/> is able to be parsed.</returns>
        /// <exception cref="FormatException">This exception is thrown if the string's length is not exactly 40 characters, or
        /// if the components are unable to be parsed as 64 bit unsigned integers</exception>
        public static SecurityIdentifier Parse(string value)
        {
            if (!TryParse(value, out var identifier, out var exception))
            {
                throw exception;
            }

            return identifier;
        }

        /// <summary>
        /// Attempts to parse the specified <see paramref="value"/> as a <see cref="SecurityIdentifier"/>.
        /// </summary>
        /// <param name="value">The string value to be parsed</param>
        /// <param name="identifier">The result of parsing, when this function returns true, <paramref name="identifier"/>
        /// was properly created and reflects the input string, when this function returns false <paramref name="identifier"/>
        /// will equal default(SecurityIdentifier)</param>
        /// <returns>True on success, otherwise false</returns>
        public static bool TryParse(string value, out SecurityIdentifier identifier)
        {
            return TryParse(value, out identifier, out _);
        }

        /// <summary>
        /// Helper method impl to be used by parse and try parse
        /// </summary>
        private static bool TryParse(string value, out SecurityIdentifier identifier, out Exception exception)
        {
            return TryParseProperties(value, out exception, out identifier);
        }

        private static readonly char[] SplitSpace = { ' ' };

        /// <summary>
        /// Parses the string into its component ulong pieces
        /// </summary>
        private static bool TryParseProperties(string value, out Exception exception, out SecurityIdentifier identifier)
        {
            exception = null;

            if (string.IsNullOrWhiteSpace(value))
            {
                identifier = Empty;
                return true;
            }

            // for performance, we first verify if we already have parsed this SecurityIdentifier
            if (SecurityIdentifierCache.TryGetValue(value, out identifier))
            {
                return true;
            }
            // after calling TryGetValue because if it failed it will set identifier to default
            identifier = Empty;

            try
            {
                var sids = value.Split('|');
                for (var i = sids.Length - 1; i > -1; i--)
                {
                    var current = sids[i];
                    var parts = current.Split(SplitSpace, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                    {
                        exception = new FormatException("The string must be splittable on space into two parts.");
                        return false;
                    }

                    var symbol = parts[0];
                    var properties = parts[1];
                    if (!properties.StartsWith("/"))
                    {
                        exception = new FormatException("Invalid symbol's properties value.");
                        return false;
                    }

                    // toss the previous in as the underlying, if Empty, ignored by ctor
                    identifier = new SecurityIdentifier(symbol, SecurityProperties.Parse(properties), identifier, null);
                }
            }
            catch (Exception error)
            {
                exception = error;
                Log.Error("SecurityIdentifier.TryParseProperties(): Error parsing SecurityIdentifier: '{0}', Exception: {1}", value, exception);
                return false;
            }

            SecurityIdentifierCache.TryAdd(value, identifier);
            return true;
        }

        #endregion

        #region Equality members and ToString

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public bool Equals(SecurityIdentifier other)
        {
            //if (QuantConnect.Market.IsMarketCompare(market) && QuantConnect.Market.IsMarketCompare(other.market) && securityType == SecurityType.Option && other.securityType == SecurityType.Option)
            //{
            //    return _contractId == other._contractId
            //   && symbol == other.symbol
            //   && market == other.market
            //   && _underlying == other._underlying;
            //}
            //return _properties == other._properties
            //    && symbol == other.symbol
            //    && _underlying == other._underlying;

            return other != null
                   && contractId == other.contractId
                   && properties == other.properties
                   && _underlying == other._underlying
                   && symbol == other.symbol;
        }

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
            if (obj.GetType() != GetType()) return false;
            return Equals((SecurityIdentifier)obj);
        }

        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode() => hashCode;

        /// <summary>
        /// Override equals operator
        /// </summary>
        public static bool operator ==(SecurityIdentifier left, SecurityIdentifier right)
        {
            return Equals(left, right);
        }

        /// <summary>
        /// Override not equals operator
        /// </summary>
        public static bool operator !=(SecurityIdentifier left, SecurityIdentifier right)
        {
            return !Equals(left, right);
        }

        private string GetStringValue()
        {
            var props = properties.StringValue;
            if (_underlying != null)
            {
                return symbol + ' ' + props + '|' + _underlying.SecurityIdentifier;
            }
            return symbol + ' ' + props;
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
            return stringValue;
        }

        #endregion

        /// <summary>
        /// Provides a reference type container for a security identifier instance.
        /// This is used to maintain a reference to an underlying
        /// </summary>
        private sealed class SidBox : IEquatable<SidBox>
        {
            public readonly SecurityIdentifier SecurityIdentifier;
            public SidBox(SecurityIdentifier securityIdentifier)
            {
                SecurityIdentifier = securityIdentifier;
            }
            public bool Equals(SidBox other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return SecurityIdentifier.Equals(other.SecurityIdentifier);
            }
            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is SidBox sidBox && Equals(sidBox);
            }
            public override int GetHashCode()
            {
                return SecurityIdentifier.GetHashCode();
            }
            public static bool operator ==(SidBox left, SidBox right)
            {
                return Equals(left, right);
            }
            public static bool operator !=(SidBox left, SidBox right)
            {
                return !Equals(left, right);
            }
        }
    }
}
