using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomPosition : PositionField
    {
        public override string ToString()
        {
            return $"{userId},{accountId},{exchangeSymbol},{ConstantHelper.GetName<MomPosiDirectionType>(posiDirection)}," +
                   $"P:{position},TP:{todayPosition}";
        }

        public MomPosition Clone()
        {
            return (MomPosition)MemberwiseClone();
        }
    }
}
