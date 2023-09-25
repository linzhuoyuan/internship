using System.Runtime.Serialization;
using System.Threading;
using ProtoBuf;

namespace Quantmom.Api
{
    [DataContract]
    [ProtoInclude(50, typeof(MomInputOrder))]
    [ProtoInclude(100, typeof(MomFundInputOrder))]
    public abstract class InputOrderField
    {
        internal int index = -1;
        internal int channelIndex = -1;

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
        internal string fundAccountId = string.Empty;

        [DataMember(Order = 3)]
        public string FundChannelType
        {
            get => fundChannelType;
            set => fundChannelType = value;
        }
        internal string fundChannelType = string.Empty;

        [DataMember(Order = 4)]
        public string UserId
        {
            get => userId;
            set => userId = value;
        }
        internal string userId = string.Empty;

        [DataMember(Order = 5)]
        public string? AccountId
        {
            get => accountId;
            set => accountId = value;
        }
        internal string? accountId;

        [DataMember(Order = 6)]
        public long OrderRef
        {
            get => orderRef;
            set => orderRef = value;
        }
        internal long orderRef;

        [DataMember(Order = 7)]
        public long InputLocalId
        {
            get => inputLocalId;
            set => inputLocalId = value;
        }
        internal long inputLocalId;

        [DataMember(Order = 8)]
        public long StrategyInputId
        {
            get => strategyInputId;
            set => strategyInputId = value;
        }
        internal long strategyInputId;

        [DataMember(Order = 9)]
        public string InstrumentId
        {
            get => instrumentId;
            set => instrumentId = value;
        }
        internal string instrumentId = string.Empty;

        [DataMember(Order = 10)]
        public byte ProductClass
        {
            get => productClass;
            set => productClass = value;
        }
        internal byte productClass;

        [DataMember(Order = 11)]
        public string ExchangeSymbol
        {
            get => exchangeSymbol;
            set => exchangeSymbol = value;
        }
        internal string exchangeSymbol = string.Empty;

        [DataMember(Order = 12)]
        public string ExchangeId
        {
            get => exchangeId;
            set => exchangeId = value;
        }
        internal string exchangeId = string.Empty;

        [DataMember(Order = 13)]
        public byte OrderStatus
        {
            get => orderStatus;
            set => orderStatus = value;
        }
        internal byte orderStatus = MomOrderStatusType.NoCheck;

        [DataMember(Order = 14)]
        public byte OrderSubmitStatus
        {
            get => orderSubmitStatus;
            set => orderSubmitStatus = value;
        }
        internal byte orderSubmitStatus = MomOrderSubmitStatusType.InsertRejected;

        [DataMember(Order = 15)]
        public string OrderSysId
        {
            get => orderSysId;
            set => orderSysId = value;
        }
        internal string orderSysId = string.Empty;

        [DataMember(Order = 16)]
        public byte OrderPriceType
        {
            get => orderPriceType;
            set => orderPriceType = value;
        }
        internal byte orderPriceType = MomOrderPriceTypeType.LimitPrice;

        [DataMember(Order = 17)]
        public byte Direction
        {
            get => direction;
            set => direction = value;
        }
        internal byte direction = MomDirectionType.Buy;

        [DataMember(Order = 18)]
        public byte OffsetFlag
        {
            get => offsetFlag;
            set => offsetFlag = value;
        }
        internal byte offsetFlag = MomOffsetFlagType.AutoOpenClose;

        [DataMember(Order = 19)]
        public decimal LimitPrice
        {
            get => limitPrice;
            set => limitPrice = value;
        }
        internal decimal limitPrice;

        [DataMember(Order = 20)]
        public decimal VolumeTotalOriginal
        {
            get => volumeTotalOriginal;
            set => volumeTotalOriginal = value;
        }
        internal decimal volumeTotalOriginal;

        [DataMember(Order = 21)]
        public decimal OpenVolume
        {
            get => openVolume;
            set => openVolume = value;
        }
        internal decimal openVolume;

        [DataMember(Order = 22)]
        public decimal CloseVolume
        {
            get => closeVolume;
            set => closeVolume = value;
        }
        internal decimal closeVolume;

        [DataMember(Order = 23)]
        public decimal CloseTodayVolume
        {
            get => closeTodayVolume;
            set => closeTodayVolume = value;
        }
        internal decimal closeTodayVolume;

        [DataMember(Order = 24)]
        public decimal VolumeTraded
        {
            get => volumeTraded;
            set => volumeTraded = value;
        }
        internal decimal volumeTraded;

        [DataMember(Order = 25)]
        public byte TimeCondition
        {
            get => timeCondition;
            set => timeCondition = value;
        }
        internal byte timeCondition = MomTimeConditionType.GFD;

        [DataMember(Order = 26)]
        public byte VolumeCondition
        {
            get => volumeCondition;
            set => volumeCondition = value;
        }
        internal byte volumeCondition = MomVolumeConditionType.AnyVolume;

        [DataMember(Order = 27)]
        public byte ContingentCondition
        {
            get => contingentCondition;
            set => contingentCondition = value;
        }
        internal byte contingentCondition = MomContingentConditionType.Immediately;

        [DataMember(Order = 28)]
        public decimal StopPrice
        {
            get => stopPrice;
            set => stopPrice = value;
        }
        internal decimal stopPrice;

        [DataMember(Order = 29)]
        public decimal FrozenCommission
        {
            get => frozenCommission;
            set => frozenCommission = value;
        }
        internal decimal frozenCommission;

        [DataMember(Order = 30)]
        public decimal Commission
        {
            get => commission;
            set => commission = value;
        }
        internal decimal commission;

        [DataMember(Order = 31)]
        public decimal FrozenMargin
        {
            get => frozenMargin;
            set => frozenMargin = value;
        }
        internal decimal frozenMargin;

        [DataMember(Order = 32)]
        public decimal UseMargin
        {
            get => useMargin;
            set => useMargin = value;
        }
        internal decimal useMargin;

        [DataMember(Order = 33)]
        public decimal FrozenPremium
        {
            get => frozenPremium;
            set => frozenPremium = value;
        }
        internal decimal frozenPremium;

        [DataMember(Order = 34)]
        public decimal Premium
        {
            get => premium;
            set => premium = value;
        }
        internal decimal premium;

        [DataMember(Order = 35)]
        public decimal FrozenFinancing
        {
            get => frozenFinancing;
            set => frozenFinancing = value;
        }
        internal decimal frozenFinancing;

        [DataMember(Order = 36)]
        public decimal UseFinancing
        {
            get => useFinancing;
            set => useFinancing = value;
        }
        internal decimal useFinancing;

        [DataMember(Order = 37)]
        public string StatusMsg
        {
            get => statusMsg;
            set => statusMsg = value;
        }
        internal string statusMsg = string.Empty;

        [DataMember(Order = 38)]
        public byte OrderSource
        {
            get => orderSource;
            set => orderSource = value;
        }
        internal byte orderSource = MomOrderSourceType.Strategy;

        [DataMember(Order = 39)]
        public long Timestamp1
        {
            get => timestamp1;
            set => timestamp1 = value;
        }
        internal long timestamp1;

        [DataMember(Order = 40)]
        public long Timestamp2
        {
            get => timestamp2;
            set => timestamp2 = value;
        }
        internal long timestamp2;

        [DataMember(Order = 41)]
        public int Version
        {
            get => version;
            set => version = value;
        }
        internal int version;

        [DataMember(Order = 42)]
        public string ShortKey
        {
            get => shortKey;
            set => shortKey = value;
        }
        internal string shortKey = string.Empty;

        [DataMember(Order = 43)]
        public string Trader
        {
            get => trader;
            set => trader = value;
        }
        internal string trader = string.Empty;

        [DataMember(Order = 44)]
        public string? Advanced
        {
            get => advanced;
            set => advanced = value;
        }
        internal string? advanced;

        public void UpdateVersion()
        {
            Interlocked.Increment(ref version);
        }
    }
}
