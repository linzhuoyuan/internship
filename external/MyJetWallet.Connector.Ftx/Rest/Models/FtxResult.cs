using Newtonsoft.Json;

namespace FtxApi.Rest.Models
{
    public class FtxResult<T>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("result")]
        public T Result { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }

        [JsonIgnore] 
        public string? Request { get; set; }
    }
}
