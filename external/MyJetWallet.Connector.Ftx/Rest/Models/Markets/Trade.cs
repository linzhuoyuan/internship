﻿using System;
using Newtonsoft.Json;

namespace FtxApi.Rest.Models.Markets
{
    public class Trade
    {
        [JsonProperty("id")]
        public decimal Id { get; set; }

        [JsonProperty("liquidation")]
        public bool Liquidation { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("side")]
        public string Side { get; set; }

        [JsonProperty("size")]
        public decimal Size { get; set; }

        [JsonProperty("time")]
        public DateTime Time { get; set; }
    }
}
