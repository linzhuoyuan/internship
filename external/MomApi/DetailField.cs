using System.Runtime.Serialization;
using System.Threading;
using ProtoBuf;

namespace Quantmom.Api
{
    [DataContract]
    [ProtoInclude(40, typeof(MomPositionDetail))]
    [ProtoInclude(80, typeof(MomFundPositionDetail))]
    public abstract class DetailField
    {
        [DataMember(Order = 1)]
        public long DetailId
        {
            get => detailId;
            set => detailId = value;
        }
        internal long detailId;

        [DataMember(Order = 2)]
        public long PositionId
        {
            get => positionId;
            set => positionId = value;
        }
        internal long positionId;

        [DataMember(Order = 3)]
        public long FundId
        {
            get => fundId;
            set => fundId = value;
        }
        internal long fundId;

        [DataMember(Order = 4)]
        public string? UserId
        {
            get => userId;
            set => userId = value;
        }
        internal string? userId;
        
        [DataMember(Order = 5)]
        public string? AccountId
        {
            get => accountId;
            set => accountId = value;
        }
        internal string? accountId;

        [DataMember(Order = 6)]
        public string FundAccountId
        {
            get => fundAccountId;
            set => fundAccountId = value;
        }
        internal string fundAccountId = string.Empty;

        [DataMember(Order = 7)]
        public string TradeId
        {
            get => tradeId;
            set => tradeId = value;
        }
        internal string tradeId = string.Empty;

        [DataMember(Order = 8)]
        public string InstrumentId
        {
            get => instrumentId;
            set => instrumentId = value;
        }
        internal string instrumentId = string.Empty;

        [DataMember(Order = 9)]
        public string ExchangeSymbol
        {
            get => exchangeSymbol;
            set => exchangeSymbol = value;
        }
        internal string exchangeSymbol = string.Empty;

        [DataMember(Order = 10)]
        public byte ProductClass
        {
            get => productClass;
            set => productClass = value;
        }
        internal byte productClass;

        [DataMember(Order = 11)]
        public string ExchangeId
        {
            get => exchangeId;
            set => exchangeId = value;
        }
        internal string exchangeId = string.Empty;

        [DataMember(Order = 12)]
        public byte Direction
        {
            get => direction;
            set => direction = value;
        }
        internal byte direction;

        [DataMember(Order = 13)]
        public int OpenDate
        {
            get => openDate;
            set => openDate = value;
        }
        internal int openDate;

        [DataMember(Order = 14)]
        public decimal Volume
        {
            get => volume;
            set => volume = value;
        }
        internal decimal volume;

        [DataMember(Order = 15)]
        public decimal OpenPrice
        {
            get => openPrice;
            set => openPrice = value;
        }
        internal decimal openPrice;

        [DataMember(Order = 16)]
        public decimal OpenVolume
        {
            get => openVolume;
            set => openVolume = value;
        }
        internal decimal openVolume;

        [DataMember(Order = 17)]
        public decimal Commission
        {
            get => commission;
            set => commission = value;
        }
        internal decimal commission;

        [DataMember(Order = 18)]
        public decimal Margin
        {
            get => margin;
            set => margin = value;
        }
        internal decimal margin;

        [DataMember(Order = 19)]
        public decimal CloseVolume
        {
            get => closeVolume;
            set => closeVolume = value;
        }
        internal decimal closeVolume;

        [DataMember(Order = 20)]
        public decimal CloseAmount
        {
            get => closeAmount;
            set => closeAmount = value;
        }
        internal decimal closeAmount;

        [DataMember(Order = 21)]
        public decimal CloseProfitByTrade
        {
            get => closeProfitByTrade;
            set => closeProfitByTrade = value;
        }
        internal decimal closeProfitByTrade;

        [DataMember(Order = 22)]
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
