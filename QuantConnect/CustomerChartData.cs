/*
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
*/

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuantConnect
{
    /// <summary>
    /// 
    /// </summary>
    public class CustomerChartData
    {

        /// name
        public string Name
        { get; set; }

        /// id
        public int Id
        { get; set; }

        /// data time
        public DateTime DataTime
        { get; set; }

        ///Max Value
        public double MaxValue
        { get; set; }

        /// Min Value
        public double MinValue
        { get; set; }

        ///Max X Value
        public double MaxXValue
        { get; set; }

        /// Min X Value
        public double MinXValue
        { get; set; }

        /// xLableName
        public string XLableName
        { get; set; }

        /// yLableName
        public string YLableName
        { get; set; }

        /// y1LableName
        public string Y1LableName
        { get; set; }

        /// x
        public double[] XList
        { get; set; }

        /// y
        public double[] YList
        { get; set; }

        /// y1
        public double[] Y1List
        { get; set; }

        /// <summary>
        /// Section
        /// </summary>
        public double Section
        { get; set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public CustomerChartData() { }

        /// <summary>
        /// Constructor that takes both x, y value paris
        /// </summary>
        /// <param name="dataTime">data time</param>
        /// <param name="xLableName">X value often representing a time in seconds</param>
        /// <param name="yLableName">Y value</param>
        /// <param name="y1LableName">Y1 value</param>
        /// <param name="minValue">Y minValue</param>
        /// <param name="maxValue">Y maxValue</param>
        /// <param name="minXValue">Y minValue</param>
        /// <param name="maxXValue">Y maxValue</param>
        public CustomerChartData(DateTime dataTime, string xLableName, string yLableName, string y1LableName, double minValue = 0, double maxValue = 1, double minXValue = 0, double maxXValue = 10, double section = 0d)
        {
            MinValue = minValue;
            MaxValue = maxValue;
            DataTime = dataTime;
            XLableName = xLableName;
            YLableName = yLableName;
            Y1LableName = y1LableName;
            MinXValue = minXValue;
            MaxXValue = maxXValue;
            Section = section;
        }

        ///Cloner Constructor:
        public CustomerChartData(CustomerChartData customerChartData)
        {
            Name = customerChartData.Name;
            MaxValue = customerChartData.MaxValue;
            MinValue = customerChartData.MinValue;
            DataTime = customerChartData.DataTime;
            XLableName = customerChartData.XLableName;
            YLableName = customerChartData.YLableName;
            Y1LableName = customerChartData.Y1LableName;
            MaxXValue = customerChartData.MaxXValue;
            MinXValue = customerChartData.MinXValue;
            Section = customerChartData.Section;
        }

    }
}
