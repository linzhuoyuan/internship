using System.Runtime.Serialization;
using System.Threading;
using ProtoBuf;

namespace MomCrypto.Api
{
    [DataContract]
    [ProtoInclude(50, typeof(MomInputOrder))]
    public abstract class InputOrderField
    {
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
        internal string fundAccountId;

        [DataMember(Order = 3)]
        public string FundChannelType
        {
            get => fundChannelType;
            set => fundChannelType = value;
        }
        internal string fundChannelType;

        [DataMember(Order = 4)]
        public string UserId
        {
            get => userId;
            set => userId = value;
        }
        internal string userId;
        
        [DataMember(Order = 5)]
        public string AccountId
        {
            get => accountId;
            set => accountId = value;
        }
        internal string accountId = string.Empty;

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
        internal string exchangeSymbol = null!;

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
        internal byte orderStatus = MomOrderStatusType.NoTradeNotQueueing;

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
        public decimal LimitPrice
        {
            get => limitPrice;
            set => limitPrice = value;
        }
        internal decimal limitPrice;

        [DataMember(Order = 19)]
        public decimal VolumeTotalOriginal
        {
            get => volumeTotalOriginal;
            set => volumeTotalOriginal = value;
        }
        internal decimal volumeTotalOriginal;

        [DataMember(Order = 20)]
        public decimal OpenVolume
        {
            get => openVolume;
            set => openVolume = value;
        }
        internal decimal openVolume;

        [DataMember(Order = 21)]
        public decimal CloseVolume
        {
            get => closeVolume;
            set => closeVolume = value;
        }
        internal decimal closeVolume;

        [DataMember(Order = 22)]
        public decimal VolumeTraded
        {
            get => volumeTraded;
            set => volumeTraded = value;
        }
        internal decimal volumeTraded;

        [DataMember(Order = 23)]
        public byte TimeCondition
        {
            get => timeCondition;
            set => timeCondition = value;
        }
        internal byte timeCondition = MomTimeConditionType.GFD;

        [DataMember(Order = 24)]
        public byte VolumeCondition
        {
            get => volumeCondition;
            set => volumeCondition = value;
        }
        internal byte volumeCondition = MomVolumeConditionType.AnyVolume;

        [DataMember(Order = 25)]
        public byte ContingentCondition
        {
            get => contingentCondition;
            set => contingentCondition = value;
        }
        internal byte contingentCondition = MomContingentConditionType.Immediately;

        [DataMember(Order = 26)]
        public decimal StopPrice
        {
            get => stopPrice;
            set => stopPrice = value;
        }
        internal decimal stopPrice;

        [DataMember(Order = 27)]
        public decimal FrozenCommission
        {
            get => frozenCommission;
            set => frozenCommission = value;
        }
        internal decimal frozenCommission;

        [DataMember(Order = 28)]
        public decimal FrozenMargin
        {
            get => frozenMargin;
            set => frozenMargin = value;
        }
        internal decimal frozenMargin;

        [DataMember(Order = 29)]
        public decimal FrozenPremium
        {
            get => frozenPremium;
            set => frozenPremium = value;
        }
        internal decimal frozenPremium;

        [DataMember(Order = 30)]
        public string? Advanced 
        { 
            get => advanced;
            set => advanced = value;
        }
        internal string? advanced;

        [DataMember(Order = 31)]
        public byte TriggerType 
        { 
            get => triggerType;
            set => triggerType = value;
        }
        internal byte triggerType = MomStopWorkingTypeType.MarkPrice;

        [DataMember(Order = 32)]
        public string? StopOrderId 
        { 
            get => stopOrderId;
            set => stopOrderId = value;
        }
        internal string? stopOrderId;

        [DataMember(Order = 33)]
        public byte StopWorkingType 
        { 
            get => stopWorkingType;
            set => stopWorkingType = value;
        }
        internal byte stopWorkingType = MomStopWorkingTypeType.ContractPrice;

        [DataMember(Order = 34)]
        public string? StatusMsg
        {
            get => statusMsg;
            set => statusMsg = value;
        }
        internal string? statusMsg;

        [DataMember(Order = 35)]
        public long Timestamp1
        {
            get => timestamp1;
            set => timestamp1 = value;
        }
        internal long timestamp1;

        [DataMember(Order = 36)]
        public long Timestamp2
        {
            get => timestamp2;
            set => timestamp2 = value;
        }
        internal long timestamp2;

        [DataMember(Order = 37)]
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
