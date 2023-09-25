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

using Newtonsoft.Json;
using QuantConnect.Data.Market;
using QuantConnect.Logging;
using System.Text.RegularExpressions;
using QuantConnect.Orders;
using RestSharp;
using System;
using System.Timers;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using QuantConnect.Securities;
using WebSocketSharp;
using QuantConnect.Orders.Fees;
using TheOne.Deribit;

namespace QuantConnect.Brokerages.Deribit
{
    /// <summary>
    /// Utility methods for Bitfinex brokerage
    /// </summary>
    public partial class DeribitBrokerage
    {
        /// <summary>
        /// Unix Epoch
        /// </summary>
        public readonly DateTime dt1970 = new DateTime(1970, 1, 1);
        /// <summary>
        /// jsonrpc
        /// </summary>
        public const string JsonRPC = "2.0";

        private const string ORDER_PREFIX = "QCDERIBIT";

        /// <summary>
        /// Encode string in base64 format.
        /// </summary>
        /// <param name="text">String to be encoded.</param>
        /// <returns>Encoded string.</returns>

        public static string Base64Encode(string text)
        {
            return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="orgAvgPrice"></param>
        /// <param name="orgVolume"></param>
        /// <param name="newAvgPrice"></param>
        /// <param name="newVolume"></param>
        /// <returns></returns>
        public static decimal CaculateFillPrice(SecurityType type,decimal orgAvgPrice, decimal orgVolume, decimal newAvgPrice, decimal newVolume)
        {
            if (type == SecurityType.Option)
            {
                orgVolume = Math.Abs(orgVolume);
                newVolume = Math.Abs(newVolume);
                var fillPrice = (newAvgPrice * newVolume - orgAvgPrice * orgVolume) / (newVolume - orgVolume);
                return fillPrice;
            }

            if (type == SecurityType.Future)
            {
                orgVolume = Math.Abs(orgVolume);
                newVolume = Math.Abs(newVolume);
                var fillAmount = newVolume - orgVolume;
                decimal v = 0;
                if (orgAvgPrice == 0)
                {
                    v = (newVolume / newAvgPrice);
                }
                else
                {
                    v = ((newVolume / newAvgPrice) - (orgVolume / orgAvgPrice));
                }
                
                return fillAmount / v;
            }

            throw new Exception($"CaculateFillPrice type {type}");
        }

        private static OrderStatus ConvertOrderStatus(DeribitMessages.Trade trade)
        {
            if (trade.state == "open" && trade.amount > 0)
            {
                return Orders.OrderStatus.PartiallyFilled;
            }
            else if (trade.state == "filled")
            {
                return Orders.OrderStatus.Filled;
            }

            return Orders.OrderStatus.None;
        }

        private static OrderStatus ConvertOrderStatus(DeribitMessages.StopOrderEntity order)
        {
            if (order.order_state == "new")
            {
                return Orders.OrderStatus.Submitted;
            }
            else if (order.order_state == "triggered")
            {
                return Orders.OrderStatus.Submitted;
            }
            else if (order.order_state == "cancelled")
            {
                return Orders.OrderStatus.Canceled;
            }
            else
            {
                throw new Exception($"OrderStatus {order.order_state}");
            }

            return Orders.OrderStatus.None;

        }


        private static OrderStatus ConvertOrderStatus(DeribitMessages.Order order)
        {
        //order state, "open", "filled", "rejected", "cancelled", "untriggered"
            if (order.order_state == "open" && order.filled_amount == 0)
            {
                if (order.triggered == true)
                {
                    return Orders.OrderStatus.Triggered;
                }
                return Orders.OrderStatus.Submitted;
            }
            else if (order.order_state == "open" && order.amount > order.filled_amount)
            {
                return Orders.OrderStatus.PartiallyFilled;
            }
            else if (order.order_state == "filled" && order.amount > order.filled_amount)
            {
                return Orders.OrderStatus.PartiallyFilled;
            }
            else if (order.order_state == "filled" && order.amount == order.filled_amount)
            {
                return Orders.OrderStatus.Filled;
            }
            else if (order.order_state == "cancelled")
            {
                return Orders.OrderStatus.Canceled;
            }
            else if (order.order_state == "rejected" || order.order_state.Contains("rejected"))
            {
                return Orders.OrderStatus.Invalid;
            }
            else if (order.order_state == "untriggered")
            {
                return Orders.OrderStatus.Submitted;
            }
            else if (order.order_state == "triggered")
            {
                return Orders.OrderStatus.Triggered;
            }
            else
            {
                throw new Exception($"OrderStatus {order.order_state}");
            }

            return Orders.OrderStatus.None;
        }

        private static string ConvertOrderType(OrderType orderType)
        {
            string outputOrderType = string.Empty;
            switch (orderType)
            {
                case OrderType.Limit:
                    outputOrderType = "limit";
                    break;
                case OrderType.Market:
                    outputOrderType = "market";
                    break;
                case OrderType.StopLimit:
                    outputOrderType = "stop_limit";
                    break;
                case OrderType.StopMarket:
                    outputOrderType = "stop_market";
                    break;
                default:
                    throw new NotSupportedException($"DeribitBrokerage.ConvertOrderType: Unsupported order type: {orderType}");
            }
            return outputOrderType;
        }

        private static string ConvertOrderDirection(OrderDirection orderDirection)
        {
            if (orderDirection == OrderDirection.Buy || orderDirection == OrderDirection.Sell)
            {
                return orderDirection.ToString().ToLower();
            }

            throw new NotSupportedException($"BitfinexBrokerage.ConvertOrderDirection: Unsupported order direction: {orderDirection}");
        }

        public Holding ConvertHolding(DeribitMessages.Position position)
        {
            Holding ret = null;
            var symbol = _symbolMapper.GetLeanSymbol(position.instrument_name);
            if (symbol.SecurityType == SecurityType.Option)
            {
                string currencySymbol = "$";
                string s = symbol.Value.Substring(0, 3);
                if (s == "BTC")
                {
                    currencySymbol = Currencies.GetCurrencySymbol("BTC");
                }
                if (s == "ETH")
                {
                    currencySymbol = Currencies.GetCurrencySymbol("ETH");
                }

                var holding = new Holding
                {
                    Symbol = _symbolMapper.GetLeanSymbol(position.instrument_name),
                    AveragePrice = position.average_price,
                    Quantity = position.size,
                    UnrealizedPnL = position.floating_profit_loss_usd,
                    CurrencySymbol = currencySymbol,
                    MarketPrice = position.mark_price,
                    Type = _symbolMapper.GetBrokerageSecurityType(position.instrument_name)
                };
                ret = holding;
            }
            else if (symbol.SecurityType == SecurityType.Future)
            {
                var holding = new Holding
                {
                    Symbol = _symbolMapper.GetLeanSymbol(position.instrument_name),
                    AveragePrice = position.average_price,
                    Quantity = position.size,
                    UnrealizedPnL = position.floating_profit_loss_usd,
                    CurrencySymbol = "$",
                    MarketPrice = position.mark_price,
                    Type = _symbolMapper.GetBrokerageSecurityType(position.instrument_name)
                };
                ret = holding;
            }
            return ret;
        }

        public OrderRecord ConvertOrderRecord(DeribitMessages.Order item)
        {
            var order =ConvertOrder(item);
            return new OrderRecord(){order = order};
        }

        public OrderRecord ConvertOrderRecord(DeribitMessages.StopOrderEntity item)
        {
            var order = ConvertOrder(item);
            return new OrderRecord() { order = order };
        }

        public Order ConvertOrder(DeribitMessages.Order item)
        {
            Order order = null;
            if (item.order_type == "market")
            {
                if (item.stop_order_id == null && item.stop_price == 0)
                {
                    order = new MarketOrder {Price = item.price};
                    order.BrokerId = new List<string> {item.order_id};
                }
                else
                {
                    order = new StopMarketOrder { StopPrice = item.stop_price, StopTriggered = item.triggered };
                    order.BrokerId = new List<string> { item.order_id, item.stop_order_id };
                }
            }
            else if (item.order_type == "limit")
            {
                // stop limit order 触发后 stop_order_id有值，order_type 为limit
                if (item.stop_order_id == null && item.stop_price == 0)//普通订单
                {
                    if (item.advanced.IsNullOrEmpty())
                    {
                        order = new LimitOrder { LimitPrice = item.price };
                    }
                    else
                    {
                        if (item.advanced == "implv")
                        {
                            order = new LimitOrder { LimitPrice = item.price, Advance = item.advanced, LimitPriceAdvance = item.implv };
                        }
                        else if (item.advanced == "usd")
                        {
                            order = new LimitOrder { LimitPrice = item.price, Advance = item.advanced, LimitPriceAdvance = item.usd };
                        }
                    }
                    order.BrokerId = new List<string> { item.order_id };
                }
                else//stop_limit 已触发 转化的limit order
                {
                    //if (item.advanced.IsNullOrEmpty())
                    //{
                    //    order = new StopLimitOrder { LimitPrice = item.price, StopPrice = item.stop_price, StopTriggered = item.triggered };
                    //}
                    //else
                    //{
                    //    if (item.advanced == "implv")
                    //    {
                    //        order = new StopLimitOrder { LimitPrice = item.price, StopPrice = item.stop_price, StopTriggered = item.triggered};
                    //    }
                    //    else if (item.advanced == "usd")
                    //    {
                    //        order = new StopLimitOrder { LimitPrice = item.price, StopPrice = item.stop_price, StopTriggered = item.triggered};
                    //    }
                    //}

                    //只有期货有stop单，期货没有advanced
                    order = new StopLimitOrder { LimitPrice = item.price, StopPrice = item.stop_price, StopTriggered = item.triggered };
                    order.BrokerId = new List<string> { item.order_id, item.stop_order_id };
                }
            }
            else if (item.order_type == "stop_market")
            {
                //order_id "SLTB-3057795" 有值 stop_order_id == null 未触发时 
                order = new StopMarketOrder { StopPrice = item.stop_price,StopTriggered = item.triggered };
                order.BrokerId = new List<string> { item.order_id };
            }
            else if (item.order_type == "stop_limit")
            {
                //order_id "SLTB-3057795" 有值 stop_order_id == null 未触发时 
                if (item.advanced.IsNullOrEmpty())
                {
                    order = new StopLimitOrder { LimitPrice = item.price, StopPrice = item.stop_price, StopTriggered = item.triggered };
                }
                else
                {
                    if (item.advanced == "implv")
                    {
                        order = new StopLimitOrder { LimitPrice = item.price, StopPrice = item.stop_price, StopTriggered = item.triggered };
                    }
                    else if (item.advanced == "usd")
                    {
                        order = new StopLimitOrder { LimitPrice = item.price, StopPrice = item.stop_price, StopTriggered = item.triggered};
                    }
                }
                order.BrokerId = new List<string> { item.order_id };
            }
            else
            {
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1 ,
                    "DeribitBrokerage.ConvertOrder: Unsupported order type returned from brokerage: " + item.order_type));
                return null;
            }

            order.Quantity = item.direction == "sell" ? -item.amount : item.amount;
            order.Symbol = _symbolMapper.GetLeanSymbol(item.instrument_name);
            order.Time = Time.UnixTimeStampToDateTime((double)item.creation_timestamp / 1000);
            order.LastUpdateTime = Time.UnixTimeStampToDateTime((double)item.last_update_timestamp / 1000);
            order.Status = ConvertOrderStatus(item);
            order.Price = item.price;

            //必须注意 添加原有的label解析出来的orderid
            long orderId;
            if (ParseQcOrderId(item.label,out orderId))
            {
                order.Id = orderId;
            }
            else
            {
                order.Id = _algorithm.Transactions.GetIncrementOrderId();
            }

            return order;
        }

        private bool ParseQcOrderId(string label,out long orderId)
        {
            orderId = 0;
            if (!label.IsNullOrEmpty())
            {
                if (long.TryParse(label, out orderId))
                {
                    return true;
                }
            }
            return false;
        }

        public Order ConvertOrder(DeribitMessages.StopOrderEntity item)
        {
            Order order = null;
            if (item.order_type == "market")
            {
                order = new StopMarketOrder { Price = item.price, StopPrice = item.stop_price };
                if (item.order_state == "new" && item.order_id !=null)
                {
                    ((StopMarketOrder)order).StopTriggered = true;
                    order.BrokerId = new List<string> { item.order_id,item.stop_id };
                }

                if (item.order_state == "cancelled")
                {
                    ((StopMarketOrder)order).StopTriggered = false;
                    order.BrokerId = new List<string> { item.stop_id };
                }

                if (item.order_state.Contains("rejected"))
                {
                    ((StopMarketOrder)order).StopTriggered = false;
                    order.BrokerId = new List<string> { item.stop_id };
                }

                if (item.order_state.Contains("triggered"))
                {
                    ((StopMarketOrder)order).StopTriggered = true;
                    order.BrokerId = new List<string> { item.order_id,item.stop_id };
                }
            }
            else if (item.order_type == "limit")
            {
                order = new StopLimitOrder { Price = item.price, StopPrice = item.stop_price};
                if (item.order_state == "new" && item.order_id != null)
                {
                    ((StopLimitOrder)order).StopTriggered = true;
                    order.BrokerId = new List<string> { item.order_id, item.stop_id };
                }

                if (item.order_state == "cancelled")
                {
                    ((StopLimitOrder)order).StopTriggered = false;
                    order.BrokerId = new List<string> { item.stop_id };
                }

                if (item.order_state.Contains("rejected"))
                {
                    ((StopLimitOrder)order).StopTriggered = false;
                    order.BrokerId = new List<string> { item.stop_id };
                }
            }

            order.Quantity = item.direction == "sell" ? -item.amount : item.amount;
            order.Symbol = _symbolMapper.GetLeanSymbol(item.instrument_name);
            order.Time = Time.UnixTimeStampToDateTime((double)item.timestamp / 1000);
            order.LastUpdateTime = Time.UnixTimeStampToDateTime((double)item.last_update_timestamp / 1000);
            order.Status = ConvertOrderStatus(item);
            order.Price = item.price;

            //必须注意 添加原有的label解析出来的orderid
            if (!item.label.IsNullOrEmpty())
            {
                long orderId;
                if (long.TryParse(item.label, out orderId))
                {
                    order.Id = orderId;
                }
            }

            return order;
        }


        public TradeRecord ConvertTradeRecord(DeribitMessages.Trade trade)
        {
            TradeRecord record = new TradeRecord();
            record.Symbol = _symbolMapper.GetLeanSymbol(trade.instrument_name);
            record.TradeId = trade.trade_id;
            record.OrderId = trade.order_id;
            record.Status = ConvertOrderStatus(trade);
            record.Direction = trade.direction == "buy" ? OrderDirection.Buy : OrderDirection.Sell;
            record.Offset = OrderOffset.None;
            record.Time = Time.UnixTimeStampToDateTime(((double)trade.timestamp) / 1000);
            record.Amount = trade.amount;
            record.Price = trade.price;
            record.ProfitLoss = trade.profit_loss ?? 0;
            record.Fee = new OrderFee(new CashAmount(trade.fee, trade.fee_currency));
            record.IndexPrice = trade.index_price;
            record.UnderlyingPrice = trade.underlying_price;
            record.MarkPrice = trade.mark_price;
            return record;
        }

        private static string ByteArrayToString(byte[] ba)
        {
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
        
        /// <summary>
        /// 在指定时间过后执行指定的表达式
        /// </summary>
        /// <param name="interval">事件之间经过的时间（以毫秒为单位）</param>
        /// <param name="action">要执行的表达式</param>
        public static void SetTimeout(double interval, System.Action action)
        {
            System.Timers.Timer timer = new System.Timers.Timer(interval);
            timer.Elapsed += delegate (object sender, System.Timers.ElapsedEventArgs e)
            {
               timer.Enabled = false;
               action();
            };
            timer.Enabled = true;
        }

        /// <summary>
        /// 在指定时间周期重复执行指定的表达式
        /// </summary>
        /// <param name="interval">事件之间经过的时间（以毫秒为单位）</param>
        /// <param name="action">要执行的表达式</param>
        public static void SetInterval(double interval, Action<ElapsedEventArgs> action)
        {
            System.Timers.Timer timer = new System.Timers.Timer(interval);
            timer.Elapsed += delegate (object sender, System.Timers.ElapsedEventArgs e)
            {
                action(e);
            };
            timer.Enabled = true;
        }
    }
}
