using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public class MomInstrumentFeeRate
    {
        internal int instrumentIndex;

        [DataMember(Order = 1)]
        public string Symbol
        {
            get => symbol;
            set => symbol = value;
        }
        internal string symbol;

        [DataMember(Order = 2)]
        public string UserId;

        [DataMember(Order = 3)]
        public decimal TakerCommission
        {
            get => takerCommission;
            set => takerCommission = value;
        }
        internal decimal takerCommission;

        [DataMember(Order = 4)]
        public decimal MakerCommission
        {
            get => makerCommission;
            set => makerCommission = value;
        }
        internal decimal makerCommission;
    }
}
