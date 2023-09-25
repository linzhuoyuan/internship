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

namespace QuantConnect.Securities
{
    /// <summary>
    /// Represents a cash amount which can be converted to account currency using a currency converter
    /// </summary>
    public struct CashAmount
    {
        /// <summary>
        /// The amount of cash
        /// </summary>
        public decimal Amount { get; }

        /// <summary>
        /// The currency in which the cash amount is denominated
        /// </summary>
        public string Currency { get; }

        /// <summary>
        /// 区分CashBook and UnsettledCashBook的标志
        /// </summary>
        public bool Settled { get; }

        public Security ConversionRateSecurity { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CashAmount"/> class
        /// </summary>
        /// <param name="amount">The amount</param>
        /// <param name="currency">The currency</param>
        /// <param name="settled">The settled</param>
        public CashAmount(decimal amount, string currency, bool settled = true, Security conversionRateSecurity = null)
        {
            if (string.IsNullOrWhiteSpace(currency))
            {
                throw new ArgumentNullException(nameof(currency), "Invalid currency");
            }

            Amount = amount;
            Currency = currency;
            Settled = settled;
            ConversionRateSecurity = conversionRateSecurity;
        }

        /// <summary>
        /// Will determine if two <see cref="CashAmount"/> instances are equal
        /// Useful to compare against the default instance
        /// </summary>
        /// <returns>True if <see cref="Currency"/> and <see cref="Amount"/> are equal</returns>
        public static bool operator ==(CashAmount lhs, CashAmount rhs)
        {
            return Equals(lhs, rhs);
        }

        /// <summary>
        /// Will determine if two <see cref="CashAmount"/> instances are different
        /// Useful to compare against the default instance
        /// </summary>
        /// <returns>True if <see cref="Currency"/> or <see cref="Amount"/> are different</returns>
        public static bool operator !=(CashAmount lhs, CashAmount rhs)
        {
            return !Equals(lhs, rhs);
        }

        /// <summary>
        /// Used to compare two <see cref="CashAmount"/> instances.
        /// Useful to compare against the default instance
        /// </summary>
        /// <param name="obj">The other object to compare with</param>
        /// <returns>True if <see cref="Currency"/> and <see cref="Amount"/> are equal</returns>
        public override bool Equals(object obj)
        {
            if (obj is CashAmount cashAmount)
            {
                return Amount == cashAmount.Amount
                       && Currency == cashAmount.Currency
                       && Settled == cashAmount.Settled;
            }
            return false;
        }

        /// <summary>
        /// Serves as a hash function for a particular type.
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Amount.GetHashCode() * 397) ^ Currency.GetHashCode();
            }
        }
    }
}
