using Newtonsoft.Json;
using QuantConnect.Orders;

namespace QuantConnect.Packets
{
    /// <summary>
    /// 
    /// </summary>
    public class TradeRecordPacket : Packet
    {
        /// <summary>
        /// Order event object
        /// </summary>
        [JsonProperty(PropertyName = "oTradeRecord")]
        public TradeRecord Trade;

        /// <summary>
        /// Algorithm id for this order event
        /// </summary>
        [JsonProperty(PropertyName = "sAlgorithmID")]
        public string AlgorithmId;

        /// <summary>
        /// Default constructor for JSON
        /// </summary>
        public TradeRecordPacket()
            : base(PacketType.TradeRecord)
        { }

        /// <summary>
        /// Create a new instance of the order event packet
        /// </summary>
        public TradeRecordPacket(string algorithmId, TradeRecord trade)
            : base(PacketType.TradeRecord)
        {
            AlgorithmId = algorithmId;
            Trade = trade;
        }
    }
}
