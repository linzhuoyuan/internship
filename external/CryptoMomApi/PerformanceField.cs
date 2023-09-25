using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public abstract class PerformanceField
    {
        [DataMember(Order = 1)]
        public string TradingDay { get; set; }

        [DataMember(Order = 2)]
        public string AccountId { get; set; }

        [DataMember(Order = 3)]
        public string AccountType { get; set; }

        [DataMember(Order = 4)]
        public double Equity { get; set; }

        [DataMember(Order = 5)]
        public double EquityBySettle { get; set; }

        [DataMember(Order = 6)]
        public double Commission { get; set; }

        [DataMember(Order = 7)]
        public double CloseProfit { get; set; }

        [DataMember(Order = 8)]
        public double PositionProfit { get; set; }

        [DataMember(Order = 9)]
        public double PositionProfitBySettle { get; set; }

        [DataMember(Order = 10)]
        public double UseMargin { get; set; }

        [DataMember(Order = 11)]
        public double PremiumIn { get; set; }

        [DataMember(Order = 12)]
        public double PremiumOut { get; set; }

        [DataMember(Order = 13)]
        public double Available { get; set; }

        [DataMember(Order = 14)]
        public double MarketValue { get; set; }

        [DataMember(Order = 15)]
        public double MarketValueSettle { get; set; }

        [DataMember(Order = 16)]
        public double Deposit { get; set; }

        [DataMember(Order = 17)]
        public double Withdraw { get; set; }
    }
}
