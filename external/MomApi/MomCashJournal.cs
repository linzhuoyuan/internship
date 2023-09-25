using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomCashJournal
    {
        internal long inserted;
        internal string accountId = string.Empty;
        internal string fundAccountId = string.Empty;
        internal MomCashJournalTypeType cashJournalType;
        internal decimal amount;
        internal string description = string.Empty;

        [DataMember(Order = 1)]
        public long Inserted
        {
            get => inserted;
            set => inserted = value;
        }

        [DataMember(Order = 2)]
        public string AccountId
        {
            get => accountId;
            set => accountId = value;
        }

        [DataMember(Order = 3)]
        public string FundAccountId
        {
            get => fundAccountId;
            set => fundAccountId = value;
        }

        [DataMember(Order = 4)]
        public MomCashJournalTypeType CashJournalType
        {
            get => cashJournalType;
            set => cashJournalType = value;
        }

        [DataMember(Order = 5)]
        public decimal Amount
        {
            get => amount;
            set => amount = value;
        }

        [DataMember(Order = 6)]
        public string Description
        {
            get => description;
            set => description = value;
        }

        public MomCashJournal Clone()
        {
            return (MomCashJournal)MemberwiseClone();
        }
    }
}
