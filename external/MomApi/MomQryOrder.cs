using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomQryOrder : MomQryField
    {
        [DataMember(Order = 1)]
        public string OwnerId = string.Empty;

        [DataMember(Order = 2)]
        public string OrderSysId = string.Empty;

        [DataMember(Order = 3)]
        public int InsertTimeStart;

        [DataMember(Order = 4)]
        public int InsertTimeEnd;
    }
}
