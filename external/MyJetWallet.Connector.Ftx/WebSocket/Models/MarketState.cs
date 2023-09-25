using System;

// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace FtxApi.WebSocket.Models
{
    public class MarketState
    {
        public const string SpotType = "spot";
        public const string FutureType = "future";

        public string name {get; set; }
        public bool enabled {get; set; }
        public bool postOnly {get; set; }
        public double priceIncrement {get; set; }
        public double sizeIncrement {get; set; }
        public string type {get; set; }
        public string baseCurrency {get; set; }
        public string quoteCurrency {get; set; }
        public bool restricted {get; set; }
        public string underlying {get; set; }
        public bool highLeverageFeeExempt {get; set; }
        public FutureState future { get; set; }

        public string id { get; set; }


        public class FutureState
        {
            public string name { get; set; }
            public string underlying { get; set; }
            public string description { get; set; }
            public string type { get; set; }
            public DateTime? expiry { get; set; }
            public bool perpetual { get; set; }
            public bool expired { get; set; }
            public bool enabled { get; set; }
            public bool postOnly { get; set; }
            public double imfFactor { get; set; }
            public string underlyingDescription { get; set; }
            public string expiryDescription { get; set; }
            public DateTime? moveStart { get; set; }
            public double positionLimitWeight { get; set; }
            public string group { get; set; }
        }
    }



}