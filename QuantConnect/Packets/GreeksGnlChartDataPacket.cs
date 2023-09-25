using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuantConnect.Packets
{
    /// <summary>
    /// 
    /// </summary>
    public class GreeksGnlChartDataPacket : Packet
    {
        /// <summary>
        /// customer chart data
        /// </summary>
        [JsonProperty(PropertyName = "Results")]
        public List<GreeksPnlChartData> Results = new List<GreeksPnlChartData>();

        /// <summary>
        /// Default constructor for JSON
        /// </summary>
        public GreeksGnlChartDataPacket()
            : base(PacketType.GreeksPnl)
        { }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="datas"></param>
        public GreeksGnlChartDataPacket(List<GreeksPnlChartData> datas)
            : base(PacketType.GreeksPnl)
        {
            Results = datas;
        }
    }
}
