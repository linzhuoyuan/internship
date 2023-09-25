using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomQryCashJournal : MomQryField
    {
        [DataMember(Order = 1)]
        public string UserId = string.Empty;

        [DataMember(Order = 2)]
        public string AccountId = string.Empty;

        [DataMember(Order = 3)]
        public string FundAccountId = string.Empty;

        [DataMember(Order = 4)]
        public long InsertTimeStart;

        [DataMember(Order = 5)]
        public long InsertTimeEnd;
    }
}
