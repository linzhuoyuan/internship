using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomReqUserLogin
    {
        [DataMember(Order = 1)]
        public string UserId = string.Empty;

        [DataMember(Order = 2)]
        public string Password = string.Empty;

        [DataMember(Order = 3)]
        public string StrategyId = string.Empty;

        [DataMember(Order = 4)]
        public string ClientAppId = string.Empty;

        [DataMember(Order = 5)]
        public string ClientIpAddress = string.Empty;

        [DataMember(Order = 6)]
        public string ClientMac = string.Empty;

        [DataMember(Order = 7)]
        public string LoginRemark = string.Empty;
    }
}
