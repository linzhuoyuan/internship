using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public class MomQryAccount : MomQryField
    {
        [DataMember(Order = 1)]
        public string UserId = string.Empty;
    }
}
