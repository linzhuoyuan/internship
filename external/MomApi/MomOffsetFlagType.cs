using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomOffsetFlagType
    {
        private MomOffsetFlagType() {}

        [Description("Open")]
        public const byte Open = (byte)'0';//48
        [Description("Close")]
        public const byte Close = (byte)'1';//49
        [Description("CloseToday")]
        public const byte CloseToday = (byte)'2';//50
        [Description("AutoOpenClose")]
        public const byte AutoOpenClose = (byte)'3';//51
    }
}
