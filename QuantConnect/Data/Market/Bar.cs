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

using System.Threading;

namespace QuantConnect.Data.Market
{
    /// <summary>
    /// Base Bar Class: Open, High, Low, Close and Period.
    /// </summary>
    public class Bar : IBar
    {
        internal decimal open;
        internal decimal high;
        internal decimal low;
        internal decimal close;
        internal decimal forwardAdjustFactor = 1m;
        internal decimal backwardAdjustFactor = 1m;

        /// <summary>
        /// Opening price of the bar: Defined as the price at the start of the time period.
        /// </summary>
        public decimal Open
        {
            get => open;
            set => open = value;
        }

        /// <summary>
        /// High price of the bar during the time period.
        /// </summary>
        public decimal High
        {
            get => high;
            set => high = value;
        }

        /// <summary>
        /// Low price of the bar during the time period.
        /// </summary>
        public decimal Low
        {
            get => low;
            set => low = value;
        }

        /// <summary>
        /// Closing price of the bar. Defined as the price at Start Time + TimeSpan.
        /// </summary>
        public decimal Close
        {
            get => close;
            set => close = value;
        }

        public decimal ForwardAdjustFactor => forwardAdjustFactor;

        public decimal BackwardAdjustFactor => backwardAdjustFactor;

        public (decimal open, decimal high, decimal low, decimal close) GetData()
        {
            return (open, high, low, close);
        }

        /// <summary>
        /// Default initializer to setup an empty bar.
        /// </summary>
        public Bar()
        {
        }

        /// <summary>
        /// Initializer to setup a bar with a given information.
        /// </summary>
        /// <param name="open">Decimal Opening Price</param>
        /// <param name="high">Decimal High Price of this bar</param>
        /// <param name="low">Decimal Low Price of this bar</param>
        /// <param name="close">Decimal Close price of this bar</param>
        public Bar(decimal open, decimal high, decimal low, decimal close)
        {
            this.open = open;
            this.high = high;
            this.low = low;
            this.close = close;
        }

        /// <summary>
        /// Updates the bar with a new value. This will aggregate the OHLC bar
        /// </summary>
        /// <param name="value">The new value</param>
        public void Update(decimal value)
        {
            // Do not accept zero as a new value
            if (value == 0) return;

            if (open == 0) open = high = low = close = value;
            if (value > high) high = value;
            if (value < low) low = value;
            close = value;
        }

        /// <summary>
        /// Returns a clone of this bar
        /// </summary>
        public Bar Clone()
        {
            return new Bar(open, high, low, close);
        }
    }
}