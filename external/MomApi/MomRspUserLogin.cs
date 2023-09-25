using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomNotice
    {
        [DataMember(Order = 1)]
        public int TradingDay;

        [DataMember(Order = 2)]
        public string SystemName = string.Empty;

        [DataMember(Order = 3)]
        public MomSystemStatus SystemStatus;

        [DataMember(Order = 4)]
        public string? Notice;
    }

    [DataContract]
    public class MomRspUserLogin
    {
        [DataMember(Order = 5)]
        public int TradingDay;

        [DataMember(Order = 6)]
        public string UserId = string.Empty;

        [DataMember(Order = 7)]
        public string SystemName = string.Empty;

        [DataMember(Order = 8)]
        public MomSystemStatus SystemStatus;

        [DataMember(Order = 9)]
        public int SystemTime;

        [DataMember(Order = 10)]
        public int SettleTime;

        [DataMember(Order = 11)]
        public int MarketOpenTime;

        [DataMember(Order = 12)]
        public int MarketCloseTime;

        [DataMember(Order = 13)]
        public SettlementNotice? SettlementNotice;
    }
}
