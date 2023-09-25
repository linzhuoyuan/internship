using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomFundPositionDetail : DetailField
    {
        public override string ToString()
        {
            return $"{userId},{instrumentId},{exchangeSymbol}," +
                   $"v:{volume},p:{openPrice},tid:{tradeId}";
        }

        public MomFundPositionDetail Clone()
        {
            return (MomFundPositionDetail)MemberwiseClone();
        }
    }
}
