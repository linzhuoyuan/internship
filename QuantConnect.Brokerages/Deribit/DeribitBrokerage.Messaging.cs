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
using QuantConnect.Securities;
using QuantConnect.Util;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Threading;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Configuration;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TheOne.Deribit;

namespace QuantConnect.Brokerages.Deribit
{
    public partial class DeribitBrokerage
    {
        public class DeribitOrderInfo
        {
            /// <summary>
            /// 
            /// </summary>
            public DeribitOrderInfo()
            {
                Trades = new List<DeribitMessages.Trade>();
            }
            /// <summary>
            /// 唯一编码
            /// </summary>
            public long InputLocalId { get; set; }

            /// <summary>
            /// 回报报单
            /// </summary>
            public DeribitMessages.Order DeribitOrder;

            /// <summary>
            /// 成交记录
            /// </summary>
            public List<DeribitMessages.Trade> Trades;

            /// <summary>
            /// qc原始order
            /// </summary>
            public Order Order;

            /// <summary>
            /// 最新状态
            /// </summary>
            public OrderStatus LatestStatus;
        }

        private const string ApiVersion = "v2";
        private readonly IAlgorithm _algorithm;
        private readonly ISecurityProvider _securityProvider;
        private readonly IPriceProvider _priceProvider;
        private readonly DeribitSymbolMapper _symbolMapper;
        private readonly ConcurrentDictionary<string, QuantConnect.Data.Market.Tick> _lastTicks = new ConcurrentDictionary<string, QuantConnect.Data.Market.Tick>();
        private readonly ConcurrentDictionary<long, DeribitOrderInfo> _ordersInfo =new ConcurrentDictionary<long, DeribitOrderInfo>();
        //private readonly ConcurrentDictionary<long, Order> _cachedOrders = new ConcurrentDictionary<long, Order>();
        private readonly ConcurrentDictionary<string, DeribitMessages.Trade> _trades = new ConcurrentDictionary<string, DeribitMessages.Trade>();

        private readonly bool _updateCashbook = false;

        private Dictionary<string,string> _lastTradeId =new Dictionary<string, string>()
        {
            {"BTC","0"},
            {"ETH","0"}
        };
        private readonly TickBaseIdGen _idGen;
        private readonly object _lock = new object();
        private bool _initQueryOrder = false;
        private bool _enableSyncOrder = true;
        private enum TradingActionType
        {
            OnOrder,
            OnTrade,
            CancelOrder,
        }

        private struct TradingAction
        {
            public readonly DeribitMessages.Trade Trade;
            public readonly DeribitMessages.Order Order;
            public readonly TradingActionType Type;
            public readonly bool IsReturn;

            public TradingAction(DeribitMessages.Order order,bool isReturn)
            {
                Trade = null;
                Order = order;
                Type = TradingActionType.OnOrder;
                IsReturn = isReturn;
            }

            public TradingAction(DeribitMessages.Trade trade, bool isReturn)
            {
                Trade = trade;
                Order = null;
                Type = TradingActionType.OnTrade;
                IsReturn = isReturn;
            }
        }

        //////////////////////////////////////////////////////////////////
        private readonly DeribitRestApi _restApi;
        private readonly DeribitWebSocket _mdSubApi;
        private readonly DeribitWebSocket _tdSubApi;
        private readonly ActionBlock<TradingAction> _tradingAction;

        private System.Timers.Timer _SyncTradingTimer;

        public DeribitRestApi RestApi
        {
            get
            {
                return _restApi;
            }
        }

        /// <summary>
        /// Constructor for brokerage
        /// </summary>
        /// <param name="wssUrl">websockets url</param>
        /// <param name="restUrl">rest api url</param>
        /// <param name="apiKey">api key</param>
        /// <param name="apiSecret">api secret</param>
        /// <param name="algorithm">the algorithm instance is required to retrieve account type</param>
        /// <param name="priceProvider">The price provider for missing FX conversion rates</param>
        public DeribitBrokerage(string wssUrl, string restUrl, string apiKey, string apiSecret, IAlgorithm algorithm, IPriceProvider priceProvider):base("Deribit")
        {
            _algorithm = algorithm;
            _securityProvider = algorithm?.Portfolio;
            _priceProvider = priceProvider;
            _symbolMapper= new DeribitSymbolMapper(algorithm);
            _updateCashbook = Config.GetBool("deribit-update-cashbook", false);
            _enableSyncOrder = Config.GetBool("deribit-sync-order", true);

            _idGen = new TickBaseIdGen();
            _tradingAction = new ActionBlock<TradingAction>((Action<TradingAction>)ProcessTradingAction);

            var tdlog = NLog.LogManager.GetLogger("deribitTradef");
            var mdlog = NLog.LogManager.GetLogger("deribitQuotef");
            _restApi =new DeribitRestApi(restUrl, apiKey, apiSecret, tdlog);
            _mdSubApi =new DeribitWebSocket(wssUrl, apiKey,apiSecret, mdlog);
            _mdSubApi.OnTick += (sender, arg) => OnTick(arg);
            _mdSubApi.OnIndexTick += (sender, arg) => OnIndexTick(arg);
            _mdSubApi.OnBook += (sender, arg) => OnBook(arg);
            _mdSubApi.OnTrade += (sender, arg) => OnTrade(arg);
            _mdSubApi.SetRecvIndexTick(true);
            _mdSubApi.SetRecvTick(true);
            //_mdSubApi.SetRecvBook(true);
            //_mdSubApi.SetRecvTrade(true);
            _tdSubApi = new DeribitWebSocket(wssUrl, apiKey, apiSecret, tdlog);
            _tdSubApi.OnUserOrder += (sender, arg) => OnUserOrder(arg);
            _tdSubApi.OnUserTrade += (sender, arg) => OnUserTrade(arg);
            _tdSubApi.OnUserAccount += (sender, arg) => OnUserAccount(arg);
            _tdSubApi.SetRecvUserOrder(true);
            _tdSubApi.SetRecvUserTrade(true);
            _tdSubApi.SetRecvUserCash(true);

            _SyncTradingTimer = new System.Timers.Timer()
            {
                Interval = TimeSpan.FromSeconds(10).TotalMilliseconds,
                Enabled = false,
            };
            _SyncTradingTimer.Elapsed += (sender, e) =>
            {
                SyncOrder();
            };
            if (_enableSyncOrder)
            {
                _SyncTradingTimer.Start();
            }
        }

        private void ProcessTradingAction(TradingAction action)
        {
            switch (action.Type)
            {
                case TradingActionType.OnOrder:
                    ProcessOrder(action.Order, action.IsReturn);
                    return;
                case TradingActionType.OnTrade:
                    ProcessTrade(action.Trade, action.IsReturn);
                    return;
            }
        }

        private void OnTick(DeribitMessages.Tick tick)
        {
            try
            {
                var symbol = _symbolMapper.GetLeanSymbol(tick.instrument_name);
                if (symbol == null)
                {
                    return;
                }

                QuantConnect.Data.Market.Tick lastTick;
                if (!_lastTicks.TryGetValue(tick.instrument_name, out lastTick))
                {
                    lastTick = new Tick { Symbol = symbol, TickType = TickType.Quote };
                    lock (_lastTicks)
                        _lastTicks.TryAdd(symbol.Value, lastTick);
                }


                DateTime dt = Time.UnixTimeStampToDateTime(((double)tick.timestamp) / 1000);
                lastTick.Time = DateTime.UtcNow;
                lastTick.LocalTime = DateTime.Now;
                var span = dt - lastTick.Time;

                //Log.Trace($"tick--------------------间隔{span.TotalMilliseconds}，{dt.ToString("yyyy-MM-dd HH:mm:ss.ffffff")},{lastTick.Time.ToString("yyyy-MM-dd HH:mm:ss.ffffff")}");

                lastTick.Value = tick.last_price ?? 0;
                lastTick.BidPrice = tick.best_bid_price ?? 0;
                lastTick.BidSize = tick.best_bid_amount ?? 0;
                lastTick.AskPrice = tick.best_ask_price ?? 0;
                lastTick.AskSize = tick.best_ask_amount ?? 0;
                lastTick.AskIV = tick.ask_iv ?? 0;
                lastTick.BidIV = tick.bid_iv ?? 0;
                lastTick.DeliveryPrice = tick.delivery_price ?? 0;
                if (tick.greeks != null)
                {
                    lastTick.Delta = tick.greeks.delta ?? 0;
                    lastTick.Gamma = tick.greeks.gamma ?? 0;
                    lastTick.Rho = tick.greeks.rho ?? 0;
                    lastTick.Theta = tick.greeks.theta ?? 0;
                    lastTick.Vega = tick.greeks.vega ?? 0;
                }
                if (tick.stats != null)
                {
                    lastTick.High = tick.stats.high ?? 0;
                    lastTick.Low = tick.stats.low ?? 0;
                    lastTick.Volume = tick.stats.volume ?? 0;
                }
                lastTick.InterestRate = tick.interest_rate ?? 0;
                lastTick.MarkIV = tick.mark_iv ?? 0;
                lastTick.MarkPrice = tick.mark_price ?? 0;
                lastTick.MaxPrice = tick.max_price ?? 0;
                lastTick.MinPrice = tick.min_price ?? 0;
                lastTick.OpenInterest = tick.open_interest ?? 0;
                lastTick.SettlementPrice = tick.settlement_price ?? 0;
                lastTick.State = tick.state;
                lastTick.UnderlyingIndex = tick.underlying_index;
                lastTick.UnderlyingPrice = tick.underlying_price ?? 0;
                lastTick.Updated = true;

            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void OnIndexTick(DeribitMessages.IndexTick tick)
        {
            try
            {
                var symbol = _symbolMapper.GetLeanSymbol(tick.index_name);
                var time = Time.UnixTimeStampToDateTime(((double)tick.timestamp) / 1000);
                var price = tick.price;

                ///*
                QuantConnect.Data.Market.Tick lastTick;
                if (!_lastTicks.TryGetValue(symbol.Value, out lastTick))
                {
                    lastTick = new Tick { Symbol = symbol, TickType = TickType.Trade };
                    _lastTicks.TryAdd(symbol.Value, lastTick);
                }

                lastTick.Value = price;
                //lastTick.Time = time;
                lastTick.Time = DateTime.UtcNow;
                lastTick.Symbol = symbol;
                lastTick.Quantity = 0;
                lastTick.Updated = true;
                //*/
            }
            catch (Exception e)

            {
                Log.Error(e);
                throw;
            }
        }

        private void OnBook(DeribitMessages.BookDepthLimitedData book)
        {
            try
            {
                var symbol = _symbolMapper.GetLeanSymbol(book.instrument_name);
                if (symbol == null)
                {
                    return;
                }

                QuantConnect.Data.Market.Tick lastTick;
                if (!_lastTicks.TryGetValue(symbol.Value, out lastTick))
                {
                    //以ticker为主，order仅作盘口数据的补充
                    //如果ticker不存在，不处理盘口数据
                    Log.Trace($"symbol:{symbol.Value} ticker does not exists");
                    return;
                }

                int i = 1;
                foreach (var bid in book.bids)
                {
                    decimal price = bid.price;
                    decimal amount = bid.amount;
                    if (i == 1)
                    {
                        lastTick.BidPrice1 = price;
                        lastTick.BidSize1 = amount;
                    }
                    else if (i == 2)
                    {
                        lastTick.BidPrice2 = price;
                        lastTick.BidSize2 = amount;
                    }
                    else if (i == 3)
                    {
                        lastTick.BidPrice3 = price;
                        lastTick.BidSize3 = amount;
                    }
                    else if (i == 4)
                    {
                        lastTick.BidPrice4 = price;
                        lastTick.BidSize4 = amount;
                    }
                    else if (i == 5)
                    {
                        lastTick.BidPrice5 = price;
                        lastTick.BidSize5 = amount;
                    }
                    else
                    {
                        break;
                    }
                    i++;
                }

                i = 1;
                foreach (var ask in book.asks)
                {
                    decimal price = ask.price;
                    decimal amount = ask.amount;
                    if (i == 1)
                    {
                        lastTick.AskPrice1 = price;
                        lastTick.AskSize1 = amount;
                    }
                    else if (i == 2)
                    {
                        lastTick.AskPrice2 = price;
                        lastTick.AskSize2 = amount;
                    }
                    else if (i == 3)
                    {
                        lastTick.AskPrice3 = price;
                        lastTick.AskSize3 = amount;
                    }
                    else if (i == 4)
                    {
                        lastTick.AskPrice4 = price;
                        lastTick.AskSize4 = amount;
                    }
                    else if (i == 5)
                    {
                        lastTick.AskPrice5 = price;
                        lastTick.AskSize5 = amount;
                    }
                    else
                    {
                        break;
                    }
                    i++;
                }
                DateTime dt = Time.UnixTimeStampToDateTime(((double)book.timestamp) / 1000);
                lastTick.Time = DateTime.UtcNow;
                lastTick.LocalTime = DateTime.Now;
                var span = dt - lastTick.Time;

                //Log.Trace($"book--------------------间隔{span.TotalMilliseconds}，{dt.ToString("yyyy-MM-dd HH:mm:ss.ffffff")},{lastTick.Time.ToString("yyyy-MM-dd HH:mm:ss.ffffff")}");
                lastTick.Updated = true;
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void OnTrade(DeribitMessages.Trade[] trades)
        {
            try
            {
                var symbol = _symbolMapper.GetLeanSymbol(trades.First().instrument_name);
                if (symbol == null)
                {
                    return;
                }
                foreach (var trade in trades)
                {
                    // pass time, price, amount
                    EmitTradeTick(symbol, trade);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void EmitTradeTick(Symbol symbol, DeribitMessages.Trade trade)
        {
            try
            {
                var time = Time.UnixTimeStampToDateTime(((double)trade.timestamp) / 1000);
                var price = (decimal)trade.price;
                var amout = (decimal)trade.amount;
                var mark_price = (decimal)trade.mark_price;

                Tick lastTick;
                if (!_lastTicks.TryGetValue(symbol.Value, out lastTick))
                {
                    lastTick = new Tick { Symbol = symbol, TickType = TickType.Quote };
                    _lastTicks.TryAdd(symbol.Value, lastTick);
                }

                //lastTick.Time = time;
                lastTick.Time = DateTime.UtcNow;
                lastTick.Value = price;
                lastTick.Volume = amout;
                lastTick.MarkPrice = mark_price;
                lastTick.Updated = true;
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void OnUserOrder(DeribitMessages.Order order)
        {
            try
            {
                //ProcessOrder(order,true);
                _tradingAction.Post(new TradingAction(order, true));
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void OnUserTrade(DeribitMessages.Trade[] trades)
        {
            try
            {
                foreach (var trade in trades)
                {
                    //ProcessTrade(trade,true);
                    _tradingAction.Post(new TradingAction(trade, true));
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
                throw;
            }
        }

        private void OnUserAccount(DeribitMessages.Portfolio portfolio)
        {

        }

        private void ProcessOrder(DeribitMessages.Order order, bool isReturn)
        {
            try
            {
                //if (isReturn) return;
                if(!_initQueryOrder) return;

                Order cached = null;
                DeribitOrderInfo orderInfo = null;
                long orderId;
                if (ParseQcOrderId(order.label, out orderId))
                {
                    if (!_ordersInfo.TryGetValue(orderId, out orderInfo))
                    {
                        Log.Error($"ProcessOrder: qc OrderId {orderId} 没在缓存中，外部订单 order_id:{order.order_id} stop_order_id:{order.stop_order_id}");
                        return;
                    }
                    else
                    {
                        if(orderInfo.Order ==null)
                        {
                            Log.Error($"ProcessOrder: qc OrderId {orderId} Order ==null，外部订单 order_id:{order.order_id} stop_order_id:{order.stop_order_id}");
                            return;
                        }
                        cached = orderInfo.Order;
                    }
                }
                else
                {
                    if (order.order_id.IsNullOrEmpty())
                    {
                        Log.Error($"ProcessOrder: order_id is null !!");
                        return;
                    }

                    foreach (var item in _ordersInfo)
                    {
                        var qcOrder = item.Value.Order;
                        if (qcOrder.BrokerId[0] == order.order_id || qcOrder.BrokerId[0] == order.stop_order_id)
                        {
                            cached = qcOrder;
                            orderInfo = item.Value;
                            break;
                        }
                    }
                    if (cached == null)
                    {
                        Log.Error($"ProcessOrder: qc OrderId {orderId} 没在缓存中 Order ==null，外部订单 order_id:{order.order_id} stop_order_id:{order.stop_order_id}");
                        return;
                    }
                }

                if (orderInfo.DeribitOrder==null || order.last_update_timestamp > orderInfo.DeribitOrder.last_update_timestamp)
                {
                    orderInfo.DeribitOrder = order;
                }
                //如果是波特率下单。更新价格
                //cached.ApplyUpdateAdvanceOrder(order.price);

                //已结束的订单再有状态变更不处理
                if (cached.Status.IsClosed() || orderInfo.LatestStatus.IsClosed())
                {
                    Log.Trace($"ProcessOrder cached.Status:{cached.Status} orderInfo.LatestStatus:{orderInfo.LatestStatus} IsClosed skip");
                    return;
                }

                var newStatus = ConvertOrderStatus(order);
                //状态相同不处理
                if (cached.Status == newStatus || orderInfo.LatestStatus== newStatus)
                {
                    Log.Trace($"ProcessOrder cached.Status:{cached.Status} orderInfo.LatestStatus:{orderInfo.LatestStatus} == newStatus:{newStatus} skip");
                    return;
                }

                lock (_lock)
                {
                    if (!cached.BrokerId.Contains(order.order_id))
                    {
                        cached.BrokerId.Insert(0,order.order_id);
                    }
                    if (!order.stop_order_id.IsNullOrEmpty())
                    {
                        if (!cached.BrokerId.Contains(order.stop_order_id))
                        {
                            cached.BrokerId.Add(order.stop_order_id);
                        }
                    }
                }                

                if (cached.Type == OrderType.StopLimit)
                {
                    var stopOrder = cached as StopLimitOrder;
                    if (order.order_state == "triggered")
                    {
                        stopOrder.StopTriggered = true;
                    }
                }
                if (cached.Type == OrderType.StopMarket)
                {
                    var stopOrder = cached as StopMarketOrder;
                    if (order.order_state == "triggered")
                    {
                        stopOrder.StopTriggered = true;
                    }
                }

                //cached.Status = newStatus;
                orderInfo.LatestStatus = newStatus;


                // 成交在成交回报中发消息
                if (newStatus != OrderStatus.Filled &&
                    newStatus != OrderStatus.PartiallyFilled)
                {
                    var orderEvent = new OrderEvent(
                        cached,
                        DateTime.UtcNow,
                        OrderFee.Zero)
                    {
                        Status = newStatus,
                    };

                    OnOrderEvent(orderEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"ProcessOrder {ex}");
            }
        }

        private void ProcessTrade(DeribitMessages.Trade trade, bool isReturn)
        {
            try
            {
                //if (isReturn) return;
                if (!_initQueryOrder) return;

                if (_trades.ContainsKey(trade.trade_id))
                {
                    Log.Trace($"ProcessTrade 重复的成交,tid:{trade.trade_id},order_id:{trade.order_id} label:{trade.label}");
                    return;
                }

                Order cached = null;
                DeribitOrderInfo orderInfo = null;
                long orderId;
                if (ParseQcOrderId(trade.label, out orderId))
                {
                    if (!_ordersInfo.TryGetValue(orderId, out orderInfo))
                    {
                        Log.Error($"ProcessTrade: qc OrderId {orderId} 没在缓存中，外部订单 order_id:{trade.order_id}");
                        return;
                    }
                    else
                    {
                        if (orderInfo.Order == null)
                        {
                            Log.Error($"ProcessTrade: qc OrderId {orderId} Order == null，外部订单 order_id:{trade.order_id}");
                            return;
                        }
                        cached = orderInfo.Order;
                    }
                }
                else
                {                    

                    foreach (var item in _ordersInfo)
                    {
                        bool found = false;
                        var qcOrder = item.Value.Order;
                        foreach(var brokerId in qcOrder.BrokerId)
                        {
                            if(brokerId == trade.order_id)
                            {
                                cached = qcOrder;
                                orderInfo = item.Value;
                                found = true;
                                break;
                            }
                        }
                        if (found) break;
                    }
                    if (cached == null)
                    {
                        Log.Error($"ProcessTrade: qc OrderId {orderId} 没在缓存中 Order ==null，外部订单 order_id:{trade.order_id}");
                        return;
                    }
                }

                _trades.TryAdd(trade.trade_id, trade);
                orderInfo.Trades.Add(trade);
                var volume = cached.AbsoluteQuantity;
                var partiallyFilled = orderInfo.Trades.Sum(x => x.amount) < volume;

                var multi = 1;
                if (cached.Direction == OrderDirection.Sell)
                {
                    multi = -1;
                }

                //cached.Status = partiallyFilled ? OrderStatus.PartiallyFilled : OrderStatus.Filled;
                var newStatus = partiallyFilled ? OrderStatus.PartiallyFilled : OrderStatus.Filled;
                orderInfo.LatestStatus = newStatus;

                var orderEvent = new OrderEvent(
                    cached,
                    DateTime.UtcNow,
                    OrderFee.Zero)
                {
                    Status = newStatus,
                    FillPrice = trade.price,
                    FillQuantity = trade.amount * multi,
                    OrderFee = new OrderFee(new CashAmount(trade.fee,trade.fee_currency)),
                };

                OnOrderEvent(orderEvent);
                
            }
            catch(Exception ex)
            {
                Log.Error($"ProcessTrade {ex}");
            }
        }

        private void SyncOrder()
        {

            if (!_initQueryOrder) return;

            var openOrdersInfo = _ordersInfo.Where(x => !x.Value.Order.Status.IsClosed()).Select(x => x.Value);

            foreach (var info in openOrdersInfo)
            {
                Thread.Sleep(200);
                if (info.Order.Type == OrderType.StopLimit || info.Order.Type == OrderType.StopMarket)
                {

                    if (info.Order.BrokerId.Count == 1)//可能未触发也可能触发了
                    {
                        Log.Trace($"SyncOrder qcOrder BrokerId0:{info.Order.BrokerId[0]} Status:{info.Order.Status} BrokerId1:{""}");

                        var result = _restApi.GetStopOrderHistory(
                            info.Order.Symbol.Value.Substring(0, 3),
                            info.Order.Symbol.Value
                        );
                        foreach (var o in result)
                        {
                            if (o.stop_id == info.Order.BrokerId[0])
                            {
                                Log.Trace($"SyncOrder dOrder order_id:{o.order_id} order_state:{o.order_state} stop_order_id:{o.stop_id} trigger:{o.trigger} last order_state:{info.DeribitOrder.order_state}");

                                if (o.order_state == DeribitEnums.OrderState.cancelled && 
                                    info.DeribitOrder.order_state != DeribitEnums.OrderState.cancelled)
                                {
                                    var dOrder = info.DeribitOrder.Clone();

                                    bool updated = false;
                                    if (o.last_update_timestamp > info.DeribitOrder.last_update_timestamp)
                                    {
                                        dOrder.last_update_timestamp = o.last_update_timestamp;
                                        updated = true;
                                    }
                                    if (o.timestamp > info.DeribitOrder.last_update_timestamp)
                                    {
                                        dOrder.last_update_timestamp = o.timestamp;
                                        updated = true;
                                    }

                                    if (!updated)
                                    {
                                        dOrder.last_update_timestamp++;
                                    }

                                    dOrder.order_state = o.order_state;

                                    _tradingAction.Post(new TradingAction(dOrder, false));
                                }
                                else if(o.order_state == DeribitEnums.OrderState.triggered)
                                {
                                    var oid = o.order_id;
                                    DeribitMessages.Order dOrder;
                                    if (!_restApi.GetOrderState(oid, out dOrder))
                                        continue;


                                    _tradingAction.Post(new TradingAction(dOrder, false));
                                    SyncTrade(oid);
                                }
                                break;
                            }
                        }
                    }
                    else if (info.Order.BrokerId.Count == 2)//说明触发了
                    {
                        Log.Trace($"SyncOrder qcOrder BrokerId0:{info.Order.BrokerId[0]} Status:{info.Order.Status} BrokerId1:{info.Order.BrokerId[1]}");
                        var oid = info.Order.BrokerId[0];
                        DeribitMessages.Order dOrder;
                        if (!_restApi.GetOrderState(oid, out dOrder))
                            continue;

                        _tradingAction.Post(new TradingAction(dOrder, false));
                        SyncTrade(oid);
                    }
                }
                else
                {
                    if (info.Order.BrokerId.Count == 0)
                        continue;

                    var oid = info.Order.BrokerId[0];
                    DeribitMessages.Order dOrder;
                    if (!_restApi.GetOrderState(oid,out dOrder))
                        continue;

                    _tradingAction.Post(new TradingAction(dOrder, false));
                    SyncTrade(oid);
                }
            }
        }

        private void SyncTrade(string orderId)
        {
            var trades =_restApi.GetUserTradesByOrder(orderId);
            foreach (var trade in trades)
            {
                _tradingAction.Post(new TradingAction(trade, false));
            }
        }


        /*
        private void ConvertToOrderEvent(DeribitMessages.Order order)
        {
            long orderId;
            if (!GetLocalOrderId(order.label, out orderId))
            {
                //条件单
                if (order.stop_order_id != null)
                {
                    var lst = CachedOrderIDs.Where(x => x.Value.BrokerId[0] == order.stop_order_id || x.Value.BrokerId[0] == order.order_id).Select
                        (x => x.Key).ToList();
                    if (lst.Count > 0)
                    {
                        orderId = lst.FirstOrDefault();
                    }
                    else
                    {
                        Log.Error($"DeribitBrokerage.ConvertToOrderEvent,not in CachedOrderIDs, label:{order.label} id:{order.order_id} status:{order.order_state} " +
                                  $"symbol:{order.instrument_name} price:{order.price} amt:{order.amount}");
                        return;
                    }
                }
                else//普通单或者条件单刚触发
                {
                    var lst = CachedOrderIDs.Where(x => x.Value.BrokerId[0] == order.order_id).Select
                        (x => x.Key).ToList();
                    if (lst.Count > 0)
                    {
                        orderId = lst.FirstOrDefault();
                    }
                    else
                    {
                        Log.Error($"DeribitBrokerage.ConvertToOrderEvent not in CachedOrderIDs, label:{order.label} id:{order.order_id} status:{order.order_state} " +
                                  $"symbol:{order.instrument_name} price:{order.price} amt:{order.amount}");
                        return;
                    }
                }
            }

            lock (orderLocker)
            {
                //if (CachedOrderIDs.ContainsKey(Int32.Parse(order.label)))
                if (CachedOrderIDs.ContainsKey(orderId))
                {
                    //var cached = CachedOrderIDs[Int32.Parse(order.label)];
                    var cached = CachedOrderIDs[orderId];
                    //如果是波特率下单。更新价格
                    cached.ApplyUpdateAdvanceOrder(order.price);

                    if (cached.Status == OrderStatus.Filled || cached.Status == OrderStatus.Canceled)
                    {
                        Log.Trace($"DeribitBrokerage.ConvertToOrderEvent Order status changed - Id: {cached.Id} symbol:{cached.Symbol} status:{cached.Status} " +
                        $" amount:{order.filled_amount} have been filled, no send OnOrderEvent");
                        return;
                    }
                    if (cached.Status == OrderStatus.PartiallyFilled && Math.Abs(cached.FillQuantity) >= order.filled_amount)
                    {
                        Log.Trace($"DeribitBrokerage.ConvertToOrderEvent Order status changed - Id: {cached.Id} symbol:{cached.Symbol} status:{cached.Status} " +
                        $" amount:{order.filled_amount} have been partially filled, no send OnOrderEvent");
                        return;
                    }

                    var status = ConvertOrderStatus(order);

                    // 针对 StopLimitOrder  记录：先是 {stop_limit triggered:true  stop_order_id==null} 然后 {limit triggered:true stop_order_id=原order_id}
                    var stopTriggered = false;
                    var stopLimitOrder = cached as StopLimitOrder;
                    if (stopLimitOrder != null && stopLimitOrder.StopTriggered != order.triggered && order.stop_order_id != null)
                    {
                        //open
                        stopLimitOrder.BrokerId.Insert(0, order.order_id);
                        stopLimitOrder.StopTriggered = order.triggered;
                        stopTriggered = true;
                    }

                    var stopMarketOrder = cached as StopMarketOrder;
                    if (stopMarketOrder != null && stopMarketOrder.StopTriggered != order.triggered && order.stop_order_id != null)
                    {
                        //open
                        stopMarketOrder.BrokerId.Insert(0, order.order_id);
                        stopMarketOrder.StopTriggered = order.triggered;
                        stopTriggered = true;
                    }

                    if (status != OrderStatus.Filled && 
                        status != OrderStatus.PartiallyFilled && 
                        status != OrderStatus.Canceled &&
                        stopTriggered == false)
                    {
                        //include untriggered triggered
                        Log.Trace($"DeribitBrokerage.ConvertToOrderEvent Order status changed - Id: {cached.Id} symbol:{cached.Symbol} status:{status} " +
                        $" amount:{order.filled_amount} is not filled, no send OnOrderEvent");
                        return;
                    }
                    var orderFee = OrderFee.Zero;
                    cached.Status = status;
                    if (status != OrderStatus.Canceled)
                    {
                        cached.LastUpdateTime = Time.UnixTimeStampToDateTime((double)order.last_update_timestamp / 1000);
                        cached.FillTime = (DateTime)cached.LastUpdateTime;
                        if (cached.Status == OrderStatus.Filled)
                        {
                            cached.LastFillTime = cached.FillTime;
                        }
                        orderFee = new OrderFee(new CashAmount(order.commission- cached.Commission, cached.Symbol.Value.Contains("BTC") ? "BTC" : "ETH"));

                        decimal fillAmount = 0;
                        decimal fillPrice = 0;
                        Log.Trace(
                            $"CaculateFillPrice 0 fillPrice:{fillPrice} cached.AverageFillPrice:{cached.AverageFillPrice} cached.FillQuantity:{cached.FillQuantity} order.average_price:{order.average_price} order.filled_amount:{order.filled_amount}"
                        );

                        if (order.filled_amount > 0)
                        {
                            fillAmount = Math.Abs(order.filled_amount) - Math.Abs(cached.FillQuantity);
                            fillAmount = cached.Quantity > 0 ? fillAmount : -fillAmount;
                            fillPrice = CaculateFillPrice(
                                cached.SecurityType,
                                cached.AverageFillPrice,
                                cached.FillQuantity,
                                order.average_price,
                                order.filled_amount
                            );
                        }

                        Log.Trace(
                            $"CaculateFillPrice 1 fillPrice:{fillPrice} cached.AverageFillPrice:{cached.AverageFillPrice} cached.FillQuantity:{cached.FillQuantity} order.average_price:{order.average_price} order.filled_amount:{order.filled_amount}"
                        );

                        OnOrderEvent(new OrderEvent(cached, Time.UnixTimeStampToDateTime((double)order.last_update_timestamp / 1000), orderFee, "Deribit Order Event")
                        { FillQuantity = fillAmount, FillPrice = fillPrice });
                    } 
                    else
                    {
                        OnOrderEvent(new OrderEvent(cached,
                            Time.UnixTimeStampToDateTime((double)order.last_update_timestamp / 1000),
                            OrderFee.Zero,
                            "Deribit Cancel Order Response")
                        { Status = OrderStatus.Canceled });
                    }
                }
                else
                {
                    Log.Error($"DeribitBrokerage.ConvertToOrderEvent recive an unsubmit order change event, label:{order.label} id:{order.order_id} status:{order.order_state} " +
                        $"symbol:{order.instrument_name} price:{order.price} amt:{order.amount}");
                }
            }
        }
        */
    }
}
