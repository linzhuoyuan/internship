using System;
using System.Collections.Generic;
using QLNet;
using QuantConnect.Orders;

namespace QuantConnect.OptionQuote
{
    public class QuoteRequest
    {
        public static QuoteRequest Empty = new();
        public string Id;
        public OptionInfo Option;
        public OrderDirection? Side;
        public decimal Size;
        public DateTime Time;
        public DateTime RequestExpiry;
        public OrderStatus Status;
        public bool? HideLimitPrice;
        public decimal? LimitPrice;
        public List<Quote> Quotes = new();

        public override string ToString()
        {
            return $"{Id},{Side},{Option},{LimitPrice ?? 0},{Status},expiry:{RequestExpiry:MM/dd HH:mm:ss}";
        }
    }
}