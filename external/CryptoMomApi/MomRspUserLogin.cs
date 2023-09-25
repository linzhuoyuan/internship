using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace MomCrypto.Api
{
	[StructLayout(LayoutKind.Sequential)]
	[DataContract]
	public class MomRspUserLogin
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
		[DataMember(Order = 1)]
		public string TradingDay;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
		[DataMember(Order = 2)]
		public string LoginTime;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
		[DataMember(Order = 3)]
		public string BrokerID;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
		[DataMember(Order = 4)]
		public string UserID;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 41)]
		[DataMember(Order = 5)]
		public string SystemName;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 13)]
		[DataMember(Order = 6)]
		public string MaxOrderRef;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
		[DataMember(Order = 7)]
		public string SHFETime;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
		[DataMember(Order = 8)]
		public string DCETime;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
		[DataMember(Order = 9)]
		public string CZCETime;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
		[DataMember(Order = 10)]
		public string FFEXTime;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
		[DataMember(Order = 11)]
		public string INETime;
	}
}
