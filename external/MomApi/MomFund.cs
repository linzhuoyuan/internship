using System;
using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomFund
    {
        [DataMember(Order = 1)]
        public long FundId
        {
            get => fundId;
            set => fundId = value;
        }
        internal long fundId;

        [DataMember(Order = 2)]
        public string FundName
        {
            get => fundName;
            set => fundName = value;
        }
        internal string fundName = string.Empty;

        [DataMember(Order = 3)]
        public decimal InitialCapital
        {
            get => initialCapital;
            set => initialCapital = value;
        }
        internal decimal initialCapital;

        [DataMember(Order = 4)]
        public DateTime CreatedDate
        {
            get => createdDate;
            set => createdDate = value;
        }
        internal DateTime createdDate = DateTime.Today;

        [DataMember(Order = 5)]
        public string Description
        {
            get => description;
            set => description = value;
        }
        internal string description = string.Empty;

        public MomFund Clone()
        {
            return (MomFund)MemberwiseClone();
        }
    }
}
