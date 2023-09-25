using System.Runtime.Serialization;
using ProtoBuf;

namespace Quantmom.Api
{
    [DataContract]
    [ProtoInclude(20, typeof(MomPerformance))]
    [ProtoInclude(40, typeof(MomFundPerformance))]
    public abstract class PerformanceField
    {
        [DataMember(Order = 1)]
        public string TradingDay { get; set; } = string.Empty;

        [DataMember(Order = 2)]
        public string UserId { get; set; } = string.Empty;

        [DataMember(Order = 3)]
        public string FundAccountId { get; set; } = string.Empty;

        [DataMember(Order = 4)]
        public string AccountId { get; set; } = string.Empty;

        [DataMember(Order = 5)]
        public string AccountType { get; set; } = string.Empty;

        [DataMember(Order = 6)]
        public decimal Equity { get; set; }

        [DataMember(Order = 7)]
        public decimal Commission { get; set; }
        
        [DataMember(Order = 8)]
        public decimal MaintMargin { get; set; }

        [DataMember(Order = 9)]
        public decimal Premium { get; set; }
       
        [DataMember(Order = 10)]
        public decimal Available { get; set; }

        [DataMember(Order = 11)]
        public decimal FinancingUsed { get; set; }

        [DataMember(Order = 12)]
        public decimal MarketValue { get; set; }
        
        [DataMember(Order = 13)]
        public decimal RealizedPnL { get; set; }

        [DataMember(Order = 14)]
        public decimal UnrealizedPnL { get; set; }

        [DataMember(Order = 15)]
        public decimal Deposit { get; set; }

        [DataMember(Order = 16)]
        public decimal Withdraw { get; set; }
    }
}
