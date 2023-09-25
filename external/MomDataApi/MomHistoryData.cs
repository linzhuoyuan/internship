using System;

namespace MomCrypto.DataApi
{
    public class MomHistoryData
    {
        //
        // 摘要:
        //     The time this candlestick opened
        public DateTime OpenTime { get; set; }
        //
        // 摘要:
        //     The price at which this candlestick opened
        public decimal Open { get; set; }
        //
        // 摘要:
        //     The highest price in this candlestick
        public decimal High { get; set; }
        //
        // 摘要:
        //     The lowest price in this candlestick
        public decimal Low { get; set; }
        //
        // 摘要:
        //     The price at which this candlestick closed
        public decimal Close { get; set; }
        //
        // 摘要:
        //     The volume traded during this candlestick
        public decimal BaseVolume { get; set; }
        //
        // 摘要:
        //     The close time of this candlestick
        public DateTime CloseTime { get; set; }
        //
        // 摘要:
        //     The volume traded during this candlestick in the asset form
        public decimal QuoteVolume { get; set; }
        //
        // 摘要:
        //     The amount of trades in this candlestick
        public int TradeCount { get; set; }
        //
        // 摘要:
        //     Taker buy base asset volume
        public decimal TakerBuyBaseVolume { get; set; }
        //
        // 摘要:
        //     Taker buy quote asset volume
        public decimal TakerBuyQuoteVolume { get; set; }
    }
}
