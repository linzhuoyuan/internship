using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomQryInstrument
    {
        [DataMember(Order = 1)]
        public string InstrumentId = string.Empty;

        [DataMember(Order = 2)]
        public string ExchangeId = string.Empty;

        [DataMember(Order = 3)]
        public string ProductId = string.Empty;
    }
}
