using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public class MomQryTrade : MomQryField
    {
        [DataMember(Order = 1)]
        public string UserId = string.Empty;

        [DataMember(Order = 2)]
        public string InstrumentId = string.Empty;

        [DataMember(Order = 3)]
        public string ExchangeId = string.Empty;

        [DataMember(Order = 4)]
        public string TradeId = string.Empty;

        [DataMember(Order = 5)]
        public string TradeTimeStart = string.Empty;

        [DataMember(Order = 6)]
        public string TradeTimeEnd = string.Empty;
    }
}
