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
using Calculators.Margins;
using QuantConnect.Orders;
using QuantConnect.Util;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents a buying power model for FTX
    /// </summary>
    public class FTXBuyingPowerModel : BuyingPowerModel
    {
        private IFTXMarginModel _ftxMarginModel;
        private bool _enableSpotMargin;

        public FTXBuyingPowerModel(bool enableSpotMargin)
        {
            _ftxMarginModel = FTXMarginModelFactory.Resolve(enableSpotMargin);
            _enableSpotMargin = enableSpotMargin;
        }

        /// <summary>
        /// Gets the current leverage of the security
        /// </summary>
        /// <param name="security">The security to get leverage for</param>
        /// <returns>The current leverage in the security</returns>
        // TODO: read leverage from Brokerage!
        public override decimal GetLeverage(Security security)
        {
            return 1 / Converters.InitialMarginRequirement;
        }

        /// <summary>
        /// Sets the leverage for the applicable securities, i.e, equities
        /// </summary>
        /// <remarks>
        /// This is added to maintain backwards compatibility with the old margin/leverage system
        /// </remarks>
        /// <param name="security">The security to set leverage for</param>
        /// <param name="leverage">The new leverage</param>
        public override void SetLeverage(Security security, decimal leverage)
        {
            Converters.InitialMarginRequirement = 1 / leverage;
        }

        /// <summary>
        /// Check if there is sufficient buying power to execute this order.
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the order</param>
        /// <returns>Returns buying power information for an order</returns>
        public override HasSufficientBuyingPowerForOrderResult HasSufficientBuyingPowerForOrder(
            HasSufficientBuyingPowerForOrderParameters parameters)
        {
            var oldPosition = parameters.Security.Symbol.SecurityType == SecurityType.Future 
                              ? parameters.Security.Holdings.Quantity
                              : parameters.Portfolio.CashBook[(parameters.Security as IBaseCurrencySymbol).BaseCurrencySymbol].Amount;
            var newPosition = parameters.Order.Quantity * parameters.Security.Holdings.Quantity > 0
                ? parameters.Order.Quantity
                : Math.Abs(oldPosition) > parameters.Order.AbsoluteQuantity
                    ? 0
                    : parameters.Order.Quantity - oldPosition;
            if (newPosition <= 0)
            {
                return new HasSufficientBuyingPowerForOrderResult(true, "");
            }
            bool isSufficient;
            string reason = string.Empty;
            if (parameters.Security.Symbol.SecurityType == SecurityType.Future || _enableSpotMargin)
            {
                isSufficient = _ftxMarginModel.GetAvailableCollateral(Converters.GetHoldings(parameters.Portfolio)) >=
                               Math.Abs(newPosition) * parameters.Order.Price;
                if (!isSufficient)
                {
                    reason =
                        $"Do not have enough collateral for order {parameters.Order.SymbolValue} quantity: {parameters.Order.Quantity} price: {parameters.Order.Price}!";
                }
            }
            else if (newPosition < 0)
            {
                isSufficient = false;
                reason =
                    $"Do not have enough spot to sell for order {parameters.Order.SymbolValue} quantity: {parameters.Order.Quantity} price: {parameters.Order.Price}!";
            }
            else
            {
                isSufficient = parameters.Portfolio.CashBook[parameters.Portfolio.CashBook.AccountCurrency].Amount >=
                               newPosition * parameters.Order.Price;
                if (!isSufficient)
                {
                    reason =
                        $"Do not have enough {parameters.Portfolio.CashBook.AccountCurrency} to sell for order {parameters.Order.SymbolValue} quantity: {parameters.Order.Quantity} price: {parameters.Order.Price}!";
                }
            }
            //return new HasSufficientBuyingPowerForOrderResult(true, reason);
            return new HasSufficientBuyingPowerForOrderResult(isSufficient, reason);
        }

        /// <summary>
        /// Get the maximum market order quantity to obtain a position with a given value in account currency. Will not take into account buying power.
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the target percentage holdings</param>
        /// <returns>Returns the maximum allowed market order quantity and if zero, also the reason</returns>
        public override GetMaximumOrderQuantityForTargetValueResult GetMaximumOrderQuantityForTargetValue(GetMaximumOrderQuantityForTargetValueParameters parameters)
        {
            var currentHolding = parameters.Security.Type == SecurityType.Future
                ? parameters.Portfolio.Securities[parameters.Security.Symbol].Holdings.Quantity
                : parameters.Portfolio.CashBook[(parameters.Security as IBaseCurrencySymbol).BaseCurrencySymbol].Amount;
            var needPosition = parameters.Target / parameters.Security.Price - currentHolding;

            // add directionality back in
            return new GetMaximumOrderQuantityForTargetValueResult(needPosition);
        }

        /// <summary>
        /// Gets the amount of buying power reserved to maintain the specified position
        /// </summary>
        /// <param name="parameters">A parameters object containing the security</param>
        /// <returns>The reserved buying power in account currency</returns>
        public override ReservedBuyingPowerForPosition GetReservedBuyingPowerForPosition(ReservedBuyingPowerForPositionParameters parameters)
        {
            // TODO: not correnct for spot margin, need to fix holdings in Securities first!
            var initialRequirement = parameters.Security.Type == SecurityType.Future
                ? parameters.Security.Holdings.Quantity * Converters.InitialMarginRequirement
                : 0;
            return parameters.ResultInAccountCurrency(initialRequirement);
        }

        /// <summary>
        /// Gets the buying power available for a trade
        /// </summary>
        /// <param name="parameters">A parameters object containing the algorithm's potrfolio, security, and order direction</param>
        /// <returns>The buying power available for the trade</returns>
        public override BuyingPower GetBuyingPower(BuyingPowerParameters parameters)
        {
            var collateral = _ftxMarginModel.GetAvailableCollateral(Converters.GetHoldings(parameters.Portfolio));

            return parameters.ResultInAccountCurrency(collateral);
        }

        private static decimal GetOrderPrice(Security security, Order order)
        {
            var orderPrice = 0m;
            switch (order.Type)
            {
                case OrderType.Market:
                    orderPrice = security.Price;
                    break;

                case OrderType.Limit:
                    orderPrice = ((LimitOrder)order).LimitPrice;
                    break;

                case OrderType.StopMarket:
                    orderPrice = ((StopMarketOrder)order).StopPrice;
                    break;

                case OrderType.StopLimit:
                    orderPrice = ((StopLimitOrder)order).LimitPrice;
                    break;
            }

            return orderPrice;
        }
    }
}
