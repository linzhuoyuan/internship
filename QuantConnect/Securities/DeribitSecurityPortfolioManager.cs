using System;
using System.Collections;
using System.Collections.Generic;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using System.Globalization;
using QuantConnect.Logging;
using QuantConnect.Securities.Option;
using QuantConnect.Securities.Future;

namespace QuantConnect.Securities
{
    /// <summary>
    /// =============================deribit==============================================================
    /// Equity	 = cahs Balance( 初始Balance- 手续费 ) + future profit(unrealize + realize)  + option value  
    /// Margin Balance = cahs balance + future profit(unrealize + realize) 保证金余额
    /// Available Balance = Margin Balance  - Initial Margin
    /// Initial Margin =
    /// Maintenance Margin	= 持仓保证金
    /// 
    /// ============================quantconnect ===========================================================
    /// GetTotalPortfolioValueForCurrency 总资产 = cash(cash balance)+ unsettle_cash + 期权市值 + 期货未实现利润
    /// GetMarginRemainingForCurrency 剩余保证金（用来开仓） = 总资产 - 期权市值 - 期权初始保证金 - 期货初始保证金
    /// </summary>
    public class DeribitSecurityPortfolioManager : SecurityPortfolioManager
    {
        private readonly Dictionary<string, decimal> _totalPortfolioValueForCurrency;
        private readonly Dictionary<string, bool> _isTotalPortfolioValueValidForCurrency;


        /// <summary>
        /// Initialise security mom portfolio manager.
        /// </summary>
        public DeribitSecurityPortfolioManager(
            SecurityManager securityManager,
            SecurityTransactionManager transactions,
            IOrderProperties defaultOrderProperties = null)
            : base(securityManager, transactions, defaultOrderProperties)
        {

            _isTotalPortfolioValueValidForCurrency = new Dictionary<string, bool>
            {
                 {"BTC", false},
                 {"ETH", false},
            };
            _totalPortfolioValueForCurrency = new Dictionary<string, decimal>
            {
                 {"BTC", 0},
                 {"ETH", 0},
            };

            MarginCallModel = new DeribitMarginCallModel(this, defaultOrderProperties);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cash"></param>
        public override void InvalidateTotalPortfolioValueForCurrency(string cash)
        {
            if (_isTotalPortfolioValueValidForCurrency.ContainsKey(cash))
            {
                _isTotalPortfolioValueValidForCurrency[cash] = false;
            }
        }

        /// <summary>
        /// Gets the remaining margin on the account in the account's currency
        /// </summary>
        /// <see cref="GetMarginRemaining(decimal)"/>
        //public override decimal MarginRemaining => GetMarginRemaining(TotalPortfolioValue);

        /// <summary>
        /// Gets the remaining margin on the account in the account's currency
        /// for the given total portfolio value
        /// 剩余保证金 = 总资产 - 未结算账单 - 总维持保证金 
        /// </summary>
        /// <remarks>This method is for performance, for when the user already knows
        /// the total portfolio value, we can avoid re calculating it. Else use
        /// <see cref="MarginRemaining"/></remarks>
        /// <param name="totalPortfolioValue">The total portfolio value <see cref="TotalPortfolioValue"/></param>
        public override decimal GetMarginRemaining(decimal totalPortfolioValue)
        {
            var total_btc = GetTotalPortfolioValueForCurrency("BTC");
            var total_eth = GetTotalPortfolioValueForCurrency("ETH");
            var v1 = GetMarginRemainingForCurrency("BTC", total_btc);
            var v2 = GetMarginRemainingForCurrency("BTC", total_eth);
            return v1 + v2;
        }


        /// <summary>
        /// Total portfolio value if we sold all holdings at current market rates. 
        /// 结算账户所有剩余资金 + 未结算账户所有剩余资金 + (证券+期权)的持仓市值 + 期货+cfd)盈亏
        /// </summary>
        /// <remarks>Cash + TotalUnrealisedProfit + TotalUnleveredAbsoluteHoldingsCost</remarks>
        /// <seealso cref="Cash"/>
        /// <seealso cref="TotalUnrealizedProfit"/>
        /// <seealso cref="TotalUnleveredAbsoluteHoldingsCost"/>
        //public override decimal TotalPortfolioValue
        //{
        //    get
        //    {
        //        if (!_isTotalPortfolioValueValid)
        //        {
        //            decimal totalHoldingsValueWithoutForexCryptoFutureCfd = 0;
        //            decimal totalFuturesAndCfdHoldingsValue = 0;
        //            foreach (var kvp in Securities)
        //            {
        //                var position = kvp.Value;
        //                var securityType = position.Type;
        //                // we can't include forex in this calculation since we would be double accounting with respect to the cash book
        //                // we also exclude futures and CFD as they are calculated separately
        //                if (securityType != SecurityType.Forex && securityType != SecurityType.Crypto &&
        //                    securityType != SecurityType.Future && securityType != SecurityType.Cfd)
        //                {
        //                    totalHoldingsValueWithoutForexCryptoFutureCfd += (position.Holdings.HoldingsValue + position.LongHoldings.HoldingsValue + position.ShortHoldings.HoldingsValue);
        //                }

        //                if (securityType == SecurityType.Future || securityType == SecurityType.Cfd)
        //                {
        //                    totalFuturesAndCfdHoldingsValue += (position.Holdings.UnrealizedProfit + position.LongHoldings.UnrealizedProfit + position.ShortHoldings.UnrealizedProfit);
        //                }
        //            }

        //            // 结算账户所有剩余资金 + 未结算账户所有剩余资金 + (证券+期权)的持仓市值 + 期货+cfd)盈亏
        //            _totalPortfolioValue = CashBook.TotalValueInAccountCurrency +
        //               UnsettledCashBook.TotalValueInAccountCurrency +
        //               totalHoldingsValueWithoutForexCryptoFutureCfd +
        //               totalFuturesAndCfdHoldingsValue;

        //            _isTotalPortfolioValueValid = true;
        //        }

        //        return _totalPortfolioValue;
        //    }
        //}

        /// <summary>
        /// 只为基于某种货币统计的资产
        /// </summary>
        /// <param name="quoteCurrency"> BTC or ETH</param>
        /// <returns></returns>
        public override decimal GetTotalPortfolioValueForCurrency(string quoteCurrency)
        {
            decimal totalPortfolioValueForCurrency = 0;
            decimal totalHoldingsValueWithoutForexCryptoFutureCfd = 0;
            decimal totalFuturesAndCfdHoldingsValue = 0;

            if (!_isTotalPortfolioValueValidForCurrency[quoteCurrency])
            {
                foreach (var pair in Securities.Listed())
                {
                    var position = pair.Value;
                    var securityType = position.Type;
                    // we can't include forex in this calculation since we would be double accounting with respect to the cash book
                    // we also exclude futures and CFD as they are calculated separately
                    //在算剩余保证金的时候这部分要减去。
                    if (securityType != SecurityType.Forex && securityType != SecurityType.Crypto &&
                        securityType != SecurityType.Future && securityType != SecurityType.Cfd &&
                        position.QuoteCurrency.Symbol == quoteCurrency &&
                        position.Holdings.AbsoluteQuantity > 0)
                    {
                        totalHoldingsValueWithoutForexCryptoFutureCfd += position.Holdings.HoldingsValue;
                        //var marginParam = new ReservedBuyingPowerForPositionParameters(position);
                        //totalHoldingsValueWithoutForexCryptoFutureCfd += position.BuyingPowerModel.GetReservedBuyingPowerForPosition(marginParam).Value;
                    }

                    if ((securityType == SecurityType.Future || securityType == SecurityType.Cfd) &&
                        position.QuoteCurrency.Symbol == "USD" && position.Symbol.Value.Contains(quoteCurrency) &&
                        position.Holdings.AbsoluteQuantity > 0)
                    {
                        totalFuturesAndCfdHoldingsValue += position.Holdings.UnrealizedProfit;
                        //var marginParam = new ReservedBuyingPowerForPositionParameters(position);
                        //totalFuturesAndCfdHoldingsValue += position.BuyingPowerModel.GetReservedBuyingPowerForPosition(marginParam).Value;
                    }
                }

                // 结算账户所有剩余资金 + (证券+期权)的持仓市值 + 期货+cfd)盈亏
                if (CashBook.ContainsKey(quoteCurrency))
                {
                    totalPortfolioValueForCurrency = CashBook[quoteCurrency].ValueInAccountCurrency;
                }

                if (UnsettledCashBook.ContainsKey(quoteCurrency))
                {
                    totalPortfolioValueForCurrency += UnsettledCashBook[quoteCurrency].ValueInAccountCurrency;
                }

                totalPortfolioValueForCurrency += (totalHoldingsValueWithoutForexCryptoFutureCfd + totalFuturesAndCfdHoldingsValue);
                _totalPortfolioValueForCurrency[quoteCurrency] = totalPortfolioValueForCurrency;

                _isTotalPortfolioValueValidForCurrency[quoteCurrency] = true;
            }

            return _totalPortfolioValueForCurrency[quoteCurrency];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="quoteCurrency"></param>
        /// <param name="totalPortfolioValueForSecurity"></param>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public override decimal GetMarginRemainingForCurrency(
            string quoteCurrency,
            decimal totalPortfolioValueForSecurity,
            long orderId = long.MaxValue)
        {
            //if (UnsettledCashBook.ContainsKey(quoteCurrency))
            //{
            //    totalPortfolioValueForSecurity -= UnsettledCashBook[quoteCurrency].ValueInAccountCurrency;
            //}

            //期权的持仓不算在内。所以减去, 因为上面的总资产里加上持仓了
            decimal totalHoldingsValueWithoutForexCryptoFutureCfd = 0;
            foreach (var pair in Securities.Listed())
            {
                var position = pair.Value;
                var securityType = position.Type;
                // we can't include forex in this calculation since we would be double accounting with respect to the cash book
                // we also exclude futures and CFD as they are calculated separately
                if (securityType != SecurityType.Forex && securityType != SecurityType.Crypto &&
                    securityType != SecurityType.Future && securityType != SecurityType.Cfd &&
                    position.QuoteCurrency.Symbol == quoteCurrency && position.Holdings.AbsoluteQuantity > 0)
                {
                    totalHoldingsValueWithoutForexCryptoFutureCfd += position.Holdings.HoldingsValue;
                }
            }

            var optionInitialMargin = DeribitOptionMarginModel.GetOptionTotalInitialMargin(this, quoteCurrency, orderId);
            var futureInitialMargin = DeribitFutureMarginModel.GetFutureTotalInitialMargin(this, quoteCurrency, orderId);

            var result = totalPortfolioValueForSecurity - totalHoldingsValueWithoutForexCryptoFutureCfd
                - optionInitialMargin - futureInitialMargin;

            return result;
        }

        public override decimal TotalMarginUsed
        {
            get
            {
                decimal sum = 0;
                sum += TotalMarginUsedForCurrency("BTC");
                sum += TotalMarginUsedForCurrency("ETH");
                return sum;
            }
        }

        /// <summary>
        /// Gets the total margin used across all securities in the account's currency 所有security的维持保证金  
        /// 如果是买期权没有保证金要确保保证金率为0 而不是1， 在其他的样例中买期权保证金率是1
        /// </summary>
        public override decimal TotalMarginUsedForCurrency(string quoteCurrency)
        {
            //decimal sum = 0;
            //foreach (var kvp in Securities)
            //{
            //    //如果是买期权没有保证金要确保保证金率为0 而不是1， 在其他的样例中买期权保证金率是1
            //    //只加卖期权和期货 跟0,1无关了
            //    var security = kvp.Value;
            //    if(security.Type == SecurityType.Option)
            //    {
            //        if(security.Holdings.IsShort && quoteCurrency == security.QuoteCurrency.Symbol)
            //        {
            //            var context = new ReservedBuyingPowerForPositionParameters(security);
            //            var reservedBuyingPower = security.BuyingPowerModel.GetReservedBuyingPowerForPosition(context);//持仓成本* 维持保证金
            //            sum += reservedBuyingPower.Value;
            //        }
            //    }
            //    if(security.Type == SecurityType.Future)
            //    {
            //        if(security.QuoteCurrency.Symbol == "USD" && security.Symbol.Value.Contains(quoteCurrency))
            //        {
            //            var context = new ReservedBuyingPowerForPositionParameters(security);
            //            var reservedBuyingPower = security.BuyingPowerModel.GetReservedBuyingPowerForPosition(context);//持仓成本* 维持保证金
            //            sum += reservedBuyingPower.Value;
            //        }
            //    } 
            //}
            //return sum;
            var optionInitialMargin = DeribitOptionMarginModel.GetOptionTotalInitialMargin(this, quoteCurrency);
            var futureInitialMargin = DeribitFutureMarginModel.GetFutureTotalInitialMargin(this, quoteCurrency);
            return optionInitialMargin + futureInitialMargin;
        }

        /// <summary>
        /// Logs margin information for debugging
        /// </summary>
        public override void LogMarginInformation(OrderRequest orderRequest = null)
        {
            if (orderRequest == null)
            {
                Log.Trace("Total margin information: " +
                      $"TotalMarginUsed: {TotalMarginUsed.ToString("F2", CultureInfo.InvariantCulture)}, " +
                      $"MarginRemaining: {MarginRemaining.ToString("F2", CultureInfo.InvariantCulture)}");
            }
            else
            {
                Security security;
                if (orderRequest is SubmitOrderRequest orderSubmit)
                {
                    security = Securities[orderSubmit.Symbol];
                }
                else
                {
                    var tick = Transactions.GetOrderTicket(orderRequest.OrderId);
                    security = Securities[tick.Symbol];
                }
                var quoteCurrency = "";
                if (security.Type == SecurityType.Future)
                {
                    quoteCurrency = security.Symbol.Value.Contains("BTC") ? "BTC" : "ETH";
                }
                else if (security.Type == SecurityType.Option)
                {
                    quoteCurrency = security.QuoteCurrency.Symbol;
                }

                try
                {
                    //如果是submit 新的单子还没有加入到缓存中，所以不用使用id来过滤
                    var total = GetTotalPortfolioValueForCurrency(quoteCurrency);
                    var optionInitialMargin = DeribitOptionMarginModel.GetOptionTotalInitialMargin(this, quoteCurrency);
                    var futureInitialMargin = DeribitFutureMarginModel.GetFutureTotalInitialMargin(this, quoteCurrency);

                    Log.Trace("Total margin information: " +
                          $"TotalMarginUsed For {quoteCurrency}: {(optionInitialMargin + futureInitialMargin).ToString("F2", CultureInfo.InvariantCulture)}, " +
                          $"MarginRemaining For {quoteCurrency}: {GetMarginRemainingForCurrency(quoteCurrency, total).ToString("F2", CultureInfo.InvariantCulture)}");
                }
                catch (Exception ex)
                {
                    if (orderRequest is CancelOrderRequest)
                    {
                        Log.Trace($"LogMarginInformation Exception {ex.Message}");
                    }
                    else
                    {
                        Log.Error($"LogMarginInformation Exception {ex.Message}");
                    }
                }
            }

            try
            {
                if (orderRequest is SubmitOrderRequest orderSubmitRequest)
                {
                    var direction = orderSubmitRequest.Quantity > 0 ? OrderDirection.Buy : OrderDirection.Sell;
                    var security = Securities[orderSubmitRequest.Symbol];
                    string quoteCurrency = "";
                    if (security.Type == SecurityType.Future)
                    {
                        quoteCurrency = security.Symbol.Value.Contains("BTC") ? "BTC" : "ETH";
                    }
                    else if (security.Type == SecurityType.Option)
                    {
                        quoteCurrency = security.QuoteCurrency.Symbol;
                    }

                    decimal marginUsed = 0;
                    if (security.Type == SecurityType.Future)
                    {
                        marginUsed = DeribitFutureMarginModel.GetFutureTotalInitialMargin(this, quoteCurrency);
                    }
                    if (security.Type == SecurityType.Option)
                    {
                        marginUsed = DeribitOptionMarginModel.GetOptionInitialMargin(this, security);
                    }

                    var marginRemaining = security.BuyingPowerModel.GetBuyingPower(
                        new BuyingPowerParameters(this, security, direction, orderSubmitRequest.Offset, orderSubmitRequest.OrderId)
                    );

                    Log.Trace("Order request margin information: " +
                              $"MarginUsed: {marginUsed.ToString("F2", CultureInfo.InvariantCulture)}, " +
                              $"MarginRemaining: {marginRemaining.Value.ToString("F2", CultureInfo.InvariantCulture)}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"LogMarginInformation Exception {ex.Message}");
            }
        }
    }
}
