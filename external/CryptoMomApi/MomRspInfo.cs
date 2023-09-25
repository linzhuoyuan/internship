using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace MomCrypto.Api
{
	[StructLayout(LayoutKind.Sequential)]
	[DataContract]
	public class MomRspInfo
	{
		[DataMember(Order = 1)]
		public int ErrorID;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 81)]
		[DataMember(Order = 2)]
		public string ErrorMsg;

        public override string ToString()
        {
            return $"RspInfo:{ErrorID},{ErrorMsg}";
        }
    }
}
