using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomQryPerformance : MomQryField
    {
        [DataMember(Order = 1)]
        public string AccountId = string.Empty;
    }
}
