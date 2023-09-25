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
using System.Linq;
using Newtonsoft.Json;
using QuantConnect.Interfaces;
using QuantConnect.Securities;

namespace QuantConnect.Orders
{
    /// <summary>
    /// Order struct for placing new trade
    /// </summary>
    public abstract class Order
    {
        internal decimal quantity;
        internal decimal price;
        internal Symbol symbol;
        internal long id;
        internal int contingentId;
        internal decimal fillQuantity;
        internal decimal averageFillPrice;
        internal decimal commission;
        internal List<string> brokerId;
        internal string priceCurrency;
        internal DateTime time;
        internal DateTime fillTime;
        internal DateTime timeZoneTime;
        internal DateTime? lastFillTime;
        internal DateTime? lastUpdateTime;
        internal DateTime? canceledTime;
        internal decimal tradeValue;
        internal OrderStatus status;
        internal IOrderProperties properties;
        internal string tag;
        internal OrderOffset offset;
        internal OrderSubmissionData orderSubmissionData;

        /// <summary>
        /// Order ID.
        /// </summary>
        public long Id
        {
            get => id;
            internal set => id = value;
        }

        /// <summary>
        /// Order id to process before processing this order.
        /// </summary>
        public int ContingentId
        {
            get => contingentId;
            internal set => contingentId = value;
        }

        /// <summary>
        /// filled quantity already this order
        /// </summary>
        public decimal FillQuantity
        {
            get => fillQuantity;
            internal set => fillQuantity = value;
        }

        /// <summary>
        /// filled average price this order
        /// </summary>
        public decimal AverageFillPrice
        {
            get => averageFillPrice;
            internal set => averageFillPrice = value;
        }

        /// <summary>
        /// filled commission this order
        /// </summary>
        public decimal Commission
        {
            get => commission;
            internal set => commission = value;
        }

        /// <summary>
        /// Brokerage Id for this order for when the brokerage splits orders into multiple pieces
        /// </summary>
        public List<string> BrokerId
        {
            get => brokerId;
            internal set => brokerId = value;
        }

        /// <summary>
        /// Symbol of the Asset
        /// </summary>
        public Symbol Symbol
        {
            get => symbol;
            internal set => symbol = value;
        }

        /// <summary>
        /// Symbol of the Asset //lean-monitor界面显示修改 writer LH
        /// </summary>
        public string SymbolValue => symbol.value;

        /// <summary>
        /// Price of the Order.
        /// </summary>
        public decimal Price
        {
            get => price;
            internal set => price = value.Normalize();
        }

        /// <summary>
        /// Currency for the order price
        /// </summary>
        public string PriceCurrency
        {
            get => priceCurrency;
            internal set => priceCurrency = value;
        }

        /// <summary>
        /// Gets the utc time the order was created.
        /// </summary>
        public DateTime Time
        {
            get => time;
            internal set => time = value;
        }

        /// <summary>
        /// Gets the fill time of the order
        /// </summary>
        public DateTime FillTime
        {
            get => fillTime;
            internal set => fillTime = value;
        }
        //lean-monitor界面显示修改 writer LH

        /// <summary>
        /// Gets the create time of the order in the timezone
        /// </summary>
        public DateTime TimeZoneTime
        {
            get => timeZoneTime;
            internal set => timeZoneTime = value;
        }
        //lean-monitor界面显示修改 writer LH

        /// <summary>
        /// Gets the utc time this order was created. Alias for <see cref="Time"/>
        /// </summary>
        public DateTime CreatedTime => time;

        /// <summary>
        /// Gets the utc time the last fill was received, or null if no fills have been received
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastFillTime
        {
            get => lastFillTime;
            internal set => lastFillTime = value;
        }

        /// <summary>
        /// Gets the utc time this order was last updated, or null if the order has not been updated.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? LastUpdateTime
        {
            get => lastUpdateTime;
            internal set => lastUpdateTime = value;
        }

        /// <summary>
        /// Gets the utc time this order was canceled, or null if the order was not canceled.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? CanceledTime
        {
            get => canceledTime;
            internal set => canceledTime = value;
        }

        /// <summary>
        /// Number of shares to execute.
        /// </summary>
        public decimal Quantity
        {
            get => quantity;
            internal set => quantity = value.Normalize();
        }

        /// <summary>
        /// Order Type
        /// </summary>
        public abstract OrderType Type { get; }

        /// <summary>
        /// Status of the Order
        /// </summary>
        public OrderStatus Status
        {
            get => status;
            internal set => status = value;
        }

        /// <summary>
        /// Order Time In Force
        /// </summary>
        public TimeInForce TimeInForce => properties.TimeInForce;

        /// <summary>
        /// Tag the order with some custom data
        /// </summary>
        public string Tag
        {
            get => tag;
            internal set => tag = value;
        }

        /// <summary>
        /// Additional properties of the order
        /// </summary>
        public IOrderProperties Properties
        {
            get => properties;
            private set => properties = value;
        }

        /// <summary>
        /// The symbol's security type
        /// </summary>
        public SecurityType SecurityType => symbol.id.SecurityType;

        /// <summary>
        /// Order Direction Property based off Quantity.
        /// </summary>
        public OrderDirection Direction
        {
            get
            {
                if (quantity > 0)
                {
                    return OrderDirection.Buy;
                }
                return quantity < 0 ? OrderDirection.Sell : OrderDirection.Hold;
            }
        }

        /// <summary>
        /// Order Offset Property based off TradeSide.
        /// </summary>
        public OrderOffset Offset
        {
            get => offset;
            set => offset = value;
        }

        /// <summary>
        /// Get the absolute quantity for this order
        /// </summary>
        public decimal AbsoluteQuantity => Math.Abs(quantity);

        /// <summary>
        /// Gets the executed value of this order. If the order has not yet filled,
        /// then this will return zero.
        /// </summary>
        public decimal Value => quantity * price;

        ///<summary>
        ///lean-monitor界面显示修改 证券买入金额 writer LH
        ///</summary>
        public decimal TradeValue
        {
            get => tradeValue;
            internal set => tradeValue = value;
        }

        /// <summary>
        /// Gets the price data at the time the order was submitted
        /// </summary>
        public OrderSubmissionData OrderSubmissionData
        {
            get => orderSubmissionData;
            internal set => orderSubmissionData = value;
        }

        /// <summary>
        /// Returns true if the order is a marketable order.
        /// </summary>
        public bool IsMarketable
        {
            get
            {
                if (Type == OrderType.Limit)
                {
                    // check if marketable limit order using bid/ask prices
                    var limitOrder = (LimitOrder)this;
                    return orderSubmissionData != null &&
                           (Direction == OrderDirection.Buy && limitOrder.LimitPrice >= OrderSubmissionData.AskPrice ||
                            Direction == OrderDirection.Sell && limitOrder.LimitPrice <= OrderSubmissionData.BidPrice);
                }

                return Type == OrderType.Market;
            }
        }

        /// <summary>
        /// Added a default constructor for JSON Deserialization:
        /// </summary>
        protected Order()
        {
            time = new DateTime();
            fillTime = new DateTime();//lean-monitor界面显示修改
            timeZoneTime = new DateTime();
            price = 0;
            priceCurrency = string.Empty;
            quantity = 0;
            symbol = Symbol.Empty;
            status = OrderStatus.None;
            tag = "";
            brokerId = new List<string>();
            contingentId = 0;
            properties = new OrderProperties();
            offset = OrderOffset.None;
            fillQuantity = 0;
            averageFillPrice = 0;
            commission = 0;
        }

        /// <summary>
        /// New order constructor
        /// </summary>
        /// <param name="symbol">Symbol asset we're seeking to trade</param>
        /// <param name="quantity">Quantity of the asset we're seeking to trade</param>
        /// <param name="time">Time the order was placed</param>
        /// <param name="tag">User defined data tag for this order</param>
        /// <param name="properties">The order properties for this order</param>
        /// <param name="offset"></param>
        protected Order(
            Symbol symbol,
            decimal quantity,
            DateTime time,
            string tag = "",
            IOrderProperties properties = null,
            OrderOffset offset = OrderOffset.None)
        {
            this.time = time;
            timeZoneTime = time;
            price = 0;
            priceCurrency = string.Empty;
            this.quantity = quantity;
            this.symbol = symbol;
            status = OrderStatus.None;
            this.tag = tag;
            brokerId = new List<string>();
            this.contingentId = 0;
            this.properties = properties ?? new OrderProperties();
            this.offset = offset;
            fillQuantity = 0;
            averageFillPrice = 0;
            commission = 0;
        }

        /// <summary>
        /// Gets the value of this order at the given market price in units of the account currency
        /// NOTE: Some order types derive value from other parameters, such as limit prices
        /// </summary>
        /// <param name="security">The security matching this order's symbol</param>
        /// <returns>The value of this order given the current market price</returns>
        public decimal GetValue(Security security)
        {
            var value = GetValueImpl(security);
            return value * security.QuoteCurrency.ConversionRate * security.SymbolProperties.ContractMultiplier;
        }

        /// <summary>
        /// Gets the order value in units of the security's quote currency for a single unit.
        /// A single unit here is a single share of stock, or a single barrel of oil, or the
        /// cost of a single share in an option contract.
        /// </summary>
        /// <param name="security">The security matching this order's symbol</param>
        protected abstract decimal GetValueImpl(Security security);

        /// <summary>
        /// Modifies the state of this order to match the update request
        /// </summary>
        /// <param name="request">The request to update this order object</param>
        public virtual void ApplyUpdateOrderRequest(UpdateOrderRequest request)
        {
            if (request.OrderId != Id)
            {
                throw new ArgumentException("Attempted to apply updates to the incorrect order!");
            }
            if (request.Quantity.HasValue)
            {
                Quantity = request.Quantity.Value;
            }
            if (request.Tag != null)
            {
                Tag = request.Tag;
            }
        }

        public virtual void ApplyUpdateAdvanceOrder(decimal price)
        {

        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            var sysOrder = "BrokerId:";
            if (BrokerId != null && BrokerId.Count > 0)
            {
                sysOrder += string.Join(",", BrokerId);
            }
            return $"OrderId: {Id} {Status} {Type} order for {Quantity} unit{(Quantity == 1 ? "" : "s")} of {Symbol} fill:{FillQuantity} {sysOrder}";
        }

        /// <summary>
        /// Creates a deep-copy clone of this order
        /// </summary>
        /// <returns>A copy of this order</returns>
        public abstract Order Clone();

        /// <summary>
        /// Copies base Order properties to the specified order
        /// </summary>
        /// <param name="order">The target of the copy</param>
        protected void CopyTo(Order order)
        {
            order.id = id;
            order.time = time;
            order.fillTime = fillTime;//lean-monitor界面显示修改
            order.timeZoneTime = timeZoneTime;
            order.lastFillTime = lastFillTime;
            order.lastUpdateTime = lastUpdateTime;
            order.canceledTime = canceledTime;
            order.brokerId = brokerId.ToList();
            order.contingentId = contingentId;
            order.price = price.Normalize();
            order.priceCurrency = priceCurrency;
            order.quantity = quantity.Normalize();
            order.status = status;
            order.symbol = symbol;
            order.tag = tag;
            order.properties = properties.Clone();
            order.orderSubmissionData = orderSubmissionData?.Clone();
            order.offset = offset;
            order.fillQuantity = fillQuantity;
            order.averageFillPrice = averageFillPrice;
            order.commission = commission;
        }

        /// <summary>
        /// Creates an <see cref="Order"/> to match the specified <paramref name="request"/>
        /// </summary>
        /// <param name="request">The <see cref="SubmitOrderRequest"/> to create an order for</param>
        /// <returns>The <see cref="Order"/> that matches the request</returns>
        public static Order CreateOrder(SubmitOrderRequest request)
        {
            Order order;
            switch (request.OrderType)
            {
                case OrderType.Market:
                    order = new MarketOrder(request.Symbol, request.Quantity, request.Time, request.Tag, request.OrderProperties);
                    break;

                case OrderType.Limit:
                    order = new LimitOrder(request.Symbol, request.Quantity, request.LimitPrice, request.Time, request.Tag, request.OrderProperties, request.LimitPriceAdvence, request.Advence);
                    break;

                case OrderType.StopMarket:
                    order = new StopMarketOrder(request.Symbol, request.Quantity, request.StopPrice, request.Time, request.Tag, request.OrderProperties, request.StopPriceTriggerType);
                    break;

                case OrderType.StopLimit:
                    order = new StopLimitOrder(request.Symbol, request.Quantity, request.StopPrice, request.LimitPrice, request.Time, request.Tag, request.OrderProperties, request.StopPriceTriggerType);
                    break;

                case OrderType.MarketOnOpen:
                    order = new MarketOnOpenOrder(request.Symbol, request.Quantity, request.Time, request.Tag, request.OrderProperties);
                    break;

                case OrderType.MarketOnClose:
                    order = new MarketOnCloseOrder(request.Symbol, request.Quantity, request.Time, request.Tag, request.OrderProperties);
                    break;

                case OrderType.OptionExercise:
                    order = new OptionExerciseOrder(request.Symbol, request.Quantity, request.Time, request.Tag, request.OrderProperties);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
            order.Status = OrderStatus.New;
            order.Id = request.OrderId;
            order.Offset = request.Offset;
            order.LastUpdateTime = request.Time; // 订单生成的时候给order.LastUpdateTime赋值，方便模拟撮合是用该时间戳做比较； writter:fifi
            if (request.Tag != null)
            {
                order.Tag = request.Tag;
            }
            return order;
        }
    }
}
