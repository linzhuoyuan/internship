using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomTradeSourceType
    {
        [Description(nameof(Exchange))]
        public const byte Exchange = (byte)'0';
        [Description(nameof(Manual))]
        public const byte Manual = (byte)'1';
    }
}