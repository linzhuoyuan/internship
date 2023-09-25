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
using QuantConnect.Orders.Fills;
using QuantConnect.Securities;


namespace QuantConnect.Orders.Fees
{
    /// <summary>
    /// Provides the default implementation of <see cref="IFeeModel"/>
    /// </summary>
    public class SseBrokersFeeModel : FeeModel
    {
        // option commission function takes number of contracts and the size of the option premium and returns total commission
        private const decimal _optionFeePerUnit = 2.3m;
        private readonly Dictionary<string, Func<decimal, decimal, CashAmount>> _optionFee = new();

        private readonly Dictionary<string, EquityFee> _equityFee =
            new()
            {
                { Market.SSE, new EquityFee("USD", minimumFee: 5, feeRate: 0.0003m) },
                { Market.SZSE, new EquityFee("USD", minimumFee: 5, feeRate: 0.0003m) },
                { Market.HKG, new EquityFee("USD", minimumFee: 5, feeRate: 0.0003m) },
                { Market.HKA, new EquityFee("USD", minimumFee: 5, feeRate: 0.0003m) },
                { Market.USA, new EquityFee("USD", minimumFee: 0.35m, feeRate: 0.0035m) }
            };

        private readonly Dictionary<string, FutureFee> _futureFee =
            new()
            {
                { Market.CFFEX, new FutureFee("USD", minimumFee: 0, feeRate: 0.00003m) }
            };

        /// <summary>
        /// Initializes a new instance of the <see cref="SseBrokersFeeModel"/>
        /// </summary>
        public SseBrokersFeeModel()
        {
            ProcessOptionsRateSchedule(out var optionsCommissionFunc);
            _optionFee.Add(Market.SSE, optionsCommissionFunc);
            _optionFee.Add(Market.SZSE, optionsCommissionFunc);
            _optionFee.Add(Market.HKG, optionsCommissionFunc);
            _optionFee.Add(Market.USA, optionsCommissionFunc);
        }

        /// <summary>
        /// Gets the order fee associated with the specified order. This returns the cost
        /// of the transaction in the account currency
        /// </summary>
        /// <param name="parameters">A <see cref="OrderFeeParameters"/> object
        /// containing the security and order</param>
        /// <returns>The cost of the order in units of the account currency</returns>
        public override OrderFee GetOrderFee(OrderFeeParameters parameters)
        {
            var order = parameters.Order;
            var security = parameters.Security;

            // Option exercise for equity options is free of charge
            if (order.Type == OrderType.OptionExercise)
            {
                var optionOrder = (OptionExerciseOrder)order;

                if (optionOrder.Symbol.id.SecurityType == SecurityType.Option &&
                    optionOrder.Symbol.ID.Underlying.SecurityType == SecurityType.Equity)
                {
                    return OrderFee.Zero;
                }
            }

            decimal feeResult;
            string feeCurrency;
            var market = security.Symbol.ID.Market;
            switch (security.Type)
            {
                case SecurityType.Option:
                    if (!_optionFee.TryGetValue(market, out var optionsCommissionFunc))
                    {
                        throw new Exception($"SseBrokersFeeModel(): unexpected option Market {market}");
                    }
                    //期权手续费规则调整  期权卖出开仓不收取手续费
                    //writer: lh
                    if (order.Offset == OrderOffset.Open && order.Direction == OrderDirection.Sell)
                    {
                        return OrderFee.Zero;
                    }

                    // applying commission function to the order
                    var optionFee = optionsCommissionFunc(order.AbsoluteQuantity, order.Price);
                    feeResult = optionFee.Amount;
                    feeCurrency = optionFee.Currency;
                    break;

                case SecurityType.Future:
                    if (market is Market.Globex or Market.NYMEX or Market.CBOT or Market.ICE or Market.CBOE or Market.NSE)
                    {
                        // just in case...
                        market = Market.USA;
                    }

                    if (!_futureFee.TryGetValue(market, out var futureFee))
                    {
                        throw new Exception($"SseBrokersFeeModel(): unexpected future Market {market}");
                    }
                    var futureTradeValue = Math.Abs(order.GetValue(security));

                    var futureTradeFee = futureFee.FeeRate * futureTradeValue;

                    if (futureTradeFee < futureFee.MinimumFee)
                    {
                        futureTradeFee = futureFee.MinimumFee;
                    }

                    feeCurrency = futureFee.Currency;
                    //Always return a positive fee.
                    feeResult = Math.Abs(futureTradeFee);
                    break;

                case SecurityType.Equity:
                    if (!_equityFee.TryGetValue(market, out var equityFee))
                    {
                        throw new Exception($"SseBrokersFeeModel(): unexpected equity Market {market}");
                    }
                    var tradeValue = Math.Abs(order.GetValue(security));

                    var tradeFee = equityFee.FeeRate * tradeValue;

                    if (tradeFee < equityFee.MinimumFee)
                    {
                        tradeFee = equityFee.MinimumFee;
                    }

                    feeCurrency = equityFee.Currency;
                    //Always return a positive fee.
                    feeResult = Math.Abs(tradeFee);
                    break;

                default:
                    // unsupported security type
                    throw new ArgumentException($"Unsupported security type: {security.Type}");
            }

            return new OrderFee(new CashAmount(
                feeResult,
                feeCurrency));
        }

        /// <summary>
        /// Determines which tier an account falls into based on the monthly trading volume
        /// </summary>
        private static void ProcessOptionsRateSchedule(out Func<decimal, decimal, CashAmount> optionsCommissionFunc)
        {
            optionsCommissionFunc = (orderSize, premium) => new CashAmount(orderSize * _optionFeePerUnit, Currencies.USD);
        }

        /// <summary>
        /// Helper class to handle Equity fees
        /// </summary>
        private class EquityFee
        {
            public string Currency { get; }
            public decimal MinimumFee { get; }
            public decimal FeeRate { get; }

            public EquityFee(string currency,
                decimal minimumFee,
                decimal feeRate)
            {
                Currency = currency;
                MinimumFee = minimumFee;
                FeeRate = feeRate;
            }
        }

        private class FutureFee
        {
            public string Currency { get; }
            public decimal MinimumFee { get; }
            public decimal FeeRate { get; }

            public FutureFee(string currency,
                decimal minimumFee,
                decimal feeRate)
            {
                Currency = currency;
                MinimumFee = minimumFee;
                FeeRate = feeRate;
            }
        }
    }
}
