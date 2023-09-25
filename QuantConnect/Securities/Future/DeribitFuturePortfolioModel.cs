using System;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace QuantConnect.Securities.Future
{
    internal class DeribitFuturePortfolioModel : SecurityPortfolioModel
    {
        /// <summary>
        /// Performs application of an OrderEvent to the portfolio
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">The fill's security</param>
        /// <param name="fill">The order event fill object to be applied</param>
        public override void ProcessFill(SecurityPortfolioManager portfolio, Security security, OrderEvent fill)
        {
            var order = portfolio.Transactions.GetOrderById(fill.OrderId);
            if (order == null)
            {
                Log.Error("OptionPortfolioModel.ProcessFill(): Unable to locate Order with id " + fill.OrderId);
                return;
            }

            var quoteCash = security.Symbol.Value.Contains("BTC") ? "BTC" : "ETH";

            //Get the required information from the vehicle this order will affect
            var isLong = security.Holdings.IsLong;
            var isShort = security.Holdings.IsShort;
            //Make local decimals to avoid any rounding errors from int multiplication
            var quantityHoldings = (decimal)security.Holdings.Quantity;
            var absoluteHoldingsQuantity = security.Holdings.AbsoluteQuantity;
            var averageHoldingsPrice = security.Holdings.AveragePrice;
            var settlePrice = security.Cache.SettlementPrice;


            try
            {
                // apply sales value to holdings in the account currency
                security.Holdings.AddNewSale(fill.AbsoluteFillQuantity);

                // subtract transaction fees from the portfolio
                var feeInAccountCurrency = 0m;
                if (fill.OrderFee != OrderFee.Zero
                    // this is for user friendliness because some
                    // Security types default to use 0 USD ConstantFeeModel
                    && fill.OrderFee.Value.Amount != 0)
                {
                    var feeThisOrder = fill.OrderFee.Value;
                    feeInAccountCurrency = portfolio.CashBook.ConvertToAccountCurrency(feeThisOrder).Amount;
                    security.Holdings.AddNewFee(feeInAccountCurrency);

                    var fee = feeThisOrder.Amount;
                    portfolio.CashBook[feeThisOrder.Currency].AddAmount(-fee);
                }

                // did we close or open a position further?
                var closedPosition = isLong && fill.Direction == OrderDirection.Sell
                                     || isShort && fill.Direction == OrderDirection.Buy;

                // calculate the last trade profit
                if (closedPosition)
                {
                    // profit = (closed sale value - cost)*conversion to account currency
                    // closed sale value = quantity closed * fill price       BUYs are deemed negative cash flow
                    // cost = quantity closed * average holdings price        SELLS are deemed positive cash flow
                    var absoluteQuantityClosed = Math.Min(fill.AbsoluteFillQuantity, absoluteHoldingsQuantity);

                    var multi = security.SymbolProperties.ContractMultiplier;
                    decimal lastTradeProfit = 0m;
                    lastTradeProfit = (multi / averageHoldingsPrice - multi / fill.FillPrice) * Math.Sign(-fill.FillQuantity) * absoluteQuantityClosed;

                    var lastTradeProfitInAccountCurrency = lastTradeProfit * fill.FillPrice;

                    // Reflect account cash adjustment for futures/CFD position
                    if (security.Type == SecurityType.Future || security.Type == SecurityType.Cfd)
                    {
                        security.SettlementModel.ApplyFunds(portfolio, security, fill.UtcTime, quoteCash, lastTradeProfit);
                    }

                    //Update Vehicle Profit Tracking:
                    security.Holdings.AddNewProfit(lastTradeProfitInAccountCurrency);
                    security.Holdings.SetLastTradeProfit(lastTradeProfitInAccountCurrency);
                    portfolio.AddTransactionRecord(security.LocalTime.ConvertToUtc(
                        security.Exchange.TimeZone),
                        lastTradeProfitInAccountCurrency - 2 * feeInAccountCurrency);
                }

                //UPDATE HOLDINGS QUANTITY, AVG PRICE:
                //Currently NO holdings. The order is ALL our holdings.
                if (quantityHoldings == 0)
                {
                    //First transaction just subtract order from cash and set our holdings:
                    averageHoldingsPrice = fill.FillPrice;
                    quantityHoldings = fill.FillQuantity;
                }
                else if (isLong)
                {
                    //If we're currently LONG on the stock.
                    switch (fill.Direction)
                    {
                        case OrderDirection.Buy:
                            //Update the Holding Average Price: Total Value / Total Quantity:
                            averageHoldingsPrice = (quantityHoldings + fill.FillQuantity) / (quantityHoldings / averageHoldingsPrice + fill.FillQuantity / fill.FillPrice);
                            //Add the new quantity:
                            quantityHoldings += fill.FillQuantity;
                            break;

                        case OrderDirection.Sell:
                            quantityHoldings += fill.FillQuantity; //+ a short = a subtraction
                            if (quantityHoldings < 0)
                            {
                                //If we've now passed through zero from selling stock: new avg price:
                                averageHoldingsPrice = fill.FillPrice;
                            }
                            else if (quantityHoldings == 0)
                            {
                                averageHoldingsPrice = 0;
                            }
                            break;
                    }
                }
                else if (isShort)
                {
                    //We're currently SHORTING the stock: What is the new position now?
                    switch (fill.Direction)
                    {
                        case OrderDirection.Buy:
                            //Buying when we're shorting moves to close position:
                            quantityHoldings += fill.FillQuantity;
                            if (quantityHoldings > 0)
                            {
                                //If we were short but passed through zero, new average price is what we paid. The short position was closed.
                                averageHoldingsPrice = fill.FillPrice;
                            }
                            else if (quantityHoldings == 0)
                            {
                                averageHoldingsPrice = 0;
                            }
                            break;

                        case OrderDirection.Sell:
                            //We are increasing a Short position:
                            //E.g.  -100 @ $5, adding -100 @ $10: Avg: $7.5
                            //      dAvg = (-500 + -1000) / -200 = 7.5
                            averageHoldingsPrice = (quantityHoldings + fill.FillQuantity) / (quantityHoldings / averageHoldingsPrice + fill.FillQuantity / fill.FillPrice);
                            quantityHoldings += fill.FillQuantity;
                            break;
                    }
                }
            }
            catch (Exception err)
            {
                Log.Error(err);
            }

            //Set the results back to the vehicle.
            security.Holdings.SetHoldings(averageHoldingsPrice, quantityHoldings, 0);
        }
    }
}
