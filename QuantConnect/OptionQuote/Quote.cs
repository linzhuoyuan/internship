using System;
using QuantConnect.Orders;

namespace QuantConnect.OptionQuote
{
    public class RequestResult<T>
    {
        public bool Success;
        public T Result;
        public string Error;
        
        public RequestResult() 
        {
        }

        public RequestResult(T data, string error = null)
        {
            Result = data;
            Error = error;
            Success = Error == null;
        }
    }

    public class Quote
    {
        public decimal Collateral;
        public string Id;
        public OptionInfo? Option;
        public decimal Price;
        public DateTime? QuoteExpiry;
        public OrderDirection? QuoterSide;
        public string? RequestId;
        public OrderDirection? RequestSide;
        public decimal? Size;
        public OrderStatus Status;
        public DateTime Time;

        public override string ToString()
        {
            return $"{Id},{Price},{Size},{Status},collateral:{Collateral},expiry:{QuoteExpiry:MM/dd HH:mm:ss}";
        }
    }
}