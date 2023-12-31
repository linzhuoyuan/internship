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
*/

using System;
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Limit order type definition
    /// </summary>
    public class LimitOrder : Order
    {
        internal decimal limitPrice;
        internal decimal limitPriceAdvance;
        internal string advance;

        /// <summary>
        /// Limit price for this order.
        /// </summary>
        public decimal LimitPrice
        {
            get => limitPrice;
            internal set => limitPrice = value;
        }


        /// <summary>
        /// Limit Order Type
        /// </summary>
        public override OrderType Type => OrderType.Limit;

        /// <summary>
        /// Added a default constructor for JSON Deserialization:
        /// </summary>
        public LimitOrder()
        {
        }

        /// <summary>
        /// New limit order constructor
        /// </summary>
        /// <param name="symbol">Symbol asset we're seeking to trade</param>
        /// <param name="quantity">Quantity of the asset we're seeking to trade</param>
        /// <param name="time">Time the order was placed</param>
        /// <param name="limitPrice">Price the order should be filled at if a limit order</param>
        /// <param name="tag">User defined data tag for this order</param>
        /// <param name="properties">The order properties for this order</param>
        /// <param name="limitPriceAdvence"></param>
        /// <param name="advence"></param>
        public LimitOrder(
            Symbol symbol,
            decimal quantity,
            decimal limitPrice,
            DateTime time,
            string tag = "",
            IOrderProperties properties = null,
            decimal limitPriceAdvance = 0m,
            string advance = "")
            : base(symbol, quantity, time, tag, properties)
        {
            this.limitPrice = limitPrice;
            this.limitPriceAdvance = limitPriceAdvance;
            this.advance = advance;

            if (tag == "")
            {
                //Default tag values to display limit price in GUI.
                Tag = "Limit Price: " + limitPrice.ToString("C");
            }
        }

        /// <summary>
        /// Gets the order value in units of the security's quote currency
        /// </summary>
        /// <param name="security">The security matching this order's symbol</param>
        protected override decimal GetValueImpl(Security security)
        {
            // selling, so higher price will be used
            if (quantity < 0)
            {
                return quantity * Math.Max(limitPrice, security.Price);
            }

            // buying, so lower price will be used
            if (quantity > 0)
            {
                return quantity * Math.Min(limitPrice, security.Price);
            }

            return 0m;
        }

        /// <summary>
        /// Modifies the state of this order to match the update request
        /// </summary>
        /// <param name="request">The request to update this order object</param>
        public override void ApplyUpdateOrderRequest(UpdateOrderRequest request)
        {
            base.ApplyUpdateOrderRequest(request);
            if (request.limitPrice.HasValue)
            {
                LimitPrice = request.limitPrice.Value;
            }
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return $"{base.ToString()} at limit {LimitPrice.SmartRounding()}";
        }

        /// <summary>
        /// Creates a deep-copy clone of this order
        /// </summary>
        /// <returns>A copy of this order</returns>
        public override Order Clone()
        {
            var order = new LimitOrder {
                limitPrice = limitPrice,
                LimitPriceAdvance = limitPriceAdvance,
                advance = advance
            };
            CopyTo(order);
            return order;
        }


        ///=================================================================================
        /// <summary>
        /// 
        /// </summary>
        public decimal LimitPriceAdvance
        {
            get => limitPriceAdvance;
            internal set => limitPriceAdvance = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public string Advance
        {
            get => advance;
            internal set => advance = value;
        }

        /// <summary>
        /// 
        /// </summary>
        public bool IsAdvance => Advance != "" && LimitPriceAdvance != 0;

        public override void ApplyUpdateAdvanceOrder(decimal price)
        {
            Price = price;
        }

    }
}
