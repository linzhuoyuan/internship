using System;
using Newtonsoft.Json;

namespace FtxApi.Rest.Models
{
    public class QuoteRequest
    {
        [JsonProperty("id")]
        public string? Id;
        [JsonProperty("option")]
        public FtxOption Option = null!;
        [JsonProperty("side")]
        public string Side = null!;
        [JsonProperty("size")]
        public decimal Size;
        [JsonProperty("time")]
        public DateTime Time;
        [JsonProperty("requestExpiry")]
        public DateTime RequestExpiry;
        [JsonProperty("status")]
        public string Status = null!;
        [JsonProperty("hideLimitPrice")]
        public bool? HideLimitPrice;
        [JsonProperty("limitPrice")]
        public decimal? LimitPrice;
        [JsonProperty("quotes")]
        public OptionQuote[]? Quotes;

        public override string ToString()
        {
            return $"{Id},{Side},{Option},{LimitPrice ?? 0},{Status},expiry:{RequestExpiry:MM/dd HH:mm:ss}";
        }
    }
}