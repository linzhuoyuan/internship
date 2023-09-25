using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomQryUncheckInput : MomQryField
    {
        [DataMember(Order = 1)]
        public string OwnerId = string.Empty;
    }
}
