using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomFundPosition : PositionField
    {
        public override string ToString()
        {
            return $"{fundAccountId},{exchangeSymbol},{ConstantHelper.GetName<MomPosiDirectionType>(posiDirection)}," +
                   $"P:{position},TP:{todayPosition}";
        }

        public MomFundPosition Clone()
        {
            return (MomFundPosition)MemberwiseClone();
        }
    }
}
