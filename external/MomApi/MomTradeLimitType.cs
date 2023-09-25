using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomTradeLimitType
    {
        [Description(nameof(TradeLimitNone))] //48
        public const int TradeLimitNone = 0;

        [Description(nameof(TradeLimitAllow))] //49
        public const int TradeLimitAllow = 1;

        [Description(nameof(TradeLimitReject))] //50
        public const int TradeLimitReject = 2;
    }
}
