using System;
using System.Runtime.Serialization;
using System.Threading;
using ProtoBuf;

namespace Quantmom.Api
{
    [DataContract]
    [ProtoInclude(40, typeof(MomPosition))]
    [ProtoInclude(80, typeof(MomFundPosition))]
    public abstract class PositionField
    {
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
        public string? UserId
        {
            get => userId;
            set => userId = value;
        }
        internal string? userId;

        [DataMember(Order = 4)]
        public string? AccountId
        {
            get => accountId;
            set => accountId = value;
        }
        internal string? accountId;

        [DataMember(Order = 5)]
        public long PositionId
        {
            get => positionId;
            set => positionId = value;
        }
        internal long positionId;

        [DataMember(Order = 6)]
        public string InstrumentId
        {
            get => instrumentId;
            set => instrumentId = value;
        }
        internal string instrumentId = string.Empty;

        [DataMember(Order = 7)]
        public string ExchangeSymbol
        {
            get => exchangeSymbol;
            set => exchangeSymbol = value;
        }
        internal string exchangeSymbol = string.Empty;

        [DataMember(Order = 8)]
        public byte ProductClass
        {
            get => productClass;
            set => productClass = value;
        }
        internal byte productClass;

        [DataMember(Order = 9)]
        public string ExchangeId
        {
            get => exchangeId;
            set => exchangeId = value;
        }
        internal string exchangeId = string.Empty;

        [DataMember(Order = 10)]
        public byte PosiDirection
        {
            get => posiDirection;
            set => posiDirection = value;
        }
        internal byte posiDirection;

        [DataMember(Order = 11)]
        public byte HedgeFlag
        {
            get => hedgeFlag;
            set => hedgeFlag = value;
        }
        internal byte hedgeFlag;

        [DataMember(Order = 12)]
        public decimal CreditPosition
        {
            get => creditPosition;
            set => creditPosition = value;
        }
        internal decimal creditPosition;

        [DataMember(Order = 13)]
        public decimal Position
        {
            get => position;
            set => position = value;
        }
        internal decimal position;

        [DataMember(Order = 14)]
        public decimal TodayPosition
        {
            get => todayPosition;
            set => todayPosition = value;
        }
        internal decimal todayPosition;

        [DataMember(Order = 15)]
        public decimal CloseFrozen
        {
            get => closeFrozen;
            set => closeFrozen = value;
        }
        internal decimal closeFrozen;

        [DataMember(Order = 16)]
        public decimal TodayFrozen
        {
            get => todayFrozen;
            set => todayFrozen = value;
        }
        internal decimal todayFrozen;

        [DataMember(Order = 17)]
        public decimal PreOpen
        {
            get => preOpen;
            set => preOpen = value;
        }
        internal decimal preOpen;

        [DataMember(Order = 18)]
        public decimal OpenVolume
        {
            get => openVolume;
            set => openVolume = value;
        }
        internal decimal openVolume;

        [DataMember(Order = 19)]
        public decimal CloseVolume
        {
            get => closeVolume;
            set => closeVolume = value;
        }
        internal decimal closeVolume;

        [DataMember(Order = 20)]
        public decimal OpenAmount
        {
            get => openAmount;
            set => openAmount = value;
        }
        internal decimal openAmount;

        [DataMember(Order = 21)]
        public decimal CloseAmount
        {
            get => closeAmount;
            set => closeAmount = value;
        }
        internal decimal closeAmount;

        [DataMember(Order = 22)]
        public decimal UseMargin
        {
            get => useMargin;
            set => useMargin = value;
        }
        internal decimal useMargin;

        [DataMember(Order = 23)]
        public decimal Commission
        {
            get => commission;
            set => commission = value;
        }
        internal decimal commission;

        [DataMember(Order = 24)]
        public decimal RealizedPnL
        {
            get => realizedPnL;
            set => realizedPnL = value;
        }
        internal decimal realizedPnL;

        [DataMember(Order = 25)]
        public decimal UnrealizedPnL
        {
            get => unrealizedPnL;
            set => unrealizedPnL = value;
        }
        internal decimal unrealizedPnL;

        [DataMember(Order = 26)]
        public decimal OpenCost
        {
            get => openCost;
            set => openCost = value;
        }
        internal decimal openCost;

        [DataMember(Order = 27)]
        public int Version
        {
            get => version;
            set => version = value;
        }
        internal int version = -1;

        public void UpdateVersion()
        {
            Interlocked.Increment(ref version);
        }

        protected PositionField()
        {
        }

        protected PositionField(PositionField position)
        {
            FundId = position.fundId;
            FundAccountId = position.fundAccountId;
            PositionId = position.positionId;
            UserId = position.userId;
            AccountId = position.accountId;
            InstrumentId = position.instrumentId;
            ExchangeSymbol = position.exchangeSymbol;
            ProductClass = position.productClass;
            ExchangeId = position.exchangeId;
            PosiDirection = position.posiDirection;
            HedgeFlag = position.hedgeFlag;
            CreditPosition = position.creditPosition;
            Position = position.position;
            TodayPosition = position.todayPosition;
            CloseFrozen = position.closeFrozen;
            TodayFrozen = position.todayFrozen;
            PreOpen = position.preOpen;
            OpenVolume = position.openVolume;
            CloseVolume = position.closeVolume;
            OpenAmount = position.openAmount;
            CloseAmount = position.closeAmount;
            UseMargin = position.useMargin;
            Commission = position.commission;
            RealizedPnL = position.realizedPnL;
            UnrealizedPnL = position.unrealizedPnL;
            OpenCost = position.openCost;
        }
    }
}
