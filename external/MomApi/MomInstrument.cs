using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomInstrument
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
        internal string symbol = string.Empty;

        [DataMember(Order = 2)]
        public string Exchange
        {
            get => exchange;
            set => exchange = value;
        }
        internal string exchange = string.Empty;

        [DataMember(Order = 3)]
        public string Market
        {
            get => market;
            set => market = value;
        }
        internal string market = string.Empty;

        [DataMember(Order = 4)]
        public string InstrumentName
        {
            get => instrumentName;
            set => instrumentName = value;
        }
        internal string instrumentName = string.Empty;

        [DataMember(Order = 5)]
        public string ExchangeSymbol
        {
            get => exchangeSymbol;
            set => exchangeSymbol = value;
        }
        internal string exchangeSymbol = string.Empty;

        [DataMember(Order = 6)]
        public string ProductId
        {
            get => productId;
            set => productId = value;
        }
        internal string productId = string.Empty;

        [DataMember(Order = 7)]
        public byte ProductClass
        {
            get => productClass;
            set => productClass = value;
        }
        internal byte productClass;

        [DataMember(Order = 8)]
        public int DeliveryYear
        {
            get => deliveryYear;
            set => deliveryYear = value;
        }
        internal int deliveryYear;

        [DataMember(Order = 9)]
        public int DeliveryMonth
        {
            get => deliveryMonth;
            set => deliveryMonth = value;
        }
        internal int deliveryMonth;

        [DataMember(Order = 10)]
        public int MaxMarketOrderVolume
        {
            get => maxMarketOrderVolume;
            set => maxMarketOrderVolume = value;
        }
        internal int maxMarketOrderVolume;

        [DataMember(Order = 11)]
        public int MinMarketOrderVolume
        {
            get => minMarketOrderVolume;
            set => minMarketOrderVolume = value;
        }
        internal int minMarketOrderVolume;

        [DataMember(Order = 12)]
        public int MaxLimitOrderVolume
        {
            get => maxLimitOrderVolume;
            set => maxLimitOrderVolume = value;
        }
        internal int maxLimitOrderVolume;

        [DataMember(Order = 13)]
        public int MinLimitOrderVolume
        {
            get => minLimitOrderVolume;
            set => minLimitOrderVolume = value;
        }
        internal int minLimitOrderVolume;

        [DataMember(Order = 14)]
        public int VolumeMultiple
        {
            get => volumeMultiple;
            set => volumeMultiple = value;
        }
        internal int volumeMultiple;

        [DataMember(Order = 15)]
        public decimal PriceTick
        {
            get => priceTick;
            set => priceTick = value;
        }
        internal decimal priceTick;

        [DataMember(Order = 16)]
        public string CreateDate
        {
            get => createDate;
            set => createDate = value;
        }
        internal string createDate = string.Empty;

        [DataMember(Order = 17)]
        public string OpenDate
        {
            get => openDate;
            set => openDate = value;
        }
        internal string openDate = string.Empty;

        [DataMember(Order = 18)]
        public string ExpireDate
        {
            get => expireDate;
            set => expireDate = value;
        }
        internal string expireDate = string.Empty;

        [DataMember(Order = 19)]
        public string StartDeliveryDate
        {
            get => startDeliveryDate;
            set => startDeliveryDate = value;
        }
        internal string startDeliveryDate = string.Empty;

        [DataMember(Order = 20)]
        public string EndDeliveryDate
        {
            get => endDeliveryDate;
            set => endDeliveryDate = value;
        }
        internal string endDeliveryDate = string.Empty;

        [DataMember(Order = 21)]
        public byte InstLifePhase
        {
            get => instLifePhase;
            set => instLifePhase = value;
        }
        internal byte instLifePhase;

        [DataMember(Order = 22)]
        public int TradingRules
        {
            get => tradingRules;
            set => tradingRules = value;
        }
        internal int tradingRules;

        [DataMember(Order = 23)]
        public byte PositionType
        {
            get => positionType;
            set => positionType = value;
        }
        internal byte positionType;

        [DataMember(Order = 24)]
        public string UnderlyingSymbol
        {
            get => underlyingSymbol;
            set => underlyingSymbol = value;
        }
        internal string underlyingSymbol = string.Empty;

        [DataMember(Order = 25)]
        public decimal StrikePrice
        {
            get => strikePrice;
            set => strikePrice = value;
        }
        internal decimal strikePrice;

        [DataMember(Order = 26)]
        public byte OptionsType
        {
            get => optionsType;
            set => optionsType = value;
        }
        internal byte optionsType;

        [DataMember(Order = 27)]
        public decimal UnderlyingMultiple
        {
            get => underlyingMultiple;
            set => underlyingMultiple = value;
        }
        internal decimal underlyingMultiple;

        [DataMember(Order = 28)]
        public string? QuoteCurrency
        {
            get => quoteCurrency;
            set => quoteCurrency = value;
        }
        internal string? quoteCurrency;

        [DataMember(Order = 29)]
        public string? BaseCurrency
        {
            get => baseCurrency;
            set => baseCurrency = value;
        }
        internal string? baseCurrency;

        [DataMember(Order = 30)]
        public string? PrimaryExchange
        {
            get => primaryExchange;
            set => primaryExchange = value;
        }
        internal string? primaryExchange;

        [DataMember(Order = 31)]
        public string? TradingClass
        {
            get => tradingClass;
            set => tradingClass = value;
        }
        internal string? tradingClass;

        [DataMember(Order = 32)]
        public decimal MarketPrice
        {
            get => marketPrice;
            set => marketPrice = value;
        }
        internal decimal marketPrice;

        public override string ToString()
        {
            return $"{InstrumentName},{ExchangeSymbol}";
        }

        public MomInstrument Clone()
        {
            return (MomInstrument)MemberwiseClone();
        }
    }
}
