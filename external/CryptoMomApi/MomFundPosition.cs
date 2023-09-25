using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public sealed class MomFundPosition : PositionField
    {
        public override string ToString()
        {
            return $"{fundAccountId},{instrumentId},{exchangeId}," +
                   $"P:{position},Cost:{positionCost}";
        }

        public MomFundPosition Clone()
        {
            return (MomFundPosition)MemberwiseClone();
        }
    }
}
