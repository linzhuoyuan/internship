using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [StructLayout(LayoutKind.Sequential)]
    [DataContract]
    class MomReqInstrument
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 31)]
        [DataMember(Order = 1)]
        public string InstrumentID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
        [DataMember(Order = 2)]
        public string ExchangeID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 31)]
        [DataMember(Order = 3)]
        public string ExchangeInstID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 31)]
        [DataMember(Order = 4)]
        public string ProductID;
    }
}
