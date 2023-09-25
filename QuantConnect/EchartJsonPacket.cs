using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using QuantConnect.Logging;
using QuantConnect.Packets;

namespace QuantConnect
{
    /// <summary>
    ///echart json 
    /// </summary>
    /// 
    public class EchartJsonPacket : Packet
    {
        /// <summary>
        /// Result data object for this backtest
        /// </summary>
        [JsonProperty(PropertyName = "Results")]
        public EchartJsonResult Results = new EchartJsonResult();

        /// <summary>
        /// Default constructor for JSON Serialization
        /// </summary>
        public EchartJsonPacket()
            : base(PacketType.None)
        { }

        /// <summary>
        /// Compose the packet from a JSON string:
        /// </summary>
        public EchartJsonPacket(string json)
            : base(PacketType.None)
        {
            try
            {
                var packet = JsonConvert.DeserializeObject<EchartJsonPacket>(json, new JsonSerializerSettings {
                    TypeNameHandling = TypeNameHandling.Auto
                });
                Results = packet.Results;
            }
            catch (Exception err)
            {
                Log.Trace("EchartJsonPacket(): Error converting json: " + err);
            }
        }


        /// <summary>
        /// Compose result data packet - with tradable dates from the backtest job task and the partial result packet.
        /// </summary>
        /// <param name="job">Job that started this request</param>
        /// <param name="results">Results class for the Backtest job</param>
        /// <param name="progress">Progress of the packet. For the packet we assume progess of 100%.</param>
        public EchartJsonPacket(EchartJsonResult results)
            : base(PacketType.None)
        {
            try
            {
                Results = results;
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

    }
    public class EchartJsonResult
    {
        /// <summary>
        /// echartJsons adds for the  algorithm since the last result packet
        /// </summary>
        public List<EchartJson> EchartJsons = new List<EchartJson>();

        public EchartJsonResult(List<EchartJson> echartJsons)
        {
            EchartJsons = echartJsons;
        }
            public EchartJsonResult() { }
        }

}
