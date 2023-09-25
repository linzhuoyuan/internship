using System.ComponentModel;

namespace MomCrypto.Api
{
    public sealed class MomStopWorkingTypeType
    {
        [Description(nameof(MarkPrice))]
        public const byte MarkPrice = (byte)'1';
        [Description(nameof(ContractPrice))]
        public const byte ContractPrice = (byte)'2';
        [Description(nameof(IndexPrice))]
        public const byte IndexPrice = (byte)'3';
    }
}