using System.ComponentModel;

namespace MomCrypto.Api
{
    public sealed class MomOffsetFlagType
    {
        private MomOffsetFlagType() {}

        [Description("Open")]
        public const byte Open = (byte)'0';
        [Description("Close")]
        public const byte Close = (byte)'1';
        [Description("CloseToday")]
        public const byte CloseToday = (byte)'2';
        [Description("AutoOpenClose")]
        public const byte AutoOpenClose = (byte)'3';
    }
}
