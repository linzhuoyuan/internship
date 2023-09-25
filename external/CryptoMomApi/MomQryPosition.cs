using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public class MomQryPosition : MomQryField
    {
        [DataMember(Order = 1)]
        public string UserId = string.Empty;

        [DataMember(Order = 2)]
        public string InstrumentId = string.Empty;

        [DataMember(Order = 3)]
        public string ExchangeId = string.Empty;
    }
}
