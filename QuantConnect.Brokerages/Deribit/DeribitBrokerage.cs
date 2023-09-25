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
using Newtonsoft.Json.Linq;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Packets;
using QuantConnect.Securities;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using QuantConnect.Orders.Fees;
using QuantConnect.Configuration;
using System.Collections.Concurrent;
using System.Globalization;
using System.Collections;
using System.Threading;
using QuantConnect.Util;
using WebSocketSharp;
using TheOne.Deribit;

namespace QuantConnect.Brokerages.Deribit
{
    public partial class DeribitBrokerage :Brokerage, IDataQueueHandler, IDataQueueUniverseProvider
    {      

        #region IBrokerage
        /// <summary>
        /// Checks if the websocket connection is connected or in the process of connecting
        /// </summary>
        public override bool IsConnected => _mdSubApi.IsLogined && _tdSubApi.IsLogined;

        /// <summary>
        /// 
        /// </summary>
        public bool IsLogin => _tdSubApi.IsLogined;
        /// <summary>
        /// Specifies whether the brokerage will instantly update account balances
        /// </summary>
        public override bool AccountInstantlyUpdated { get; } = true;        


        /// <summary>
        /// Places a new order and assigns a new broker ID to the order
        /// </summary>
        /// <param name="order">The order to be placed</param>
        /// <returns>True if the request for a new order has been placed, false otherwise</returns>
        public override bool PlaceOrder(Order order)
        {
            Log.Trace("DeribitBrokerage.PlaceOrder: " + order.ToString());
            // BTC期货的下单量必须是10的倍数
            if (order.Symbol.SecurityType == SecurityType.Future && order.Symbol.Value.Contains("BTC"))
            {
                if (Math.Abs(Convert.ToInt32(order.Quantity)) % 10 != 0)
                {
                    Log.Error($"DeribitBrokerage.PlaceOrder order id {order.Id} symbol {order.Symbol.Value} Quantity {order.Quantity} must be multiple of 10");
                    return false;
                }
            }

            //不支持现货下单
            if (order.Symbol.SecurityType == SecurityType.Crypto)
            {
                Log.Error($"DeribitBrokerage.PlaceOrder order id {order.Id} symbol {order.Symbol.Value} Quantity {order.Quantity} is crypto, deribit not support");
                return false;
            }

            if (!IsLogin)
            {
                Log.Error($"DeribitBrokerage.PlaceOrder order id {order.Id} symbol {order.Symbol.Value} Quantity {order.Quantity} error, brokerage disconnect");
                return false;
            }

            //_cachedOrders.TryAdd(order.Id, order.Clone());
            var orderData = new DeribitOrderInfo
            {
                InputLocalId = order.Id,
                Order = order
            };
            if(!_ordersInfo.TryAdd(order.Id, orderData))
            {
                Log.Error($"DeribitBrokerage.PlaceOrder order id {order.Id} symbol {order.Symbol.Value} Quantity {order.Quantity} error,order id has exist");
                return false;
            }

            if (order.Direction == OrderDirection.Buy)
            {
                switch (order.Type)
                {
                    case OrderType.Limit:
                        return DeribitLimitBuy(order, InsertOrderCallBack);
                    case OrderType.Market:
                        return DeribitMarketBuy(order, InsertOrderCallBack);
                    case OrderType.StopLimit:
                        return DeribitStopLimitBuy(order, InsertOrderCallBack);
                    case OrderType.StopMarket:
                        return DeribitStopMarketBuy(order, InsertOrderCallBack);
                    default:
                        Log.Error($"DeribitBrokerage.PlaceOrder order id {order.Id} 不支持的订单类型");
                        return false;
                }
            }
            else if (order.Direction == OrderDirection.Sell)
            {
                switch (order.Type)
                {
                    case OrderType.Limit:
                        return DeribitLimitSell(order, InsertOrderCallBack);
                    case OrderType.Market:
                        return DeribitMarketSell(order, InsertOrderCallBack);
                    case OrderType.StopLimit:
                        return DeribitStopLimitSell(order, InsertOrderCallBack);
                    case OrderType.StopMarket:
                        return DeribitStopMarketSell(order, InsertOrderCallBack);
                    default:
                        Log.Error($"DeribitBrokerage.PlaceOrder order id {order.Id} 不支持的订单类型");
                        return false;
                }
            }
            else
            {
                //throw new ArgumentOutOfRangeException("Deribit.PlaceOrder : quantity can not be zero when place a order.");       
                Log.Error($"Deribit.PlaceOrder : quantity can not be zero when place a order. {order.Id}");
            }
            return false;
        }

        private bool InsertOrderCallBack(
            Order input,
            bool succeeded,
            DeribitMessages.PlaceOrderResult result)
        {
            if (!succeeded)
            {
                if (result == null)
                {
                    return false;
                }
                if (result.error != null)
                {
                    OnOrderEvent(new OrderEvent(input, DateTime.UtcNow, OrderFee.Zero, "Deribit Order Event") { Status = OrderStatus.Invalid, Message = result.error.ErrorMsg });
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, (int)result.error.ErrorCode, result.error.ErrorMsg));
                    return true;
                }

                return false;
            }
            else
            {
                if (result == null)
                {
                    Log.Error($"InsertOrderCallBack succeed:{succeeded} result is null");
                    return false;
                }

                if (result.error != null)//此条件不应该发生
                {
                    OnOrderEvent(new OrderEvent(input, DateTime.UtcNow, OrderFee.Zero, "Deribit Order Event") { Status = OrderStatus.Invalid, Message = result.error.ErrorMsg });
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, (int)result.error.ErrorCode, result.error.ErrorMsg));
                    return false;
                }

                if (string.IsNullOrEmpty(result.place_order.order.order_id))
                {
                    OnOrderEvent(new OrderEvent(input, DateTime.UtcNow, OrderFee.Zero, "Deribit Order Event") { Status = OrderStatus.Invalid, Message = "place order failed" });
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, (int)-1, "place order failed"));
                    return false;
                }
                //先加上brokerage id ,防止撤单找不到deribit的撤单id
                DeribitOrderInfo orderInfo;
                if (!_ordersInfo.TryGetValue(input.Id, out orderInfo))
                {
                    Log.Error($"缓存中找不到订单 qcId:{input.Id}");
                    return false;
                }

                if (orderInfo.DeribitOrder == null)
                {
                    orderInfo.DeribitOrder = result.place_order.order;
                }

                lock(_lock)
                {
                    if(!input.BrokerId.Contains(result.place_order.order.order_id))
                    {
                        input.BrokerId.Insert(0,result.place_order.order.order_id);
                    }
                    if (!result.place_order.order.stop_order_id.IsNullOrEmpty())
                    {
                        if (!input.BrokerId.Contains(result.place_order.order.stop_order_id))
                        {
                            input.BrokerId.Add(result.place_order.order.stop_order_id);
                        }
                    }
                }

                Log.Trace($"InsertOrderCallBack dOrder order_id:{result.place_order.order.order_id} order_state:{result.place_order.order.order_state} stop_order_id:{result.place_order.order.stop_order_id} triggered:{result.place_order.order.triggered}");

                _tradingAction.Post(new TradingAction(result.place_order.order, false));
                foreach (var trade in result.place_order.trades)
                {
                    _tradingAction.Post(new TradingAction(trade, false));
                }

                return true;
            }
        }

        private bool DeribitLimitBuy(
            Order input,
            Func<Order, bool, DeribitMessages.PlaceOrderResult,bool> callback)
        {
            try
            {
                DeribitMessages.PlaceOrderResult response;
                var limitOrder = input as LimitOrder;
                var successful = _restApi.Buy(new DeribitBuySellRequest()
                {
                    instrument_name = limitOrder.Symbol.Value,
                    price = limitOrder.LimitPrice,
                    amount = limitOrder.AbsoluteQuantity,
                    type = DeribitEnums.OrderType.limit,
                    label = limitOrder.Id.ToString(),
                    advanced = limitOrder.Advance,
                }, out response);

                return callback(input, successful, response);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return callback(input, false, null);
            }
        }
        private bool DeribitLimitSell(
            Order input,
            Func<Order, bool, DeribitMessages.PlaceOrderResult,bool> callback)
        {
            try
            {
                DeribitMessages.PlaceOrderResult response;
                var limitOrder = input as LimitOrder;
                var successful = _restApi.Sell(new DeribitBuySellRequest()
                {
                    instrument_name = limitOrder.Symbol.Value,
                    price = limitOrder.LimitPrice,
                    amount = limitOrder.AbsoluteQuantity,
                    type = DeribitEnums.OrderType.limit,
                    label = limitOrder.Id.ToString(),
                    advanced = limitOrder.Advance,
                }, out response);

                return callback(input, successful, response);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return callback(input, false, null);
            }
        }

        private bool DeribitMarketBuy(
            Order input,
            Func<Order, bool, DeribitMessages.PlaceOrderResult,bool> callback)
        {
            try
            {
                DeribitMessages.PlaceOrderResult response;
                var marketOrder = input as MarketOrder;
                var successful = _restApi.Buy(new DeribitBuySellRequest()
                {
                    instrument_name = marketOrder.Symbol.Value,
                    amount = marketOrder.AbsoluteQuantity,
                    type = DeribitEnums.OrderType.market,
                    label = marketOrder.Id.ToString(),
                }, out response);

                return callback(input, successful, response);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return callback(input, false, null);
            }
        }

        private bool DeribitMarketSell(
            Order input,
            Func<Order, bool, DeribitMessages.PlaceOrderResult, bool> callback)
        {
            try
            {
                DeribitMessages.PlaceOrderResult response;
                var marketOrder = input as MarketOrder;
                var successful = _restApi.Sell(new DeribitBuySellRequest()
                {
                    instrument_name = marketOrder.Symbol.Value,
                    amount = marketOrder.AbsoluteQuantity,
                    type = DeribitEnums.OrderType.market,
                    label = marketOrder.Id.ToString(),
                }, out response);

                return callback(input, successful, response);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return callback(input, false, null);
            }
        }

        private bool DeribitStopLimitBuy(
            Order input,
            Func<Order, bool, DeribitMessages.PlaceOrderResult, bool> callback)
        {
            try
            { 
                DeribitMessages.PlaceOrderResult response;
                var stopOrder = input as StopLimitOrder;
               var successful = _restApi.Buy(new DeribitBuySellRequest()
                {
                    instrument_name = stopOrder.Symbol.Value,
                    price = stopOrder.LimitPrice,
                    stop_price = stopOrder.StopPrice,
                    amount = stopOrder.AbsoluteQuantity,
                    type = DeribitEnums.OrderType.stop_limit,
                    label = stopOrder.Id.ToString(),
                    trigger = DeribitEnums.OrderTriggerType.last_price,
                },out response);

               return callback(input, successful, response);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return callback(input, false, null);
            }
        }

        private bool DeribitStopLimitSell(
            Order input,
            Func<Order, bool, DeribitMessages.PlaceOrderResult, bool> callback)
        {
            try
            {
                DeribitMessages.PlaceOrderResult response;
                var stopOrder = input as StopLimitOrder;
                var successful = _restApi.Sell(new DeribitBuySellRequest()
                {
                    instrument_name = stopOrder.Symbol.Value,
                    price = stopOrder.LimitPrice,
                    stop_price = stopOrder.StopPrice,
                    amount = stopOrder.AbsoluteQuantity,
                    type = DeribitEnums.OrderType.stop_limit,
                    label = stopOrder.Id.ToString(),
                    trigger = DeribitEnums.OrderTriggerType.last_price,
                }, out response);

                return callback(input, successful, response);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return callback(input, false, null);
            }
        }

        private bool DeribitStopMarketBuy(
            Order input,
            Func<Order, bool, DeribitMessages.PlaceOrderResult,bool> callback)
        {
            try
            {
                DeribitMessages.PlaceOrderResult response;
                var stopOrder = input as StopMarketOrder;
                var successful = _restApi.Buy(new DeribitBuySellRequest()
                {
                    instrument_name = stopOrder.Symbol.Value,
                    stop_price = stopOrder.StopPrice,
                    amount = stopOrder.AbsoluteQuantity,
                    type = DeribitEnums.OrderType.stop_market,
                    label = stopOrder.Id.ToString(),
                    trigger = DeribitEnums.OrderTriggerType.last_price,
                }, out response);

                return callback(input, successful, response);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return callback(input, false, null);
            }
        }

        private bool DeribitStopMarketSell(
            Order input,
            Func<Order, bool, DeribitMessages.PlaceOrderResult,bool> callback)
        {
            try
            {
                DeribitMessages.PlaceOrderResult response;
                var stopOrder = input as StopMarketOrder;
                var successful = _restApi.Sell(new DeribitBuySellRequest()
                {
                    instrument_name = stopOrder.Symbol.Value,
                    stop_price = stopOrder.StopPrice,
                    amount = stopOrder.AbsoluteQuantity,
                    type = DeribitEnums.OrderType.stop_market,
                    label = stopOrder.Id.ToString(),
                    trigger = DeribitEnums.OrderTriggerType.last_price,
                }, out response);

                return callback(input, successful, response);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return callback(input, false, null);
            }
        }

        private bool DeribitUpdateOrder(
            Order input,
            Func<Order, bool, DeribitMessages.PlaceOrderResult, bool> callback)
        {
            try
            {
                DeribitMessages.PlaceOrderResult response;
                var stopOrder = input as StopMarketOrder;
                var successful = _restApi.Sell(new DeribitBuySellRequest()
                {
                    instrument_name = stopOrder.Symbol.Value,
                    stop_price = stopOrder.StopPrice,
                    amount = stopOrder.AbsoluteQuantity,
                    type = DeribitEnums.OrderType.stop_market,
                    label = stopOrder.Id.ToString(),
                    trigger = DeribitEnums.OrderTriggerType.last_price,
                }, out response);

                return callback(input, successful, response);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return callback(input, false, null);
            }
        }

        /// <summary>
        /// Updates the order with the same id
        /// </summary>
        /// <param name="order">The new order information</param>
        /// <returns>True if the request was made for the order to be updated, false otherwise</returns>
        public override bool UpdateOrder(Order order)
        {
            Log.Trace($"UpdateOrder not support");
            return false;
            if (order.BrokerId.Count == 0)
            {
                throw new ArgumentNullException("DeribitBrokerage.UpdateOrder: There is no brokerage id to be updated for this order.");
            }
            if (order.BrokerId.Count > 1)
            {
                throw new NotSupportedException("DeribitBrokerage.UpdateOrder: Multiple orders update not supported. Please cancel and re-create.");
            }

            if (!IsLogin)
            {
                Log.Error($"Deribit PlaceOrder order id {order.Id} symbol {order.Symbol.Value} Quantity {order.Quantity} error, brokerage disconnect");
                return false;
            }

            return DeribitUpdateOrder(order, InsertOrderCallBack);
        }


        private bool CancelOrderCallBack(
            Order action,
            bool succeeded,
            DeribitMessages.CancelOrderResult result)
        {
            try
            {
                if (!succeeded)
                {
                    if (result.error != null)
                    {
                        Log.Trace($"CancelOrderCallBack 撤单失败 {result.error.ErrorMsg}");
                        return false;
                    }
                    Log.Trace($"CancelOrderCallBack 撤单失败");
                    return false;                   
                }
                else
                {
                    if (result == null)
                    {
                        Log.Error($"CancelOrderCallBack succeed:{succeeded} result is null");
                        return false;
                    }

                    if (result.error != null)
                    {
                        Log.Trace($"CancelOrderCallBack 撤单失败 {result.error.ErrorMsg}");
                        return false;
                    }

                    Log.Trace($"CancelOrderCallBack dOrder order_id:{result.order.order_id} order_state:{result.order.order_state} stop_order_id:{result.order.stop_order_id} triggered:{result.order.triggered}");

                    _tradingAction.Post(new TradingAction(result.order, false));
                    return true;

                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                return false;
            }
        }
        private bool DeribitCancelOrder(Order action, Func<Order, bool, DeribitMessages.CancelOrderResult,bool> callback)
        {
            try
            {
                var orderId = action.BrokerId.FirstOrDefault();
                DeribitMessages.CancelOrderResult response;
                var successful = _restApi.Cancel(orderId, out response);
                return callback(action, successful, response);
            }
            catch (Exception e)
            {
                Log.Error(e);
                return callback(action, false, null);
            }
        }



        /// <summary>
        /// Cancels the order with the specified ID
        /// </summary>
        /// <param name="order">The order to cancel</param>
        /// <returns>True if the request was submitted for cancellation, false otherwise</returns>
        public override bool CancelOrder(Order order)
        {
            Log.Trace($"DeribitBrokerage.CancelOrder(): {order.ToString()}");

            if (!order.BrokerId.Any())
            {
                // we need the brokerage order id in order to perform a cancellation
                Log.Trace("DeribitBrokerage.CancelOrder(): Unable to cancel order without BrokerId.");
                return false;
            }

            if (!IsLogin)
            {
                Log.Error($"Deribit PlaceOrder order id {order.Id} symbol {order.Symbol.Value} Quantity {order.Quantity} error, brokerage disconnect");
                return false;
            }

            return DeribitCancelOrder(order, CancelOrderCallBack);
        }

        /// <summary>
        /// Closes the websockets connection
        /// </summary>
        public override void Connect()
        {
            _mdSubApi.Connect();
            _tdSubApi.Connect();
        }

        /// <summary>
        /// Closes the websockets connection
        /// </summary>
        public override void Disconnect()
        {
            _SyncTradingTimer.Stop();
            _mdSubApi.Dispose();
            _tdSubApi.Dispose();
        }

        /// <summary>
        /// Gets all orders not yet closed
        /// </summary>
        /// <returns></returns>
        public override List<Order> GetOpenOrders()
        {
            // 只有自己下的单 并且是qc系统下的（标签对的）才能进入qc系统
            var list = new List<Order>();
            {
                var mOrders = _restApi.GetOpenOrdersByCurrency("BTC");
                foreach(var mOrder in mOrders)
                {
                    var order = ConvertOrder(mOrder);
                    var trades =new List<DeribitMessages.Trade>();
                    if (mOrder.order_type != DeribitEnums.OrderType.stop_market &&
                        mOrder.order_type != DeribitEnums.OrderType.stop_limit)
                    {
                        trades = _restApi.GetUserTradesByOrder(mOrder.order_id);
                    }

                    var orderData = new DeribitOrderInfo
                    {
                        InputLocalId = order.Id,
                        Order = order,
                        DeribitOrder = mOrder,
                    };
                    foreach (var trade in trades)
                    {
                        if (_trades.ContainsKey(trade.trade_id))
                            continue;
                        _trades.TryAdd(trade.trade_id, trade);
                        orderData.Trades.Add(trade);
                    }
                    _ordersInfo.TryAdd(order.Id, orderData);

                    list.Add(order);
                }
            }
            {
                var mOrders = _restApi.GetOpenOrdersByCurrency("ETH");
                foreach (var mOrder in mOrders)
                {
                    var order = ConvertOrder(mOrder);
                    var trades = new List<DeribitMessages.Trade>();
                    if (mOrder.order_type != DeribitEnums.OrderType.stop_market &&
                        mOrder.order_type != DeribitEnums.OrderType.stop_limit)
                    {
                        trades = _restApi.GetUserTradesByOrder(mOrder.order_id);
                    }
                    var orderData = new DeribitOrderInfo
                    {
                        InputLocalId = order.Id,
                        Order = order.Clone(),
                        DeribitOrder = mOrder,
                    };
                    foreach (var trade in trades)
                    {
                        if (_trades.ContainsKey(trade.trade_id))
                            continue;
                        _trades.TryAdd(trade.trade_id, trade);
                        orderData.Trades.Add(trade);
                    }
                    _ordersInfo.TryAdd(order.Id, orderData);

                    list.Add(order);
                }
            }
            return list;
        }
        
        /// <summary>
        /// Gets all open positions
        /// </summary>
        /// <returns></returns>
        public override List<Holding> GetAccountHoldings()
        {
            var list = _restApi.GetPositions("BTC");
            var btcHolding = list.Where(p => p.size != 0)
                .Select(ConvertHolding)
                .ToList();

            list = _restApi.GetPositions("ETH");
            var ethHolding = list.Where(p => p.size != 0)
                .Select(ConvertHolding)
                .ToList();

            _initQueryOrder = true;
            return btcHolding.Union(ethHolding).ToList();
        }

        /// <summary>
        /// Gets the total account cash balance for specified account type
        /// </summary>
        /// <returns></returns>
        public override List<CashAmount> GetCashBalance()
        {
            var list = new List<CashAmount>();
            var account = _restApi.GetAccountSummary("BTC");
            if (account.balance > 0)
            {
                list.Add(new CashAmount(account.balance, account.currency.ToUpper()));
                //添加未结算利润
                list.Add(new CashAmount(account.session_rpl, account.currency.ToUpper(),false));
            }

            account = _restApi.GetAccountSummary("ETH");
            if (account.balance > 0)
            {
                list.Add(new CashAmount(account.balance, account.currency.ToUpper()));
                //添加未结算利润
                list.Add(new CashAmount(account.session_rpl, account.currency.ToUpper(), false));
            }

            list.Add(new CashAmount(0, "USD"));
            return list;
        }

        /// <summary>
        /// Gets the history for the requested security
        /// </summary>
        /// <param name="request">The historical data request</param>
        /// <returns>An enumerable of bars covering the span specified in the request</returns>
        public override IEnumerable<BaseData> GetHistory(Data.HistoryRequest request)
        {
            if (request.Symbol.SecurityType != SecurityType.Option)
            {
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, "InvalidSecurityType",
                    $"{request.Symbol.SecurityType} security type not supported, no history returned"));
                yield break;
            }

            if (request.Resolution == Resolution.Tick || request.Resolution == Resolution.Second)
            {
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, "InvalidResolution",
                    $"{request.Resolution} resolution not supported, no history returned"));
                yield break;
            }

            if (request.Symbol == null || request.StartTimeUtc == null || request.EndTimeUtc == null)
            {
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, "Invalid Symbol or Time",
                    $"symbol:{request.Symbol} start:{request.StartTimeUtc} end:{request.EndTimeUtc} not supported, no history returned"));
                yield break;
            }

            var resolutionInMin = request.Resolution.ToTimeSpan().Minutes.ToString();
            long start = (long)Time.DateTimeToUnixTimeStamp(request.StartTimeUtc) * 1000;
            long end = (long)Time.DateTimeToUnixTimeStamp(request.EndTimeUtc) * 1000;

            var candles = _restApi.GetTradingViewChartData(request.Symbol.Value, start, end, resolutionInMin); 
            var period = request.Resolution.ToTimeSpan();
            if(candles.status == "ok")
            { 
                yield break;
            }
            for (int i = 0; i < candles.ticks.Length; i++)
            {
                yield return new TradeBar()
                {
                    Time = Time.UnixMillisecondTimeStampToDateTime(candles.ticks[i]/1000),
                    Symbol = request.Symbol,
                    Low = candles.low[i],
                    High = candles.high[i],
                    Open = candles.open[i],
                    Close = candles.close[i],
                    Volume = candles.volume[i],
                    Value = candles.close[i],
                    DataType = MarketDataType.TradeBar,
                    Period = period,
                    EndTime = Time.UnixMillisecondTimeStampToDateTime((candles.ticks[i] + (long)period.TotalMilliseconds)/1000)
                };
            }
        }

        #endregion

        #region IDataQueueHandler
        /// <summary>
        /// Get the next ticks from the live trading data queue
        /// </summary>
        /// <returns>IEnumerable list of ticks since the last update.</returns>
        public IEnumerable<BaseData> GetNextTicks()
        {
            //优化版本
            var copys = new List<BaseData>();
            ///*
            foreach (var item in _lastTicks)
            {
                if (item.Value.Updated)
                {
                    if ((item.Value.Symbol.SecurityType == SecurityType.Future ||
                         item.Value.Symbol.SecurityType == SecurityType.Option) && item.Value.MarkPrice>0)

                    {
                        copys.Add(item.Value.Clone());
                        item.Value.Updated = false;
                    }
                    else
                    {
                        copys.Add(item.Value.Clone());
                        item.Value.Updated = false;
                    }
                    //Log.Trace($"span:------ { (DateTime.UtcNow - item.Value.Time).TotalMilliseconds }");
                    
                }
            }
            //*/
            /*
            int max = 100;
            int count = 0;
            while (true)
            {
                BaseData data;
                if (_blockingCollection.TryDequeue(out data))
                {
                    copys.Add(data);
                    count++;
                    if (count >= 10) break;
                }
                else
                {
                    break;
                }
            }*/
            return copys;
        }

        /// <summary>
        /// Adds the specified symbols to the subscription
        /// </summary>
        /// <param name="job">Job we're subscribing for:</param>
        /// <param name="symbols">The symbols to be added keyed by SecurityType</param>
        public void Subscribe(LiveNodePacket job, IEnumerable<Symbol> symbols)
        {
            List<string> list =new List<string>();
            foreach (var symbol in symbols)
            {
                if (symbol.Value.StartsWith("QC-UNIVERSE-USERDEFINED-DERIBIT"))
                {
                    Log.Trace("default security do nothing");
                }
                else if (symbol.Value.Equals("BTCUSD"))
                {
                    list.Add("btc_usd");
                }
                else if (symbol.Value.Equals("ETHUSD"))
                {
                    list.Add("eth_usd");
                }
                else
                {
                    if (symbol.IsCanonical())
                    {
                        continue;
                    }

                    if (symbol.SecurityType == SecurityType.Future && !symbol.Value.Contains("PERPETUAL"))
                    {
                        continue;
                    }
                    list.Add(symbol.Value);
                }
            }

            if (list.Count > 0)
            {
                _mdSubApi.SubscribeMD(list);
            }
        }


        /// <summary>
        /// Removes the specified symbols to the subscription
        /// </summary>
        /// <param name="job">Job we're processing.</param>
        /// <param name="symbols">The symbols to be removed keyed by SecurityType</param>
        public void Unsubscribe(LiveNodePacket job, IEnumerable<Symbol> symbols)
        {
            //_mdSubApi.Unsubscribe(symbols);
        }
        #endregion

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            
        }

        public override List<string> SupportMarkets()
        {
            var lst = new List<string>();
            lst.Add(Market.Deribit);
            return lst;
        }


        /// <summary>
        /// Method returns a collection of Symbols that are available at the broker.
        /// </summary>
        /// <param name="lookupName">string representing the name to lookup</param>
        /// <param name="securityType">Expected security type of the returned symbols (if any)</param>
        /// <param name="securityCurrency">Expected security currency(if any)</param>
        /// <param name="securityExchange">Expected security exchange name(if any)</param>
        /// <returns></returns>
        public IEnumerable<Symbol> LookupSymbols(string lookupName, SecurityType securityType, string securityCurrency = null, string securityExchange = null)
        {
            // connect will throw if it fails
            //Connect();

            Log.Trace("DeribitBrokerage.LookupSymbols(): Requesting symbol list for " + lookupName + " ...");

            var symbols = new List<Symbol>();

            if (securityType == SecurityType.Option)
            {
                var underlyingSymbol = Symbol.Create(lookupName, SecurityType.Crypto, Market.Deribit);
                symbols.AddRange(_algorithm.OptionChainProvider.GetOptionContractList(underlyingSymbol, DateTime.Today));
            }
            else if(securityType == SecurityType.Future)
            {
                var underlyingSymbol = Symbol.Create(lookupName, SecurityType.Crypto, Market.Deribit);
                symbols.AddRange(_algorithm.FutureChainProvider.GetFutureContractList(underlyingSymbol, DateTime.Today));
            }
            else
            {
                throw new ArgumentException("WindBrokerage.LookupSymbols() not support securityType:" + securityType);
            }

            Log.Trace("WindBrokerage.LookupSymbols(): Returning {0} contract(s) for {1}", symbols.Count, lookupName);

            return symbols;
        }


        public Tick GetTick(Symbol symbol)
        {
            var tick =_restApi.Ticker(symbol.Value);
            return new Tick(DateTime.UtcNow, symbol, tick.best_bid_price??0, tick.best_ask_price??0);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="order"></param>
        public override void SetOrderCached(Order order)
        {
            //_cachedOrders.TryAdd(order.Id, order.Clone());
            //_cachedOrders.TryAdd(order.Id, order);
        }


        //private void GetOpenOrdersForOrderEvent()
        //{
        //    try
        //    {
        //        var openOrders = CachedOrderIDs.Where(x => !x.Value.Status.IsClosed()).ToList();
        //        return;
        //        foreach (var order in openOrders)
        //        {
        //            if (order.Value.BrokerId.Count > 0)
        //            {
        //                bool triggered = false;
        //                bool isStopOrder = false;
        //                var stopLimitOrder = order.Value as StopLimitOrder;
        //                if (stopLimitOrder != null)
        //                {
        //                    isStopOrder = true;
        //                    triggered = stopLimitOrder.StopTriggered;
        //                }

        //                var stopMarketOrder = order.Value as StopMarketOrder;
        //                if (stopMarketOrder != null)
        //                {
        //                    isStopOrder = true;
        //                    triggered = stopLimitOrder.StopTriggered;
        //                }

        //                var brokerId = order.Value.BrokerId[0];
        //                if (isStopOrder && triggered != false && brokerId.Contains("SL"))
        //                {
        //                    var mOrder = GetOrderState(brokerId);
        //                    if (mOrder != null) //说明还没触发
        //                    {
        //                        ConvertToOrderEvent(mOrder);
        //                    }
        //                    else // not found ,already triggered
        //                    {
        //                        var list = GetStopOrderHistory(order.Value.Symbol.Value.Substring(0, 3), order.Value.Symbol.Value);
        //                        foreach (var item in list)
        //                        {
        //                            if (item.stop_id == brokerId)
        //                            {
        //                                var mOrder2 = GetOrderState(item.order_id);
        //                                ConvertToOrderEvent(mOrder2);
        //                                var trades = GetUserTradesByOrder(item.order_id);
        //                                var records = trades.Select(ConvertTradeRecord).ToList();
        //                                OnTradeOccurred(records);
        //                            }
        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    var mOrder = GetOrderState(brokerId);
        //                    if (mOrder != null)
        //                    {
        //                        ConvertToOrderEvent(mOrder);
        //                        var trades = GetUserTradesByOrder(brokerId);
        //                        var records = trades.Select(ConvertTradeRecord).ToList();
        //                        OnTradeOccurred(records);
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Log.Error(ex.Message);
        //    }
        //}



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override List<TradeRecord> GetHistoryTrades()
        {
            var list = new List<TradeRecord>(_historyTradeRecord);
            _historyTradeRecord.Clear();
            return list;
        }

        private List<TradeRecord> _historyTradeRecord = new List<TradeRecord>();

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override List<OrderRecord> GetHistoryOrders()
        {
            var list = new List<OrderRecord>();

            //var days = Config.GetInt(
            //    "deribit-history-trade-last-days",0);
            //if (days == 0)
            //{
            //    return list;
            //}

            //var lastDate = DateTime.UtcNow.AddDays(-1*days);

            
            //string[] currencies = {"BTC","ETH"};
            //foreach (var currency in currencies)
            //{
            //    var results = GetOrderHistoryByCurrency(currency);
            //    var orders = results.Select(ConvertOrderRecord).ToList();
            //    orders = orders.Where(x => x.order.CreatedTime >= lastDate).ToList();
            //    list.AddRange(orders);

            //    results = GetOpenOrdersByCurrency(currency);
            //    orders = results.Select(ConvertOrderRecord).ToList();
            //    orders = orders.Where(x => x.order.CreatedTime >= lastDate).ToList();
            //    list.AddRange(orders);
            //}

            ////添加所有订单成交
            //_historyTradeRecord = GetAllOrderTrades(list);

            ////查询条件单
            //foreach (var currency in currencies)
            //{
            //    var stopResults = GetStopOrderHistory(currency);
            //    var orders = stopResults.Select(ConvertOrderRecord).ToList();
            //    orders = orders.Where(x => x.order.CreatedTime >= lastDate && x.order.Status == OrderStatus.Canceled)
            //        .ToList();
            //    list.AddRange(orders);
            //}

            return list;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="orders"></param>
        /// <returns></returns>
        public List<TradeRecord> GetAllOrderTrades(List<OrderRecord> orders)
        {
            var records = new List<TradeRecord>();
            foreach (var order in orders)
            {
                var trades = GetOrderTrades(order.order);
                records.AddRange(trades);
            }

            return records;
        }

        private List<TradeRecord> GetOrderTrades(Order order)
        {
            var records = new List<TradeRecord>();

            //foreach (var brokerId in order.BrokerId)
            //{
            //    if (brokerId.Contains("SL"))
            //    {
            //        continue;
            //    }

            //    var trades= GetUserTradesByOrder(brokerId);
            //    var results = trades.Select(ConvertTradeRecord).ToList();
            //    records.AddRange(results);
            //}

            return records;
        }

        /// <summary>
        /// 
        /// </summary>
        public void CloseWebSocket()
        {
            _tdSubApi.CloseSocket();
            _mdSubApi.CloseSocket();
        }
    }

}
