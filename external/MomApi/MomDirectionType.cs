using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomDirectionType
    {
        private MomDirectionType() { }

        [Description("Buy")]
        public const byte Buy = 48;
        [Description("Sell")]
        public const byte Sell = 49;
        [Description("FinancingBuy")]
        public const byte FinancingBuy = 50;
        [Description("RepaymentSell")]
        public const byte RepaymentSell = 51;
    }
}