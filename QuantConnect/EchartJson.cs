
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuantConnect
{
    /// <summary>
    /// ChartJson Object for Custom Charting
    /// </summary>
    [JsonObject]
    public class EchartJson
    {
        public DateTime Time;

        /// List of Jsons Objects for this Chart:
        public List<string> Jsons = new List<string>();

        /// <summary>
        /// Default constructor for chart:
        /// </summary>
        public EchartJson()
        {
        }

        /// <summary>
        /// Constructor for a ChartJson
        /// </summary>
        /// <param name="time">the time of the chart</param>
        /// <param name="jsons">json string of the chart</param>
        public EchartJson(DateTime time, List<string> jsons)
        {
            Time = time;
            Jsons = jsons;
        }

    }
}
