using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomPositionDetail : DetailField
    {
        public override string ToString()
        {
            return $"{userId},{instrumentId},{exchangeSymbol}," +
                   $"v:{volume},p:{openPrice},tid:{tradeId}";
        }

        public MomPositionDetail Clone()
        {
            return (MomPositionDetail)MemberwiseClone();
        }
    }
}
