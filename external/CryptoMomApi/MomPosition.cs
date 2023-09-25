using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public class MomPosition : PositionField
    {
        public override string ToString()
        {
            return $"{userId},{accountId},{instrumentId},{exchangeSymbol},"  +
                   $"P:{position}";
        }

        public MomPosition Clone()
        {
            return (MomPosition)MemberwiseClone();
        }
    }
}
