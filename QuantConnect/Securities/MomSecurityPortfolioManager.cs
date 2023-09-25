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

namespace QuantConnect.Securities
{
    /// <summary>
    /// Portfolio manager class groups popular properties and makes them accessible through one interface.
    /// It also provide indexing by the vehicle symbol to get the Security.Holding objects.
    /// </summary>
    public class MomSecurityPortfolioManager : SecurityPortfolioManager
    {

        private bool _isMomTotalPortfolioValueValid;
        /// <summary>
        /// Initialise security mom-portfolio manager.
        /// </summary>
        public MomSecurityPortfolioManager(
            SecurityManager securityManager,
            SecurityTransactionManager transactions,
            IOrderProperties defaultOrderProperties = null)
            : base(securityManager, transactions, defaultOrderProperties) { }

        /// <summary>
        /// Gets the remaining margin on the account in the account's currency
        /// for the given total portfolio value
        /// 剩余保证金 = 总资产 - 持仓市值 - 未结算账单 - 总维持保证金 
        /// </summary>
        /// <remarks>
        /// This method is for performance, for when the user already knows
        /// the total portfolio value, we can avoid re calculating it. Else use
        /// <param name="totalPortfolioValue">The total portfolio value <see cref="TotalPortfolioValue"/></param>
        /// </remarks>
        public override decimal GetMarginRemaining(decimal totalPortfolioValue)
        {
            if (!_isMomTotalPortfolioValueValid)
            {
                decimal totalHoldingsValueWithoutForexCryptoFutureCfd = 0;
                foreach (var kvp in Securities)
                {
                    var position = kvp.Value;
                    var securityType = position.Type;
                    // we can't include forex in this calculation since we would be double accounting with respect to the cash book
                    // we also exclude futures and CFD as they are calculated separately
                    if (securityType != SecurityType.Forex && securityType != SecurityType.Crypto &&
                        securityType != SecurityType.Future && securityType != SecurityType.Cfd)
                    {
                        totalHoldingsValueWithoutForexCryptoFutureCfd += (position.Holdings.HoldingsValue + position.LongHoldings.HoldingsValue + position.ShortHoldings.HoldingsValue);
                    }
                }
                totalPortfolioValue -= totalHoldingsValueWithoutForexCryptoFutureCfd;
                _isMomTotalPortfolioValueValid = true;
            }
            return totalPortfolioValue - UnsettledCashBook.TotalValueInAccountCurrency - TotalMarginUsed;
        }

        /// <summary>
        /// Will flag the current TotalPortfolioValue as invalid
        /// so it is recalculated when gotten
        /// </summary>
        public override void InvalidateTotalPortfolioValue(string cash = "")
        {
            _isTotalPortfolioValueValid = false;
            _isMomTotalPortfolioValueValid = false;
        }

        public override decimal TotalMarginUsed
        {
            get
            {
                decimal sum = 0;
                foreach (var pair in Securities.Listed())
                {
                    var security = pair.Value;
                    if (!security.HoldStock || security.Symbol.SecurityType != SecurityType.Option)
                    {
                        continue;
                    }
                    var context = new ReservedBuyingPowerForPositionParameters(security);
                    var reservedBuyingPower = security.BuyingPowerModel.GetReservedBuyingPowerForPosition(context);
                    //持仓成本* 维持保证金
                    sum += reservedBuyingPower.Value;
                }
                return sum;
            }
        }
    }
}
