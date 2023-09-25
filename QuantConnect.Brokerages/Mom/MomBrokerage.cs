using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Newtonsoft.Json;
using NLog;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;
using Quantmom.Api;
using Convert = System.Convert;
using HistoryRequest = QuantConnect.Data.HistoryRequest;
using Order = QuantConnect.Orders.Order;
using OrderField = Quantmom.Api.OrderField;

namespace QuantConnect.Brokerages.Mom;

/// <summary>
/// 
/// </summary>
[BrokerageFactory(typeof(MomBrokerageFactory))]
public partial class MomBrokerage : Brokerage, IDataQueueHandler, IDataQueueUniverseProvider
{
    private enum TradingActionType
    {
        Response,
        PlaceOrder,
        CancelOrder,
        Timer
    }

    private readonly struct TradingAction
    {
        public readonly MomResponse? Response;
        public readonly Order? Order;
        public readonly TradingActionType Type;

        public TradingAction(MomResponse response)
        {
            Response = response;
            Order = null;
            Type = TradingActionType.Response;
        }

        public TradingAction(Order order, TradingActionType type = TradingActionType.PlaceOrder)
        {
            Response = null;
            Order = order;
            Type = type;
        }

        public TradingAction(TradingActionType type = TradingActionType.Timer)
        {
            Response = null;
            Order = null;
            Type = type;
        }
    }

    private const int EventTimeout = -1;
    private const int MaxOrdersToKeep = 1000;

    private readonly object _locker = new();
    private readonly MomTraderApi _traderApi;
    private readonly MomMarketDataApi _marketApi;

    private readonly IAlgorithm _algorithm;
    private readonly AutoResetEvent _marketConnectEvent = new(false);
    private readonly AutoResetEvent _tradingConnectEvent = new(false);
    private readonly AutoResetEvent _queryPositionEvent = new(false);
    private readonly AutoResetEvent _queryAccountEvent = new(false);
    private readonly AutoResetEvent _queryOrderEvent = new(false);
    private readonly AutoResetEvent _queryTradeEvent = new(false);
    private readonly AutoResetEvent _querySyncDataEvent = new(false);
    private readonly AutoResetEvent _queryExchangeCashEvent = new(false);
    private readonly AutoResetEvent _queryExchangeOrderEvent = new(false);
    private readonly AutoResetEvent _queryExchangePositionEvent = new(false);

    private readonly bool _enableSyncQuery;

    private volatile bool _initDataSyncing = true;
    private volatile bool _initQueryOrder = true;
    private volatile bool _initQueryTrade = true;
    private volatile bool _initQueryPosition = true;
    private volatile bool _firstGetCashBalance = true;

    private readonly MomUser _tradeUser;
    private readonly MomUser _mdUser;

    private readonly HashSet<long> _cancelPendingList = new();
    private readonly ConcurrentDictionary<long, MomOrderData> _orderData = new();
    private readonly ConcurrentDictionary<long, MomOrderData> _openOrders = new();
    private readonly ConcurrentDictionary<string, Symbol> _symbols = new();
    private readonly ConcurrentDictionary<string, string> _symbolsMapper = new();
    private readonly ConcurrentDictionary<string, SubscribeData> _subscribedSymbol = new();
    private bool _underlyingSend;
    private readonly ConcurrentDictionary<Symbol, Symbol> _underlying = new();
    private readonly ConcurrentQueue<Tick> _ticks = new();
    private readonly ConcurrentDictionary<Symbol, Tick> _marketSnapshots = new();

    private readonly ConcurrentDictionary<long, MomPosition> _momPositions = new();
    private readonly ConcurrentDictionary<string, MomTrade> _momTradeIds = new();
    private readonly ConcurrentDictionary<string, MomAccount> _momTradingAccount = new();

    private readonly MomSymbolMapper _momSymbolMapper = new();

    private readonly MomOptionChainProvider _optionChainProvider;
    private readonly MomFutureChainProvider _futureChainProvider;
    private readonly ActionBlock<TradingAction> _tradingAction;

    //都没加锁
    private readonly List<MomFundAccount> _qryExchangeAccountData = new();
    private readonly List<MomFundOrder> _qryExchangeOrderData = new();
    private readonly List<MomFundPosition> _qryExchangePositionData = new();

    private readonly System.Timers.Timer _orderCheckTimer;
    private readonly TickIdGen _idGen;
    private long _lastTradeId;

    private readonly AtomicBoolean _tradingConnecting = new();
    private readonly CancellationTokenSource _exitToken = new();
    private volatile bool _tradingIsReady = true;
    private readonly ExchangeTimeManager _exchangeTimeManager = new();
    private readonly List<BrokerageAccountInfo> _accountInfos = new();
    private readonly Dictionary<string, BrokerageAccountInfo> _accountMap = new();

    private readonly Logger _mdLog;

    /// <summary>
    /// 
    /// </summary>
    public MomBrokerage(
        IAlgorithm algorithm,
        string tradeServerAddress,
        string tradeUser,
        string tradePassword,
        string mdServerAddress,
        string mdUser,
        string mdPassword,
        bool enableSyncQuery = true) : base("Mom")
    {
        _tradingAction = new ActionBlock<TradingAction>(ProcessTradingAction);
        _algorithm = algorithm;
        _algorithm.BrokerageAccountInfos = _accountInfos.ToArray();
        _algorithm.GetMarketPriceFromSnapshot = GetMarketPrice;
        _algorithm.BrokerageSubscribe = BrokerageSubscribe;
        _algorithm.BrokerageUnsubscribe = BrokerageUnsubscribe;
        _optionChainProvider = new MomOptionChainProvider(this);
        _futureChainProvider = new MomFutureChainProvider(this);

        _idGen = new TickIdGen();
        _tradeUser = new MomUser { ServerAddr = tradeServerAddress, UserId = tradeUser, Password = tradePassword };
        _mdUser = new MomUser { ServerAddr = mdServerAddress, UserId = mdUser, Password = mdPassword };

        var tradeLog = LogManager.GetLogger("momTradeLog");
        _mdLog = LogManager.GetLogger("momMdLog");

        TradingConnected = false;
        InstrumentIsReady = false;
        MarketConnected = false;

        _traderApi = new MomTraderApi(_tradeUser.ServerAddr, tradeLog, true);
        _traderApi.OnResponse += response => _tradingAction.Post(new TradingAction(response));
        _traderApi.OnConnected += OnTraderConnected;
        _traderApi.OnDisconnected += OnTraderDisconnected;
        _traderApi.OnRspUserLogin += OnTraderRspUserLogin;
        _traderApi.OnRspError += OnTraderRspError;
        _traderApi.InstrumentReady += OnInstrumentReady;
        _traderApi.OnMarketOpen += OnTraderMarketOpen;
        _traderApi.OnMarketClose += OnTraderMarketClose;

        _marketApi = new MomMarketDataApi(_mdUser.ServerAddr, _mdLog, false);
        _marketApi.OnConnected += OnMarketConnected;
        _marketApi.OnDisconnected += OnMarketDisconnected;
        _marketApi.OnRspUserLogin += OnMarketRspUserLogin;
        _marketApi.OnRspError += OnMarketRspError;
        _marketApi.ReturnData += OnDepthMarketData;
        _marketApi.RspSubscribe += OnRspSubscribe;
        _marketApi.RspUnsubscribe += OnRspUnsubscribe;

        _orderCheckTimer = new System.Timers.Timer
        {
            Interval = TimeSpan.FromSeconds(5).TotalMilliseconds,
            Enabled = false,
        };

        _orderCheckTimer.Elapsed += (_, _) =>
        {
            CheckOrderAndTrade();
        };

        _enableSyncQuery = enableSyncQuery;
        //_enableSyncQuery = false;
        //_symbolProperties = ReadSymbolProperties();
        UseSyncData = true;
    }

    private void BrokerageUnsubscribe(Symbol symbol)
    {
        Unsubscribe(null, new[] { symbol });
    }

    private void BrokerageSubscribe(Symbol symbol)
    {
        Subscribe(null, new[] { symbol });
    }

    private void NotifyException(string msg)
    {
        if (_algorithm.SimulationMode)
        {
            return;
        }
        _algorithm.Notify.MomDingDing($"{_algorithm.GetType().Name}_{_tradeUser.UserId},{msg}", "ActionRequired");
    }

    private decimal GetMarketPrice(Symbol symbol)
    {
        return _marketSnapshots.TryGetValue(symbol, out var tick) ? tick.Price : 0;
    }

    public override bool TradingIsReady()
    {
        return _tradingIsReady;
    }

    private void OnTraderMarketClose()
    {
        if (!_algorithm.SimulationMode)
            return;
        _tradingIsReady = false;
        Task.Run(() =>
        {
            RemoveDelistingOnMarketClose();
            CloseOpenOrdersOnMarketClose();
            ClearSecurityCache();
        });
    }

    private void ClearSecurityCache()
    {
        foreach (var security in _algorithm.Securities.Values)
        {
            security.Cache.Clear();
        }
    }

    private void CloseOpenOrdersOnMarketClose()
    {
        foreach (var orderData in _orderData.Values)
        {
            if (orderData.Order == null)
            {
                continue;
            }

            if (orderData.Order.Status.IsClosed())
            {
                continue;
            }

            var orderEvent = new OrderEvent(
                orderData.Order,
                GetTradingTime(orderData.Order.Symbol).UtcDateTime,
                OrderFee.Zero,
                $"{nameof(MomBrokerage)},ref:{orderData.OrderRef}")
            {
                Status = OrderStatus.Canceled
            };

            OnOrderEvent(orderEvent);
            OnOrderOccurred(new[] { new OrderRecord { order = orderData.Order } });
        }
    }

    private void RemoveDelistingOnMarketClose()
    {
        var info = _traderApi.UserInfo!;
        var tradingDay = DateHelper.IntToDate(info.TradingDay);
        var closeTime = tradingDay.Add(DateHelper.IntToTime(info.MarketCloseTime));
        var delisting = new List<Symbol>();

        foreach (var (_, symbol) in _symbols)
        {
            if (symbol.SecurityType == SecurityType.Option && symbol.ID.Date <= closeTime)
            {
                delisting.Add(symbol);
            }
        }

        if (delisting.Count > 0)
        {
            _algorithm.OnSymbolDelisted(delisting);
            foreach (var symbol in delisting)
            {
                var key = GetSymbolValue(symbol);
                _subscribedSymbol.TryRemove(key, out _);
                _symbolsMapper.TryRemove(key, out var id);
                _symbols.TryRemove(id, out _);
                _algorithm.Securities.Remove(symbol);
                SymbolCache.TryRemove(symbol);
            }
        }

        _optionChainProvider.Clear();
    }

    private void OnTraderMarketOpen()
    {
        if (!_algorithm.SimulationMode)
            return;

        Task.Run(() =>
        {
            AccountHoldingOnMarketOpen();
            SubscriptionOnMarketOpen();
            _tradingIsReady = true;
        });
    }

    private void AccountHoldingOnMarketOpen()
    {
        _querySyncDataEvent.Reset();
        _traderApi.DataSync();
        _querySyncDataEvent.WaitOne();

        SetAccountHolding();
        SetAccountCash();
    }

    private void SetAccountCash()
    {
        var balances = new List<CashAmount>();
        lock (_momTradingAccount)
        {
            var value = 0m;
            foreach (var account in _momTradingAccount)
            {
                switch (account.Value.AccountType)
                {
                    case "F":
                        value += account.Value.Available + account.Value.MaintMargin;
                        break;
                    case "S":
                        value += account.Value.Available;
                        break;
                    case "SO":
                        value += account.Value.Available + account.Value.MaintMargin;
                        break;
                }
            }

            var balance = new CashAmount(value, "USD");
            balances.Add(balance);
        }

        foreach (var cash in balances)
        {
            if (cash.Settled)
            {
                Log.Trace($"{nameof(MomBrokerage)},{nameof(SetAccountCash)}: Setting " + cash.Currency +
                          " cash to " + cash.Amount);
                _algorithm.Portfolio.SetCash(cash.Currency, cash.Amount, 1);
            }
            else
            {
                Log.Trace($"{nameof(MomBrokerage)},{nameof(SetAccountCash)}: Setting Unsettled " +
                          cash.Currency + " cash to " + cash.Amount);
                _algorithm.Portfolio.SetUnsettledCash(cash.Currency, cash.Amount, 1);
            }
        }
    }

    private void SetAccountHolding()
    {
        var holdings = new List<Holding>();
        foreach (var item in _momPositions)
        {
            var position = item.Value;
            if (!_symbols.TryGetValue(position.InstrumentId, out var symbol))
            {
                Log.Error($"{nameof(MomBrokerage)},{nameof(AccountHoldingOnMarketOpen)},持仓合约未找到,{position.InstrumentId}");
                continue;
            }

            var holding = ConvertPosition(position, symbol);
            holdings.Add(holding);
        }

        foreach (var holding in holdings.OrderByDescending(x => x.Type))
        {
            //改为通用开平仓机制  replace up
            var securityHolding = holding.HoldingType switch
            {
                SecurityHoldingType.Long => _algorithm.Securities[holding.Symbol].LongHoldings,
                SecurityHoldingType.Short => _algorithm.Securities[holding.Symbol].ShortHoldings,
                _ => _algorithm.Securities[holding.Symbol].Holdings
            };
            securityHolding.SetHoldings(holding.AveragePrice, holding.Quantity, holding.QuantityT0);
            securityHolding.SetProfit(holding.RealizedPnL);
            securityHolding.SetTotalFee(holding.Commission);

            if (holding.MarketPrice != 0m)
            {
                _algorithm.Securities[holding.Symbol].SetMarketPrice(new TradeBar
                {
                    Time = DateTime.Now,
                    Open = holding.MarketPrice,
                    High = holding.MarketPrice,
                    Low = holding.MarketPrice,
                    Close = holding.MarketPrice,
                    Volume = 0,
                    Symbol = holding.Symbol,
                    DataType = MarketDataType.TradeBar
                });
            }
        }
    }

    private void SubscriptionOnMarketOpen()
    {
        InstrumentIsReady = false;
        var options = _symbols.Values.Where(n => n.SecurityType == SecurityType.Option).ToHashSet(n => n.value);
        _traderApi.QueryInstrument();
        while (!InstrumentIsReady)
        {
            Thread.Sleep(10);
        }

        var listing = new List<Symbol>();
        foreach (var (_, symbol) in _symbols)
        {
            if (symbol.SecurityType != SecurityType.Option)
            {
                continue;
            }

            if (options.Contains(symbol.value))
            {
                continue;
            }

            listing.Add(symbol);
        }

        var subscriptions = _subscribedSymbol.Values.Select(n => n.InstrumentId).ToArray();
        _marketApi.Subscribe(subscriptions!);
        //_subscribedSymbol.Clear();

        //foreach (var subscription in subscriptions)
        //{
        //    if (subscription == null)
        //    {
        //        continue;
        //    }
        //    if (subscription.SecurityType == SecurityType.Option)
        //    {
        //        _algorithm.AddOptionContract(subscription);
        //    }
        //    else
        //    {
        //        _algorithm.AddSecurity(
        //            subscription.SecurityType,
        //            subscription.value,
        //            Resolution.Tick,
        //            subscription.ID.Market,
        //            true,
        //            0m,
        //            false,
        //            subscription.SymbolProperties);
        //    }
        //}

        if (listing.Count > 0)
        {
            _algorithm.OnSymbolListed(listing);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public bool UseSyncData { get; set; }

    /// <summary>
    /// Specifies whether the brokerage will instantly update account balances
    /// </summary>
    public override bool AccountInstantlyUpdated => true;


    /// <summary>
    /// 是否出错
    /// </summary>
    /// <param name="rspInfo"></param>
    /// <returns></returns>
    public bool IsError(MomRspInfo rspInfo)
    {
        if (rspInfo != null && rspInfo.ErrorID != 0)
        {
            Log.Error($"{nameof(MomBrokerage)},Error:{rspInfo.ErrorID},{rspInfo.ErrorMsg}");
            return true;
        }

        return false;
    }

    /// <summary>
    /// 初始化数据
    /// </summary>
    public void ClearData()
    {
        lock (_locker)
        {
            _orderData.Clear();
            _symbols.Clear();
            _subscribedSymbol.Clear();
            _underlying.Clear();
            _optionChainProvider.Clear();
        }
    }

    /// <summary>
    /// 支持的证券类型,根据实现更新
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public bool SupportSymbol(Symbol symbol)
    {
        var market = symbol.ID.Market;
        if (symbol.Value.ToLower().IndexOf("universe", StringComparison.Ordinal) != -1)
            return false;

        return market is Market.SSE
            or Market.SZSE
            or Market.CFFEX
            or Market.USA
            or Market.HKG
            or Market.HKA;
    }

    private void ProcessTradingAction(TradingAction action)
    {
        switch (action.Type)
        {
            case TradingActionType.Timer:
                InternalCheckOrderAndTrade();
                return;
            case TradingActionType.PlaceOrder:
                InternalPlace(action.Order!);
                return;
            case TradingActionType.CancelOrder:
                InternalCancel(action.Order!);
                return;
        }

        if (action.Response == null)
            return;

        try
        {
            var response = action.Response;
            switch (response.MsgId)
            {
                case MomMessageType.RtnInputOrder:
                case MomMessageType.RspInputOrder:
                    OnRspInputOrder(response.Data.AsInputOrder!, response.RspInfo);
                    return;
                case MomMessageType.RspOrderAction:
                    OnRspOrderAction(response.Data.AsInputOrderAction!, response.RspInfo);
                    return;
                case MomMessageType.RtnOrder:
                    OnReturnOrder(response.Data.AsOrder!, response.Index);
                    return;
                case MomMessageType.RtnTrade:
                    OnReturnTrade(response.Data.AsTrade!, response.Index);
                    return;
                case MomMessageType.RtnPosition:
                    OnReturnPosition(response.Data.AsPosition!);
                    return;
                case MomMessageType.RtnAccount:
                    OnReturnAccount(response.Data.AsAccount!);
                    return;
                case MomMessageType.RtnFundAccount:
                    OnReturnFundAccount(response.Data.AsFundAccount!);
                    return;
                case MomMessageType.RspQryOrder:
                    OnRspQryOrder(response.Data.AsOrder!, response.RspInfo, response.Last);
                    return;
                case MomMessageType.RspQryTrade:
                    OnRspQryTrade(response.Data.AsTrade!, response.RspInfo, response.Last);
                    return;
                case MomMessageType.RspQryPosition:
                    OnRspQryPosition(response.Data.AsPosition!, response.RspInfo, response.Last);
                    return;
                case MomMessageType.RspQryAccount:
                    OnRspQryAccount(response.Data.AsAccount!, response.RspInfo, response.Last);
                    return;
                case MomMessageType.RspQryExchangeAccount:
                    OnRspExchangeAccount(response.Data.AsFundAccount!, response.RspInfo, response.Last);
                    return;
                case MomMessageType.RspQryExchangeOrder:
                    OnRspExchangeOrder(response.Data.AsFundOrder!, response.RspInfo, response.Last);
                    return;
                case MomMessageType.RspQryExchangePosition:
                    OnRspExchangePosition(response.Data.AsFundPosition!, response.RspInfo, response.Last);
                    return;

            }
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}");
        }
    }

    /// <summary>
    /// 交易连接成功回调
    /// </summary>
    public void OnTraderConnected()
    {
        TradingConnected = true;
        var msg = $"{nameof(MomBrokerage)},{_tradeUser.UserId},交易连接成功";
        Log.Trace(msg);
    }

    /// <summary>
    /// 交易连接断开回调
    /// </summary>
    /// <param name="type"></param>
    public void OnTraderDisconnected(int type)
    {
        if (!TradingConnected)
        {
            return;
        }

        TradingConnected = false;
        InstrumentIsReady = false;

        if (_enableSyncQuery)
        {
            _orderCheckTimer.Stop();
        }

        var msg = $"{nameof(MomBrokerage)},{_mdUser.UserId},交易连接中断({type}).";
        Log.Trace(msg);
        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Disconnect, -1, msg));

        if (type != 0 && !_exitToken.IsCancellationRequested)
        {
            msg = $"交易连接中断({type}),稍后尝试重新连接.";
            Log.Trace(msg);
            NotifyException(msg);
            Task.Delay(TimeSpan.FromSeconds(5), _exitToken.Token).ContinueWith(_ =>
            {
                TraderConnect();
            });
        }
        else
        {
            _tradingConnectEvent.Set();
        }
    }

    /// <summary>
    /// 交易错误回调
    /// </summary>
    /// <param name="rspInfo"></param>
    public void OnTraderRspError(MomRspInfo rspInfo)
    {
        if (rspInfo == null)
            return;

        Log.Error($"{nameof(MomBrokerage)},{_tradeUser.UserId},{rspInfo.ErrorID},{rspInfo.ErrorMsg}");
    }

    /// <summary>
    /// 交易登录成功回调
    /// </summary>
    /// <param name="userLogin"></param>
    /// <param name="rspInfo"></param>
    public void OnTraderRspUserLogin(MomRspUserLogin userLogin, MomRspInfo rspInfo)
    {
        if (IsError(rspInfo))
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(OnTraderRspUserLogin)},{rspInfo.ErrorMsg}");
            NotifyException($"交易登录失败,{rspInfo.ErrorMsg}");
            TraderDisconnect();
            return;
        }

        Log.Trace($"{nameof(MomBrokerage)},{_tradeUser.UserId},交易登陆成功");

        if (!TradingConnected)
        {
            TradingConnected = true;
        }

        if (userLogin.SettlementNotice != null)
        {
            var notice = userLogin.SettlementNotice;
            if (notice.Assigned.Any() || notice.Dividend.Any() || notice.Missing.Any())
            {
                _algorithm.OnSettlementNotice(notice.Assigned, notice.Dividend, notice.Missing);
            }
        }

        if (_enableSyncQuery)
        {
            _orderCheckTimer.Start();
        }
        _traderApi.QueryInstrument();
    }


    /// <summary>
    /// 下单回调 只有出错的时候才回调
    /// </summary>
    /// <param name="inputOrder"></param>
    /// <param name="rspInfo"></param>
    public void OnRspInputOrder(MomInputOrder inputOrder, MomRspInfo rspInfo)
    {
        if (!IsError(rspInfo))
        {
            return;
        }

        if (!_orderData.TryGetValue(inputOrder.OrderRef, out var orderData))
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(OnRspInputOrder)},订单未找到,ref:{inputOrder.OrderRef}");
            return;
        }

        if (orderData.Order == null)
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(OnRspInputOrder)},策略订单未找到,ref:{inputOrder.OrderRef}");
            return;
        }

        Log.Error($"{nameof(MomBrokerage)},{nameof(OnRspInputOrder)},id:{orderData.Order.Id},ref:{inputOrder.OrderRef},{rspInfo.ErrorMsg}");

        if (orderData.Order.Status == OrderStatus.Invalid)
        {
            Log.Trace($"{nameof(MomBrokerage)},{nameof(OnRspInputOrder)},策略订单已经无效,id:{orderData.Order.Id},ref:{inputOrder.OrderRef}");
            return;
        }

        var orderEvent = new OrderEvent(
            orderData.Order,
            GetTradingTime(orderData.Order.Symbol).UtcDateTime,
            OrderFee.Zero,
            $"{rspInfo.ErrorMsg}")
        {
            Status = OrderStatus.Invalid
        };

        OnOrderEvent(orderEvent);
    }

    /// <summary>
    /// 查询mom资金账户
    /// </summary>
    /// <param name="OnRspQryAccount"></param>
    /// <param name="rspInfo"></param>
    /// <param name="isLast"></param>
    public void OnRspQryAccount(MomAccount momAccount, MomRspInfo rspInfo, bool isLast)
    {
        if (IsError(rspInfo))
        {
            Log.Error($"OnRspQryTradingAccount: {rspInfo.ErrorMsg}");
            _queryAccountEvent.Set();
            return;
        }

        if (momAccount == null)
        {
            _queryAccountEvent.Set();
            return;
        }

        UpdateBrokerageAccountInfo(momAccount);

        if (isLast)
        {
            _queryAccountEvent.Set();
            //_initQueryAccount = true;
        }
    }

    /// <summary>
    /// 查询Mom报单
    /// </summary>
    /// <param name="momOrder"></param>
    /// <param name="rspInfo"></param>
    /// <param name="isLast"></param>
    public void OnRspQryOrder(MomOrder momOrder, MomRspInfo rspInfo, bool isLast)
    {
        if (IsError(rspInfo))
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(OnRspQryOrder)},{rspInfo.ErrorMsg}");
            _queryOrderEvent.Set();
            return;
        }

        if (momOrder != null)
        {
            Log.Trace($"{nameof(MomBrokerage)},{nameof(OnRspQryOrder)},ref:{momOrder.OrderRef},{ConstantHelper.GetName<MomOrderStatusType>(momOrder.OrderStatus)}");
            ProcessOrder(momOrder, false);
        }

        if (isLast)
        {
            _queryOrderEvent.Set();
        }
    }

    /// <summary>
    /// 查询Mom成交
    /// </summary>
    /// <param name="momTrade"></param>
    /// <param name="rspInfo"></param>
    /// <param name="isLast"></param>
    public void OnRspQryTrade(MomTrade momTrade, MomRspInfo rspInfo, bool isLast)
    {
        if (IsError(rspInfo))
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(OnRspQryTrade)},{rspInfo.ErrorMsg}");
            _queryTradeEvent.Set();
            return;
        }

        if (momTrade != null)
        {
            Log.Trace($"{nameof(MomBrokerage)},{nameof(OnRspQryTrade)},tid:{momTrade.TradeId}");

            try
            {
                _lastTradeId = Math.Max(_lastTradeId, momTrade.TradeLocalId);
                OnReturnTrade(momTrade, 0);
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(MomBrokerage)},{nameof(OnRspQryTrade)},{ex.Message}");
                _queryTradeEvent.Set();
            }
        }

        if (isLast)
        {
            Log.Trace($"{nameof(MomBrokerage)},{nameof(OnRspQryTrade)},lastTradeId:{_lastTradeId}");
            _queryTradeEvent.Set();
        }
    }

    /// <summary>
    /// 查询mom持仓
    /// </summary>
    /// <param name="momPosition"></param>
    /// <param name="rspInfo"></param>
    /// <param name="isLast"></param>
    public void OnRspQryPosition(MomPosition momPosition, MomRspInfo rspInfo, bool isLast)
    {
        if (IsError(rspInfo))
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(OnRspQryPosition)}: {rspInfo.ErrorMsg}");
            _queryPositionEvent.Set();
            _querySyncDataEvent.Set();
            return;
        }

        if (momPosition != null && momPosition.Position != 0)
        {
            _momPositions.AddOrUpdate(momPosition.PositionId, _ => momPosition, (_, _) => momPosition);
        }

        if (isLast)
        {
            _queryPositionEvent.Set();
            _querySyncDataEvent.Set();
        }
    }

    public void OnRspOrderAction(MomInputOrderAction momAction, MomRspInfo rspInfo)
    {
        if (!IsError(rspInfo))
            return;

        if (momAction == null)
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(OnRspOrderAction)},MomInputOrderAction为空");
            return;
        }

        if (!_orderData.TryGetValue(momAction.OrderRef, out var orderData))
        {
            Log.Trace($"{nameof(MomBrokerage)},{nameof(OnRspOrderAction)},撤单目标未找到,ref:{momAction.OrderRef}");
            return;
        }

        if (orderData.Order == null)
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(OnRspOrderAction)},策略订单未找到,ref:{momAction.OrderRef}");
            return;
        }

        Log.Trace($"{nameof(MomBrokerage)},{nameof(OnRspOrderAction)},撤单失败,id:{orderData.Order.Id},ref:{momAction.OrderRef},{rspInfo.ErrorMsg}");
    }



    private Order CreateQcOrder(MomOrder momOrder)
    {
        Order order;
        if (momOrder.OrderPriceType == MomOrderPriceTypeType.AnyPrice)
        {
            order = new MarketOrder();
        }
        else
        {
            order = new LimitOrder { LimitPrice = Convert.ToDecimal(momOrder.LimitPrice) };
            order.Price = Convert.ToDecimal(momOrder.LimitPrice);
        }

        order.Time = momOrder.GetInsertTime();
        order.LastUpdateTime = momOrder.GetUpdateTime();
        order.CanceledTime = momOrder.GetCancelTime();

        order.Offset = GetQCOrderOffset(momOrder);
        order.Status = GetQCOrderStatus(momOrder.OrderStatus);
        order.BrokerId.Add(Convert.ToString(momOrder.OrderRef));
        order.Quantity = momOrder.Direction == MomDirectionType.Buy ?
            Convert.ToDecimal(momOrder.VolumeTotalOriginal) :
            (-1 * Convert.ToDecimal(momOrder.VolumeTotalOriginal));

        if (!_symbols.TryGetValue(momOrder.InstrumentId, out var symbol))
        {
            Log.Error($"MomBrokerage symbol not found:{momOrder.InstrumentId}");
        }

        order.Symbol = symbol;
        if (symbol != null && !SupportOffset.IsSupportOffset(symbol))
        {
            order.Offset = OrderOffset.None;
        }

        order.Id = momOrder.OrderRef;

        return order;
    }

    /// <summary>
    /// 推送报单
    /// </summary>
    public void OnReturnOrder(MomOrder momOrder, uint index)
    {
        Log.Trace($"{nameof(MomBrokerage)},{nameof(OnReturnOrder)},{index},ref:{momOrder.OrderRef},{ConstantHelper.GetName<MomOrderStatusType>(momOrder.OrderStatus)}");

        //System.Diagnostics.Debug.WriteLine($"{nameof(MomBrokerage)},{nameof(OnReturnOrder)},{index},ref:{momOrder.OrderRef},{ConstantHelper.GetName<MomOrderStatusType>(momOrder.OrderStatus)} InsertDate:{momOrder.InsertDate} InsertTime:{momOrder.InsertTime} CancelTime:{momOrder.CancelTime} UpdateTime:{momOrder.UpdateTime}");

        ProcessOrder(momOrder, true);
    }

    private void ProcessOrder(MomOrder momOrder, bool isReturn)
    {
        try
        {
            if (_orderData.TryGetValue(momOrder.OrderRef, out var orderData))
            {
                if (orderData.MomOrder != null && orderData.MomOrder.OrderStatus >= momOrder.OrderStatus)
                {
                    Log.Debug($"错误的订单状态,old:{ConstantHelper.GetName<MomOrderStatusType>(orderData.MomOrder.OrderStatus)},new:{ConstantHelper.GetName<MomOrderStatusType>(momOrder.OrderStatus)}");
                    return;
                }

                orderData.MomOrder = momOrder;
                orderData.InputLocalId = momOrder.InputLocalId;

                if (momOrder.IsFilled() || momOrder.IsPartiallyFilled())
                {
                    return;
                }

                if (momOrder.IsPartCanceled() && orderData.GetTradeVolume() < momOrder.VolumeTraded)
                {
                    return;
                }
            }
            else
            {
                if (isReturn || momOrder.IsDone())
                {
                    return;
                }

                orderData = new MomOrderData
                {
                    OrderRef = momOrder.OrderRef,
                    MomOrder = momOrder
                };
                _orderData.TryAdd(momOrder.OrderRef, orderData);
            }

            if (!_openOrders.ContainsKey(momOrder.OrderRef) && !momOrder.IsDone())
            {
                _openOrders.TryAdd(momOrder.OrderRef, orderData);
            }

            if (orderData.Order != null)
            {
                //已结束的订单不处理
                if (orderData.Order.Status.IsClosed())
                {
                    //Log.Trace($"ProcessOrder BrokerId:{orderData.Order.BrokerId[0]} Order.Status:{orderData.Order.Status} closed,skip");
                    return;
                }

                var newStatus = GetQCOrderStatus(momOrder.OrderStatus);
                if (orderData.Trades.Count > 0 && newStatus != OrderStatus.Canceled)
                {
                    return;
                }

                if (orderData.Order.Status == newStatus)
                {
                    //Log.Trace($"ProcessOrder BrokerId:{orderData.Order.BrokerId[0]} Order.Status:{orderData.Order.Status} == newStatus:{newStatus},skip");
                    return;
                }

                if (orderData.Order.Status == OrderStatus.CancelPending
                    && newStatus == OrderStatus.Submitted
                    && isReturn == false)
                {
                    CancelOrder(orderData.Order);
                    return;
                }

                if (newStatus == OrderStatus.Triggered)
                {
                    if (orderData.Order.Type == OrderType.StopMarket)
                    {
                        var stopOrder = orderData.Order as StopMarketOrder;
                        stopOrder!.StopTriggered = true;
                    }
                    if (orderData.Order.Type == OrderType.StopLimit)
                    {
                        var stopOrder = orderData.Order as StopLimitOrder;
                        stopOrder!.StopTriggered = true;
                    }
                }

                //System.Diagnostics.Debug.WriteLine($"ProcessOrder BrokerId:{orderData.Order.BrokerId[0]} Order.Status:{orderData.Order.Status} newStatus:{newStatus}");
                //Log.Trace($"ProcessOrder BrokerId:{orderData.Order.BrokerId[0]} Order.Status:{orderData.Order.Status} newStatus:{newStatus}");

                if (orderData.Order.Status == OrderStatus.Triggered && newStatus == OrderStatus.Submitted)
                {
                    return;
                }

                orderData.Order.Status = newStatus;
                orderData.Order.LastUpdateTime = momOrder.GetUpdateTime();
                orderData.Order.CanceledTime = momOrder.GetCancelTime();

                // 成交在成交回报中发消息
                if (orderData.Order.Status != OrderStatus.Filled &&
                    orderData.Order.Status != OrderStatus.PartiallyFilled)
                {
                    if (orderData.Order.Status == OrderStatus.Submitted && orderData.Trades.Count > 0)
                    {
                        return;
                    }

                    var orderEvent = new OrderEvent(
                        orderData.Order,
                        GetTradingTime(orderData.Order.Symbol).UtcDateTime,
                        OrderFee.Zero,
                        $"{nameof(MomBrokerage)},ref:{momOrder.OrderRef}")
                    {
                        Status = orderData.Order.Status,
                    };

                    OnOrderEvent(orderEvent);
                    OnOrderOccurred(new[] { new OrderRecord { order = orderData.Order } });
                }
            }
            else
            {
                orderData.Order = CreateQcOrder(momOrder);
                Log.Trace($"{nameof(MomBrokerage)},{_tradeUser.UserId},导入未完成订单,id:{orderData.Order.Id},ref:{momOrder.OrderRef}");
            }
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(ProcessOrder)},{e}");
        }
    }

    public void OnReturnTrade(MomTrade momTrade, uint index)
    {
        if (index != 0)
        {
            Log.Trace($"{nameof(MomBrokerage)},{nameof(OnReturnTrade)},{index},tid:{momTrade.TradeId},ref:{momTrade.OrderRef},volume:{momTrade.Volume}");
            //System.Diagnostics.Debug.WriteLine($"{nameof(MomBrokerage)},{nameof(OnReturnTrade)},{index},tid:{momTrade.TradeId},ref:{momTrade.OrderRef} Volume:{momTrade.Volume}");
        }

        try
        {
            var tradeIdKey = $"{momTrade.OrderRef}_{momTrade.TradeId}";
            if (_momTradeIds.ContainsKey(tradeIdKey))
            {
                Log.Trace($"{nameof(MomBrokerage)},{nameof(OnReturnTrade)},重复的成交,tid:{momTrade.TradeId},ref:{momTrade.OrderRef}");
                return;
            }

            var initQuery = UseSyncData ? _initDataSyncing : _initQueryTrade;

            // 说明是查询回来的成交 说明推送遗漏
            if (index == 0 && !initQuery)
            {
                Log.Trace($"{nameof(MomBrokerage)},{nameof(OnReturnTrade)},查询到未推送成交,tid:{momTrade.TradeId},ref:{momTrade.OrderRef},volume:{momTrade.Volume}");
            }

            if (!_orderData.TryGetValue(momTrade.OrderRef, out var orderData))
            {
                Log.Trace($"{nameof(MomBrokerage)},{nameof(OnReturnTrade)},成交未找到订单,ref:{momTrade.OrderRef}");
                return;
            }

            _momTradeIds.TryAdd(tradeIdKey, momTrade);
            orderData.Trades.Add(momTrade);

            var volume = orderData.InputOrder?.VolumeTotalOriginal ?? (orderData.Order?.Quantity ?? (orderData.MomOrder?.VolumeTotalOriginal ?? 0));
            var partiallyFilled = orderData.GetTradeVolume() < volume;

            if (orderData.Order == null)
            {
                Log.Trace($"{nameof(MomBrokerage)},{nameof(OnReturnTrade)},成交未找到对应QC订单,ref:{momTrade.OrderRef}");
                return;
            }

            //初始化查询只添加不发订单状态
            if (index == 0 && initQuery)
            {
                return;
            }

            var sign = 1;
            if (orderData.Order.Direction == OrderDirection.Sell)
            {
                sign = -1;
            }

            OrderStatus orderStatus;
            if (orderData.MomOrder != null && orderData.MomOrder.IsPartCanceled())
            {
                orderStatus = orderData.GetTradeVolume() == orderData.MomOrder.VolumeTraded
                    ? OrderStatus.Canceled
                    : OrderStatus.PartiallyFilled;
            }
            else
            {
                orderStatus = partiallyFilled ? OrderStatus.PartiallyFilled : OrderStatus.Filled;
            }

            // 发事件
            var msg = $"{nameof(MomBrokerage)},tid:{momTrade.TradeId},ref:{momTrade.OrderRef}";
            if (index > 0)
            {
                msg += $",index:{index}";
            }

            orderData.Order!.status = orderStatus != OrderStatus.Canceled
                ? orderStatus
                : OrderStatus.PartiallyFilled;

            var orderEvent = new OrderEvent(
                orderData.Order,
                GetTradingTime(orderData.Order.Symbol).UtcDateTime,
                new OrderFee(new CashAmount(momTrade.Commission, "USD")),
                msg)
            {
                FillPrice = momTrade.Price,
                FillQuantity = momTrade.Volume * sign,
                Offset = orderData.Order.offset
            };
            OnOrderEvent(orderEvent);

            if (orderStatus == OrderStatus.Canceled)
            {
                orderData.Order.status = orderStatus;
                OnOrderEvent(new OrderEvent(orderData.Order, GetTradingTime(orderData.Order.Symbol).UtcDateTime, OrderFee.Zero));
            }

            OnOrderOccurred(new[] { new OrderRecord { order = orderData.Order } });
            var record = ConvertToTradeRecord(momTrade);
            record.Tag = orderData.Order.Tag;
            OnTradeOccurred(new[] { record });
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(OnReturnTrade)},{e}");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    private TradeRecord ConvertToTradeRecord(MomTrade trade)
    {
        var item = new TradeRecord();

        if (!_symbols.TryGetValue(trade.InstrumentId, out var symbol))
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(ConvertToTradeRecord)},没有找到合约,{trade.InstrumentId}");
        }
        item.Symbol = symbol;
        item.TradeId = trade.TradeId;
        item.OrderId = trade.OrderSysId;
        item.Status = OrderStatus.Filled;
        item.Direction = trade.Direction == MomDirectionType.Buy ? OrderDirection.Buy : OrderDirection.Sell;

        if (symbol != null && !SupportOffset.IsSupportOffset(symbol))
        {
            item.Offset = OrderOffset.None;
        }
        else
        {
            item.Offset = GetQCTradeOffset(trade);
        }

        item.Time = trade.GetTradeTime();
        item.Amount = trade.Volume;
        item.Price = trade.Price;
        item.TradeValue = item.Amount * item.Price;
        return item;
    }

    private void OnReturnPosition(MomPosition position)
    {
        _momPositions.AddOrUpdate(position.PositionId, _ => position, (_, _) => position);
    }

    //private void OnReturnAccount(MomTradingAccount account)
    //{
    //    _momTradingAccount.AddOrUpdate(account.AccountId,_ => account,(id,old) => account);
    //}

    private void UpdateBrokerageAccountInfo(AccountField tradeAccount)
    {
        if (tradeAccount is MomAccount account)
        {
            lock (_momTradingAccount)
            {
                _momTradingAccount[account.AccountType] = account.Clone();
            }
        }

        var info = new BrokerageAccountInfo
        {
            IsFundAccount = tradeAccount is MomFundAccount,
            AccountId = tradeAccount.AccountId,
            AccountType = tradeAccount.AccountType,
            RealizedPnL = tradeAccount.RealizedPnL,
            UnrealizedPnL = tradeAccount.UnrealizedPnL,
            MaintMargin = tradeAccount.MaintMargin,
            Available = tradeAccount.Available,
            CashBalance = tradeAccount.CashBalance,
            BuyingPower = tradeAccount.BuyingPower,
            CashFlow = tradeAccount.Premium,
            FinancingUsed = tradeAccount.FinancingUsed,
            FinancingCommission = tradeAccount.FinancingCommission,
            FinancingRate = tradeAccount.FinancingRate,
            GuaranteeRate = tradeAccount.GuaranteeRate,
            DateTime = DateTime.UtcNow,
            CustomData = tradeAccount.CustomData == null
                ? null
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(tradeAccount.CustomData)
        };

        if (info.CustomData != null)
        {
            try
            {
                if (info.CustomData.TryGetValue("StockMarketValue", out var stockMarketValue))
                {
                    info.StockMarketValue = stockMarketValue.ToDecimal();
                }
                if (info.CustomData.TryGetValue("OptionMarketValue", out var optionMarketValue))
                {
                    info.OptionMarketValue = optionMarketValue.ToDecimal();
                }
                //IB 接口
                if (info.CustomData.TryGetValue("USD:StockMarketValue", out stockMarketValue))
                {
                    info.StockMarketValue = stockMarketValue.ToDecimal();
                }
                if (info.CustomData.TryGetValue("USD:OptionMarketValue", out optionMarketValue))
                {
                    info.OptionMarketValue = optionMarketValue.ToDecimal();
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        _accountInfos.Add(info);
        _algorithm.BrokerageAccountInfos = _accountInfos.ToArray();

        _accountMap[info.AccountId] = info;
        _algorithm.BrokerageAccountMap = _accountMap.ToDictionary();
    }

    public void OnReturnFundAccount(MomFundAccount fundAccount)
    {
        UpdateBrokerageAccountInfo(fundAccount);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="account"></param>
    public void OnReturnAccount(MomAccount account)
    {
        if (account.UserId != _tradeUser.UserId)
        {
            return;
        }

        UpdateBrokerageAccountInfo(account);
    }

    /// <summary>
    /// 合约查询回调
    /// </summary>
    /// <param name="instrument"></param>
    /// <param name="isLast"></param>
    public void OnInstrumentReady(MomInstrument? instrument, bool isLast)
    {
        if (instrument == null || instrument.Symbol.Length == 0)
            return;

        try
        {
            /*
             * MomInstrument
             * InstrumentName："50ETF购3月2908A"
             * Symbol："510050C2103A02950"
             * ExchangeSymbol："10002725"
             *
             * _symbolsMapper "10002725":"510050C2103A02950"
             * _symbols "510050C2103A02950" : Symbol
             * Symbol Value 10002725
             */
            Log.Trace($"MomBrokerage : OnInstrumentReady,InstrumentName：{instrument.InstrumentName} Symbol:{instrument.Symbol} ExchangeSymbol:{instrument.ExchangeSymbol}");
            var symbol = _momSymbolMapper.GetSymbol(instrument);
            SymbolCache.Set(symbol.Value, symbol);
            _marketSnapshots.AddOrUpdate(symbol,
                new Tick(DateTime.UtcNow.ConvertFromUtc(symbol.ExchangeTimeZone), symbol, instrument.MarketPrice, 0, 0));
            _symbols.TryAdd(instrument.Symbol, symbol);
            _symbolsMapper.TryAdd(GetSymbolValue(symbol), instrument.Symbol);

            if (symbol.SecurityType == SecurityType.Option)
            {
                //_symbolsMapper.TryAdd(inst.ExchangeSymbol,inst.Symbol);
                if (!string.IsNullOrEmpty(instrument.UnderlyingSymbol))
                {
                    _optionChainProvider.Add($"{MomSymbolMapper.GetQCUnderlying(instrument.UnderlyingSymbol)}", symbol);
                }
                else
                {
                    throw new ArgumentException("Lack of definition of underlying contract in option contract");
                }
            }

            if (isLast)
            {
                _optionChainProvider.SetReady();
                foreach (var item in _optionChainProvider.OptionChain)
                {
                    Log.Trace($"MomBrokerage OptionChain {item.Key} {item.Value.Count}");
                }
                InstrumentIsReady = true;
                _tradingConnectEvent.Set();
            }
        }
        catch (Exception e)
        {
            Log.Error($"MomBrokerage OnInstrumentReady:{e}");
        }

    }

    private void QueryOrder(long orderRef = 0)
    {
        _traderApi.QueryOrder(_tradeUser.UserId, orderRef);
    }

    private void QueryTrade(long tradeId = 0)
    {
        _traderApi.QueryTrade(_tradeUser.UserId, tradeId);
    }

    /// <summary>
    /// 行情登录回调
    /// </summary>
    /// <param name="userLogin"></param>
    /// <param name="info"></param>
    public void OnMarketRspUserLogin(MomRspUserLogin userLogin, MomRspInfo info)
    {
        if (IsError(info))
        {
            MarketDisconnect();
            NotifyException($"行情登录失败,{info.ErrorMsg}");
            Log.Trace($"MomBrokerage:{_mdUser.UserId},行情登录失败({info.ErrorMsg})");
            return;
        }
        if (!MarketConnected)
        {
            MarketConnected = true;
            _marketConnectEvent.Set();
        }
        ResetSubscribe();
        Log.Trace($"MomBrokerage:{_mdUser.UserId},行情接口登陆成功");
    }

    /// <summary>
    /// 行情连接成功回调
    /// </summary>
    public void OnMarketConnected()
    {
        const string msg = $"{nameof(MomBrokerage)},行情连接成功";
        Log.Trace(msg);
        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Reconnect, 0, msg));
        MarketConnected = true;
        _marketConnectEvent.Set();
    }

    /// <summary>
    /// 行情断开连接回调
    /// </summary>
    /// <param name="type"></param>
    public void OnMarketDisconnected(int type)
    {
        if (!MarketConnected)
        {
            return;
        }
        MarketConnected = false;

        var msg = $"{nameof(MomBrokerage)},{_mdUser.UserId},行情连接中断({type})";
        Log.Trace(msg);
        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Disconnect, -1, msg));

        if (type != 0 && !_exitToken.IsCancellationRequested)
        {
            msg = $"行情连接中断({type}),稍后尝试重新连接.";
            Log.Trace(msg);
            NotifyException(msg);
            Task.Delay(TimeSpan.FromSeconds(5), _exitToken.Token).ContinueWith(_ => MarketConnect());
        }
        else
        {
            _marketConnectEvent.Set();
        }
    }

    /// <summary>
    /// 行情错误回调
    /// </summary>
    /// <param name="rspInfo"></param>
    public void OnMarketRspError(MomRspInfo? rspInfo)
    {
        if (rspInfo == null)
            return;
        Log.Error($"{nameof(MomBrokerage)},行情接口错误,{rspInfo.ErrorMsg}");
    }

    /// <summary>
    /// 行情推送
    /// </summary>
    /// <param name="data"></param>
    public void OnDepthMarketData(ref MomDepthMarketData data)
    {
        if (!_subscribedSymbol.TryGetValue(data.ExchangeSymbol, out var subscribeData))
            if (!_subscribedSymbol.TryGetValue(data.Symbol, out subscribeData))
                return;

        var tick = ConvertTick(data, subscribeData.SubscribeSymbol!);
        if (_algorithm.SimulationMode)
        {
            _exchangeTimeManager.SetExchangeTime(tick.Symbol, tick.Time);
        }
        _marketSnapshots.AddOrUpdate(tick.symbol, tick);
        _mdLog.Debug($"OnData: {data.ExchangeSymbol}, {data.UpdateTime}, {data.LastPrice}, {data.Volume}, {data.BidPrice1}, {data.AskPrice1}");

        if (tick.IsValid())
        {
            _ticks.Enqueue(tick);
        }
    }

    /// <summary>
    /// 订阅合约回调
    /// </summary>
    /// <param name="inst"></param>
    /// <param name="rspInfo"></param>
    public void OnRspSubscribe(MomSpecificInstrument inst, MomRspInfo rspInfo)
    {
        if (IsError(rspInfo))
        {
            Log.Trace($"{nameof(MomBrokerage)},{inst.InstrumentId},{rspInfo.ErrorMsg}");
            return;
        }

        if (_subscribedSymbol.TryGetValue(inst.InstrumentId, out var subscribeData))
        {
            subscribeData.SubscribeStatus = 1;
        }
        Log.Trace($"{nameof(MomBrokerage)},行情订阅成功,{inst.InstrumentId}");
    }

    /// <summary>
    /// 退订合约回调
    /// </summary>
    /// <param name="inst"></param>
    /// <param name="rspInfo"></param>
    public void OnRspUnsubscribe(MomSpecificInstrument inst, MomRspInfo rspInfo)
    {
        if (IsError(rspInfo))
            return;

        _subscribedSymbol.TryRemove(inst.InstrumentId, out _);
        Log.Trace($"{nameof(MomBrokerage)},行情订阅取消成功,{inst.InstrumentId}");
    }

    /// <summary>
    /// 交易是否连接
    /// </summary>
    /// <returns></returns>
    public bool IsTraderConnected()
    {
        return TradingConnected && _traderApi.Connected;
    }

    /// <summary>
    /// 连接并登录交易服务器
    /// </summary>
    public void TraderConnect()
    {
        if (_exitToken.IsCancellationRequested)
        {
            return;
        }

        if (TradingConnected || _traderApi.Connected)
        {
            return;
        }

        if (!_tradingConnecting.FalseToTrue())
        {
            return;
        }

        _traderApi.Release();
        _traderApi.Init();
        _traderApi.Login(_tradeUser.UserId, _tradeUser.Password);
        Log.Trace($"{nameof(MomBrokerage)},开始连接交易接口");

        Task.Delay(TimeSpan.FromSeconds(30), _exitToken.Token).ContinueWith(_ =>
        {
            try
            {
                _tradingConnecting.TrueToFalse();
                if (_exitToken.IsCancellationRequested)
                {
                    return;
                }

                if (!TradingConnected || !InstrumentIsReady)
                {
                    if (!TradingConnected)
                    {
                        Log.Trace($"{nameof(MomBrokerage)},连接交易超时");
                    }

                    if (!InstrumentIsReady)
                    {
                        Log.Trace($"{nameof(MomBrokerage)},接收合约超时");
                    }

                    NotifyException("交易连接超时,尝试重新连接");
                    TraderDisconnect();
                    TraderConnect();
                }

                if (!MarketConnected)
                {
                    Log.Trace($"{nameof(MomBrokerage)},行情连接超时");
                    NotifyException("行情连接超时,尝试重新连接");
                    MarketDisconnect();
                    MarketConnect();
                }

                if (IsConnected)
                {
                    NotifyException("交易行情连接成功!");
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        });
    }

    private void QueryMomAccount()
    {
        _queryAccountEvent.Reset();
        _traderApi.QueryAccount(_tradeUser.UserId);
        _queryAccountEvent.WaitOne(EventTimeout);
    }

    /// <summary>
    /// 断开交易服务器
    /// </summary>
    public void TraderDisconnect()
    {
        if (!TradingConnected && !_traderApi.Connected)
            return;

        _traderApi.Release();
    }

    /// <summary>
    /// 连接行情服务器
    /// </summary>
    public void MarketConnect()
    {
        if (_exitToken.IsCancellationRequested)
        {
            return;
        }

        if (MarketConnected || _marketApi.Connected)
        {
            return;
        }

        _marketApi.Release();
        _marketApi.Init();
        _marketApi.Login(_mdUser.UserId, _mdUser.Password);
        Log.Trace($"{nameof(MomBrokerage)},开始连接行情接口");
    }

    /// <summary>
    /// 行情是否连接
    /// </summary>
    /// <returns></returns>
    public bool IsMarketConnected()
    {
        return MarketConnected && _marketApi.Connected;
    }

    /// <summary>
    /// 断开行情服务器
    /// </summary>
    public void MarketDisconnect()
    {
        if (!MarketConnected && !_marketApi.Connected)
            return;

        _marketApi.Release();
    }

    /// <summary>
    /// 断线重新订阅
    /// </summary>
    public void ResetSubscribe()
    {
        var subscribe = _subscribedSymbol.Values.ToArray();
        //_subscribeSymbol.Clear();
        foreach (var symbol in subscribe)
        {
            _marketApi.Subscribe(symbol.InstrumentId!);
            Log.Trace($"{nameof(MomBrokerage)},自动订阅合约,{symbol.InstrumentId}");
        }
    }

    /// <summary>
    /// 重新连接api
    /// </summary>
    public bool ResetConnection()
    {
        Disconnect();
        Connect();
        Log.Trace("MomBrokerage: Reconnected");
        return IsConnected;
    }

    #region IDataQueueUniverseProvider implementation

    /// <summary>
    /// Method returns a collection of Symbols that are available at the broker.
    /// </summary>
    /// <param name="lookupName"></param>
    /// <param name="securityType"></param>
    /// <param name="securityCurrency"></param>
    /// <param name="securityExchange"></param>
    /// <returns></returns>
    public IEnumerable<Symbol> LookupSymbols(string lookupName, SecurityType securityType, string? securityCurrency = null, string? securityExchange = null)
    {
        Log.Trace("MomBrokerage.LookupSymbols(): Requesting symbol list for " + lookupName + " ...");

        var symbols = new List<Symbol>();

        if (securityType == SecurityType.Option)
        {
            var underlyingSymbol = Symbol.Create(lookupName, SecurityType.Equity, lookupName.EndsWith(".SZ") ? Market.SZSE : Market.SSE);
            symbols.AddRange(_algorithm.OptionChainProvider.GetOptionContractList(underlyingSymbol, DateTime.Today));
        }
        else
        {
            throw new ArgumentException("MomBrokerage.LookupSymbols() not support securityType:" + securityType);
        }

        Log.Trace("MomBrokerage.LookupSymbols(): Returning {0} contract(s) for {1}", symbols.Count, lookupName);

        return symbols;
    }
    #endregion

    /// <summary>
    /// Gets the history for the requested security
    /// </summary>
    /// <param name="request">The historical data request</param>
    /// <returns>An enumerable of bars covering the span specified in the request</returns>
    public override IEnumerable<BaseData> GetHistory(HistoryRequest request)
    {
        //LeanData.GenerateZipFilePath()
        return Array.Empty<BaseData>();
    }

    #region IDataQueueHandler implementation
    /// <summary>
    /// IDataQueueHandler interface implementation
    /// </summary>
    /// <returns></returns>
    public IEnumerable<BaseData> GetNextTicks()
    {
        while (_ticks.TryDequeue(out var tick))
        {
            if (_underlyingSend)
            {
                yield return tick;
            }
            else
            {
                if (tick.Symbol.SecurityType == SecurityType.Option)
                    continue;
                if (tick.Symbol.SecurityType == SecurityType.Equity)
                    _underlyingSend = true;
            }

            if (_underlying.ContainsKey(tick.Symbol))
            {
                var underlyingTick = tick.Clone();
                underlyingTick.Symbol = _underlying[tick.Symbol];
                yield return underlyingTick;
            }
        }
    }

    private static string GetSymbolValue(Symbol symbol)
    {
        if (symbol.IsChinaMarket() && symbol.SecurityType == SecurityType.Equity)
        {
            if (symbol.Value.StartsWith("SH") || symbol.Value.StartsWith("SZ"))
                return symbol.Value[2..];
        }

        return symbol.Value;
    }

    /// <summary>
    /// Adds the specified symbols to the subscription
    /// </summary>
    /// <param name="job"></param>
    /// <param name="symbols"></param>
    public void Subscribe(LiveNodePacket? job, IEnumerable<Symbol> symbols)
    {
        var subscribes = new List<string>();

        foreach (var symbol in symbols)
        {
            if (!SupportSymbol(symbol))
                continue;

            if (symbol.IsCanonical())
            {
                //SubscribeCanonicalSymbol(symbol);
                return;
            }

            UpdateSymbolProperties(symbol);
            _exchangeTimeManager.AddSymbol(symbol);

            var subscribeSymbol = GetSymbolValue(symbol);
            //10002930,510050P2101A03900
            if (!_symbolsMapper.TryGetValue(subscribeSymbol, out var mappedSymbol))
            {
                mappedSymbol = subscribeSymbol;
            }

            //10002930,symbol
            if (_subscribedSymbol.TryGetValue(subscribeSymbol, out var subscribeData))
            {
                if (subscribeData.SubscribeStatus == 1)
                    continue;
            }
            else
            {
                subscribeData = new SubscribeData
                {
                    InstrumentId = mappedSymbol,
                    SubscribeSymbol = symbol,
                    SubscribeStatus = 0
                };
                //System.Diagnostics.Debug.WriteLine($"_subscribeSymbol {subscribeSymbol}");
                _subscribedSymbol.TryAdd(subscribeSymbol, subscribeData);
            }

            subscribes.Add(mappedSymbol);

            if (symbol.Underlying != null && !symbol.IsCanonical())
            {
                var underLying = symbol.Underlying;
                _underlying.TryAdd(underLying, symbol);
            }
        }

        if (!subscribes.Any())
            return;

        var momSubscribe = subscribes.ToArray();
        _marketApi.Subscribe(momSubscribe);

        foreach (var symbol in subscribes.ToArray())
        {
            Log.Trace($"{nameof(MomBrokerage)} 订阅合约: {symbol}");
        }
    }

    private static void UpdateSymbolProperties(Symbol symbol)
    {
        if (SymbolCache.TryGetSymbol(symbol.Value, out var cached) && cached.SymbolProperties != null)
        {
            symbol.SymbolProperties = cached.SymbolProperties;
        }
    }

    /// <summary>
    /// Removes the specified symbols to the subscription
    /// </summary>
    /// <param name="job"></param>
    /// <param name="symbols"></param>
    public void Unsubscribe(LiveNodePacket? job, IEnumerable<Symbol> symbols)
    {
        var unsubscribes = new List<string>();

        foreach (var symbol in symbols)
        {
            if (!SupportSymbol(symbol))
                continue;

            var subscribeSymbol = symbol.Value;
            if (_subscribedSymbol.TryGetValue(subscribeSymbol, out var subscribeData))
            {
                subscribeData.SubscribeStatus = 2;
            }

            if (symbol.ID.SecurityType == SecurityType.Option && symbol.ID.StrikePrice == 0.0m)
            {
                _underlying.TryRemove(symbol.Underlying, out _);
            }

            unsubscribes.Add(subscribeSymbol);
        }

        if (!unsubscribes.Any())
            return;

        var momUnsubscribe = unsubscribes.ToArray();
        _marketApi.Unsubscribe(momUnsubscribe);

        Log.Trace($"MomBrokerage: unsubscribe  request ({string.Join(",", momUnsubscribe)})");
    }

    #endregion
    #region IBrokerage implementation
    /// <summary>
    /// Places a new order and assigns a new broker ID to the order
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public override bool PlaceOrder(Order order)
    {
        if (!IsTraderConnected())
        {
            if (!ResetConnection())
            {
                Log.Error($"MomBrokerage TradeConnect Failed (trade connect failed)- OrderId: {order.Id}");
                return false;
            }
        }

        Log.Trace($"{nameof(MomBrokerage)},{nameof(PlaceOrder)},id:{order.Id},{order.Symbol.Value} {order.Direction} {order.Offset}");

        _tradingAction.Post(new TradingAction(order));
        return true;
    }

    private DateTimeOffset GetTradingTime(Symbol symbol)
    {
        return _algorithm.SimulationMode ? _exchangeTimeManager.GetExchangeTime(symbol) : DateTimeOffset.Now;
    }

    private void InternalPlace(Order order)
    {
        try
        {
            if (!_tradingIsReady || _cancelPendingList.Contains(order.Id))
            {
                //order.CanceledTime = GetTradingTime(order.symbol).DateTime;
                var cancelEvent = new OrderEvent(
                    order,
                    GetTradingTime(order.symbol).UtcDateTime,
                    OrderFee.Zero)
                {
                    Status = OrderStatus.Canceled,
                };
                OnOrderEvent(cancelEvent);

                return;
            }

            var inputOrder = CreateInputOrder(order);

            if (order.BrokerId.Count > 0)
            {
                order.BrokerId[0] = inputOrder.OrderRef.ToString();
            }
            else
            {
                order.BrokerId.Add(inputOrder.OrderRef.ToString());
            }

            var orderData = new MomOrderData
            {
                OrderRef = inputOrder.OrderRef,
                InputOrder = inputOrder,
                Order = order.Clone()
            };
            orderData.Order.Status = OrderStatus.Submitted;
            _orderData.TryAdd(inputOrder.OrderRef, orderData);

            Log.Trace($"{nameof(MomBrokerage)},{nameof(InternalPlace)},id:{order.Id},ref:{inputOrder.OrderRef},{order.Symbol.Value} {inputOrder.InstrumentId} {order.Direction} {order.Offset}");
            //System.Diagnostics.Debug.WriteLine($"==========={nameof(MomBrokerage)},{nameof(InternalPlace)},id:{order.Id},ref:{inputOrder.OrderRef},{order.Symbol.Value} {inputOrder.InstrumentId} {order.Direction} {order.Offset}");

            _traderApi.InputOrder(inputOrder);

            var submittedEvent = new OrderEvent(
                order,
                DateTime.UtcNow,
                OrderFee.Zero,
                $"ref:{inputOrder.OrderRef}")
            {
                Status = OrderStatus.Submitted,
            };

            OnOrderEvent(submittedEvent);
        }
        catch (Exception e)
        {
            var cancelEvent = new OrderEvent(
                order,
                GetTradingTime(order.symbol).UtcDateTime,
                OrderFee.Zero)
            {
                Status = OrderStatus.Canceled,
            };
            OnOrderEvent(cancelEvent);
            Log.Error($"{nameof(MomBrokerage)},{nameof(InternalPlace)},{e}");
        }
    }

    /// <summary>
    /// 创建一个Mom报单对象
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    private MomInputOrder CreateInputOrder(Order order)
    {
        var inputOrder = new MomInputOrder();

        inputOrder.OrderRef = _idGen.Next();

        if (!_symbolsMapper.TryGetValue(GetSymbolValue(order.Symbol), out var instrumentId))
        {
            instrumentId = order.Symbol.Value;
        }

        inputOrder.InstrumentId = instrumentId;
        inputOrder.Direction = GetMomDirection(order);
        inputOrder.SetOffsetFlag(GetMomOrderOffset(order));
        inputOrder.OrderPriceType = GetMomOrderPriceType(order);
        var limit = order as LimitOrder;
        inputOrder.LimitPrice = limit?.LimitPrice ?? 0;
        inputOrder.VolumeTotalOriginal = (int)Math.Abs(order.Quantity);
        if (order.properties is MomBrokersOrderProperties properties)
        {
            inputOrder.AccountId = properties.Account;
        }
        else
        {
            inputOrder.AccountId = string.Empty;
        }

        inputOrder.UserId = _tradeUser.UserId;
        return inputOrder;
    }

    /// <summary>
    /// 未完成单
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public static bool IsOpenOrder(Order? order)
    {
        return order?.Status is OrderStatus.New
            or OrderStatus.CancelPending
            or OrderStatus.Submitted
            or OrderStatus.PartiallyFilled
            or OrderStatus.Triggered
            or OrderStatus.Untriggered;
    }

    private bool TryGetOrderData(Order order, out MomOrderData? orderData)
    {
        orderData = null;
        if (order.BrokerId.Count == 0)
        {
            return false;
        }

        var id = long.Parse(order.BrokerId.First());
        return _orderData.TryGetValue(id, out orderData) && IsOpenOrder(orderData.Order);
    }

    /// <summary>
    /// 改单，可实现为先撤后发
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public override bool UpdateOrder(Order order)
    {
        Log.Trace($"MomBrokerage.UpdateOrder(),Order not found:{order.Id}");
        return false;
    }

    /// <summary>
    /// 撤单
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public override bool CancelOrder(Order order)
    {
        if (!IsTraderConnected())
        {
            if (!ResetConnection())
            {
                Log.Error($"{nameof(MomBrokerage)} TradeConnect failed - OrderId: {order.Id}");
                return false;
            }
        }

        if (order.BrokerId.Count > 0)
        {
            Log.Trace($"{nameof(MomBrokerage)},CancelOrder,id:{order.Id},ref:{order.BrokerId[0]},{order.Symbol.Value}");
        }
        else
        {
            Log.Trace($"{nameof(MomBrokerage)},CancelOrder,id:{order.Id},{order.Symbol.Value}");
        }

        _tradingAction.Post(new TradingAction(order, TradingActionType.CancelOrder));

        return true;
    }

    private void InternalCancel(Order order)
    {
        if (!_tradingIsReady)
        {
            return;
        }

        try
        {
            if (order.BrokerId.Count == 0)
            {
                _cancelPendingList.Add(order.Id);
                Log.Trace($"{nameof(MomBrokerage)},{nameof(InternalCancel)},登记 CancelPending,id:{order.Id}");
                return;
            }

            if (!TryGetOrderData(order, out var orderData) || orderData!.Order == null)
            {
                Log.Error($"{nameof(MomBrokerage)},{nameof(InternalCancel)},没有找到订单,id:{order.Id}");
                return;
            }

            if (orderData.Order.Status.IsClosed())
            {
                Log.Trace($"{nameof(MomBrokerage)},{nameof(InternalCancel)},策略订单已完成,id:{order.Id},ref:{orderData.OrderRef},{orderData.Order}");
                return;
            }
            orderData.Order.Status = order.Status;

            var orderAction = CreateOrderAction(order);
            _traderApi.CancelOrder(orderAction);
            Log.Trace($"{nameof(MomBrokerage)},{nameof(InternalCancel)},id:{order.Id},ref:{orderAction.OrderRef},{order.Symbol.Value}");
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(InternalCancel)},{e}");
        }
    }
    /// <summary>
    /// 创建一个MOM撤单对象
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    private MomInputOrderAction CreateOrderAction(Order order)
    {
        var orderAction = new MomInputOrderAction();
        orderAction.UserId = _tradeUser.UserId;
        orderAction.ActionFlag = MomActionFlag.Delete;
        orderAction.OrderRef = long.Parse(order.BrokerId.First());
        return orderAction;
    }

    /// <summary>
    /// 
    /// </summary>
    public override void Connect()
    {
        lock (_locker)
        {
            if (IsConnected)
                return;

            Log.Trace($"{nameof(MomBrokerage)} to Connect");

            _tradingConnectEvent.Reset();
            _marketConnectEvent.Reset();
            TraderConnect();
            MarketConnect();
            _tradingConnectEvent.WaitOne(EventTimeout);
            _marketConnectEvent.WaitOne(EventTimeout);
            if (_algorithm.SimulationMode)
            {
                QueryMomAccount();
            }
        }
    }

    /// <summary>
    /// Disconnects the client
    /// </summary>
    public override void Disconnect()
    {
        if (!IsMarketConnected() && !IsTraderConnected())
        {
            return;
        }

        Log.Trace($"{nameof(MomBrokerage)},断开连接");

        TraderDisconnect();
        MarketDisconnect();
    }

    /// <summary>
    /// 
    /// </summary>
    public override void Dispose()
    {
        Log.Trace($"{nameof(MomBrokerage)},销毁插件");
        _exitToken.Cancel();
        Disconnect();
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool IsConnected => TradingConnected && MarketConnected && InstrumentIsReady;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override List<Order> GetOpenOrders()
    {
        var orders = new List<Order>();
        if (!IsTraderConnected())
        {
            if (!ResetConnection())
            {
                Log.Error($"{nameof(MomBrokerage)},{nameof(GetOpenOrders)},trader not connected");
                return orders;
            }
        }

        if (!UseSyncData)
        {
            _initQueryOrder = true;
            _openOrders.Clear();
            _queryOrderEvent.Reset();
            QueryOrder();
            _queryOrderEvent.WaitOne(EventTimeout);
            _initQueryOrder = false;
        }

        // 全部从本地取，需登录补回全部数据
        var list = _openOrders.Values.ToArray();
        foreach (var orderData in list)
        {
            var order = orderData.Order;
            if (order != null && IsOpenOrder(order))
            {
                if (order.Id > 0)
                {
                    orders.Add(orderData.Order!.Clone());
                }
                else
                {
                    Log.Error($"{nameof(MomBrokerage)},{nameof(GetOpenOrders)},策略订单ID为零");
                }
            }
        }
        return orders;
    }


    /// <summary>
    /// 获取持仓
    /// </summary>
    /// <returns></returns>
    public override List<Holding> GetAccountHoldings()
    {
        var holdings = new List<Holding>();
        if (!IsTraderConnected())
        {
            if (!ResetConnection())
            {
                Log.Error($"{nameof(MomBrokerage)},{nameof(GetAccountHoldings)},trader not connected");
                return holdings;
            }
        }

        if (!UseSyncData)
        {
            _initQueryPosition = true;
            _queryPositionEvent.Reset();
            _traderApi.QueryPosition(_tradeUser.UserId);
            _queryPositionEvent.WaitOne(EventTimeout);
            _initQueryPosition = false;
        }

        var data = new Dictionary<string, (Holding? longPosition, Holding? shortPosition)>();
        try
        {
            foreach (var item in _momPositions)
            {
                var position = item.Value;
                if (position.Position == 0)
                {
                    continue;
                }

                if (!_symbols.TryGetValue(position.InstrumentId, out var symbol))
                {
                    Log.Error($"{nameof(MomBrokerage)},{nameof(GetAccountHoldings)},持仓合约未找到,{position.InstrumentId}");
                    continue;
                }

                var lastPrice = symbol.SecurityType == SecurityType.Option ? position.OpenAmount : 0m;
                var holding = ConvertPosition(position, symbol, lastPrice: lastPrice);
                if (!data.TryGetValue(position.InstrumentId, out var positions))
                {
                    positions = (null, null);
                }
                data[position.InstrumentId] = holding.HoldingType == SecurityHoldingType.Short
                        ? (positions.longPosition, holding)
                        : (holding, positions.shortPosition);
            }
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(GetAccountHoldings)},{e}");
        }

        foreach (var (_, (longPosition, shortPosition)) in data)
        {
            if (longPosition != null && shortPosition == null)
            {
                longPosition.HoldingType = SecurityHoldingType.Net;
                holdings.Add(longPosition);
            }
            else if (longPosition == null && shortPosition != null)
            {
                shortPosition.HoldingType = SecurityHoldingType.Net;
                holdings.Add(shortPosition);
            }
            else if (longPosition != null && shortPosition != null)
            {
                holdings.Add(longPosition);
                holdings.Add(shortPosition);
            }
        }
        return holdings;
    }

    /// <summary>
    /// 按币种获取现金量
    /// </summary>
    /// <returns></returns>
    public override List<CashAmount> GetCashBalance()
    {
        var balances = new List<CashAmount>();

        if (!IsTraderConnected())
        {
            if (!ResetConnection())
            {
                Log.Error($"MomBrokerage TradeConnect not connected");
                return balances;
            }
        }

        if (!IsTraderConnected())
        {
            if (!ResetConnection())
            {
                Log.Error($"{nameof(MomBrokerage)},{nameof(GetCashBalance)},trader not connected");
                return balances;
            }
        }

        if (UseSyncData && _firstGetCashBalance)
        {
            _initDataSyncing = true;
            _querySyncDataEvent.Reset();
            _traderApi.DataSync();
            _querySyncDataEvent.WaitOne(EventTimeout);
            _initDataSyncing = false;
            _firstGetCashBalance = false;
        }

        lock (_momTradingAccount)
        {
            var value = 0m;
            foreach (var (_, account) in _momTradingAccount)
            {
                switch (account.AccountType)
                {
                    case "F":
                        value += account.Available + account.MaintMargin;
                        break;
                    case "S":
                        value += account.Available;
                        break;
                    case "SO":
                        value += account.Available + account.MaintMargin;
                        break;
                    default:
                        value += Math.Max(account.Available, account.BuyingPower);
                        break;
                }
            }
            var balance = new CashAmount(value, "USD");
            balances.Add(balance);
        }

        return balances;
    }

    #endregion

    /// <summary>
    /// 交易连接状态
    /// </summary>
    public volatile bool TradingConnected;

    /// <summary>
    /// 合约查询是否完成
    /// </summary>
    public volatile bool InstrumentIsReady;

    /// <summary>
    /// 行情连接状态
    /// </summary>
    public volatile bool MarketConnected;

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override List<TradeRecord> GetHistoryTrades()
    {
        return QueryHistoryTrades();
    }

    /// <summary>
    /// 用来查询还是查询触发OrderEvent
    /// </summary>
    /// <returns></returns>
    private List<TradeRecord> QueryHistoryTrades()
    {
        var list = new List<TradeRecord>();

        foreach (var trade in _momTradeIds.Values)
        {
            var record = ConvertToTradeRecord(trade);
            list.Add(record);
            //System.Diagnostics.Debug.WriteLine($"---------------QueryHistoryTrades {record.Status}");
        }
        return list;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override List<OrderRecord> GetHistoryOrders()
    {
        var list = new List<OrderRecord>();
        foreach (var item in _orderData)
        {
            if (item.Value.Order != null)
            {
                list.Add(new OrderRecord { order = item.Value.Order });
            }
            else
            {
                list.Add(new OrderRecord { order = CreateQcOrder(item.Value.MomOrder!) });
            }
        }

        return list;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="date"></param>
    /// <returns></returns>
    public IEnumerable<Symbol> GetOptionContractList(Symbol symbol, DateTime date)
    {
        return _optionChainProvider.GetOptionContractList(symbol, date);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="date"></param>
    /// <returns></returns>
    public IEnumerable<Symbol> GetFutureContractList(Symbol symbol, DateTime date)
    {
        return _futureChainProvider.GetFutureContractList(symbol, date);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override void QueryOpenOrders(string requestId)
    {
        var task = new Task(
            () =>
            {
                var orders = GetOpenOrders();
                foreach (var order in orders)
                {
                    OnQueryOpenOrderEvent(
                        new AlgorithmQueryArgs()
                        { Type = AlgorithmQueryType.OpenOrder, RequestId = requestId, Order = order }
                    );
                }
            });
        task.Start();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override void QueryAccountHoldings(string requestId)
    {
        var task = new Task(() =>
        {
            var holdings = GetAccountHoldings();
            foreach (var holding in holdings)
            {
                OnQueryOpenOrderEvent(
                    new AlgorithmQueryArgs
                    {
                        Type = AlgorithmQueryType.Holding,
                        RequestId = requestId,
                        Holding = holding
                    }
                );
            }
        });
        task.Start();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override void QueryCashBalance(string requestId)
    {
        var task = new Task(
            () =>
            {
                var balances = GetCashBalance();
                foreach (var balance in balances)
                {
                    OnQueryOpenOrderEvent(
                        new AlgorithmQueryArgs()
                        { Type = AlgorithmQueryType.CashBalance, RequestId = requestId, Balance = balance }
                    );
                }
            });
        task.Start();

    }

    private void CheckOrderAndTrade()
    {
        _tradingAction.Post(new TradingAction(TradingActionType.Timer));
    }

    private void InternalCheckOrderAndTrade()
    {
        try
        {
            if (UseSyncData)
            {
                if (!TradingConnected || _initDataSyncing)
                    return;
            }
            else
            {
                if (!TradingConnected || _initQueryTrade || _initQueryOrder || _initQueryPosition)
                    return;
            }

            QueryTrade(_lastTradeId);

            DeleteClosedOrder();

            var pendingList = _orderData.Values
                .Where(x => x.Order != null
                            && (x.Order.Status == OrderStatus.CancelPending
                                || x.Order.Status == OrderStatus.PartiallyFilled))
                .Select(x => x.OrderRef)
                .ToList();

            foreach (var id in pendingList)
            {
                QueryOrder(id);
            }
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(InternalCheckOrderAndTrade)},{e.Message}");
        }
    }

    private void DeleteClosedOrder()
    {
        try
        {
            //删除过多的订单
            if (_orderData.Count < MaxOrdersToKeep)
                return;

            var deleted = new List<long>();

            foreach (var item in _orderData)
            {
                var order = item.Value.Order;
                if (order == null)
                {
                    continue;
                }
                //如果订单状态还没结束，不能删除
                if (order.Status.IsClosed())
                {
                    deleted.Add(item.Key);
                    Log.Trace($"{nameof(MomBrokerage)},清除已关闭的策略订单,id:{order.Id},ref:{item.Key}");
                }
            }

            foreach (var id in deleted)
            {
                _orderData.TryRemove(id, out _);
                _openOrders.TryRemove(id, out _);
            }
        }
        catch
        {
            //ignore
        }
    }

    private void OnRspExchangeAccount(MomFundAccount account, MomRspInfo rspInfo, bool isLast)
    {
        if (IsError(rspInfo))
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(OnRspExchangeAccount)},{rspInfo.ErrorMsg}");
            _queryExchangeCashEvent.Set();
            return;
        }

        _qryExchangeAccountData.Add(account);

        if (isLast)
        {
            _queryExchangeCashEvent.Set();
        }

    }
    private void OnRspExchangeOrder(MomFundOrder order, MomRspInfo rspInfo, bool isLast)
    {
        if (IsError(rspInfo))
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(OnRspExchangeOrder)},{rspInfo.ErrorMsg}");
            _queryExchangePositionEvent.Set();
            return;
        }

        _qryExchangeOrderData.Add(order);

        if (isLast)
        {
            _queryExchangeOrderEvent.Set();
        }
    }
    private void OnRspExchangePosition(MomFundPosition position, MomRspInfo rspInfo, bool isLast)
    {
        if (IsError(rspInfo))
        {
            Log.Error($"{nameof(MomBrokerage)},{nameof(OnRspExchangePosition)},{rspInfo.ErrorMsg}");
            _queryExchangeOrderEvent.Set();
            return;
        }

        _qryExchangePositionData.Add(position);

        if (isLast)
        {
            _queryExchangePositionEvent.Set();
        }
    }

    private static Order ConvertExchangeOrder(OrderField momOrder)
    {
        Order order;
        switch (momOrder.OrderPriceType)
        {
            case MomOrderPriceTypeType.AnyPrice:
                order = new MarketOrder();
                break;
            case MomOrderPriceTypeType.LimitPrice:
                order = new LimitOrder();
                break;
            case MomOrderPriceTypeType.StopLimit:
                order = new StopLimitOrder();
                break;
            case MomOrderPriceTypeType.StopMarket:
                order = new StopMarketOrder();
                break;
            default:
                throw new ArgumentException("不识别的价格类型");
        }

        order.quantity = momOrder.VolumeTotalOriginal;
        order.price = momOrder.LimitPrice;
        order.Symbol = Symbol.Empty;

        order.id = -1;

        order.contingentId = 0;
        order.fillQuantity = momOrder.VolumeTraded;
        order.averageFillPrice = 0;
        order.commission = 0;
        order.brokerId = new List<string>();
        order.brokerId.Add(momOrder.OrderRef.ToString());
        order.priceCurrency = string.Empty;
        order.time = momOrder.GetInsertTime();
        order.fillTime = DateTime.MinValue;
        order.timeZoneTime = DateTime.MinValue;
        order.lastFillTime = null;
        order.lastUpdateTime = momOrder.GetUpdateTime();
        order.canceledTime = momOrder.GetCancelTime();
        order.tradeValue = 0;
        order.status = GetQCOrderStatus(momOrder.OrderStatus);
        order.properties = null;
        order.tag = momOrder.InstrumentId;      // 没有字段用此字段返回合约代码
        order.offset = OrderOffset.None;
        order.orderSubmissionData = null;

        return order;
    }
    public List<Order> GetExchangeOpenOrders(string uId)
    {
        var orders = new List<Order>();
        if (!IsTraderConnected())
        {
            if (!ResetConnection())
            {
                Log.Error($"MomBrokerage TradeConnect not connected");
                return orders;
            }
        }

        _qryExchangeOrderData.Clear();

        _queryExchangeOrderEvent.Reset();
        _traderApi.QueryExchangeOrder(uId);
        _queryExchangeOrderEvent.WaitOne(EventTimeout);

        foreach (var order in _qryExchangeOrderData)
        {
            orders.Add(ConvertExchangeOrder(order));
        }

        return orders;
    }
    public override void QueryExchangeOpenOrders(string uId, string requestId)
    {
        var task = new Task(
            () =>
            {
                var orders = GetExchangeOpenOrders(uId);
                foreach (var order in orders)
                {
                    var isLast = orders.IndexOf(order) == orders.Count - 1;
                    OnQueryOpenOrderEvent(
                        new AlgorithmQueryArgs()
                        { Type = AlgorithmQueryType.ExchangeOpenOrder, RequestId = requestId, Order = order, IsLast = isLast }
                    );
                }
            });
        task.Start();
    }

    private static Holding ConvertExchangePosition(PositionField momPosition)
    {
        var holding = new Holding();
        holding.CurrencySymbol = momPosition.ExchangeSymbol;     // 没有字段用此字段返回合约代码
        holding.Type = SecurityType.Base;
        holding.HoldingType = SecurityHoldingType.Net;
        holding.Quantity = momPosition.Position;
        holding.AveragePrice = momPosition.OpenCost;
        holding.RealizedPnL = momPosition.RealizedPnL;
        holding.Commission = momPosition.Commission;
        return holding;
    }
    public List<Holding> GetExchangePosition(string uId)
    {
        var holdings = new List<Holding>();
        if (!IsTraderConnected())
        {
            if (!ResetConnection())
            {
                Log.Error($"MomBrokerage TradeConnect not connected");
                return holdings;
            }
        }

        _qryExchangePositionData.Clear();

        _queryExchangePositionEvent.Reset();
        _traderApi.QueryExchangePosition(uId);
        _queryExchangePositionEvent.WaitOne(EventTimeout);

        foreach (var position in _qryExchangePositionData)
        {
            holdings.Add(ConvertExchangePosition(position));
        }

        return holdings;
    }

    public override void QueryExchangeAccountHoldings(string uId, string requestId)
    {
        var task = new Task(
            () =>
            {
                var holdings = GetExchangePosition(uId);
                foreach (var holding in holdings)
                {
                    var islast = holdings.IndexOf(holding) == holdings.Count - 1;
                    OnQueryOpenOrderEvent(
                        new AlgorithmQueryArgs()
                        { Type = AlgorithmQueryType.ExchangeHolding, RequestId = requestId, Holding = holding, IsLast = islast }
                    );
                }
            });
        task.Start();
    }


    public List<CashAmount> GetExchangeCashBalance(string uId)
    {
        var balances = new List<CashAmount>();

        if (!IsTraderConnected())
        {
            if (!ResetConnection())
            {
                Log.Error($"MomBrokerage TradeConnect not connected ");
                return balances;
            }
        }

        _qryExchangeAccountData.Clear();

        _queryExchangeCashEvent.Reset();
        _traderApi.QueryExchangeAccount(uId);
        _queryExchangeCashEvent.WaitOne(EventTimeout);

        foreach (var account in _qryExchangeAccountData)
        {
            var value = account.Available > 0 ? account.Available : account.PreBalance;
            var balance = new CashAmount(value, "USD");

            balances.Add(balance);
        }

        return balances;
    }
    public override void QueryExchangeCashBalance(string uId, string requestId)
    {
        var task = new Task(
            () =>
            {
                var balances = GetExchangeCashBalance(uId);
                foreach (var balance in balances)
                {
                    var islast = balances.IndexOf(balance) == balances.Count - 1;
                    OnQueryOpenOrderEvent(
                        new AlgorithmQueryArgs()
                        { Type = AlgorithmQueryType.ExchangeCashBalance, RequestId = requestId, Balance = balance, IsLast = islast }
                    );
                }
            });
        task.Start();
    }
}