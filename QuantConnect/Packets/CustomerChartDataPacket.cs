﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
*/

using System.Collections.Concurrent;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuantConnect.Packets
{
    /// <summary>
    /// customer chart data from the lean engine.
    /// </summary>
    public class CustomerChartDataPacket : Packet
    {
        /// <summary>
        /// customer chart data
        /// </summary>
        [JsonProperty(PropertyName = "Results")]
        public List<CustomerChartData> Results = new List<CustomerChartData>();

        /// <summary>
        /// Default constructor for JSON
        /// </summary>
        public CustomerChartDataPacket()
            : base (PacketType.CustomerChart)
        { }

        /// <summary>
        /// Create a new instance of the customer chart data packet:
        /// </summary>
        public CustomerChartDataPacket(List<CustomerChartData> customerChartDatas)
            : base(PacketType.CustomerChart)
        {
             Results = customerChartDatas;
        }
    
    }

}