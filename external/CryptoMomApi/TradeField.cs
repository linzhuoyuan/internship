using System;
using System.Runtime.Serialization;
using System.Threading;
using ProtoBuf;

namespace MomCrypto.Api
{
    [DataContract]
    [ProtoInclude(50, typeof(MomTrade))]
    public abstract class TradeField
    {
        internal DateTime timestamp;

        [DataMember(Order = 1)]
        public long FundId
        {
            get => fundId;
            set => fundId = value;
        }
        internal long fundId;

        [DataMember(Order = 2)]
        public string FundAccountId
        {
            get => fundAccountId;
            set => fundAccountId = value;
        }
        internal string fundAccountId;

        [DataMember(Order = 3)]
        public string UserId
        {
            get => userId;
            set => userId = value;
        }
        internal string userId;

        [DataMember(Order = 4)]
        public string AccountId
        {
            get => accountId;
            set => accountId = value;
        }
        internal string accountId;

        [DataMember(Order = 5)]
        public long StrategyInputId
        {
            get => strategyInputId;
            set => strategyInputId = value;
        }
        internal long strategyInputId;

        [DataMember(Order = 6)]
        public long TradeLocalId
        {
            get => tradeLocalId;
            set => tradeLocalId = value;
        }
        internal long tradeLocalId;

        [DataMember(Order = 7)]
        public long InputLocalId
        {
            get => inputLocalId;
            set => inputLocalId = value;
        }
        internal long inputLocalId;

        [DataMember(Order = 8)]
        public long OrderLocalId
        {
            get => orderLocalId;
            set => orderLocalId = value;
        }
        internal long orderLocalId;

        [DataMember(Order = 9)]
        public long OrderRef
        {
            get => orderRef;
            set => orderRef = value;
        }
        internal long orderRef;

        [DataMember(Order = 10)]
        public string InstrumentId
        {
            get => instrumentId;
            set => instrumentId = value;
        }
        internal string instrumentId;

        [DataMember(Order = 11)]
        public string ExchangeSymbol
        {
            get => exchangeSymbol;
            set => exchangeSymbol = value;
        }
        internal string exchangeSymbol;

        [DataMember(Order = 12)]
        public byte ProductClass
        {
            get => productClass;
            set => productClass = value;
        }
        internal byte productClass;

        [DataMember(Order = 13)]
        public string ExchangeId
        {
            get => exchangeId;
            set => exchangeId = value;
        }
        internal string exchangeId;

        [DataMember(Order = 14)]
        public string TradeId
        {
            get => tradeId;
            set => tradeId = value;
        }
        internal string tradeId;

        [DataMember(Order = 15)]
        public byte Direction
        {
            get => direction;
            set => direction = value;
        }
        internal byte direction;

        [DataMember(Order = 16)]
        public string OrderSysId
        {
            get => orderSysId;
            set => orderSysId = value;
        }
        internal string orderSysId;

        [DataMember(Order = 17)]
        public byte TradingRole
        {
            get => tradingRole;
            set => tradingRole = value;
        }
        internal byte tradingRole;

        [DataMember(Order = 18)]
        public decimal Price
        {
            get => price;
            set => price = value;
        }
        internal decimal price;

        [DataMember(Order = 19)]
        public decimal Volume
        {
            get => volume;
            set => volume = value;
        }
        internal decimal volume;

        [DataMember(Order = 20)]
        public decimal Margin
        {
            get => margin;
            set => margin = value;
        }
        internal decimal margin;

        [DataMember(Order = 21)]
        public decimal Commission
        {
            get => commission;
            set => commission = value;
        }
        internal decimal commission;

        [DataMember(Order = 22)]
        public string CommissionAsset
        {
            get => commissionAsset;
            set => commissionAsset = value;
        }
        internal string commissionAsset;

        [DataMember(Order = 23)]
        public string TradeDate
        {
            get => tradeDate;
            set => tradeDate = value;
        }
        internal string tradeDate;

        [DataMember(Order = 24)]
        public string TradeTime
        {
            get => tradeTime;
            set => tradeTime = value;
        }
        internal string tradeTime;

        [DataMember(Order = 25)]
        public byte TradeType
        {
            get => tradeType;
            set => tradeType = value;
        }
        internal byte tradeType = MomTradeTypeType.Common;

        [DataMember(Order = 26)]
        public byte PriceSource
        {
            get => priceSource;
            set => priceSource = value;
        }
        internal byte priceSource;

        [DataMember(Order = 27)]
        public byte TradeSource
        {
            get => tradeSource;
            set => tradeSource = value;
        }
        internal byte tradeSource = MomTradeSourceType.Exchange;

        [DataMember(Order = 28)]
        public decimal BuyVolume
        {
            get => buyVolume;
            set => buyVolume = value;
        }
        internal decimal buyVolume;

        [DataMember(Order = 29)]
        public decimal SellVolume
        {
            get => sellVolume;
            set => sellVolume = value;
        }
        internal decimal sellVolume;

        [DataMember(Order = 30)]
        public bool IsTaker
        {
            get => isTaker;
            set => isTaker = value;
        }
        internal bool isTaker;

        [DataMember(Order = 31)]
        public decimal Amount
        {
            get => amount;
            set => amount = value;
        }
        internal decimal amount;

        [DataMember(Order = 32)]
        public int Version
        {
            get => version;
            set => version = value;
        }
        internal int version;

        public void UpdateVersion()
        {
            Interlocked.Increment(ref version);
        }
    }
}
