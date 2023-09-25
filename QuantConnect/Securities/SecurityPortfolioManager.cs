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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Fasterflect;
using Python.Runtime;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Python;
using QuantConnect.Securities.Option;
using Dividend = QuantConnect.Data.Market.Dividend;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Portfolio manager class groups popular properties and makes them accessible through one interface.
    /// It also provide indexing by the vehicle symbol to get the Security.Holding objects.
    /// </summary>
    public class SecurityPortfolioManager : IDictionary<Symbol, SecurityHolding>, ISecurityProvider
    {
        // flips to true when the user called SetCash(), if true, SetAccountCurrency will throw
        private bool _setCashWasCalled;
        protected bool _isTotalPortfolioValueValid;
        protected decimal _totalPortfolioValue;


        /// <summary>
        /// Local access to the securities collection for the portfolio summation.
        /// </summary>
        public SecurityManager Securities;

        /// <summary>
        /// Local access to the transactions collection for the portfolio summation and updates.
        /// </summary>
        public SecurityTransactionManager Transactions;

        /// <summary>
        /// Gets the cash book that keeps track of all currency holdings (only settled cash)
        /// </summary>
        public CashBook CashBook { get; }

        /// <summary>
        /// Gets the cash book that keeps track of all currency holdings (only unsettled cash)
        /// </summary>
        public CashBook UnsettledCashBook { get; }

        /// <summary>
        /// The list of pending funds waiting for settlement time
        /// </summary>
        private readonly List<UnsettledCashAmount> _unsettledCashAmounts;

        // The _unsettledCashAmounts list has to be synchronized because order fills are happening on a separate thread
        private readonly object _unsettledCashAmountsLocker = new object();

        // Record keeping variables
        private Cash _baseCurrencyCash;
        private Cash _baseCurrencyUnsettledCash;

        /// <summary>
        /// Initialise security portfolio manager.
        /// </summary>
        public SecurityPortfolioManager(
            SecurityManager securityManager,
            SecurityTransactionManager transactions,
            IOrderProperties defaultOrderProperties = null)
        {
            Securities = securityManager;
            Transactions = transactions;
            MarginCallModel = new DefaultMarginCallModel(this, defaultOrderProperties);

            CashBook = new CashBook();
            UnsettledCashBook = new CashBook();
            _unsettledCashAmounts = new List<UnsettledCashAmount>();
            _baseCurrencyCash = CashBook[CashBook.AccountCurrency];
            _baseCurrencyUnsettledCash = UnsettledCashBook[CashBook.AccountCurrency];

            // default to $100,000.00
            //_baseCurrencyCash.SetAmount(100000);

            CashBook.Updated += (sender, args) => InvalidateTotalPortfolioValue(args.CashEventArgs.Cash);
            UnsettledCashBook.Updated += (sender, args) => InvalidateTotalPortfolioValue();
            _pnlData = new GreekPnlData();
        }

        #region IDictionary Implementation

        /// <summary>
        /// Add a new securities string-security to the portfolio.
        /// </summary>
        /// <param name="symbol">Symbol of dictionary</param>
        /// <param name="holding">SecurityHoldings object</param>
        /// <exception cref="NotImplementedException">Portfolio object is an adaptor for Security Manager. This method is not applicable for PortfolioManager class.</exception>
        /// <remarks>This method is not implemented and using it will throw an exception</remarks>
        public void Add(Symbol symbol, SecurityHolding holding)
        {
            throw new NotImplementedException("Portfolio object is an adaptor for Security Manager. To add a new asset add the required data during initialization.");
        }

        /// <summary>
        /// Add a new securities key value pair to the portfolio.
        /// </summary>
        /// <param name="pair">Key value pair of dictionary</param>
        /// <exception cref="NotImplementedException">Portfolio object is an adaptor for Security Manager. This method is not applicable for PortfolioManager class.</exception>
        /// <remarks>This method is not implemented and using it will throw an exception</remarks>
        public void Add(KeyValuePair<Symbol, SecurityHolding> pair) { throw new NotImplementedException("Portfolio object is an adaptor for Security Manager. To add a new asset add the required data during initialization."); }

        /// <summary>
        /// Clear the portfolio of securities objects.
        /// </summary>
        /// <exception cref="NotImplementedException">Portfolio object is an adaptor for Security Manager. This method is not applicable for PortfolioManager class.</exception>
        /// <remarks>This method is not implemented and using it will throw an exception</remarks>
        public void Clear()
        {
            throw new NotImplementedException("Portfolio object is an adaptor for Security Manager and cannot be cleared.");
        }

        /// <summary>
        /// Remove this key-value pair from the portfolio.
        /// </summary>
        /// <exception cref="NotImplementedException">Portfolio object is an adaptor for Security Manager. This method is not applicable for PortfolioManager class.</exception>
        /// <param name="pair">Key value pair of dictionary</param>
        /// <remarks>This method is not implemented and using it will throw an exception</remarks>
        public bool Remove(KeyValuePair<Symbol, SecurityHolding> pair)
        {
            throw new NotImplementedException("Portfolio object is an adaptor for Security Manager and objects cannot be removed.");
        }

        /// <summary>
        /// Remove this symbol from the portfolio.
        /// </summary>
        /// <exception cref="NotImplementedException">Portfolio object is an adaptor for Security Manager. This method is not applicable for PortfolioManager class.</exception>
        /// <param name="symbol">Symbol of dictionary</param>
        /// <remarks>This method is not implemented and using it will throw an exception</remarks>
        public bool Remove(Symbol symbol)
        {
            throw new NotImplementedException("Portfolio object is an adaptor for Security Manager and objects cannot be removed.");
        }

        /// <summary>
        /// Check if the portfolio contains this symbol string.
        /// </summary>
        /// <param name="symbol">String search symbol for the security</param>
        /// <returns>Boolean true if portfolio contains this symbol</returns>
        public bool ContainsKey(Symbol symbol)
        {
            return Securities.ContainsKey(symbol);
        }

        /// <summary>
        /// Check if the key-value pair is in the portfolio.
        /// </summary>
        /// <remarks>IDictionary implementation calling the underlying Securities collection</remarks>
        /// <param name="pair">Pair we're searching for</param>
        /// <returns>True if we have this object</returns>
        public bool Contains(KeyValuePair<Symbol, SecurityHolding> pair)
        {
            return Securities.ContainsKey(pair.Key);
        }

        /// <summary>
        /// Count the securities objects in the portfolio.
        /// </summary>
        /// <remarks>IDictionary implementation calling the underlying Securities collection</remarks>
        public int Count => Securities.Count;

        /// <summary>
        /// Check if the underlying securities array is read only.
        /// </summary>
        /// <remarks>IDictionary implementation calling the underlying Securities collection</remarks>
        public bool IsReadOnly => Securities.IsReadOnly;

        /// <summary>
        /// Copy contents of the portfolio collection to a new destination.
        /// </summary>
        /// <remarks>IDictionary implementation calling the underlying Securities collection</remarks>
        /// <param name="array">Destination array</param>
        /// <param name="index">Position in array to start copying</param>
        public void CopyTo(KeyValuePair<Symbol, SecurityHolding>[] array, int index)
        {
            array = new KeyValuePair<Symbol, SecurityHolding>[Securities.Count];
            var i = 0;
            foreach (var asset in Securities)
            {
                if (i >= index)
                {
                    array[i] = new KeyValuePair<Symbol, SecurityHolding>(asset.Key, asset.Value.Holdings);
                }
                i++;
            }
        }

        /// <summary>
        /// Symbol keys collection of the underlying assets in the portfolio.
        /// </summary>
        /// <remarks>IDictionary implementation calling the underlying securities key symbols</remarks>
        public ICollection<Symbol> Keys => Securities.Keys;

        /// <summary>
        /// Collection of securities objects in the portfolio.
        /// </summary>
        /// <remarks>IDictionary implementation calling the underlying securities values collection</remarks>
        public ICollection<SecurityHolding> Values =>
            (from kvp in Securities
             select kvp.Value.Holdings).ToList();

        /// <summary>
        /// Attempt to get the value of the securities holding class if this symbol exists.
        /// </summary>
        /// <param name="symbol">String search symbol</param>
        /// <param name="holding">Holdings object of this security</param>
        /// <remarks>IDictionary implementation</remarks>
        /// <returns>Boolean true if successful locating and setting the holdings object</returns>
        public bool TryGetValue(Symbol symbol, out SecurityHolding holding)
        {
            var success = Securities.TryGetValue(symbol, out var security);
            holding = success ? security.Holdings : null;
            return success;
        }

        /// <summary>
        /// Get the enumerator for the underlying securities collection.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        /// <returns>Enumerable key value pair</returns>
        IEnumerator<KeyValuePair<Symbol, SecurityHolding>> IEnumerable<KeyValuePair<Symbol, SecurityHolding>>.GetEnumerator()
        {
            return Securities.Select(x => new KeyValuePair<Symbol, SecurityHolding>(x.Key, x.Value.Holdings)).GetEnumerator();
        }

        /// <summary>
        /// Get the enumerator for the underlying securities collection.
        /// </summary>
        /// <remarks>IDictionary implementation</remarks>
        /// <returns>Enumerator</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return Securities.Select(x => new KeyValuePair<Symbol, SecurityHolding>(x.Key, x.Value.Holdings)).GetEnumerator();
        }

        #endregion

        /// <summary>
        /// Sum of all currencies in account in US dollars (only settled cash)
        /// </summary>
        /// <remarks>
        /// This should not be mistaken for margin available because Forex uses margin
        /// even though the total cash value is not impact
        /// </remarks>
        public decimal Cash => CashBook.TotalValueInAccountCurrency;

        /// <summary>
        /// Sum of all currencies in account in US dollars (only unsettled cash)
        /// </summary>
        /// <remarks>
        /// This should not be mistaken for margin available because Forex uses margin
        /// even though the total cash value is not impact
        /// </remarks>
        public decimal UnsettledCash => UnsettledCashBook.TotalValueInAccountCurrency;

        /// <summary>
        /// Absolute value of cash discounted from our total cash by the holdings we own.
        /// </summary>
        /// <remarks>When account has leverage the actual cash removed is a fraction of the purchase price according to the leverage</remarks>
        public decimal TotalUnleveredAbsoluteHoldingsCost =>
            //Sum of unlevered cost of holdings
            (from kvp in Securities
             select kvp.Value.Holdings.UnleveredAbsoluteHoldingsCost
                    + kvp.Value.ShortHoldings.UnleveredAbsoluteHoldingsCost
                    + kvp.Value.LongHoldings.UnleveredAbsoluteHoldingsCost).Sum();

        /// <summary>
        /// Gets the total absolute holdings cost of the portfolio. This sums up the individual
        /// absolute cost of each holding
        /// </summary>
        public decimal TotalAbsoluteHoldingsCost
        {
            get
            {
                return Securities.Listed().Aggregate(
                    0m,
                    (d, pair) => d + pair.Value.Holdings.AbsoluteHoldingsCost
                                   + pair.Value.ShortHoldings.AbsoluteHoldingsCost
                                   + pair.Value.LongHoldings.AbsoluteHoldingsCost);
            }
        }

        /// <summary>
        /// Absolute sum the individual items in portfolio.
        /// </summary>
        public decimal TotalHoldingsValue =>
            //Sum sum of holdings
            (from kvp in Securities.Listed()
             select kvp.Value.Holdings.AbsoluteHoldingsValue
                    + kvp.Value.ShortHoldings.AbsoluteHoldingsValue
                    + kvp.Value.LongHoldings.AbsoluteHoldingsValue).Sum();

        /// <summary>
        /// Boolean flag indicating we have any holdings in the portfolio.
        /// </summary>
        /// <remarks>Assumes no asset can have $0 price and uses the sum of total holdings value</remarks>
        /// <seealso cref="Invested"/>
        public bool HoldStock => TotalHoldingsValue > 0;

        /// <summary>
        /// Alias for HoldStock. Check if we have and holdings.
        /// </summary>
        /// <seealso cref="HoldStock"/>
        public bool Invested => HoldStock;

        /// <summary>
        /// Get the total unrealised profit in our portfolio from the individual security unrealized profits.
        /// </summary>
        public decimal TotalUnrealisedProfit =>
            (from kvp in Securities.Listed()
             select kvp.Value.Holdings.UnrealizedProfit + kvp.Value.LongHoldings.UnrealizedProfit + kvp.Value.ShortHoldings.UnrealizedProfit)
            .Sum();

        /// <summary>
        /// Get the total unrealised profit in our portfolio from the individual security unrealized profits.
        /// </summary>
        /// <remarks>Added alias for American spelling</remarks>
        public decimal TotalUnrealizedProfit => TotalUnrealisedProfit;

        public decimal GetMarketValue(Dictionary<string, Holding> holdings, SecurityType type)
        {
            if (holdings.Count == 0)
                return 0;

            decimal count = 0;
            foreach (var holding in holdings.Values)
            {
                if (holding.Type == type)
                {
                    count += holding.MarketValue;
                }
            }

            return count;
        }

        private readonly GreekPnlData _pnlData;
        public GreeksPnlChartData CalcGreekPnl(Slice slice)
        {
            var holdings = GetAllHolding();
            if (holdings.Count == 0)
                return null;

            var charData = new GreeksPnlChartData();
            charData.DataTime = slice.Time;

            foreach (var holding in holdings)
            {
                var holdingPnl = holding.CalcPnl(slice);
                if (holdingPnl == null)
                    continue;

                var holdingPnlData = new HoldingPnlData();
                holdingPnlData.holdingSymbol = holding.Symbol.Value;
                holdingPnlData.holdingType = holding.HoldingType;

                holdingPnlData.CopyData(holdingPnl);
                charData.HoldingsPnl.Add(holdingPnlData);

                _pnlData.Add(holdingPnl);
            }

            charData.CopyData(_pnlData);
            return charData;
        }

        public void SummaryHoldings(Dictionary<string, Holding> holdings)
        {
            if (holdings.Count == 0)
                return;

            var holding = new Holding();
            holding.Symbol = new Symbol(SecurityIdentifier.Empty, "Total");

            foreach (var hold in holdings.Values)
            {
                holding.Quantity += hold.Quantity;
                holding.MarketValue += hold.MarketValue;
                holding.UnrealizedPnL += hold.UnrealizedPnL;
                holding.RealizedPnL += hold.RealizedPnL;
            }

            holdings["Total"] = holding;
        }

        /// <summary>
        /// 期货未实现收益
        /// </summary>
        public decimal GetTotalFutureUnrealisedProfit(Dictionary<string, Holding> holdings, SecurityType future)
        {
            var totalProfit = 0m;
            foreach (var holding in holdings.Values)
            {
                if (holding.Type == SecurityType.Future || holding.Type == SecurityType.Cfd)
                {
                    totalProfit += holding.UnrealizedPnL;
                }
            }
            return totalProfit;
        }

        /// <summary>
        /// Total portfolio value if we sold all holdings at current market rates. 
        /// 结算账户所有剩余资金 + 未结算账户所有剩余资金 + (证券+期权)的持仓市值 + 期货+cfd)盈亏
        /// </summary>
        /// <remarks>Cash + TotalUnrealisedProfit + TotalUnleveredAbsoluteHoldingsCost</remarks>
        /// <seealso cref="Cash"/>
        /// <seealso cref="TotalUnrealizedProfit"/>
        /// <seealso cref="TotalUnleveredAbsoluteHoldingsCost"/>
        public virtual decimal TotalPortfolioValue
        {
            get
            {
                //_isTotalPortfolioValueValid 收到成交单处理后(process fill)置false, 
                if (!_isTotalPortfolioValueValid)
                {
                    decimal totalHoldingsValueWithoutForexCryptoFutureCfd = 0;
                    decimal totalFuturesAndCfdHoldingsValue = 0;
                    foreach (var pair in Securities.Listed())
                    {
                        var security = pair.Value;
                        var securityType = security.Type;
                        // we can't include forex in this calculation since we would be double accounting with respect to the cash book
                        // we also exclude futures and CFD as they are calculated separately
                        if (securityType != SecurityType.Forex && securityType != SecurityType.Crypto &&
                            securityType != SecurityType.Future && securityType != SecurityType.Cfd)
                        {
                            totalHoldingsValueWithoutForexCryptoFutureCfd += (
                                security.holdings.HoldingsValue
                                + security.longHoldings.HoldingsValue
                                + security.shortHoldings.HoldingsValue);
                        }

                        if (securityType is SecurityType.Future or SecurityType.Cfd)
                        {
                            totalFuturesAndCfdHoldingsValue += (
                                security.holdings.UnrealizedProfit
                                + security.longHoldings.UnrealizedProfit
                                + security.shortHoldings.UnrealizedProfit);
                        }
                    }

                    // 结算账户所有剩余资金 + 未结算账户所有剩余资金 + (证券+期权)的持仓市值 + 期货+cfd)盈亏
                    var cash = CashBook.TotalValueInAccountCurrency;
                    _totalPortfolioValue = cash
                                           + UnsettledCashBook.TotalValueInAccountCurrency
                                           + totalHoldingsValueWithoutForexCryptoFutureCfd
                                           + totalFuturesAndCfdHoldingsValue;
                    // Log.Trace($"Update TotalPortfolioValue, HoldingsValue:{totalHoldingsValueWithoutForexCryptoFutureCfd}");
                    // Log.Trace($"Update TotalPortfolioValue, Cash:{cash}");
                    _isTotalPortfolioValueValid = true;
                }

                return _totalPortfolioValue;
            }
        }

        /// <summary>
        /// 只为基于某种货币统计的资产
        /// </summary>
        /// <param name="quoteCurrency"></param>
        /// <returns></returns>
        public virtual decimal GetTotalPortfolioValueForCurrency(string quoteCurrency)
        {
            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="quoteCurrency"></param>
        /// <param name="totalPortfolioValueForSecurity"></param>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public virtual decimal GetMarginRemainingForCurrency(string quoteCurrency, decimal totalPortfolioValueForSecurity, long orderId = long.MaxValue)
        {
            if (UnsettledCashBook.ContainsKey(quoteCurrency))
            {
                totalPortfolioValueForSecurity -= UnsettledCashBook[quoteCurrency].ValueInAccountCurrency;
            }
            return totalPortfolioValueForSecurity - TotalMarginUsedForCurrency(quoteCurrency);
        }

        /// <summary>
        /// Gets the total margin used across all securities in the account's currency 所有security的维持保证金
        /// </summary>
        public virtual decimal TotalMarginUsedForCurrency(string quoteCurrency)
        {
            decimal sum = 0;
            foreach (var pair in Securities.Listed())
            {
                var security = pair.Value;
                if (quoteCurrency == security.quoteCurrency.symbol)
                {
                    var context = new ReservedBuyingPowerForPositionParameters(security);
                    var reservedBuyingPower = security.buyingPowerModel.GetReservedBuyingPowerForPosition(context);
                    //持仓成本* 维持保证金
                    sum += reservedBuyingPower.Value;
                }
            }
            return sum;
        }

        /// <summary>
        /// Will flag the current <see cref="TotalPortfolioValue"/> as invalid
        /// so it is recalculated when gotten
        /// </summary>
        public virtual void InvalidateTotalPortfolioValue(string cash = "")
        {
            _isTotalPortfolioValueValid = false;
            InvalidateTotalPortfolioValueForCurrency(cash);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="security"></param>
        public void InvalidateTotalPortfolioValue(Security security)
        {
            _isTotalPortfolioValueValid = false;

            var quoteCash = security.quoteCurrency;
            InvalidateTotalPortfolioValueForCurrency(quoteCash.symbol);
            if (security.Type == SecurityType.Forex || security.Type == SecurityType.Crypto)
            {
                // model forex fills as currency swaps
                var forex = (IBaseCurrencySymbol)security;
                InvalidateTotalPortfolioValueForCurrency(forex.BaseCurrencySymbol);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void InvalidateTotalPortfolioValueForCurrency(string cash)
        {

        }

        /// <summary>
        /// Total fees paid during the algorithm operation across all securities in portfolio.
        /// </summary>
        public decimal TotalFees =>
            (from pair in Securities.ListedAndTraded()
             select pair.Value.holdings.TotalFees
                    + pair.Value.longHoldings.TotalFees
                    + pair.Value.shortHoldings.TotalFees)
            .Sum();

        /// <summary>
        /// Sum of all gross profit across all securities in portfolio.
        /// </summary>
        public decimal TotalProfit =>
            (from pair in Securities.ListedAndTraded()
             select pair.Value.holdings.Profit
                    + pair.Value.longHoldings.Profit
                    + pair.Value.shortHoldings.Profit).Sum();

        /// <summary>
        /// Total sale volume since the start of algorithm operations.
        /// </summary>
        public decimal TotalSaleVolume =>
            (from pair in Securities.ListedAndTraded()
             select pair.Value.holdings.TotalSaleVolume
                    + pair.Value.longHoldings.TotalSaleVolume
                    + pair.Value.shortHoldings.TotalSaleVolume).Sum();

        /// <summary>
        /// Gets the total margin used across all securities in the account's currency 所有security的维持保证金
        /// </summary>
        public virtual decimal TotalMarginUsed
        {
            get
            {
                decimal sum = 0;
                foreach (var pair in Securities.Listed())
                {
                    var security = pair.Value;
                    var context = new ReservedBuyingPowerForPositionParameters(security);
                    var reservedBuyingPower = security.BuyingPowerModel.GetReservedBuyingPowerForPosition(context);
                    //持仓成本* 维持保证金
                    sum += reservedBuyingPower.Value;
                }
                return sum;
            }
        }

        /// <summary>
        /// Gets the remaining margin on the account in the account's currency
        /// </summary>
        /// <see cref="GetMarginRemaining(decimal)"/>
        public virtual decimal MarginRemaining => GetMarginRemaining(TotalPortfolioValue);

        /// <summary>
        /// Gets the remaining margin on the account in the account's currency
        /// for the given total portfolio value
        /// 剩余保证金 = 总资产 - 未结算账单 - 总维持保证金 
        /// </summary>
        /// <remarks>This method is for performance, for when the user already knows
        /// the total portfolio value, we can avoid re calculating it. Else use
        /// <see cref="MarginRemaining"/></remarks>
        /// <param name="totalPortfolioValue">The total portfolio value <see cref="TotalPortfolioValue"/></param>
        public virtual decimal GetMarginRemaining(decimal totalPortfolioValue)
        {
            return totalPortfolioValue - UnsettledCashBook.TotalValueInAccountCurrency - TotalMarginUsed;
        }

        /// <summary>
        /// Gets or sets the <see cref="MarginCallModel"/> for the portfolio. This
        /// is used to executed margin call orders.
        /// </summary>
        public IMarginCallModel MarginCallModel { get; set; }

        /// <summary>
        /// Indexer for the PortfolioManager class to access the underlying security holdings objects.
        /// </summary>
        /// <param name="symbol">Symbol object indexer</param>
        /// <returns>SecurityHolding class from the algorithm securities</returns>
        public SecurityHolding this[Symbol symbol]
        {
            get => Securities[symbol].holdings;
            set => Securities[symbol].holdings = value;
        }

        /// <summary>
        /// Indexer for the PortfolioManager class to access the underlying security holdings objects.
        /// </summary>
        /// <param name="ticker">string ticker symbol indexer</param>
        /// <returns>SecurityHolding class from the algorithm securities</returns>
        public SecurityHolding this[string ticker]
        {
            get => Securities[ticker].holdings;
            set => Securities[ticker].holdings = value;
        }

        /// <summary>
        /// Sets the account currency cash symbol this algorithm is to manage.
        /// </summary>
        /// <remarks>Has to be called before calling <see cref="SetCash(decimal)"/>
        /// or adding any <see cref="Security"/></remarks>
        /// <param name="accountCurrency">The account currency cash symbol to set</param>
        public void SetAccountCurrency(string accountCurrency)
        {
            if (Securities.Count > 0)
            {
                throw new InvalidOperationException("SecurityPortfolioManager.SetAccountCurrency(): " +
                    "Cannot change AccountCurrency after adding a Security. " +
                    "Please move SetAccountCurrency() before AddSecurity().");
            }

            if (_setCashWasCalled)
            {
                throw new InvalidOperationException("SecurityPortfolioManager.SetAccountCurrency(): " +
                    "Cannot change AccountCurrency after setting cash. " +
                    "Please move SetAccountCurrency() before SetCash().");
            }
            accountCurrency = accountCurrency.LazyToUpper();

            Log.Trace("SecurityPortfolioManager.SetAccountCurrency():" +
                $" setting account currency to {accountCurrency}");

            UnsettledCashBook.AccountCurrency = accountCurrency;
            CashBook.AccountCurrency = accountCurrency;

            _baseCurrencyCash = CashBook[accountCurrency];
            _baseCurrencyUnsettledCash = UnsettledCashBook[accountCurrency];
        }

        /// <summary>
        /// Set the account currency cash this algorithm is to manage.
        /// </summary>
        /// <param name="cash">Decimal cash value of portfolio</param>
        public void SetCash(decimal cash)
        {
            _setCashWasCalled = true;
            _baseCurrencyCash.SetAmount(cash);
        }

        /// <summary>
        /// Set the cash for the specified symbol
        /// </summary>
        /// <param name="symbol">The cash symbol to set</param>
        /// <param name="cash">Decimal cash value of portfolio</param>
        /// <param name="conversionRate">The current conversion rate for the</param>
        public void SetCash(string symbol, decimal cash, decimal conversionRate, Security conversionRateSecurity = null)
        {
            _setCashWasCalled = true;
            if (!CashBook.ContainsKey(symbol))
            {
                CashBook.Add(symbol, cash, conversionRate);

            }

            CashBook.TryGetValue(symbol, out var item);
            item.SetAmount(cash);
            item.ConversionRate = conversionRate;
            item.ConversionRateSecurity = conversionRateSecurity;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="cash"></param>
        /// <param name="conversionRate"></param>
        public void SetUnsettledCash(string symbol, decimal cash, decimal conversionRate, Security conversionRateSecurity = null)
        {
            _setCashWasCalled = true;
            if (!UnsettledCashBook.ContainsKey(symbol))
            {
                UnsettledCashBook.Add(symbol, cash, conversionRate);
            }

            UnsettledCashBook.TryGetValue(symbol, out var item);
            item.SetAmount(cash);
            item.ConversionRate = conversionRate;
            item.ConversionRateSecurity = conversionRateSecurity;
        }

        /// <summary>
        /// Gets the margin available for trading a specific symbol in a specific direction.
        /// </summary>
        /// <param name="symbol">The symbol to compute margin remaining for</param>
        /// <param name="direction">The order/trading direction</param>
        /// <param name="orderId"></param>
        /// <returns>The maximum order size that is currently executable in the specified direction</returns>
        public decimal GetMarginRemaining(Symbol symbol, OrderDirection direction = OrderDirection.Buy, OrderOffset offset = OrderOffset.None, int orderId = 0)
        {
            var security = Securities[symbol];
            var context = new BuyingPowerParameters(this, security, direction, offset, orderId);
            return security.BuyingPowerModel.GetBuyingPower(context).Value;
        }

        /// <summary>
        /// Gets the margin available for trading a specific symbol in a specific direction.
        /// Alias for <see cref="GetMarginRemaining"/>
        /// </summary>A
        /// <param name="symbol">The symbol to compute margin remaining for</param>
        /// <param name="direction">The order/trading direction</param>
        /// <returns>The maximum order size that is currently executable in the specified direction</returns>
        public decimal GetBuyingPower(Symbol symbol, OrderDirection direction = OrderDirection.Buy)
        {
            return GetMarginRemaining(symbol, direction);
        }

        /// <summary>
        /// Calculate the new average price after processing a partial/complete order fill event.
        /// </summary>
        /// <remarks>
        ///     For purchasing stocks from zero holdings, the new average price is the sale price.
        ///     When simply partially reducing holdings the average price remains the same.
        ///     When crossing zero holdings the average price becomes the trade price in the new side of zero.
        /// </remarks>
        public virtual void ProcessFill(OrderEvent fill)
        {
            var security = Securities[fill.Symbol];
            security.PortfolioModel.ProcessFill(this, security, fill);
            InvalidateTotalPortfolioValue(security);
        }

        private void ApplyExDividend(Symbol underlying)
        {
            const decimal multiplier = 10000m;

            foreach (var (symbol, security) in Securities.Listed())
            {
                if (symbol.IsOption() && symbol.Underlying == underlying)
                {
                    //分红调整更新导致合约乘数更新,期权平均持仓成本也要相应的更新
                    //计算公式  averageHoldingsPrice(New) = averageHoldingsPrice(Old） * ContractMultiplier(Old）/ ContractMultiplier(New)

                    var newSymbol = SymbolCache.GetSymbol(symbol.value);
                    var factor = multiplier / newSymbol.SymbolProperties.ContractMultiplier;

                    if (security.LongHoldings.Quantity != 0)
                    {
                        security.LongHoldings.AveragePrice *= factor;
                    }
                    if (security.ShortHoldings.Quantity != 0)
                    {
                        security.ShortHoldings.AveragePrice *= factor;
                    }
                    if (security.Holdings.Quantity != 0)
                    {
                        security.Holdings.AveragePrice *= factor;
                    }
                }
            }
        }

        /// <summary>
        /// Applies a dividend to the portfolio
        /// </summary>
        /// <param name="dividend">The dividend to be applied</param>
        /// <param name="liveMode">True if live mode, false for backtest</param>
        /// <param name="mode">The <see cref="DataNormalizationMode"/> for this security</param>
        public void ApplyDividend(Dividend dividend, bool liveMode, DataNormalizationMode mode)
        {
            // we currently don't properly model dividend payable dates, so in
            // live mode it's more accurate to rely on the brokerage cash sync
            if (liveMode)
            {
                return;
            }

            if (dividend.ExDividend)
            {
                ApplyExDividend(dividend.Symbol);
                var security = Securities[dividend.Symbol];
                security.holdings.DividendRecordQuantity = security.holdings.Quantity;
                var factor = (dividend.Price - dividend.Distribution) / dividend.Price;
                security.holdings.AveragePrice *= factor;

                var total = security.holdings.DividendRecordQuantity * dividend.Distribution;
                _baseCurrencyUnsettledCash.AddAmount(total);

                foreach (var (symbol, option) in Securities.Listed())
                {
                    if (symbol.SecurityType != SecurityType.Option)
                    {
                        continue;
                    }

                    var ticker = $"{symbol.id.contractId}A";
                    if (SymbolCache.TryGetSymbol(ticker, out var optionSymbol))
                    {
                        if (symbol.SymbolProperties.ContractMultiplier !=
                            optionSymbol.SymbolProperties.ContractMultiplier)
                        {
                            symbol.id.StrikePrice = optionSymbol.id.StrikePrice;
                            symbol.identify = optionSymbol.identify;
                            symbol.SymbolProperties.ContractMultiplier = optionSymbol.SymbolProperties.ContractMultiplier;
                            option.SymbolProperties.ContractMultiplier = symbol.SymbolProperties.ContractMultiplier;
                        }
                    }
                }
            }

            if (dividend.PayDividend)
            {
                var security = Securities[dividend.Symbol];
                // only apply dividends when we're in raw mode or split adjusted mode
                if (mode is DataNormalizationMode.Raw or DataNormalizationMode.SplitAdjusted)
                {
                    // longs get benefits, shorts get clubbed on dividends
                    var total = security.holdings.DividendRecordQuantity * dividend.Distribution;
                    security.holdings.DividendRecordQuantity = 0;
                    // assuming USD, we still need to add Currency to the security object
                    _baseCurrencyUnsettledCash.AddAmount(-total);
                    _baseCurrencyCash.AddAmount(total);
                }
            }
        }

        /// <summary>
        /// Applies a split to the portfolio
        /// </summary>
        /// <param name="split">The split to be applied</param>
        /// <param name="liveMode">True if live mode, false for backtest</param>
        /// <param name="mode">The <see cref="DataNormalizationMode"/> for this security</param>
        public void ApplySplit(Split split, bool liveMode, DataNormalizationMode mode)
        {
            var security = Securities[split.Symbol];

            // only apply splits to equities
            if (security.Type != SecurityType.Equity)
            {
                return;
            }

            // only apply splits in live or raw data mode
            if (!liveMode && mode != DataNormalizationMode.Raw)
            {
                return;
            }

            // we need to modify our holdings in light of the split factor
            var quantity = security.holdings.Quantity / split.SplitFactor;
            var avgPrice = security.holdings.AveragePrice * split.SplitFactor;

            // we'll model this as a cash adjustment
            var leftOver = quantity - (int)quantity;
            var extraCash = leftOver * split.ReferencePrice;
            _baseCurrencyCash.AddAmount(extraCash);

            security.Holdings.SetHoldings(avgPrice, quantity, 0);

            // build a 'next' value to update the market prices in light of the split factor
            var next = security.GetLastData();
            if (next == null)
            {
                // sometimes we can get splits before we receive data which
                // will cause this to return null, in this case we can't possibly
                // have any holdings or price to set since we haven't received
                // data yet, so just do nothing
                return;
            }
            next.Value *= split.SplitFactor;

            // make sure to modify open/high/low as well for trade bar data types
            if (next is TradeBar tradeBar)
            {
                tradeBar.open *= split.SplitFactor;
                tradeBar.high *= split.SplitFactor;
                tradeBar.low *= split.SplitFactor;
            }

            // make sure to modify bid/ask as well for trade bar data types
            if (next is Tick tick)
            {
                tick.AskPrice *= split.SplitFactor;
                tick.BidPrice *= split.SplitFactor;
            }

            security.SetMarketPrice(next);
            // security price updated
            InvalidateTotalPortfolioValue(security);
        }

        /// <summary>
        /// Record the transaction value and time in a list to later be processed for statistics creation.
        /// </summary>
        /// <param name="time">Time of order processed </param>
        /// <param name="transactionProfitLoss">Profit Loss.</param>
        public void AddTransactionRecord(DateTime time, decimal transactionProfitLoss)
        {
            Transactions.AddTransactionRecord(time, transactionProfitLoss);
        }

        /// <summary>
        /// Retrieves a summary of the holdings for the specified symbol
        /// </summary>
        /// <param name="symbol">The symbol to get holdings for</param>
        /// <returns>The holdings for the symbol or null if the symbol is invalid and/or not in the portfolio</returns>
        Security ISecurityProvider.GetSecurity(Symbol symbol)
        {
            return Securities.TryGetValue(symbol, out var security) ? security : null;
        }

        /// <summary>
        /// Adds an item to the list of unsettled cash amounts
        /// </summary>
        /// <param name="item">The item to add</param>
        public void AddUnsettledCashAmount(UnsettledCashAmount item)
        {
            lock (_unsettledCashAmountsLocker)
            {
                _unsettledCashAmounts.Add(item);
            }
        }

        /// <summary>
        /// Scan the portfolio to check if unsettled funds should be settled
        /// </summary>
        public void ScanForCashSettlement(DateTime timeUtc)
        {
            lock (_unsettledCashAmountsLocker)
            {
                foreach (var item in _unsettledCashAmounts.ToList())
                {
                    // check if settlement time has passed
                    if (timeUtc >= item.SettlementTimeUtc)
                    {
                        // remove item from unsettled funds list
                        _unsettledCashAmounts.Remove(item);

                        // update unsettled cashbook
                        UnsettledCashBook[item.Currency].AddAmount(-item.Amount);

                        // update settled cashbook
                        CashBook[item.Currency].AddAmount(item.Amount);
                    }
                }
            }
        }

        /// <summary>
        /// Logs margin information for debugging
        /// </summary>
        public virtual void LogMarginInformation(OrderRequest orderRequest = null)
        {
            Log.Trace("Total margin information: " +
                      $"TotalMarginUsed: {TotalMarginUsed.ToString("F2", CultureInfo.InvariantCulture)}, " +
                      $"MarginRemaining: {MarginRemaining.ToString("F2", CultureInfo.InvariantCulture)}");

            if (orderRequest is SubmitOrderRequest orderSubmitRequest)
            {
                var direction = orderSubmitRequest.Quantity > 0 ? OrderDirection.Buy : OrderDirection.Sell;
                var security = Securities[orderSubmitRequest.Symbol];

                var marginUsed = security.BuyingPowerModel.GetReservedBuyingPowerForPosition(
                    new ReservedBuyingPowerForPositionParameters(security)
                );

                var marginRemaining = security.BuyingPowerModel.GetBuyingPower(
                    new BuyingPowerParameters(this, security, direction, orderSubmitRequest.Offset, orderSubmitRequest.OrderId)
                );

                Log.Trace("Order request margin information: " +
                          $"MarginUsed: {marginUsed.Value.ToString("F2", CultureInfo.InvariantCulture)}, " +
                          $"MarginRemaining: {marginRemaining.Value.ToString("F2", CultureInfo.InvariantCulture)}");
            }
        }

        /// <summary>
        /// Sets the margin call model
        /// </summary>
        /// <param name="marginCallModel">Model that represents a portfolio's model to executed margin call orders.</param>
        public void SetMarginCallModel(IMarginCallModel marginCallModel)
        {
            MarginCallModel = marginCallModel;
        }

        /// <summary>
        /// Sets the margin call model
        /// </summary>
        /// <param name="pyObject">Model that represents a portfolio's model to executed margin call orders.</param>
        public void SetMarginCallModel(PyObject pyObject)
        {
            SetMarginCallModel(new MarginCallModelPythonWrapper(pyObject));
        }


        public List<SecurityHolding> GetAllHolding()
        {
            List<SecurityHolding> holdings = new List<SecurityHolding>();
            foreach (var kvp in Securities)
            {
                var security = kvp.Value;
                if (security.Invested && !security.Symbol.IsCanonical())
                {
                    if (security.Holdings.Invested)
                    {
                        holdings.Add(security.Holdings);
                    }

                    if (security.LongHoldings.Invested)
                    {
                        holdings.Add(security.LongHoldings);
                    }

                    if (security.ShortHoldings.Invested)
                    {
                        holdings.Add(security.ShortHoldings);
                    }
                }
            }

            return holdings;
        }


        public void TradingDayChanged()
        {
            foreach (var item in Securities.ListedAndTraded())
            {
                var security = item.Value;
                security.Holdings.TradingDayChanged();
                security.LongHoldings.TradingDayChanged();
                security.ShortHoldings.TradingDayChanged();
            }
        }
    }
}
