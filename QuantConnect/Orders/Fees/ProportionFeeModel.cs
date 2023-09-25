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
using QuantConnect.Securities;

namespace QuantConnect.Orders.Fees
{
    /// <summary>
    /// Provides an order fee model that returns proportionally order fee.
    /// </summary>
    class ProportionFeeModel : FeeModel
    {
        /// <summary>
        /// 
        /// </summary>
        private readonly decimal _rate;
        private readonly string _currency;

        /// </summary>
        /// <param name="fee">The constant order fee used by the model</param>
        /// <param name="currency">The currency of the order fee</param>
        public ProportionFeeModel(decimal rate, string currency = "USD")
        {
            _rate = Math.Abs(rate);
            _currency = currency;
        }

        /// <summary>
        /// Returns the constant fee for the model in units of the account currency
        /// </summary>
        /// <param name="parameters">A <see cref="OrderFeeParameters"/> object
        /// containing the security and order</param>
        /// <returns>The cost of the order in units of the account currency</returns>
        public override OrderFee GetOrderFee(OrderFeeParameters parameters)
        {
            decimal price = 0m;
            if (parameters.Order.Type == OrderType.Market)
            {
                price = parameters.Security.Price;
            }
            if(parameters.Order.Type == OrderType.Limit)
            {
                var limitOrder = parameters.Order as LimitOrder;
                price = limitOrder.LimitPrice;
            }

            var amount = price * Math.Abs(parameters.Order.Quantity) * _rate;
            return new OrderFee(new CashAmount(amount, AblSymbolDatabase.GetFeeCurrency(parameters, _currency)));
        }
    }
}
