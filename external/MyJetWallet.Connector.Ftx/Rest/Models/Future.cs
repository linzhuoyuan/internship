using System;
using Newtonsoft.Json;

namespace FtxApi.Rest.Models
{
    public class Future
    {
        /// <summary>
        /// best ask on the orderbook
        /// </summary>
        [JsonProperty("ask")]
        public decimal? Ask { get; set; }

        /// <summary>
        /// best bid on the orderbook
        /// </summary>
        [JsonProperty("bid")]
        public decimal? Bid { get; set; }

        /// <summary>
        /// price change in the last hour
        /// </summary>
        [JsonProperty("change1h")]
        public decimal Change1H { get; set; }

        /// <summary>
        /// price change in the last 24 hours
        /// </summary>
        [JsonProperty("change24h")]
        public decimal Change24H { get; set; }

        /// <summary>
        /// price change since midnight UTC (beginning of day)
        /// </summary>
        [JsonProperty("changeBod")]
        public decimal ChangeBod { get; set; }

        /// <summary>
        /// USD volume in the last 24 hours
        /// </summary>
        [JsonProperty("volumeUsd24h")]
        public decimal VolumeUsd24H { get; set; }

        /// <summary>
        /// quantity traded in the last 24 hours
        /// </summary>
        [JsonProperty("volume")]
        public decimal Volume { get; set; }

        [JsonProperty("description")]
        public string? Description { get; set; }

        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("expired")]
        public bool Expired { get; set; }

        [JsonProperty("expiry")]
        public DateTime? Expiry { get; set; }

        /// <summary>
        /// average of the Market Prices for the constituent markets in the index
        /// </summary>
        [JsonProperty("index")]
        public decimal Index { get; set; }

        [JsonProperty("imfFactor")]
        public decimal ImfFactor { get; set; }

        /// <summary>
        /// last price the future traded at
        /// </summary>
        [JsonProperty("last")]
        public decimal? Last { get; set; }

        /// <summary>
        /// the lowest price the future can trade at
        /// </summary>
        [JsonProperty("lowerBound")]
        public decimal LowerBound { get; set; }

        /// <summary>
        /// mark price of the future
        /// </summary>
        [JsonProperty("mark")]
        public decimal Mark { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        /// <summary>
        /// whether or not this is a perpetual contract
        /// </summary>
        [JsonProperty("perpetual")]
        public bool Perpetual { get; set; }

        [JsonProperty("positionLimitWeight")]
        public decimal PositionLimitWeight { get; set; }

        [JsonProperty("postOnly")]
        public bool PostOnly { get; set; }

        [JsonProperty("priceIncrement")]
        public decimal PriceIncrement { get; set; }

        [JsonProperty("sizeIncrement")]
        public decimal SizeIncrement { get; set; }

        [JsonProperty("underlying")]
        public string? Underlying { get; set; }

        /// <summary>
        /// the highest price the future can trade at
        /// </summary>
        [JsonProperty("upperBound")]
        public decimal UpperBound { get; set; }

        /// <summary>
        /// One of future, perpetual, or move
        /// </summary>
        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("moveStart")]
        public string? MoveStart { get; set; }
    }
}