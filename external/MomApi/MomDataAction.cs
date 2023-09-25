using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomDataAction
    {
        [DataMember(Order = 1)]
        public MomActionType ActionType { get; set; }

        [DataMember(Order = 2)]
        public MomFund? Fund { get; set; }

        [DataMember(Order = 3)]
        public MomUser? User { get; set; }

        [DataMember(Order = 4)]
        public MomAccount? Account { get; set; }

        [DataMember(Order = 5)]
        public MomFundAccount? FundAccount { get; set; }

        [DataMember(Order = 6)]
        public MomCashJournal? CashJournal { get; set; }

        [DataMember(Order = 7)]
        public MomRiskExpression? RiskExpression { get; set; }

        [DataMember(Order = 8)]
        public MomTradingChannel? TradingChannel { get; set; }

        [DataMember(Order = 9)]
        public MomTradingRoute? TradingRoute { get; set; }

        [DataMember(Order = 10)]
        public MomInputOrder? InputOrder { get; set; }
    }
}
