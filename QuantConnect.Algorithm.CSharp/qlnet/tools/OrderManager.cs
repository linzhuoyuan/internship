using System;
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Logging;
using QuantConnect.Orders;
using System.Collections.Concurrent;


namespace QuantConnect.Algorithm.CSharp.qlnet.tools
{
    public class OrderManager
    {
        private readonly Symbol _symbol;
        private readonly Func<Symbol, decimal, bool, string, Action<long>, IEnumerable<OrderTicket>> _marketOrder;
        private readonly Func<Symbol, decimal, decimal, string, decimal, string, Action<long>, IEnumerable<OrderTicket>> _limitOrder;

        private readonly Func<Symbol, decimal, decimal, decimal, OrderOffset, string, StopPriceTriggerType, OrderTicket>
            _stopLimitOrder;
        private readonly Func<Symbol, List<Order>> _getOpenOrders;
        private readonly Func<long, OrderTicket> _getOrderTicket;
        private readonly Func<long, Order> _getOrderById;

        public ConcurrentDictionary<long, MyOrder> LocalOrders { get; set; }
        public ConcurrentDictionary<long, Order> BrokerOrders { get; set; }
        public ConcurrentDictionary<long, Order> TempOrders { get; set; }

        /// <summary>
        /// 创建订单管理实例
        /// </summary>
        public OrderManager(Symbol coinSymbol,
            Func<Symbol, decimal, bool, string, Action<long>, IEnumerable<OrderTicket>> marketOrder,
            Func<Symbol, decimal, decimal, string, decimal, string, Action<long>, IEnumerable<OrderTicket>> limitOrder,
            Func<Symbol, decimal, decimal, decimal, OrderOffset, string, StopPriceTriggerType, OrderTicket>
                stopLimitOrder, Func<Symbol, List<Order>> getOpenOrders,
            Func<long, OrderTicket> getOrderTicket, Func<long, Order> getOrderById)
        {
            _symbol = coinSymbol;
            _marketOrder = marketOrder;
            _limitOrder = limitOrder;
            _stopLimitOrder = stopLimitOrder;
            _getOpenOrders = getOpenOrders;
            _getOrderTicket = getOrderTicket;
            _getOrderById = getOrderById;
            LocalOrders = new ConcurrentDictionary<long, MyOrder>();
            TempOrders = new ConcurrentDictionary<long, Order>();
            BrokerOrders = new ConcurrentDictionary<long, Order>();
        }

        /// <summary>
        /// 添加期权order
        /// </summary>
        public long AddOrder(Symbol tradeSymbol, decimal targetVolume, OrderType orderType, decimal limitPrice = 0, decimal stopPrice = 0)
        {
            MyOrder newOrder = new MyOrder(tradeSymbol, targetVolume, orderType, limitPrice, stopPrice);
            long id = SubmitOrder(newOrder);
            if (!LocalOrders.ContainsKey(id) && id != 0)
            {
                LocalOrders[id] = newOrder;
                return id;
            }
            return id;
        }

        /// <summary>
        /// 提交期货order
        /// </summary>
        // TODO: need to discuss if we need provide flexibility of asynchronous, tag, etc.
        public long SubmitOrder(MyOrder order)
        {
            OrderTicket ticket;
            switch (order.OrderType)
            {
                case OrderType.Market:
                    ticket = _marketOrder(
                        order.TradeSymbol, 
                        order.TargetVolume, 
                        false, 
                        "", 
                        id => order.OrderID = id).FirstOrDefault();
                    break;
                case OrderType.Limit:
                    ticket = _limitOrder(
                            order.TradeSymbol,
                            order.TargetVolume,
                            order.LimitPrice,
                            "",
                            0m,
                            "",
                            id => order.OrderID = id).FirstOrDefault();
                    break;
                case OrderType.StopLimit:
                    ticket = _stopLimitOrder(order.TradeSymbol, order.TargetVolume, order.StopPrice,
                        order.LimitPrice, OrderOffset.None, "", StopPriceTriggerType.LastPrice);
                    break;
                default:
                    throw new ArgumentException($"Order Type {order.OrderType} not supported in OrderManager!");
            }

            if (ticket.SubmitRequest.Response.IsError)
            {
                return 0;
            }

            order.OrderID = ticket.OrderId;
            return ticket.OrderId;

        }

        /// <summary>
        /// 处理所有的order
        /// </summary>
        public void ManageOrder(DateTime now)
        {
            var openOrders = _getOpenOrders(_symbol).Where(x => x.Type != OrderType.StopLimit).ToList();
            //如果有openorder，则cancel
            foreach (var order in openOrders)
            {
                if ((now - order.CreatedTime).TotalSeconds > 5)//********************************
                {
                    var ticket = _getOrderTicket(order.Id);
                    ticket.Cancel("Cancel Order");
                }

            }
            //如果local order完成了，则从list中删掉
            foreach (var kvp in LocalOrders.ToArray())
            {
                if (TempOrders.Keys.Contains(kvp.Key))
                {
                    LocalOrders[kvp.Key].OrderStatus = TempOrders[kvp.Key].Status;
                    if (!TempOrders.TryRemove(kvp.Key, out var order))
                    {
                        Log.Error($"Cannot remove order {order.Id} from TempOrders!");
                    }
                }

                if (LocalOrders[kvp.Key].OrderStatus == OrderStatus.Canceled ||
                    LocalOrders[kvp.Key].OrderStatus == OrderStatus.Filled ||
                    LocalOrders[kvp.Key].OrderStatus == OrderStatus.Invalid)
                {
                    LocalOrders[kvp.Key].Finished = true;
                }

                if (kvp.Value.Finished)
                {
                    if (!LocalOrders.TryRemove(kvp.Key, out var order))
                    {
                        Log.Error($"Cannot remove order {order.OrderID} from LocalOrders!");
                    }
                }
            }

            //从broker order list中删掉成交的、取消的、invalid的order
            foreach (KeyValuePair<long, Order> kvp in BrokerOrders.ToArray())
            {
                if (kvp.Value.Status == OrderStatus.Filled || kvp.Value.Status == OrderStatus.Canceled ||
                    kvp.Value.Status == OrderStatus.Invalid)
                {
                    if (!BrokerOrders.TryRemove(kvp.Key, out var order))
                    {
                        Log.Error($"Cannot remove order {order.Id} from BrokerOrders!");
                    }
                }
            }
            //if (local_orders.Count==0) return;

            //foreach(KeyValuePair<long, MyOrder> kvp in local_orders.ToArray())
            //{
            //    if (kvp.Value.order_status == OrderStatus.Submitted || kvp.Value.order_status == OrderStatus.PartiallyFilled)
            //    {
            //        var ticket = strat.Transactions.GetOrderTicket(kvp.Value.order_id);
            //        var response = ticket.Cancel("Cancel Order");
            //        local_orders.Remove(kvp.Key);
            //    }

            //}

        }

        /// <summary>
        /// 用orderevent对order信息进行更新
        /// </summary>
        public void UpdateOrder(OrderEvent orderEvent)
        {
            //if (orderEvent.Status == OrderStatus.Invalid)
            //{
            //    var orderid = orderEvent.OrderId;
            //}
            var order = _getOrderById(orderEvent.OrderId);
            var id = order.Id;
            if (order.Type == OrderType.StopLimit || id == 0)
                return;
            //更新broker order list里面的order
            BrokerOrders[id] = order;
            //更新localorder里的order状态
            if (LocalOrders.TryGetValue(id, out var localOrder))
            {
                localOrder.OrderID = orderEvent.OrderId;
                localOrder.OrderStatus = orderEvent.Status;
                localOrder.FilledVolume += orderEvent.FillQuantity;
                //if (orderEvent.FillQuantity > 0)
                //{

                //}
                //if (Math.Abs(local_orders[id].filled_volume - local_orders[id].target_volume) < 10)
                //{
                //    local_orders[id].finished = true;
                //}
                if (localOrder.OrderStatus == OrderStatus.Canceled ||
                    localOrder.OrderStatus == OrderStatus.Filled ||
                    localOrder.OrderStatus == OrderStatus.Invalid)
                {
                    localOrder.Finished = true;
                }
            }
            //if (orderEvent.Status == OrderStatus.Submitted)
            //{


            //}


        }

        /// <summary>
        /// 检查是否有open order
        /// </summary>

        public bool HasOpenOrder()
        {
            List<Order> openOrders = _getOpenOrders(_symbol).Where(x => x.Type != OrderType.StopLimit).ToList();
            //如果有openorder或者localorder里有没完成的order，则返回true
            if (openOrders.Any() || LocalOrders.Count > 0)
            {
                if (openOrders.Any())
                    Log.Debug(openOrders.First().ToString());
                return true;
            }

            return false;
            //foreach (var order in openOrders)
            //{
            //    if (order.Id == order_id)
            //    {
            //        var ticket = strat.Transactions.GetOrderTicket(order.Id);
            //        if (order_status == OrderStatus.Submitted || order_status == OrderStatus.PartiallyFilled)
            //        {
            //            var response = ticket.Cancel("Cancel Order");
            //        }
            //    }
            //}
            //if (order_status == OrderStatus.Filled)
            //{
            //    if (!finished)
            //        strat.Debug("ALERT!!! Order filled but not finished!!!");
            //}
        }

        /// <summary>
        /// 检查策略记录的订单和broker返回的订单是否一致
        /// </summary>
        public bool CheckOrder()
        {
            //如果localorder和brokerorder个数不一致，返回false
            if (LocalOrders.Count() != BrokerOrders.Count())
            {
                return false;
            }

            var openOrders = _getOpenOrders(_symbol).Where(x => x.Type != OrderType.StopLimit).ToList();
            //如果openorder和localorder个数不一致，返回false
            if (openOrders.Count != LocalOrders.Count)
            {
                if (openOrders.Any())
                    Log.Debug(openOrders.First().ToString());
                return false;
            }

            return true;
        }
    }
}
