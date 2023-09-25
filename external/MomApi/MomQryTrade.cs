using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomQryTrade : MomQryField
    {
        [DataMember(Order = 1)]
        public string OwnerId = string.Empty;

        [DataMember(Order = 2)]
        public string TradeId = string.Empty;

        [DataMember(Order = 3)]
        public int TradeTimeStart;

        [DataMember(Order = 4)]
        public int TradeTimeEnd;
    }
}
