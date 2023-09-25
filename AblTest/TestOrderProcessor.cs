using System.Collections.Concurrent;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.OptionQuote;
using QuantConnect.Orders;
using QuantConnect.Util;

namespace AblTest;

public class TestOrderProcessor : IOrderProcessor
{
    private IAlgorithm _algorithm;

    /// <summary>
    /// OrderQueue holds the newly updated orders from the user algorithm waiting to be processed. Once
    /// orders are processed they are moved into the Orders queue awaiting the brokerage response.
    /// </summary>
    protected IBusyCollection<OrderRequest> _orderRequestQueue;

    /// <summary>
    /// The _completeOrders dictionary holds all orders.
    /// Once the transaction thread has worked on them they get put here while waiting for fill updates.
    /// </summary>
    private readonly ConcurrentDictionary<long, Order> _completeOrders = new();

    /// <summary>
    /// The orders dictionary holds orders which are open. Status: New, Submitted, PartiallyFilled, None, CancelPending
    /// Once the transaction thread has worked on them they get put here while waiting for fill updates.
    /// </summary>
    private readonly ConcurrentDictionary<long, Order> _openOrders = new();

    /// <summary>
    /// The _openOrderTickets dictionary holds open order tickets that the algorithm can use to reference a specific order. This
    /// includes invoking update and cancel commands. In the future, we can add more features to the ticket, such as events
    /// and async events (such as run this code when this order fills)
    /// </summary>
    private readonly ConcurrentDictionary<long, OrderTicket> _openOrderTickets = new();

    /// <summary>
    /// The _completeOrderTickets dictionary holds all order tickets that the algorithm can use to reference a specific order. This
    /// includes invoking update and cancel commands. In the future, we can add more features to the ticket, such as events
    /// and async events (such as run this code when this order fills)
    /// </summary>
    private readonly ConcurrentDictionary<long, OrderTicket> _completeOrderTickets = new();

    /// <summary>
    /// The _cancelPendingOrders instance will help to keep track of CancelPending orders and their Status
    /// </summary>
    protected readonly CancelPendingOrders _cancelPendingOrders = new();

    private int _ordersCount;

    public int OrdersCount => _ordersCount;

    public void Initialize(IAlgorithm algorithm)
    {
        _algorithm = algorithm;
        _orderRequestQueue = new BusyBlockingCollection<OrderRequest>();
    }

    /// <summary>
    /// Get the order by its id
    /// </summary>
    /// <param name="orderId">Order id to fetch</param>
    /// <returns>The order with the specified id, or null if no match is found</returns>
    public Order? GetOrderById(long orderId)
    {
        var order = GetOrderByIdInternal(orderId);
        return order?.Clone();
    }

    private Order? GetOrderByIdInternal(long orderId)
    {
        return _completeOrders.TryGetValue(orderId, out var order) ? order : null;
    }

    public Order GetOrderByBrokerageId(string brokerageId)
    {
        var order = _openOrders.FirstOrDefault(x => x.Value.BrokerId.Contains(brokerageId)).Value
                    ?? _completeOrders.FirstOrDefault(x => x.Value.BrokerId.Contains(brokerageId)).Value;
        return order?.Clone();
    }

    public IEnumerable<OrderTicket> GetOrderTickets(Func<OrderTicket, bool> filter = null)
    {
        return _completeOrderTickets.Select(x => x.Value).Where(filter ?? (x => true));
    }

    public IEnumerable<OrderTicket> GetOpenOrderTickets(Func<OrderTicket, bool> filter = null)
    {
        return _openOrderTickets.Select(x => x.Value).Where(filter ?? (x => true));
    }

    public OrderTicket GetOrderTicket(long orderId)
    {
        _completeOrderTickets.TryGetValue(orderId, out var ticket);
        return ticket;
    }

    public IEnumerable<Order> GetOrders(Func<Order, bool> filter = null)
    {
        if (filter != null)
        {
            // return a clone to prevent object reference shenanigans, you must submit a request to change the order
            return _completeOrders.Select(x => x.Value).Where(filter).Select(x => x.Clone());
        }
        return _completeOrders.Select(x => x.Value).Select(x => x.Clone());
    }

    public List<Order> GetOpenOrders(Func<Order, bool> filter = null)
    {
        if (filter != null)
        {
            // return a clone to prevent object reference shenanigans, you must submit a request to change the order
            return _openOrders.Select(x => x.Value).Where(filter).Select(x => x.Clone()).ToList();
        }
        return _openOrders.Select(x => x.Value).Select(x => x.Clone()).ToList();
    }

    public OrderTicket AddOrder(SubmitOrderRequest request)
    {
        var response = !_algorithm.IsWarmingUp
            ? OrderResponse.Success(request)
            : OrderResponse.WarmingUp(request);

        request.SetResponse(response);
        var ticket = new OrderTicket(_algorithm.Transactions, request);

        Interlocked.Increment(ref _ordersCount);
        // send the order to be processed after creating the ticket
        if (response.IsSuccess)
        {
            _openOrderTickets.TryAdd(ticket.OrderId, ticket);
            _completeOrderTickets.TryAdd(ticket.OrderId, ticket);
            //Log.Trace($"BrokerageTransactionHandler AddOrder():  OrderId:{ticket.OrderId} _openOrderTickets.TryAdd {ret1}, _completeOrderTickets.TryAdd {ret2}");
            _orderRequestQueue.Add(request);
        }
        else
        {
            // add it to the orders collection for recall later
            var order = Order.CreateOrder(request);

            // ensure the order is tagged with a currency
            var security = _algorithm.Securities[order.Symbol];
            order.PriceCurrency = security.SymbolProperties.QuoteCurrency;

            order.Status = OrderStatus.Invalid;
            order.Tag = "Algorithm warming up.";
            ticket.SetOrder(order);
            _completeOrderTickets.TryAdd(ticket.OrderId, ticket);
            _completeOrders.TryAdd(order.Id, order);
        }
        return ticket;
    }

    /// <summary>
    /// Remove this order from outstanding queue: user is requesting a cancel.
    /// </summary>
    /// <param name="request">Request containing the specific order id to remove</param>
    public OrderTicket CancelOrder(CancelOrderRequest request)
    {
        if (!_completeOrderTickets.TryGetValue(request.OrderId, out var ticket))
        {
            return OrderTicket.InvalidCancelOrderId(_algorithm.Transactions, request);
        }

        try
        {
            // if we couldn't set this request as the cancellation then another thread/someone
            // else is already doing it or it in fact has already been cancelled
            if (!ticket.TrySetCancelRequest(request))
            {
                // the ticket has already been cancelled
                request.SetResponse(OrderResponse.Error(request, OrderResponseErrorCode.InvalidRequest, "Cancellation is already in progress."));
                return ticket;
            }

            //Error check
            var order = GetOrderByIdInternal(request.OrderId);
            if (order != null && request.Tag != null)
            {
                order.Tag = request.Tag;
            }
            if (order == null)
            {
                request.SetResponse(OrderResponse.UnableToFindOrder(request));
            }
            else if (order.Status.IsClosed())
            {
                request.SetResponse(OrderResponse.InvalidStatus(request, order));
            }
            else if (_algorithm.IsWarmingUp)
            {
                request.SetResponse(OrderResponse.WarmingUp(request));
            }
            else
            {
                _cancelPendingOrders.Set(order.Id, order.Status);
                // update the order status
                order.Status = OrderStatus.CancelPending;
                
                // send the request to be processed
                request.SetResponse(OrderResponse.Success(request), OrderRequestStatus.Processing);
                _orderRequestQueue.Add(request);
            }
        }
        catch (Exception err)
        {
            request.SetResponse(OrderResponse.Error(request, OrderResponseErrorCode.ProcessingError, err.Message));
        }

        return ticket;
    }

    public OrderTicket Process(OrderRequest request)
    {
        if (_algorithm.LiveMode)
        {
            _algorithm.Portfolio.LogMarginInformation(request);
        }

        switch (request.OrderRequestType)
        {
            case OrderRequestType.Submit:
                return AddOrder((SubmitOrderRequest)request);

            case OrderRequestType.Cancel:
                return CancelOrder((CancelOrderRequest)request);

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void Query(AlgorithmQueryType type, string requestId)
    {
    }

    public void ExchangeQuery(AlgorithmQueryType type, string uId, string requestId)
    {
    }

    public void ChangeLeverage(string symbol, int leverage, string exchange = "")
    {
    }

    public void Transfer(decimal amount, UserTransferType type = UserTransferType.Spot2UmFuture, string currency = "USDT")
    {
    }

    public bool TradingIsReady()
    {
        return true;
    }

    public RequestResult<QuoteRequest> SendQuoteRequest(QuoteRequest request)
    {
        throw new NotImplementedException();
    }

    public RequestResult<Quote> AcceptRequestQuote(string quoteId)
    {
        throw new NotImplementedException();
    }

    public RequestResult<QuoteRequest> CancelQuoteRequest(string requestId)
    {
        throw new NotImplementedException();
    }

    public RequestResult<List<Quote>> QueryOptionQuote(string requestId)
    {
        throw new NotImplementedException();
    }

    public RequestResult<List<OptionPosition>> QueryOptionPosition()
    {
        throw new NotImplementedException();
    }
}