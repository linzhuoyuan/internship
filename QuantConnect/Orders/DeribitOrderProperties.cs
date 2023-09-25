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

using QuantConnect.Interfaces;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Contains additional properties and settings for an order submitted to Bitfinex brokerage
    /// </summary>
    public class DeribitOrderProperties : OrderProperties
    {
        /// <summary>
        /// Specifies how long the order remains in effect. Default "good_til_cancelled"
        // "good_til_cancelled" - unfilled order remains in order book until cancelled
        // "fill_or_kill" - execute a transaction immediately and completely or not at all
        // "immediate_or_cancel" - execute a transaction immediately, and any portion of the order that cannot be immediately filled is cancelled
        /// </summary>
        public TimeInForceFlag TimeInForceFlag { get; set; }
        /// <summary>
        // If true, the order is considered post-only.If the new price would cause the order to be filled immediately(as taker), the price will be changed to be just below the bid.
        // Only valid in combination with time_in_force="good_til_cancelled"
        /// </summary>
        public bool PostFlag { get;  set; }
        /// <summary>
        //If true, the order is considered reduce-only which is intended to only reduce a current position
        /// </summary>
        public bool ReduceFlag { get;  set; }

        public DeribitOrderProperties()
        {
            TimeInForce = TimeInForce.GoodTilCanceled;
            TimeInForceFlag = TimeInForceFlag.GoodTilCancelled;
            PostFlag = false;
            ReduceFlag = false;
        }

        /// <summary>
        /// Returns a new instance clone of this object
        /// </summary>
        public override IOrderProperties Clone()
        {
            return (DeribitOrderProperties)MemberwiseClone();
        }
    }
}
