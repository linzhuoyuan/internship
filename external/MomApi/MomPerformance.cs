using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomPerformance : PerformanceField
    {
        public override string ToString()
        {
            return $"{TradingDay},{UserId},{AccountId},{Equity}";
        }
    }
}
