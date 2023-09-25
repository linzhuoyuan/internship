using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public class MomQryOrder : MomQryField
    {
        [DataMember(Order = 1)]
        public string UserId = string.Empty;

        [DataMember(Order = 2)]
        public string InstrumentId = string.Empty;

        [DataMember(Order = 3)]
        public string ExchangeId = string.Empty;

        [DataMember(Order = 4)]
        public string OrderSysId = string.Empty;

        [DataMember(Order = 5)]
        public string InsertTimeStart = string.Empty;

        [DataMember(Order = 6)]
        public string InsertTimeEnd = string.Empty;
    }
}
