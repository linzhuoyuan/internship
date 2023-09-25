using System.ComponentModel;

namespace MomCrypto.Api
{
    public sealed class MomVolumeConditionType
    {
        private MomVolumeConditionType() { }

        [Description(nameof(AnyVolume))]
        public const byte AnyVolume = (byte)'1';

        [Description(nameof(MinVolume))]
        public const byte MinVolume = (byte)'2';

        [Description(nameof(CompleteVolume))]
        public const byte CompleteVolume = (byte)'3';
    }
}