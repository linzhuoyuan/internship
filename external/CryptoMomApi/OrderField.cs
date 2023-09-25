using System;
using System.Runtime.Serialization;
using System.Threading;
using ProtoBuf;

namespace MomCrypto.Api
{
    [DataContract]
    [ProtoInclude(40, typeof(MomOrder))]
    [ProtoInclude(80, typeof(MomFundOrder))]
    public abstract class OrderField
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
        internal string accountId = string.Empty;

        [DataMember(Order = 5)]
        public long StrategyInputId
        {
            get => strategyInputId;
            set => strategyInputId = value;
        }
        internal long strategyInputId;

        [DataMember(Order = 6)]
        public long OrderRef
        {
            get => orderRef;
            set => orderRef = value;
        }
        internal long orderRef;

        [DataMember(Order = 7)]
        public long OrderLocalId
        {
            get => orderLocalId;
            set => orderLocalId = value;
        }
        internal long orderLocalId;

        [DataMember(Order = 8)]
        public long InputLocalId
        {
            get => inputLocalId;
            set => inputLocalId = value;
        }
        internal long inputLocalId;

        [DataMember(Order = 9)]
        public string OrderSysId
        {
            get => orderSysId;
            set => orderSysId = value;
        }
        internal string orderSysId;

        [DataMember(Order = 10)]
        public string InstrumentId
        {
            get => instrumentId;
            set => instrumentId = value;
        }
        internal string instrumentId;

        [DataMember(Order = 11)]
        public byte ProductClass
        {
            get => productClass;
            set => productClass = value;
        }
        internal byte productClass;

        [DataMember(Order = 12)]
        public string ExchangeSymbol
        {
            get => exchangeSymbol;
            set => exchangeSymbol = value;
        }
        internal string exchangeSymbol;

        [DataMember(Order = 13)]
        public string ExchangeId
        {
            get => exchangeId;
            set => exchangeId = value;
        }
        internal string exchangeId;

        [DataMember(Order = 14)]
        public byte OrderPriceType
        {
            get => orderPriceType;
            set => orderPriceType = value;
        }
        internal byte orderPriceType;

        [DataMember(Order = 15)]
        public byte Direction
        {
            get => direction;
            set => direction = value;
        }
        internal byte direction;

        [DataMember(Order = 16)]
        public decimal LimitPrice
        {
            get => limitPrice;
            set => limitPrice = value;
        }
        internal decimal limitPrice;

        [DataMember(Order = 17)]
        public decimal VolumeTotalOriginal
        {
            get => volumeTotalOriginal;
            set => volumeTotalOriginal = value;
        }
        internal decimal volumeTotalOriginal;

        [DataMember(Order = 18)]
        public byte TimeCondition
        {
            get => timeCondition;
            set => timeCondition = value;
        }
        internal byte timeCondition;

        [DataMember(Order = 19)]
        public byte VolumeCondition
        {
            get => volumeCondition;
            set => volumeCondition = value;
        }
        internal byte volumeCondition;

        [DataMember(Order = 20)]
        public decimal MinVolume
        {
            get => minVolume;
            set => minVolume = value;
        }
        internal decimal minVolume;

        [DataMember(Order = 21)]
        public byte ContingentCondition
        {
            get => contingentCondition;
            set => contingentCondition = value;
        }
        internal byte contingentCondition;

        [DataMember(Order = 22)]
        public decimal StopPrice
        {
            get => stopPrice;
            set => stopPrice = value;
        }
        internal decimal stopPrice;

        [DataMember(Order = 23)]
        public byte OrderStatus
        {
            get => orderStatus;
            set => orderStatus = value;
        }
        internal byte orderStatus;

        [DataMember(Order = 24)]
        public byte OrderSubmitStatus
        {
            get => orderSubmitStatus;
            set => orderSubmitStatus = value;
        }
        internal byte orderSubmitStatus = MomOrderSubmitStatusType.InsertRejected;

        [DataMember(Order = 25)]
        public decimal VolumeTraded
        {
            get => volumeTraded;
            set => volumeTraded = value;
        }
        internal decimal volumeTraded;

        [DataMember(Order = 26)]
        public decimal VolumeTotal
        {
            get => volumeTotal;
            set => volumeTotal = value;
        }
        internal decimal volumeTotal;

        [DataMember(Order = 27)]
        public string InsertDate
        {
            get => insertDate;
            set => insertDate = value;
        }
        internal string insertDate;

        [DataMember(Order = 28)]
        public string InsertTime
        {
            get => insertTime;
            set => insertTime = value;
        }
        internal string insertTime;
        
        [DataMember(Order = 29)]
        public string UpdateTime
        {
            get => updateTime;
            set => updateTime = value;
        }
        internal string updateTime;

        [DataMember(Order = 30)]
        public string CancelTime
        {
            get => cancelTime;
            set => cancelTime = value;
        }
        internal string cancelTime;

        [DataMember(Order = 31)]
        public string StatusMsg
        {
            get => statusMsg;
            set => statusMsg = value;
        }
        internal string statusMsg;

        [DataMember(Order = 32)]
        public decimal USD
        {
            get => usd;
            set => usd = value;
        }
        internal decimal usd;

        [DataMember(Order = 33)]
        public bool Triggered
        {
            get => triggered;
            set => triggered = value;
        }
        internal bool triggered;

        [DataMember(Order = 34)]
        public byte TriggerType
        {
            get => triggerType;
            set => triggerType = value;
        }
        internal byte triggerType;

        [DataMember(Order = 35)]
        public string StopOrderId
        {
            get => stopOrderId;
            set => stopOrderId = value;
        }
        internal string stopOrderId;

        [DataMember(Order = 36)]
        public byte StopWorkingType 
        { 
            get => stopWorkingType;
            set => stopWorkingType = value;
        }
        internal byte stopWorkingType;

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
