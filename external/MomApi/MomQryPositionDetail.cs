using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomQryPositionDetail : MomQryField
    {
        [DataMember(Order = 1)]
        public string UserId = string.Empty;

        [DataMember(Order = 2)]
        public string InstrumentId = string.Empty;
    }
}
