using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomRspInfo
    {
        [DataMember(Order = 1)]
        public int ErrorID;

        [DataMember(Order = 2)]
        public string ErrorMsg = string.Empty;

        public override string ToString()
        {
            return $"{ErrorID},{ErrorMsg}";
        }
    }
}
