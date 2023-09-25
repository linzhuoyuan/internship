using System.ComponentModel;

namespace MomCrypto.Api
{
    public sealed class MomProductClassType
    {
        private MomProductClassType() { }

        [Description("All")]
        public const byte All = (byte)'0';
        [Description("Futures")]
        public const byte Futures = (byte)'1';
        [Description("Options")]
        public const byte Options = (byte)'2';
        [Description("Stock")]
        public const byte Stock = (byte)'3';
        [Description("FuturesOptions")]
        public const byte FuturesOptions = (byte)'4';
        [Description("IndexOptions")]
        public const byte IndexOptions = (byte)'5';
        [Description("Index")]
        public const byte Index = (byte)'6';
        [Description("Etf")]
        public const byte Etf = (byte)'7';
        [Description("Crypto")]
        public const byte Crypto = (byte)'8';
        [Description("CoinFutures")]
        public const byte CoinFutures = (byte)'9';
        [Description("CoinOptions")]
        public const byte CoinOptions = (byte)'a';

        public static int GetIndex(byte value)
        {
            return value - All;
        }
    }
}
