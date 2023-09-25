using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomOrderPriceTypeType
    {
        private MomOrderPriceTypeType() { }

        [Description(nameof(AnyPrice))]
        public const byte AnyPrice = (byte)'1';
        [Description(nameof(LimitPrice))]
        public const byte LimitPrice = (byte)'2';
        [Description(nameof(BestPrice))]
        public const byte BestPrice = (byte)'3';
        [Description(nameof(LastPrice))]
        public const byte LastPrice = (byte)'4';
        [Description("Last+1")]
        public const byte LastPricePlusOneTicks = (byte)'5';
        [Description("Last+2")]
        public const byte LastPricePlusTwoTicks = (byte)'6';
        [Description("Last+3")]
        public const byte LastPricePlusThreeTicks = (byte)'7';
        [Description(nameof(AskPrice1))]
        public const byte AskPrice1 = (byte)'8';
        [Description("Ask+1")]
        public const byte AskPrice1PlusOneTicks = (byte)'9';
        [Description("Ask+2")]
        public const byte AskPrice1PlusTwoTicks = (byte)'A';
        [Description("Ask+3")]
        public const byte AskPrice1PlusThreeTicks = (byte)'B';
        [Description(nameof(BidPrice1))]
        public const byte BidPrice1 = (byte)'C';
        [Description("Bid+1")]
        public const byte BidPrice1PlusOneTicks = (byte)'D';
        [Description("Bid+2")]
        public const byte BidPrice1PlusTwoTicks = (byte)'E';
        [Description("Bid+3")]
        public const byte BidPrice1PlusThreeTicks = (byte)'F';
        [Description(nameof(FiveLevelPrice))]
        public const byte FiveLevelPrice = (byte)'G';
        [Description(nameof(StopLimit))]
        public const byte StopLimit = (byte)'H';
        [Description(nameof(StopMarket))]
        public const byte StopMarket = (byte)'I';
        [Description(nameof(MarketIfTouched))]
        public const byte MarketIfTouched = (byte)'J';
        [Description(nameof(LimitIfTouched))]
        public const byte LimitIfTouched = (byte)'K';
        [Description(nameof(TrailingStop))]
        public const byte TrailingStop = (byte)'L';
        [Description(nameof(TrailingStopLimit))]
        public const byte TrailingStopLimit = (byte)'M';
    }
}
