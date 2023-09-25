using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public class MomCashJournal
    {
        internal long inserted;
        internal string? userId;
        internal string? operatorId;
        internal string? market;
        internal byte cashJournalType;
        internal string? rollInAccountId;
        internal string? rollOutAccountId;
        internal decimal amount;
        internal string? description;
        internal string? actionDate;
        internal string? currencyType;

        [DataMember(Order = 1)]
        public long Inserted
        {
            get => inserted;
            set => inserted = value;
        }

        [DataMember(Order = 2)]
        public string? ActionDate
        {
            get => actionDate;
            set => actionDate = value;
        }

        [DataMember(Order = 3)]
        public string? UserId
        {
            get => userId;
            set => userId = value;
        }

        [DataMember(Order = 4)]
        public string? OperatorId
        {
            get => operatorId;
            set => operatorId = value;
        }

        [DataMember(Order = 5)]
        public string? RollInAccountId
        {
            get => rollInAccountId;
            set => rollInAccountId = value;
        }

        [DataMember(Order = 6)]
        public string? RollOutAccountId
        {
            get => rollOutAccountId;
            set => rollOutAccountId = value;
        }

        [DataMember(Order = 7)]
        public string? Market
        {
            get => market;
            set => market = value;
        }

        [DataMember(Order = 8)]
        public byte CashJournalType
        {
            get => cashJournalType;
            set => cashJournalType = value;
        }

        [DataMember(Order = 9)]
        public decimal Amount
        {
            get => amount;
            set => amount = value;
        }

        [DataMember(Order = 10)]
        public string? CurrencyType
        {
            get => currencyType;
            set => currencyType = value;
        }

        [DataMember(Order = 11)]
        public string? Description
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
