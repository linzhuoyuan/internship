using System;
using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomSubscribeResponse
    {
        [DataMember(Order = 1)]
        public string[] UserIdList = Array.Empty<string>();
        [DataMember(Order = 2)]
        public string[] FundAccountIdList = Array.Empty<string>();
    }
}
