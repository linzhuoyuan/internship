using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [StructLayout(LayoutKind.Sequential)]
    [DataContract]
    public class MomReqUserLogin
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        [DataMember(Order = 1)]
        public string UserID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 41)]
        [DataMember(Order = 2)]
        public string Password;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
        [DataMember(Order = 3)]
        public string UserProductInfo;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        [DataMember(Order = 4)]
        public string ClientIPAddress;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 36)]
        [DataMember(Order = 5)]
        public string LoginRemark;
    }
}
