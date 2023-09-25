using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomProductClassType
    {
        [Description("All")]
        public const byte All = (byte)'0'; //48
        [Description("Futures")]
        public const byte Futures = (byte)'1'; //49
        [Description("Options")]
        public const byte Options = (byte)'2'; //50
        [Description("Stocks")]
        public const byte Stocks = (byte)'3'; //51
        [Description("FuturesOptions")]
        public const byte FuturesOptions = (byte)'4'; //52
        [Description("IndexOptions")]
        public const byte IndexOptions = (byte)'5'; //53
        [Description("Index")]
        public const byte Index = (byte)'6'; //54
        [Description("Etf")]
        public const byte Etf = (byte)'7'; //55

        public static int GetIndex(byte value)
        {
            return value - All;
        }
    }
}
