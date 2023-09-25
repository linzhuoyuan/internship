using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomPosiDirectionType
    {
        [Description("Net")]
        public const byte Net = 49;
        [Description("Long")]
        public const byte Long = 50;
        [Description("Short")]
        public const byte Short = 51;
    }
}
