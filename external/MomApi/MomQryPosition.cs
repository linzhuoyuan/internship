using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomQryPosition : MomQryField
    {
        [DataMember(Order = 1)]
        public string OwnerId = string.Empty;

        [DataMember(Order = 2)]
        public string InstrumentId = string.Empty;
    }
}
