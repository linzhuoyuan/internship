using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public class MomChangeLeverage
    {
        [DataMember(Order = 1)]
        public string UserId = string.Empty;

        [DataMember(Order = 2)]
        public string Exchange = string.Empty;

        [DataMember(Order = 3)]
        public string Symbol = string.Empty;

        [DataMember(Order = 4)]
        public int Leverage;
    }
}
