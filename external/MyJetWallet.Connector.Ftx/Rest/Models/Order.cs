﻿using System;
using Newtonsoft.Json;

namespace FtxApi.Rest.Models
{
    public class Order
    {
        [JsonProperty("createdAt")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("filledSize")]
        public decimal? FilledSize { get; set; }

        [JsonProperty("future")]
        public string Future { get; set; }

        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("market")]
        public string Market { get; set; }

        [JsonProperty("price")]
        public decimal? Price { get; set; }

        [JsonProperty("avgFillPrice")]
        public decimal? AvgFillPrice { get; set; }

        [JsonProperty("remainingSize")]
        public decimal? RemainingSize { get; set; }

        [JsonProperty("side")]
        public string Side { get; set; }

        [JsonProperty("size")]
        public decimal? Size { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("reduceOnly")]
        public bool ReduceOnly { get; set; }

        [JsonProperty("ioc")]
        public bool Ioc { get; set; }

        [JsonProperty("postOnly")]
        public bool PostOnly { get; set; }

        [JsonProperty("clientId")]
        public string ClientId { get; set; }
    }
}
