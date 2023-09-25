using System;
using Newtonsoft.Json;

namespace FtxApi.Rest.Models
{
    public class OptionQuote
    {
        [JsonProperty("collateral")]
        public decimal Collateral;
        [JsonProperty("id")]
        public string? Id;
        [JsonProperty("option")]
        public FtxOption? Option;
        [JsonProperty("price")]
        public decimal Price;
        [JsonProperty("quoteExpiry")]
        public DateTime? QuoteExpiry;
        [JsonProperty("quoterSide")]
        public string? QuoterSide;
        [JsonProperty("requestId")]
        public string? RequestId;
        [JsonProperty("requestSide")]
        public string? RequestSide;
        [JsonProperty("size")]
        public decimal? Size;
        [JsonProperty("status")]
        public string Status = null!;
        [JsonProperty("time")]
        public DateTime Time;

        public override string ToString()
        {
            return $"{Id},{Price},{Size},{Status},collateral:{Collateral},expiry:{QuoteExpiry:MM/dd HH:mm:ss}";
        }
    }
}