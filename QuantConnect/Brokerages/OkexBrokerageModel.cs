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
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Orders.Fills;
using QuantConnect.Orders.Fees;
using System.Linq;
using QuantConnect.Util;

namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Provides Okex specific properties
    /// </summary>
    public class OkexBrokerageModel : DefaultBrokerageModel
    {
        private const decimal _maxLeverage = 3.3m;

        /// <summary>
        /// Gets a map of the default markets to be used for each security type
        /// </summary>
        public override IReadOnlyDictionary<SecurityType, string> DefaultMarkets { get; } = GetDefaultMarkets();

        /// <summary>
        /// Initializes a new instance of the <see cref="OkexBrokerageModel"/> class
        /// </summary>
        /// <param name="accountType">The type of account to be modelled, defaults to <see cref="AccountType.Margin"/></param>
        public OkexBrokerageModel(AccountType accountType = AccountType.Margin)
            : base(accountType)
        {
        }

        /// <summary>
        /// Gets a new buying power model for the security, returning the default model with the security's configured leverage.
        /// For cash accounts, leverage = 1 is used.
        /// For margin trading, max leverage = 3.3
        /// </summary>
        /// <param name="security">The security to get a buying power model for</param>
        /// <returns>The buying power model for this brokerage/security</returns>
        public override IBuyingPowerModel GetBuyingPowerModel(Security security)
        {
            //return AccountType == AccountType.Cash
            //    ? (IBuyingPowerModel)new CashBuyingPowerModel()
            //    : new SecurityMarginModel(_maxLeverage);

            // 根据SecurityType 进行扩展, 币币账户使用现金账户类型的购买力模型，其他类型使用保证金模型（SecurityMarginModel 进行扩展）
            switch (security.Type)
            {
                case SecurityType.Crypto:
                    return (IBuyingPowerModel)new CashBuyingPowerModel();
                case SecurityType.Option:
                    return (IBuyingPowerModel) new Securities.Option.OkexOptionMarginModel();
                case SecurityType.Future:
                    return (IBuyingPowerModel)new Securities.Future.OkexFutureMarginModel();
                default:
                    throw new Exception($"Invalid security type: {security.Type}");
            }
        }

        /// <summary>
        /// Bitfinex global leverage rule
        /// </summary>
        /// <param name="security"></param>
        /// <returns></returns>
        public override decimal GetLeverage(Security security)
        {
            if (AccountType == AccountType.Cash || security.IsInternalFeed())
            {
                return 1m;
            }

            switch (security.Type)
            {
                case SecurityType.Crypto:
                    return 1;
                case SecurityType.Option:
                    return _maxLeverage;
                case SecurityType.Future:
                    return _maxLeverage;
                default:
                    throw new Exception($"Invalid security type: {security.Type}");
            }
        }

        /// <summary>
        /// Provides Bitfinex fee model
        /// </summary>
        /// <param name="security"></param>
        /// <returns></returns>
        public override IFeeModel GetFeeModel(Security security)
        {
            return new OkexFeeModel();
        }

        private static IReadOnlyDictionary<SecurityType, string> GetDefaultMarkets()
        {
            var map = DefaultMarketMap.ToDictionary();
            map[SecurityType.Crypto] = Market.Okex;
            map[SecurityType.Future] = Market.Okex;
            map[SecurityType.Option] = Market.Okex;
            return map.ToReadOnlyDictionary();
        }
    }
}