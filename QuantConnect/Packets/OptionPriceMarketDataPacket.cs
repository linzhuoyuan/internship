using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using QuantConnect.Data.Market;

namespace QuantConnect.Packets
{
    /// <summary>
    /// 
    /// </summary>
    public class OptionPriceMarketDataPacket : Packet
    {
        [JsonProperty(PropertyName = "Results")]
        public List<OptionPriceMarketData> Results = new List<OptionPriceMarketData>();

        /// <summary>
        /// 
        /// </summary>
        public OptionPriceMarketDataPacket()
            : base(PacketType.OptionPriceMarketData)
        {
           
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="datas"></param>
        public OptionPriceMarketDataPacket(List<OptionPriceMarketData> datas)
            : base(PacketType.OptionPriceMarketData)
        {
            Results = datas;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class OptionPriceMarketData
    {
        /// 
        public string Market { get; set; }
        /// 
        public string Symbol { get; set; }
        /// 
        public string UnderlyingSymbol { get; set; }
        /// 
        public DateTime Time { get; set; }
        /// 
        public DateTime Expiry { get; set; }
        /// 
        public Decimal Price { get; set; }
        /// 
        public Decimal UnderlyingPrice { get; set; }
        /// 
        public string Right { get; set; }
        /// 
        public decimal Strike { get; set; }
        /// 
        public string OptionStyle { get; set; }
        /// 
        public decimal Holding { get; set; }
        /// 
        public bool Updated { get; set; }
        ///
        public decimal ContractMultiplier { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public OptionPriceMarketData()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contract"></param>
        public OptionPriceMarketData(OptionContract contract)
        {
            decimal price = 0;
            if (contract.LastPrice == 0)
            {
                int count = 0;
                if (contract.BidPrice > 0)
                {
                    count++;
                    price += contract.BidPrice;
                }

                if (contract.AskPrice > 0)
                {
                    count++;
                    price += contract.AskPrice;
                }

                price /= count;
            }
            else
            {
                price = contract.LastPrice;
            }

            Market = contract.Symbol.ID.Market;
            Symbol = contract.Symbol.Value;
            UnderlyingSymbol = contract.UnderlyingSymbol.Value;
            Time = contract.Time;
            Expiry = contract.Expiry;
            Price = price;//contract.LastPrice;
            UnderlyingPrice = contract.UnderlyingLastPrice;
            Right = contract.Right == OptionRight.Call? "call":"put";
            Strike = contract.Strike;
            OptionStyle = contract.Symbol.ID.OptionStyle== QuantConnect.OptionStyle.American? "american": "european";
            Holding = 0;
            Updated = true;
            
        }

        //public OptionPriceMarketDataPacket(string json)
        //    : base(PacketType.OptionPriceMarketData)
        //{
        //    try
        //    {
        //        var packet = JsonConvert.DeserializeObject<OptionPriceMarketDataPacket>(json);
        //        Market = packet.Market;
        //        Symbol = packet.Symbol;
        //        UnderlyingSymbol = packet.UnderlyingSymbol;
        //        Time = packet.Time;
        //        Expiry = packet.Expiry;
        //        Price = packet.Price;
        //        UnderlyingPrice = packet.UnderlyingPrice;
        //        Right = packet.Right;
        //        Strike = packet.Strike;
        //        OptionStyle = packet.OptionStyle;
        //    }
        //    catch (Exception err)
        //    {
        //        //Log.Trace("LiveResultPacket(): Error converting json: " + err);
        //    }
        //}
    }
}
