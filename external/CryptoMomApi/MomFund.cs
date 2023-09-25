using System.Runtime.Serialization;

namespace MomCrypto.Api
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
        internal string fundName;

        [DataMember(Order = 3)]
        public double InitialCapital
        {
            get => initialCapital;
            set => initialCapital = value;
        }
        internal double initialCapital;

        [DataMember(Order = 4)]
        public string CreatedDate
        {
            get => createdDate;
            set => createdDate = value;
        }
        internal string createdDate;

        [DataMember(Order = 5)]
        public string Description
        {
            get => description;
            set => description = value;
        }
        internal string description;

        public MomFund Clone()
        {
            return (MomFund)MemberwiseClone();
        }
    }
}
