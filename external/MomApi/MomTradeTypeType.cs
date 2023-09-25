using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomTradeTypeType
    {
        [Description(nameof(Common))]
        public const byte Common = (byte)'0';

        [Description(nameof(Execution))]
        public const byte Execution = (byte)'1';

        [Description(nameof(Expiration))]
        public const byte Expiration = (byte)'2';

        [Description(nameof(External))]
        public const byte External = (byte)'3';
    }
}