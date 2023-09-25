using System.Runtime.Serialization;
using System.Threading;
using ProtoBuf;

namespace MomCrypto.Api
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
        internal string fundAccountId = null!;

        [DataMember(Order = 3)]
        public string UserId
        {
            get => userId;
            set => userId = value;
        }
        internal string userId = null!;

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
        internal string instrumentId = null!;

        [DataMember(Order = 7)]
        public string ExchangeSymbol
        {
            get => exchangeSymbol;
            set => exchangeSymbol = value;
        }
        internal string exchangeSymbol = null!;

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
        internal string exchangeId = null!;

        [DataMember(Order = 10)]
        public decimal Position
        {
            get => position;
            set => position = value;
        }
        internal decimal position;

        [DataMember(Order = 11)]
        public decimal SellFrozen
        {
            get => sellFrozen;
            set => sellFrozen = value;
        }
        internal decimal sellFrozen;

        [DataMember(Order = 12)]
        public decimal BuyFrozen
        {
            get => buyFrozen;
            set => buyFrozen = value;
        }
        internal decimal buyFrozen;

        [DataMember(Order = 13)]
        public decimal SellUnfrozen
        {
            get => sellUnfrozen;
            set => sellUnfrozen = value;
        }
        internal decimal sellUnfrozen;

        [DataMember(Order = 14)]
        public decimal BuyUnfrozen
        {
            get => buyUnfrozen;
            set => buyUnfrozen = value;
        }
        internal decimal buyUnfrozen;

        [DataMember(Order = 15)]
        public decimal BuyVolume
        {
            get => buyVolume;
            set => buyVolume = value;
        }
        internal decimal buyVolume;

        [DataMember(Order = 16)]
        public decimal SellVolume
        {
            get => sellVolume;
            set => sellVolume = value;
        }
        internal decimal sellVolume;

        [DataMember(Order = 17)]
        public decimal BuyAmount
        {
            get => buyAmount;
            set => buyAmount = value;
        }
        internal decimal buyAmount;

        [DataMember(Order = 18)]
        public decimal SellAmount
        {
            get => sellAmount;
            set => sellAmount = value;
        }
        internal decimal sellAmount;

        [DataMember(Order = 19)]
        public decimal PositionCost
        {
            get => positionCost;
            set => positionCost = value;
        }
        internal decimal positionCost;

        [DataMember(Order = 20)]
        public decimal UseMargin
        {
            get => useMargin;
            set => useMargin = value;
        }
        internal decimal useMargin;

        [DataMember(Order = 21)]
        public decimal Commission
        {
            get => commission;
            set => commission = value;
        }
        internal decimal commission;

        [DataMember(Order = 22)]
        public decimal CloseProfit
        {
            get => closeProfit;
            set => closeProfit = value;
        }
        internal decimal closeProfit;

        [DataMember(Order = 23)]
        public decimal PositionProfit
        {
            get => positionProfit;
            set => positionProfit = value;
        }
        internal decimal positionProfit;

        [DataMember(Order = 24)]
        public decimal OpenCost
        {
            get => openCost;
            set => openCost = value;
        }
        internal decimal openCost;

        [DataMember(Order = 25)]
        public decimal CashPosition
        {
            get => cashPosition;
            set => cashPosition = value;
        }
        internal decimal cashPosition;

        [DataMember(Order = 26)]
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
    }
}
