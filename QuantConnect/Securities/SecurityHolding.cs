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
using System.Collections.Concurrent;
using System.Security.Permissions;
using System.Threading;
using Newtonsoft.Json;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Data;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Orders.Fills;
using QuantConnect.Util;

namespace QuantConnect.Securities
{
    /// <summary>
    /// SecurityHolding is a base class for purchasing and holding a market item which manages the asset portfolio
    /// </summary>
    [JsonConverter(typeof(SecurityHoldingJsonConverter))]
    public class SecurityHolding
    {
        //Working Variables
        private decimal averagePrice;
        private decimal quantity;
        private decimal quantityT0;
        private decimal price;
        private decimal totalSaleVolume;
        private decimal profit;
        private decimal lastTradeProfit;
        private decimal totalFees;
        internal readonly Security security;
        internal readonly ICurrencyConverter currencyConverter;
        internal Symbol symbol;
        internal SecurityHoldingType holdingType;

        /// keep pre-close order for long holding and short holding for chinese position writer by the one
        /// Order.Quantity for pre sub
        /// reverse value=(Order.Quantity-Order.FillQuantity) when Invalid or Canceled 
        private readonly ConcurrentDictionary<long, OrderTicket> _orders = new();

        /// <summary>
        /// default constructor 
        /// </summary>
        public SecurityHolding()
        {
        }

        /// <summary>
        /// Create a new holding class instance setting the initial properties to $0.
        /// </summary>
        /// <param name="security">The security being held</param>
        /// <param name="currencyConverter">A currency converter instance</param>
        /// <param name="holdingType">holding type</param>
        public SecurityHolding(Security security, ICurrencyConverter currencyConverter, SecurityHoldingType holdingType = SecurityHoldingType.Net)
        {
            this.security = security;
            symbol = this.security.symbol;
            //Total Sales Volume for the day
            totalSaleVolume = 0;
            lastTradeProfit = 0;
            this.currencyConverter = currencyConverter;
            this.holdingType = holdingType;
        }

        /// <summary>
        /// Create a new holding class instance copying the initial properties
        /// </summary>
        /// <param name="holding">The security being held</param>
        protected SecurityHolding(SecurityHolding holding)
        {
            security = holding.security;
            Symbol = security.symbol;
            averagePrice = holding.averagePrice;
            quantity = holding.quantity;
            quantityT0 = holding.quantityT0;
            price = holding.price;
            totalSaleVolume = holding.totalSaleVolume;
            profit = holding.profit;
            lastTradeProfit = holding.lastTradeProfit;
            totalFees = holding.totalFees;
            currencyConverter = holding.currencyConverter;
            holdingType = holding.holdingType;
            foreach (var kv in holding._orders)
            {
                _orders.TryAdd(kv.Key, kv.Value);
            }
        }


        private HoldingGreekPnl _greekPnl;

        private void CreatePnlObject()
        {
            if (security.Symbol.SecurityType == SecurityType.Future)
                return;

            _greekPnl = new HoldingGreekPnl(this, security);

        }

        public GreekPnlData CalcPnl(Slice slice)
        {
            if (_greekPnl == null || !_greekPnl.CalcPnl(slice))
                return null;

            return _greekPnl.PnlData;
        }


        /// <summary>
        /// The security being held
        /// </summary>
        protected Security Security => security;

        /// <summary>
        /// Gets the current target holdings for this security
        /// </summary>
        public IPortfolioTarget Target
        {
            get; set;
        }

        /// <summary>
        /// Average price of the security holdings.
        /// </summary>
        public decimal AveragePrice
        {
            get => averagePrice;
            set => averagePrice = value;
        }

        public decimal DividendRecordQuantity { get; set; }

        public decimal Quantity => quantity;
        public decimal QuantityT0 => quantityT0;
        public decimal QuantityT1 => quantity - quantityT0;

        /// <summary>
        /// Symbol identifier of the underlying security.
        /// </summary>
        public Symbol Symbol
        {
            get => symbol;
            set => symbol = value;
        }

        /// <summary>
        /// The security type of the symbol
        /// </summary>
        public SecurityType Type => security.Type;

        /// <summary>
        /// Leverage of the underlying security.
        /// </summary>
        public virtual decimal Leverage => security.buyingPowerModel.GetLeverage(security);


        /// <summary>
        /// Acquisition cost of the security total holdings in units of the account's currency.
        /// </summary>
        public virtual decimal HoldingsCost
        {
            get
            {
                if (quantity == 0)
                {
                    return 0;
                }
                return averagePrice * quantity * security.quoteCurrency.conversionRate * security.symbolProperties.ContractMultiplier;
            }
        }

        /// <summary>
        /// Unlevered Acquisition cost of the security total holdings in units of the account's currency.
        /// </summary>
        public virtual decimal UnleveredHoldingsCost => HoldingsCost / Leverage;

        /// <summary>
        /// Current market price of the security.
        /// </summary>
        public virtual decimal Price
        {
            get => price;
            protected set => price = value;
        }

        /// <summary>
        /// Absolute holdings cost for current holdings in units of the account's currency.
        /// </summary>
        /// <seealso cref="HoldingsCost"/>
        public virtual decimal AbsoluteHoldingsCost => Math.Abs(HoldingsCost);

        /// <summary>
        /// Unlevered absolute acquisition cost of the security total holdings in units of the account's currency.
        /// </summary>
        public virtual decimal UnleveredAbsoluteHoldingsCost => Math.Abs(UnleveredHoldingsCost);

        /// <summary>
        /// Market value of our holdings in units of the account's currency.
        /// </summary>
        public virtual decimal HoldingsValue
        {
            get
            {
                if (quantity == 0)
                {
                    return 0;
                }
                return price * quantity * security.quoteCurrency.conversionRate * security.symbolProperties.ContractMultiplier;
            }
        }

        /// <summary>
        /// Absolute of the market value of our holdings in units of the account's currency.
        /// </summary>
        /// <seealso cref="HoldingsValue"/>
        public virtual decimal AbsoluteHoldingsValue => Math.Abs(HoldingsValue);

        /// <summary>
        /// Boolean flat indicating if we hold any of the security
        /// </summary>
        public virtual bool HoldStock => AbsoluteQuantity > 0;

        /// <summary>
        /// Boolean flat indicating if we hold any of the security
        /// </summary>
        /// <remarks>Alias of HoldStock</remarks>
        /// <seealso cref="HoldStock"/>
        public virtual bool Invested => HoldStock;

        /// <summary>
        /// The total transaction volume for this security since the algorithm started in units of the account's currency.
        /// </summary>
        public decimal TotalSaleVolume => totalSaleVolume;

        /// <summary>
        /// Total fees for this company since the algorithm started in units of the account's currency.
        /// </summary>
        public decimal TotalFees => totalFees;

        /// <summary>
        /// Boolean flag indicating we have a net positive holding of the security.
        /// </summary>
        /// <seealso cref="IsShort"/>
        public virtual bool IsLong => quantity > 0;

        /// <summary>
        /// BBoolean flag indicating we have a net negative holding of the security.
        /// </summary>
        /// <seealso cref="IsLong"/>
        public virtual bool IsShort => quantity < 0;

        /// <summary>
        /// Absolute quantity of holdings of this security
        /// </summary>
        /// <seealso cref="Quantity"/>
        public virtual decimal AbsoluteQuantity => Math.Abs(quantity);

        /// <summary>
        /// Record of the closing profit from the last trade conducted in units of the account's currency.
        /// </summary>
        public decimal LastTradeProfit => lastTradeProfit;

        /// <summary>
        /// Calculate the total profit for this security in units of the account's currency.
        /// </summary>
        /// <seealso cref="NetProfit"/>
        public decimal Profit => profit;

        /// <summary>
        /// Return the net for this company measured by the profit less fees in units of the account's currency.
        /// </summary>
        /// <seealso cref="Profit"/>
        /// <seealso cref="TotalFees"/>
        public decimal NetProfit => profit - totalFees;

        /// <summary>
        /// Gets the unrealized profit as a percentage of holdings cost
        /// </summary>
        public decimal UnrealizedProfitPercent
        {
            get
            {
                if (AbsoluteHoldingsCost == 0)
                    return 0m;
                return UnrealizedProfit / AbsoluteHoldingsCost;
            }
        }

        /// <summary>
        /// Unrealized profit of this security when absolute quantity held is more than zero in units of the account's currency.
        /// </summary>
        public virtual decimal UnrealizedProfit => TotalCloseProfit();

        /// <summary>
        /// Adds a fee to the running total of total fees in units of the account's currency.
        /// </summary>
        /// <param name="newFee"></param>
        public void AddNewFee(decimal newFee)
        {
            totalFees += newFee;
        }

        /// <summary>
        /// Adds a profit record to the running total of profit in units of the account's currency.
        /// </summary>
        /// <param name="profitLoss">The cash change in portfolio from closing a position</param>
        public void AddNewProfit(decimal profitLoss)
        {
            profit += profitLoss;
        }

        /// <summary>
        /// Adds a new sale value to the running total trading volume in units of the account's currency.
        /// </summary>
        /// <param name="saleValue"></param>
        public void AddNewSale(decimal saleValue)
        {
            totalSaleVolume += saleValue;
        }

        /// <summary>
        /// Set the last trade profit for this security from a Portfolio.ProcessFill call in units of the account's currency.
        /// </summary>
        /// <param name="lastTradeProfit">Value of the last trade profit</param>
        public void SetLastTradeProfit(decimal lastTradeProfit)
        {
            this.lastTradeProfit = lastTradeProfit;
        }

        /// <summary>
        /// Set the quantity of holdings and their average price after processing a portfolio fill.
        /// </summary>
        public virtual void SetHoldings(decimal averagePrice, decimal quantity, decimal quantityT0 = 0)
        {
            this.averagePrice = averagePrice;
            this.quantity = quantity;
            this.quantityT0 = quantityT0;
        }

        /// <summary>
        /// ���� pang
        /// </summary>
        /// <param name="profit"></param>
        public virtual void SetProfit(decimal profit)
        {
            this.profit = profit;
        }

        /// <summary>
        /// ���� pang
        /// </summary>
        /// <param name="totalFee"></param>
        public virtual void SetTotalFee(decimal totalFee)
        {
            this.totalFees = totalFee;
        }

        /// <summary>
        /// Update local copy of closing price value.
        /// </summary>
        /// <param name="closingPrice">Price of the underlying asset to be used for calculating market price / portfolio value</param>
        public virtual void UpdateMarketPrice(decimal closingPrice)
        {
            price = closingPrice;
        }

        /// <summary>
        /// Profit if we closed the holdings right now including the approximate fees in units of the account's currency.
        /// </summary>
        /// <remarks>Does not use the transaction model for market fills but should.</remarks>
        public virtual decimal TotalCloseProfit()
        {
            if (quantity == 0)
            {
                return 0;
            }

            // this is in the account currency
            var marketOrder = new MarketOrder(
                security.symbol,
                -quantity,
                security.LocalTime.ConvertToUtc(security.exchange.TimeZone));

            var orderFee = security.feeModel.GetOrderFee(new OrderFeeParameters(security, marketOrder)).Value;
            var feesInAccountCurrency = currencyConverter.
                ConvertToAccountCurrency(orderFee).Amount;

            var lastPrice = marketOrder.Direction == OrderDirection.Sell ? security.BidPrice : security.AskPrice;
            if (lastPrice == 0)
            {
                return 0;
            }

            return (lastPrice - averagePrice) * quantity * security.quoteCurrency.conversionRate
                * security.symbolProperties.ContractMultiplier - feesInAccountCurrency;
        }

        /// <summary>
        /// the type of holding,eg. net,long,short
        /// write by theone
        /// </summary>
        public SecurityHoldingType HoldingType
        {
            get => holdingType;
            internal set => holdingType = value;
        }

        /// <summary>
        /// set order of close type in cache(_orders)
        /// write by theone
        /// </summary>
        public bool SetHoldingOrder(OrderTicket ticket)
        {
            if (!SupportOffset.IsSupportOffset(ticket.Symbol) || ticket.SubmitRequest.Offset != OrderOffset.Close) return false;

            if (_orders.ContainsKey(ticket.OrderId))
            {
                return false;
            }

            if (!_orders.TryAdd(ticket.OrderId, ticket))
            {
                return false;
            }

            return true;
        }


        /// <summary>
        /// get the pre sub quantity
        /// write by theone
        /// </summary>
        public decimal GetOpenQuantity()
        {
            lock (this)
            {
                var value = Quantity;
                foreach (var kv in _orders)
                {
                    if (!kv.Value.Status.IsClosed())
                    {
                        value += (kv.Value.Quantity - kv.Value.QuantityFilled);
                    }
                }
                return value;
            }
        }

        public void TradingDayChanged()
        {
            quantityT0 = 0;
        }

        public void OpenPosition(OrderEvent fill, decimal feeInAccountCurrency)
        {
            var filled = fill.FillQuantity;
            if (Security.Type == SecurityType.Crypto &&
                fill.OrderFee.Value.Currency == (Security as Crypto.Crypto).BaseCurrencySymbol)
            {
                filled -= fill.OrderFee.Value.Amount;
            }
            else
            {
                AddNewFee(feeInAccountCurrency);
            }
            quantityT0 += filled;
            quantity += filled;
        }

        public void ClosePosition(OrderEvent fill, decimal feeInAccountCurrency)
        {
            var absoluteT1 = Math.Abs(QuantityT1);
            var sign = Math.Sign(fill.FillQuantity);
            var closedT1 = sign * Math.Min(absoluteT1, fill.AbsoluteFillQuantity);
            quantity += closedT1;
            var closedT0 = sign * Math.Max(fill.AbsoluteFillQuantity - Math.Abs(closedT1), 0);
            if (Security.Type == SecurityType.Crypto &&
                fill.OrderFee.Value.Currency == (Security as Crypto.Crypto).BaseCurrencySymbol)
            {
                closedT0 -= fill.OrderFee.Value.Amount;
            }
            else
            {
                AddNewFee(feeInAccountCurrency);
            }
            quantityT0 += closedT0;
            quantity += closedT0;
        }
    }
}