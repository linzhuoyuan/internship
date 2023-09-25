using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomOrderSourceType
    {
        [Description(nameof(Strategy))]
        public const byte Strategy = (byte)'0';
        [Description(nameof(Manual))]
        public const byte Manual = (byte)'1';
        [Description(nameof(Admin))]
        public const byte Admin = (byte)'2';
    }
}