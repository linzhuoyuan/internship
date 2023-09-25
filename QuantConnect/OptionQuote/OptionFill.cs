using System;
using QuantConnect.Orders;

namespace QuantConnect.OptionQuote
{
    public class OptionFill
    {
        public long Id;
        
        public decimal Size;
        
        public decimal Price;

        public OptionInfo Option;
        
        public DateTime Time;
        /// <summary>
        /// taker or maker
        /// </summary>
        public string Liquidity;
        
        public decimal Fee;
        
        public decimal FeeRate;

        public OrderDirection Side;
    }
}