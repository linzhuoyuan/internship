using System;
using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public sealed class MomInstrument : IEquatable<MomInstrument>
    {
        internal int index;
        internal int underlyingIndex;
        internal int productClassIndex;

        [DataMember(Order = 1)]
        public string Symbol
        {
            get => symbol;
            set => symbol = value;
        }
        internal string symbol = null!;

        [DataMember(Order = 2)]
        public string Exchange
        {
            get => exchange;
            set => exchange = value;
        }
        internal string exchange = null!;

        [DataMember(Order = 3)]
        public string Market
        {
            get => market;
            set => market = value;
        }
        internal string market = null!;

        [DataMember(Order = 4)]
        public string InstrumentName
        {
            get => instrumentName;
            set => instrumentName = value;
        }
        internal string instrumentName = null!;

        [DataMember(Order = 5)]
        public string ExchangeSymbol
        {
            get => exchangeSymbol;
            set => exchangeSymbol = value;
        }
        internal string exchangeSymbol = null!;

        [DataMember(Order = 6)]
        public byte ProductClass
        {
            get => productClass;
            set => productClass = value;
        }
        internal byte productClass;

        [DataMember(Order = 7)]
        public int DeliveryYear
        {
            get => deliveryYear;
            set => deliveryYear = value;
        }
        internal int deliveryYear;

        [DataMember(Order = 8)]
        public int DeliveryMonth
        {
            get => deliveryMonth;
            set => deliveryMonth = value;
        }
        internal int deliveryMonth;

        [DataMember(Order = 9)]
        public decimal MaxMarketOrderVolume
        {
            get => maxMarketOrderVolume;
            set => maxMarketOrderVolume = value;
        }
        internal decimal maxMarketOrderVolume;

        [DataMember(Order = 10)]
        public decimal MinMarketOrderVolume
        {
            get => minMarketOrderVolume;
            set => minMarketOrderVolume = value;
        }
        internal decimal minMarketOrderVolume;

        [DataMember(Order = 11)]
        public decimal MaxLimitOrderVolume
        {
            get => maxLimitOrderVolume;
            set => maxLimitOrderVolume = value;
        }
        internal decimal maxLimitOrderVolume;

        [DataMember(Order = 12)]
        public decimal MinLimitOrderVolume
        {
            get => minLimitOrderVolume;
            set => minLimitOrderVolume = value;
        }
        internal decimal minLimitOrderVolume;

        [DataMember(Order = 13)]
        public decimal VolumeMultiple
        {
            get => volumeMultiple;
            set => volumeMultiple = value;
        }
        internal decimal volumeMultiple;

        [DataMember(Order = 14)]
        public decimal PriceTick
        {
            get => priceTick;
            set => priceTick = value;
        }
        internal decimal priceTick;

        [DataMember(Order = 15)]
        public string? CreateDate
        {
            get => createDate;
            set => createDate = value;
        }
        internal string? createDate;

        [DataMember(Order = 16)]
        public string? OpenDate
        {
            get => openDate;
            set => openDate = value;
        }
        internal string? openDate;

        [DataMember(Order = 17)]
        public string? ExpireDate
        {
            get => expireDate;
            set => expireDate = value;
        }
        internal string? expireDate;

        [DataMember(Order = 18)]
        public string? StartDeliveryDate
        {
            get => startDeliveryDate;
            set => startDeliveryDate = value;
        }
        internal string? startDeliveryDate;

        [DataMember(Order = 19)]
        public string? EndDeliveryDate
        {
            get => endDeliveryDate;
            set => endDeliveryDate = value;
        }
        internal string? endDeliveryDate;

        [DataMember(Order = 20)]
        public byte InstLifePhase
        {
            get => instLifePhase;
            set => instLifePhase = value;
        }
        internal byte instLifePhase;

        [DataMember(Order = 21)]
        public int TradingRules
        {
            get => tradingRules;
            set => tradingRules = value;
        }
        internal int tradingRules;

        [DataMember(Order = 22)]
        public byte PositionType
        {
            get => positionType;
            set => positionType = value;
        }
        internal byte positionType;

        [DataMember(Order = 23)]
        public string? UnderlyingSymbol
        {
            get => underlyingSymbol;
            set => underlyingSymbol = value;
        }
        internal string? underlyingSymbol;

        [DataMember(Order = 24)]
        public decimal StrikePrice
        {
            get => strikePrice;
            set => strikePrice = value;
        }
        internal decimal strikePrice;

        [DataMember(Order = 25)]
        public byte OptionsType
        {
            get => optionsType;
            set => optionsType = value;
        }
        internal byte optionsType;

        [DataMember(Order = 26)]
        public decimal UnderlyingMultiple
        {
            get => underlyingMultiple;
            set => underlyingMultiple = value;
        }
        internal decimal underlyingMultiple;

        [DataMember(Order = 27)]
        public string? QuoteCurrency
        {
            get => quoteCurrency;
            set => quoteCurrency = value;
        }
        internal string? quoteCurrency;

        [DataMember(Order = 28)]
        public string? BaseCurrency
        {
            get => baseCurrency;
            set => baseCurrency = value;
        }
        internal string? baseCurrency;

        [DataMember(Order = 29)]
        public string? TradingClass
        {
            get => tradingClass;
            set => tradingClass = value;
        }
        internal string? tradingClass;

        [DataMember(Order = 30)]
        public string? PrimaryExch
        {
            get => primaryExch;
            set => primaryExch = value;
        }
        internal string? primaryExch;

        public override string ToString()
        {
            return $"{symbol},{baseCurrency},{market}";
        }

        public MomInstrument Clone()
        {
            return (MomInstrument)MemberwiseClone();
        }

        public bool Equals(MomInstrument? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return symbol == other.symbol;
        }

        public override bool Equals(object? obj)
        {
            return ReferenceEquals(this, obj) || obj is MomInstrument other && Equals(other);
        }

        public override int GetHashCode()
        {
            return symbol.GetHashCode();
        }
    }
}
