using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomQryAccount : MomQryField
    {
        [DataMember(Order = 1)]
        public string OwnerId = string.Empty;
    }
}
