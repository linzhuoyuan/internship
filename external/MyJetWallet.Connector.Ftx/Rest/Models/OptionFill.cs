using System;
using Newtonsoft.Json;

namespace FtxApi.Rest.Models
{
    public class OptionFill
    {
        [JsonProperty("id")]
        public long Id;
        
        [JsonProperty("size")]
        public decimal Size;
        
        [JsonProperty("price")]
        public decimal Price;

        [JsonProperty("option")]
        public FtxOption? Option;
        
        [JsonProperty("time")]
        public DateTime Time;

        [JsonProperty("liquidity")]
        public string? Liquidity;
        
        [JsonProperty("fee")]
        public decimal Fee;
        
        [JsonProperty("feeRate")]
        public decimal FeeRate;

        [JsonProperty("side")]
        public string Side = null!;
    }
}