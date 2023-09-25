using Newtonsoft.Json;

namespace FtxApi.Rest.Models
{
    public class OptionPosition
    {
        [JsonProperty("netSize")]
        public decimal NetSize;
        [JsonProperty("entryPrice")]
        public decimal EntryPrice;
        [JsonProperty("size")]
        public decimal Size;
        [JsonProperty("option")]
        public FtxOption? Option;
        [JsonProperty("side")]
        public string? Side;
        [JsonProperty("pessimisticValuation")]
        public decimal? PessimisticValuation;
        [JsonProperty("pessimisticIndexPrice")]
        public decimal? PessimisticIndexPrice;
        [JsonProperty("pessimisticVol")]
        public decimal? PessimisticVol;
    }
}