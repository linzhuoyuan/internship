using Newtonsoft.Json;

namespace FtxApi.Rest.Models
{
    public class AccountLeverage
    {
        [JsonProperty("leverage")]
        public int Leverage { get; set; }
    }
}
