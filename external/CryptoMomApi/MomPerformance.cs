using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public class MomPerformance : PerformanceField
    {
        [DataMember(Order = 1)]
        public string UserId { get; set; }

        [DataMember(Order = 2)]
        public string FundAccountId { get; set; }
    }

    [DataContract]
    public class MomFundPerformance : PerformanceField
    {
    }
}
