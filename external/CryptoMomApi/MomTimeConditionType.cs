using System.ComponentModel;

namespace MomCrypto.Api
{
    public sealed class MomTimeConditionType
    {
        [Description(nameof(IOC))]
        public const byte IOC = (byte)'1';
        [Description(nameof(GFS))]
        public const byte GFS = (byte)'2';
        [Description(nameof(GFD))]
        public const byte GFD = (byte)'3';
        [Description(nameof(GTD))]
        public const byte GTD = (byte)'4';
        [Description(nameof(GTC))]
        public const byte GTC = (byte)'5';
        [Description(nameof(GFA))]
        public const byte GFA = (byte)'6';
        [Description(nameof(FOK))]
        public const byte FOK = (byte)'7';
    }
}
