using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [StructLayout(LayoutKind.Sequential)]
    [DataContract]
    public class MomInstrumentMarginRate
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 31)]
        [DataMember(Order = 1)]
        public string InstrumentID;

        [DataMember(Order = 2)]
        public byte InvestorRange;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
        [DataMember(Order = 3)]
        public string BrokerID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 13)]
        [DataMember(Order = 4)]
        public string InvestorID;

        [DataMember(Order = 5)]
        public byte HedgeFlag;

        [DataMember(Order = 6)]
        public decimal LongMarginRatioByMoney;

        [DataMember(Order = 7)]
        public decimal LongMarginRatioByVolume;

        [DataMember(Order = 8)]
        public decimal ShortMarginRatioByMoney;

        [DataMember(Order = 9)]
        public decimal ShortMarginRatioByVolume;

        [DataMember(Order = 10)]
        public int IsRelative;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 9)]
        [DataMember(Order = 11)]
        public string ExchangeID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 17)]
        [DataMember(Order = 12)]
        public string InvestUnitID;
    }
}
