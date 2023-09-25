using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomContingentConditionType
    {
        private MomContingentConditionType() { }

        [Description(nameof(Immediately))]
        public const byte Immediately = (byte)'1';
        [Description(nameof(Touch))]
        public const byte Touch = (byte)'2';
        [Description(nameof(TouchProfit))]
        public const byte TouchProfit = (byte)'3';
        [Description(nameof(ParkedOrder))]
        public const byte ParkedOrder = (byte)'4';
        [Description("Last>Stop")]
        public const byte LastPriceGreaterThanStopPrice = (byte)'5';
        [Description("Last>=Stop")]
        public const byte LastPriceGreaterEqualStopPrice = (byte)'6';
        [Description("Last<Stop")]
        public const byte LastPriceLesserThanStopPrice = (byte)'7';
        [Description("Last<=Stop")]
        public const byte LastPriceLesserEqualStopPrice = (byte)'8';
        [Description("Ask>Stop")]
        public const byte AskPriceGreaterThanStopPrice = (byte)'9';
        [Description("Ask>=Stop")]
        public const byte AskPriceGreaterEqualStopPrice = (byte)'A';
        [Description("Ask<Stop")]
        public const byte AskPriceLesserThanStopPrice = (byte)'B';
        [Description("Ask<=Stop")]
        public const byte AskPriceLesserEqualStopPrice = (byte)'C';
        [Description("Bid>Stop")]
        public const byte BidPriceGreaterThanStopPrice = (byte)'D';
        [Description("Bid>=Stop")]
        public const byte BidPriceGreaterEqualStopPrice = (byte)'E';
        [Description("Bid<Stop")]
        public const byte BidPriceLesserThanStopPrice = (byte)'F';
        [Description("Bid<=Stop")]
        public const byte BidPriceLesserEqualStopPrice = (byte)'G';
    }
}
