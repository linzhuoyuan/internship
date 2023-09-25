using System;
using System.Runtime.Serialization;
using System.Threading;
using ProtoBuf;

namespace Quantmom.Api
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
        internal string fundAccountId = string.Empty;

        [DataMember(Order = 3)]
        public string UserId
        {
            get => userId;
            set => userId = value;
        }
        internal string userId = string.Empty;

        [DataMember(Order = 4)]
        public string? AccountId
        {
            get => accountId;
            set => accountId = value;
        }
        internal string? accountId;

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
        internal string orderSysId = string.Empty;

        [DataMember(Order = 10)]
        public string InstrumentId
        {
            get => instrumentId;
            set => instrumentId = value;
        }
        internal string instrumentId = string.Empty;

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
        internal string exchangeSymbol = string.Empty;

        [DataMember(Order = 13)]
        public string ExchangeId
        {
            get => exchangeId;
            set => exchangeId = value;
        }
        internal string exchangeId = string.Empty;

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
        public byte OffsetFlag
        {
            get => offsetFlag;
            set => offsetFlag = value;
        }
        internal byte offsetFlag;

        [DataMember(Order = 17)]
        public decimal LimitPrice
        {
            get => limitPrice;
            set => limitPrice = value;
        }
        internal decimal limitPrice;

        [DataMember(Order = 18)]
        public decimal VolumeTotalOriginal
        {
            get => volumeTotalOriginal;
            set => volumeTotalOriginal = value;
        }
        internal decimal volumeTotalOriginal;

        [DataMember(Order = 19)]
        public decimal OpenVolume
        {
            get => openVolume;
            set => openVolume = value;
        }
        internal decimal openVolume;

        [DataMember(Order = 20)]
        public decimal CloseVolume
        {
            get => closeVolume;
            set => closeVolume = value;
        }
        internal decimal closeVolume;

        [DataMember(Order = 21)]
        public decimal CloseTodayVolume
        {
            get => closeTodayVolume;
            set => closeTodayVolume = value;
        }
        internal decimal closeTodayVolume;

        [DataMember(Order = 22)]
        public byte TimeCondition
        {
            get => timeCondition;
            set => timeCondition = value;
        }
        internal byte timeCondition;

        [DataMember(Order = 23)]
        public byte VolumeCondition
        {
            get => volumeCondition;
            set => volumeCondition = value;
        }
        internal byte volumeCondition;

        [DataMember(Order = 24)]
        public byte ContingentCondition
        {
            get => contingentCondition;
            set => contingentCondition = value;
        }
        internal byte contingentCondition;

        [DataMember(Order = 25)]
        public decimal StopPrice
        {
            get => stopPrice;
            set => stopPrice = value;
        }
        internal decimal stopPrice;

        [DataMember(Order = 26)]
        public byte OrderStatus
        {
            get => orderStatus;
            set => orderStatus = value;
        }
        internal byte orderStatus;

        [DataMember(Order = 27)]
        public byte OrderSubmitStatus
        {
            get => orderSubmitStatus;
            set => orderSubmitStatus = value;
        }
        internal byte orderSubmitStatus = MomOrderSubmitStatusType.InsertRejected;

        [DataMember(Order = 28)]
        public decimal VolumeTraded
        {
            get => volumeTraded;
            set => volumeTraded = value;
        }
        internal decimal volumeTraded;

        [DataMember(Order = 29)]
        public decimal AveragePrice
        {
            get => averagePrice;
            set => averagePrice = value;
        }
        internal decimal averagePrice;

        [DataMember(Order = 30)]
        public int InsertDate
        {
            get => insertDate;
            set => insertDate = value;
        }
        internal int insertDate;

        [DataMember(Order = 31)]
        public int InsertTime
        {
            get => insertTime;
            set => insertTime = value;
        }
        internal int insertTime;

        [DataMember(Order = 32)]
        public int UpdateTime
        {
            get => updateTime;
            set => updateTime = value;
        }
        internal int updateTime;

        [DataMember(Order = 33)]
        public int CancelTime
        {
            get => cancelTime;
            set => cancelTime = value;
        }
        internal int cancelTime;

        [DataMember(Order = 34)]
        public string StatusMsg
        {
            get => statusMsg;
            set => statusMsg = value;
        }
        internal string statusMsg = string.Empty;
   
        [DataMember(Order = 35)]
        public int Version
        {
            get => version;
            set => version = value;
        }
        internal int version;

        [DataMember(Order = 36)]
        public string ShortKey
        {
            get => shortKey;
            set => shortKey = value;
        }
        internal string shortKey = string.Empty;

        public void UpdateVersion()
        {
            Interlocked.Increment(ref version);
        }
    }
}
