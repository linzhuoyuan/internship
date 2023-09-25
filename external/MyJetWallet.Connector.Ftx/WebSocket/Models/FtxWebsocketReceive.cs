using Newtonsoft.Json;

namespace FtxApi.WebSocket.Models
{
    public class FtxWebsocketReceive
    {
        public const string Partial = "partial";
        public const string Update = "update";

        [JsonProperty("channel")]
        public string Channel { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("market")]
        public string Market { get; set; }

        [JsonProperty("msg")]
        public string ErrorMessage { get; set; }
    }

    public class FtxWebsocketReceive<T> : FtxWebsocketReceive
    {
        [JsonProperty("data")]
        public T Data { get; set; }
    }

    public class DataAction<T> 
    {
        [JsonProperty("data")]
        public T Data { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }
    }
}