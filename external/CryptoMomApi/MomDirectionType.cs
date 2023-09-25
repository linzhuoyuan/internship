using System.ComponentModel;

namespace MomCrypto.Api
{
    public sealed class MomDirectionType
    {
        private MomDirectionType() { }

        [Description("Buy")]
        public const byte Buy = 48;
        [Description("Sell")]
        public const byte Sell = 49;
    }
}