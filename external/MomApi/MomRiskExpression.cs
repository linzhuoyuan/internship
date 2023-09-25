using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomRiskExpression
    {
        internal long inserted;
        internal bool enable;
        internal bool limitOnly;
        internal string expressionString = null!;
        internal string? riskMessage;
        internal string? exchange;
        internal string? symbol;
        internal string? ownerId;
        internal int level;
        internal MomRiskScope scope;
        internal MomRiskAction action;
        internal bool cancelOnly;
        internal int tradeLimit;

        [DataMember(Order = 1)]
        public long Inserted
        {
            get => inserted;
            set => inserted = value;
        }

        [DataMember(Order = 2)]
        public bool Enable
        {
            get => enable;
            set => enable = value;
        }

        [DataMember(Order = 3)]
        public bool LimitOnly
        {
            get => limitOnly;
            set => limitOnly = value;
        }

        [DataMember(Order = 4)]
        public bool CancelOnly
        {
            get => cancelOnly;
            set => cancelOnly = value;
        }

        [DataMember(Order = 5)]
        public string ExpressionString
        {
            get => expressionString;
            set => expressionString = value;
        }

        [DataMember(Order = 6)]
        public string? RiskMessage
        {
            get => riskMessage;
            set => riskMessage = value;
        }

        [DataMember(Order = 7)]
        public string? Exchange
        {
            get => exchange;
            set => exchange = value;
        }

        [DataMember(Order = 8)]
        public string? Symbol
        {
            get => symbol;
            set => symbol = value;
        }

        [DataMember(Order = 9)]
        public string? OwnerId
        {
            get => ownerId;
            set => ownerId = value;
        }

        [DataMember(Order = 10)]
        public int Level
        {
            get => level;
            set => level = value;
        }

        [DataMember(Order = 11)]
        public MomRiskScope Scope
        {
            get => scope;
            set => scope = value;
        }

        [DataMember(Order = 12)]
        public MomRiskAction Action
        {
            get => action;
            set => action = value;
        }

        [DataMember(Order = 13)]
        public int TradeLimit
        {
            get => tradeLimit;
            set => tradeLimit = value;
        }
    }

    public enum MomRiskAction
    {
        Warn,
        Reject
    }

    public enum MomRiskScope
    {
        Strategy,
        Fund,
        Account,
        FundAccount
    }
}
