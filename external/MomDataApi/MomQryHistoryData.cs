using MomCrypto.Api;
using System.Runtime.Serialization;

namespace MomCrypto.DataApi
{
    [DataContract]
    public class MomQryHistoryData : MomQryField
    {
        [DataMember(Order = 1)]
        public string InstrumentId = string.Empty;

        [DataMember(Order = 2)]
        public string TimeStart = string.Empty;

        [DataMember(Order = 3)]
        public string TimeEnd = string.Empty;

        [DataMember(Order = 4)]
        public string Market = string.Empty;

        [DataMember(Order = 5)]
        public int DataType = (int)MomHistoryDataType.OneMinute;
    }
}
