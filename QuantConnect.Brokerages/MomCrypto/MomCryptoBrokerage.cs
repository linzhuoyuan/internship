using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using FtxApi;
using FtxApi.Rest;
using FtxApi.Rest.Enums;
using MomCrypto.Api;
using MomCrypto.DataApi;
using NLog;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.OptionQuote;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;
using NCrontab;
using Newtonsoft.Json;
using HistoryRequest = QuantConnect.Data.HistoryRequest;
using Order = QuantConnect.Orders.Order;
using OrderField = MomCrypto.Api.OrderField;
using OrderType = QuantConnect.Orders.OrderType;
using Option = QuantConnect.Securities.Option.Option;
using Quote = QuantConnect.OptionQuote.Quote;
using QuantConnect.Configuration;

namespace QuantConnect.Brokerages.MomCrypto;

/// <summary>
/// 
/// </summary>
[BrokerageFactory(typeof(MomCryptoBrokerageFactory))]
public partial class MomCryptoBrokerage : Brokerage, IDataQueueHandler, IDataQueueUniverseProvider
{
    private class AccountList : ConcurrentDictionary<string, ConcurrentDictionary<string, MomAccount>>
    {
        private readonly Dictionary<string, Security?> _securities = new();

        public void AddConversionRateSecurity(string currency, Security? security)
        {
            if (!_securities.ContainsKey(currency))
            {
                _securities.Add(currency, security);
            }
        }

        public void AddOrUpdate(MomAccount account)
        {
            Log.Trace($"获取Mom账户:{account.AccountId},{account.CurrencyType},{account.Available}");
            var currency = account.CurrencyType;
            if (!TryGetValue(currency, out var list))
            {
                list = new ConcurrentDictionary<string, MomAccount>();
                TryAdd(currency, list);
            }
            list.AddOrUpdate(account.AccountId, account);
        }

        public List<CashAmount> GetCashBalance()
        {
            var balances = new List<CashAmount>();
            foreach (var (currency, accounts) in this)
            {
                var amount = accounts.Values.Sum(account =>
                {
                    var available = account.Available != 0 ? account.Available : account.PreBalance;
                    return available;
                });
                var balance = new CashAmount(amount, currency, conversionRateSecurity: _securities[currency]);
                balances.Add(balance);
            }

            return balances;
        }

        internal static CashAmount GetCashBalance(AccountField account)
        {
            return new CashAmount(account.Available, account.CurrencyType);
        }
    }

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

    private readonly MomTraderApi _traderApi;
    private readonly MomMarketDataApi _marketApi;
    private readonly MomHistoryDataApi _historyDataApi;

    private readonly IAlgorithm? _algorithm;
    private readonly object _locker = new();
    private readonly AutoResetEvent _mdConnectEvent = new(false);
    private readonly AutoResetEvent _tsConnectEvent = new(false);
    private readonly AutoResetEvent _queryPositionEvent = new(false);
    private readonly AutoResetEvent _queryAccountEvent = new(false);
    private readonly AutoResetEvent _queryOrderEvent = new(false);
    private readonly AutoResetEvent _queryTradeEvent = new(false);
    private readonly AutoResetEvent _querySyncDataEvent = new(false);
    private readonly AutoResetEvent _cancelEvent = new(false);
    private readonly AutoResetEvent _queryExchangeCashEvent = new(false);
    private readonly AutoResetEvent _queryExchangeOrderEvent = new(false);
    private readonly AutoResetEvent _queryExchangePositionEvent = new(false);

    private readonly MomCryptoUser _tradeUser;
    private readonly MomCryptoUser _mdUser;

    private readonly HashSet<long> _cancelPendingList;
    private readonly ConcurrentDictionary<long, MomCryptoOrderData> _orderData;
    private readonly ConcurrentDictionary<long, MomCryptoOrderData> _openOrders;
    private readonly ConcurrentDictionary<string, byte> _momTradeIds = new();

    /// 都没加锁
    private readonly List<MomFundAccount> _qryExchangeAccountData;
    private readonly List<MomFundOrder> _qryExchangeOrderData;
    private readonly List<MomFundPosition> _qryExchangePositionData;

    private readonly ConcurrentDictionary<string, Symbol> _symbols;
    private readonly ConcurrentDictionary<string, MomInstrument> _instruments;
    private readonly ConcurrentDictionary<string, MomInstrument> _delisting;
    private readonly List<Symbol> _listingSymbols = new();
    private readonly List<Symbol> _delistingSymbols = new();

    private readonly ConcurrentDictionary<string, string> _symbolsMapper;
    private readonly ConcurrentDictionary<string, SubscribeData> _subscribeSymbol;
    private readonly ConcurrentDictionary<Symbol, Symbol> _underlying;
    private readonly ConcurrentQueue<Tick> _ticks = new();
    private MomTrade _lastMomTrade = new();

    private volatile bool _usdtFirstSend = true;
    private readonly TickIdGen _idGen;

    private readonly ConcurrentDictionary<long, MomPosition> _momPosition = new();
    private readonly AccountList _momAccounts = new();
    private readonly Dictionary<string, Tick> _lastTicks = new();
    private readonly MomCryptoOptionChainProvider _optionChainProvider;
    private readonly MomCryptoFutureChainProvider _futureChainProvider;
    private readonly MomCryptoChainProvider _cryptoChainProvider;
    private readonly ActionBlock<TradingAction> _tradingAction;

    private readonly System.Timers.Timer _checkTimer;
    private DateTime _lastUpdateInstrumentTime = DateTime.MaxValue;
    private readonly CrontabSchedule _scheduleInstrument = CrontabSchedule.Parse("11,15 */1 * * *");
    private long _lastTradeId;

    private readonly bool _enableSyncQuery;
    private volatile bool _initDataSyncing = true;
    private volatile bool _initQueryOrder = true;
    private volatile bool _initQueryTrade = true;
    private volatile bool _initQueryPosition = true;
    private volatile bool _tradingIsReady = true;

    private readonly Logger _mdLog;

    private readonly ExchangeTimeManager _exchangeTimeManager = new();
    private readonly AtomicBoolean _tradingConnecting = new();
    private readonly CancellationTokenSource _exitToken = new();
    private readonly Dictionary<string, BrokerageAccountInfo> _accountMap = new();

    /// <summary>
    /// 
    /// </summary>
    public MomCryptoBrokerage(
        IAlgorithm algorithm,
        string tradeServer,
        string tradeUser,
        string tradePassword,
        string mdServer,
        string mdUser,
        string mdPassword,
        string historyServer,
        bool enableSyncQuery = true) : base(nameof(MomCryptoBrokerage))
    {
        _tradingAction = new ActionBlock<TradingAction>(ProcessTradingAction);
        _algorithm = algorithm;

        _optionChainProvider = new MomCryptoOptionChainProvider(this);
        _futureChainProvider = new MomCryptoFutureChainProvider(this);
        _cryptoChainProvider = new MomCryptoChainProvider(this);

        if (_algorithm != null)
        {
            _algorithm.SetOptionChainProvider(_optionChainProvider);
            _algorithm.SetFutureChainProvider(_futureChainProvider);
            _algorithm.SetCryptoChainProvider(_cryptoChainProvider);
        }

        _idGen = new TickIdGen();
        _tradeUser = new MomCryptoUser { ServerAddress = tradeServer, UserId = tradeUser, Password = tradePassword };
        _mdUser = new MomCryptoUser { ServerAddress = mdServer, UserId = mdUser, Password = mdPassword };

        var tradeLog = LogManager.GetLogger("momCryptoTradeLog");
        _mdLog = LogManager.GetLogger("momCryptoMdLog");

        _cancelPendingList = new HashSet<long>();
        _orderData = new ConcurrentDictionary<long, MomCryptoOrderData>(2, MaxOrdersToKeep);
        _openOrders = new ConcurrentDictionary<long, MomCryptoOrderData>(2, MaxOrdersToKeep);
        _symbols = new ConcurrentDictionary<string, Symbol>();
        _instruments = new ConcurrentDictionary<string, MomInstrument>();
        _delisting = new ConcurrentDictionary<string, MomInstrument>();
        _subscribeSymbol = new ConcurrentDictionary<string, SubscribeData>();
        _underlying = new ConcurrentDictionary<Symbol, Symbol>();
        _symbolsMapper = new ConcurrentDictionary<string, string>();

        _qryExchangeAccountData = new List<MomFundAccount>();
        _qryExchangeOrderData = new List<MomFundOrder>();
        _qryExchangePositionData = new List<MomFundPosition>();

        _traderApi = new MomTraderApi(_tradeUser.ServerAddress, tradeLog, false);
        _traderApi.OnResponse += response => _tradingAction.Post(new TradingAction(response));
        _traderApi.OnConnected += OnTradeConnected;
        _traderApi.OnDisconnected += OnTradeDisconnected;
        _traderApi.OnRspUserLogin += OnTradeRspUserLogin;
        _traderApi.OnRspError += OnTradeRspError;
        _traderApi.InstrumentReady += OnInstrumentReady;
        _traderApi.InstrumentExpired += OnInstrumentExpired;
        _traderApi.InstrumentListed += OnInstrumentListed;
        //_traderApi.ReturnAccount
        TradingConnected = false;
        InstrumentIsReady = false;

        _marketApi = new MomMarketDataApi(_mdUser.ServerAddress, _mdLog, false);
        _marketApi.OnConnected += OnMarketConnected;
        _marketApi.OnDisconnected += OnMarketDisconnected;
        _marketApi.OnRspUserLogin += OnMarketRspUserLogin;
        _marketApi.OnRspError += OnMarketRspError;
        _marketApi.ReturnData += OnDepthMarketData;
        _marketApi.RspSubscribe += OnRspSubscribe;
        _marketApi.RspUnsubscribe += OnRspUnsubscribe;
        MarketConnected = false;

        _historyDataApi = new MomHistoryDataApi(historyServer, _mdLog);

        //_dingding = new DingDingUtility();

        _checkTimer = new System.Timers.Timer
        {
            Interval = TimeSpan.FromSeconds(5).TotalMilliseconds,
            Enabled = false,
        };

        _checkTimer.Elapsed += (_, _) =>
        {
            CheckOrderAndTrade();
            CheckInstrumentUpdate();
        };

        _enableSyncQuery = enableSyncQuery;
        //_symbolProperties = ReadSymbolProperties();
        UseSyncData = true;
    }

    private void NotifyException(string msg)
    {
        _algorithm!.Notify.MomDingDing($"{_algorithm.GetType().Name}_{_tradeUser.UserId},{msg}", "ActionRequired");
    }

    private void OnInstrumentListed(MomInstrument instrument, bool isLast)
    {
        if (!_instruments.ContainsKey(instrument.Symbol))
        {
            _instruments.TryAdd(instrument.Symbol, instrument);
            AddInstrument(instrument);
            if (_symbols.TryGetValue(instrument.Symbol, out var symbol))
            {
                _listingSymbols.Add(symbol);
            }
        }

        if (isLast)
        {
            if (_listingSymbols.Count > 0)
            {
                var list = _listingSymbols.ToList();
                _listingSymbols.Clear();
                Task.Run(() => _algorithm.OnSymbolListed(list));
            }
        }
    }

    private void OnInstrumentExpired(MomInstrument instrument, bool isLast)
    {
        if (!_symbols.TryGetValue(instrument.Symbol, out var symbol))
            return;

        if (!_delisting.ContainsKey(instrument.Symbol) && instrument.GetExpiredDateTime() <= DateTime.UtcNow)
        {
            _delisting.TryAdd(instrument.Symbol, instrument);
            _delistingSymbols.Add(symbol);
        }

        if (isLast)
        {
            if (_delistingSymbols.Count > 0)
            {
                var list = _delistingSymbols.ToList();
                _delistingSymbols.Clear();
                Task.Run(() => _algorithm.OnSymbolDelisted(list));
            }
        }
    }

    private void CheckInstrumentUpdate()
    {
        if (_lastUpdateInstrumentTime == DateTime.MaxValue)
        {
            _lastUpdateInstrumentTime = DateTime.Now;
        }

        var next = _scheduleInstrument.GetNextOccurrence(_lastUpdateInstrumentTime);
        if (DateTime.Now >= next)
        {
            _lastUpdateInstrumentTime = next;
            _traderApi.QueryInstrument();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public bool UseSyncData { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="flag"></param>
    public void SetInitQuery(bool flag)
    {
        _initQueryTrade = false;
        _initQueryOrder = false;
        _initQueryPosition = false;
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
                    OnRspQryAccount(response.Data.AsAccount, response.RspInfo, response.Last);
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
                case MomMessageType.RspCashJournal:
                    OnRspCashJournal(response.Data.AsCashJournal!, response.RspInfo);
                    return;

            }
        }
        catch (Exception ex)
        {
            Log.Error($"{ex.Message}");
        }
    }

    private void OnRspCashJournal(MomCashJournal journal, MomRspInfo? rspInfo)
    {
        var type = journal.CashJournalType == MomCashJournalTypeType.MainToUsdtFuture
            ? UserTransferType.Spot2UmFuture
            : UserTransferType.UmFuture2Spot;
        if (rspInfo == null || ErrorHelper.IsOk(rspInfo))
        {
            _algorithm?.OnTransferCompleted(journal.Amount, type, journal.CurrencyType);
        }
        else
        {
            _algorithm?.OnTransferFailed(journal.Amount, type, journal.CurrencyType, rspInfo.ErrorMsg);
        }
    }

    private void UpdateBrokerageAccountInfo(AccountField? account)
    {
        if (account == null)
        {
            return;
        }

        var info = new BrokerageAccountInfo
        {
            DateTime = DateTime.UtcNow,
            IsFundAccount = account is MomFundAccount,
            AccountId = account.AccountId,
            AccountType = account.AccountType,
            Currency = account.CurrencyType,
            Available = account.Available,
            CustomData = account.CustomData == null
                ? null
                : JsonConvert.DeserializeObject<Dictionary<string, string>>(account.CustomData)
        };
        var id = $"{info.AccountType}-{info.Currency}".ToLower();
        info.Id = info.IsFundAccount ? $"fund-{id}" : id;
        _accountMap[info.Id] = info;
        _algorithm!.BrokerageAccountMap = new Dictionary<string, BrokerageAccountInfo>(_accountMap);
    }

    public void OnReturnFundAccount(MomFundAccount fundAccount)
    {
        UpdateBrokerageAccountInfo(fundAccount);
    }

    private void OnReturnAccount(MomAccount account)
    {
        if (account.UserId != _tradeUser.UserId)
        {
            return;
        }

        UpdateBrokerageAccountInfo(account);

        _momAccounts.AddOrUpdate(account);
        //var balance = AccountList.GetCashBalance(account);
        //OnAccountChanged(new AccountEvent(balance.Currency, balance.Amount));
    }

    private void OnReturnPosition(MomPosition position)
    {
        _momPosition.AddOrUpdate(position.PositionId, position);

        CompareHolding(position);
    }

    private void CompareHolding(MomPosition position)
    {
        if (!_symbols.TryGetValue(position.InstrumentId, out var symbol))
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},CompareHolding 合约未找到,{position.InstrumentId}");
            return;
        }

        var security = _algorithm.Securities.Where(x => x.Key == symbol).Select(x => x.Value).FirstOrDefault();
        if (security != null)
        {
            var multi = 1;
            if (_lastMomTrade.Direction == MomDirectionType.Buy)
            {
                multi = 1;
            }
            else
            {
                multi = -1;
            }

            if (position.InstrumentId == _lastMomTrade.InstrumentId)
            {
                if (security.Holdings.AbsoluteQuantity != position.GetPosition() &&
                    security.Holdings.Quantity + multi * _lastMomTrade.GetVolume() != position.GetPosition())
                {
                    Log.Error($"Compare Holding: qc:{security.Holdings.Quantity} last trade {_lastMomTrade.GetVolume()} mom:{position.GetPosition()}");
                }
            }
            else
            {
                //说明最后一个trade是查询回来的？
                Log.Trace($"last position:{position.InstrumentId} {position.PositionId} != last trade:{_lastMomTrade.TradeId}");
            }
        }
    }

    /// <summary>
    /// Specifies whether the brokerage will instantly update account balances
    /// </summary>
    public override bool AccountInstantlyUpdated => true;

    /// <summary>
    /// 是否出错
    /// </summary>
    /// <param name="rspInfo"></param>
    /// <returns></returns>
    private static bool IsError(MomRspInfo? rspInfo)
    {
        if (rspInfo != null && rspInfo.ErrorID != 0)
        {
            Log.Error($"MomBrokerage Error:{rspInfo.ErrorID},{rspInfo.ErrorMsg}");
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
            _subscribeSymbol.Clear();
            _underlying.Clear();
            _optionChainProvider.Clear();
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public void ClearOrderData()
    {
        lock (_locker)
        {
            _orderData.Clear();
        }
    }

    /// <summary>
    /// 支持的证券类型,根据实现更新
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public static bool SupportSymbol(Symbol symbol)
    {
        if (symbol.Value.ToLower().IndexOf("universe", StringComparison.Ordinal) != -1)
            return false;

        var market = symbol.ID.Market;
        return market is Market.Deribit or Market.Binance or Market.FTX or Market.MEXC or Market.DYDX;
    }

    /// <summary>
    /// 交易连接成功回调
    /// </summary>
    private void OnTradeConnected()
    {
        _traderApi.Login(_tradeUser.UserId, _tradeUser.Password);
        TradingConnected = true;
        _tsConnectEvent.Set();
        var msg = $"{nameof(MomCryptoBrokerage)},{_tradeUser.UserId},交易连接成功";
        Log.Trace(msg);
        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Reconnect, 0, msg));
    }

    /// <summary>
    /// 交易连接断开回调
    /// </summary>
    /// <param name="active"></param>
    private void OnTradeDisconnected(bool active)
    {
        if (!TradingConnected)
        {
            return;
        }

        TradingConnected = false;
        InstrumentIsReady = false;
        if (_enableSyncQuery)
        {
            _checkTimer.Stop();
        }

        var msg = $"{nameof(MomCryptoBrokerage)},{_mdUser.UserId},交易连接中断({active})";
        Log.Trace(msg);
        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Disconnect, -1, msg));

        if (!active && !_exitToken.IsCancellationRequested)
        {
            msg = $"{nameof(MomCryptoBrokerage)},交易连接中断,稍后尝试重新连接.";
            Log.Trace(msg);
            msg = "交易连接中断,稍后尝试重新连接.";
            NotifyException(msg);
            Task.Delay(TimeSpan.FromSeconds(5), _exitToken.Token).ContinueWith(_ =>
            {
                TraderConnect();
            });
        }
        else
        {
            _tsConnectEvent.Set();
        }
    }

    /// <summary>
    /// 交易错误回调
    /// </summary>
    /// <param name="rspInfo"></param>
    public void OnTradeRspError(MomRspInfo rspInfo)
    {
        if (rspInfo == null)
            return;

        Log.Error($"{nameof(MomCryptoBrokerage)},{_tradeUser.UserId},{rspInfo.ErrorID},{rspInfo.ErrorMsg}");
    }

    /// <summary>
    /// 交易登录成功回调
    /// </summary>
    /// <param name="userLogin"></param>
    /// <param name="rspInfo"></param>
    public void OnTradeRspUserLogin(MomRspUserLogin userLogin, MomRspInfo rspInfo)
    {
        if (IsError(rspInfo))
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnTradeRspUserLogin)},{rspInfo.ErrorMsg}");
            TraderDisconnect();
            return;
        }

        if (_enableSyncQuery)
        {
            _checkTimer.Start();
        }

        Log.Trace($"{nameof(MomCryptoBrokerage)},{_tradeUser.UserId},交易登陆成功");
        _traderApi.QueryInstrument();
        //_lastUpdateInstrumentTime = DateTime.Now;
    }

    /// <summary>
    /// 下单回调  只有错误才能被调用
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
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnRspInputOrder)},订单未找到,ref:{inputOrder.OrderRef}");
            return;
        }

        if (orderData?.Order == null)
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnRspInputOrder)},策略订单未找到,ref:{inputOrder.OrderRef}");
            return;
        }

        Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnRspInputOrder)},id:{orderData.Order.Id},ref:{inputOrder.OrderRef},{rspInfo.ErrorMsg}");

        if (orderData.Order.Status == OrderStatus.Invalid)
        {
            Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(OnRspInputOrder)},策略订单已经无效,id:{orderData.Order.Id},ref:{inputOrder.OrderRef}");
            return;
        }

        var orderEvent = new OrderEvent(
            orderData.Order,
            GetTradingTime(orderData.Order.Symbol).DateTime,
            OrderFee.Zero,
            $"{rspInfo.ErrorMsg}")
        {
            Status = OrderStatus.Invalid
        };

        OnOrderEvent(orderEvent);
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
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnRspQryOrder)},{rspInfo.ErrorMsg}");
            _queryOrderEvent.Set();
            return;
        }

        if (momOrder != null)
        {
            Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(OnRspQryOrder)},ref:{momOrder.OrderRef},{ConstantHelper.GetName<MomOrderStatusType>(momOrder.OrderStatus)}");
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
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnRspQryTrade)},{rspInfo.ErrorMsg}");
            _queryTradeEvent.Set();
            return;
        }

        if (momTrade != null)
        {
            Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(OnRspQryTrade)},tid:{momTrade.TradeId}");

            try
            {
                _lastTradeId = Math.Max(_lastTradeId, momTrade.TradeLocalId);
                OnReturnTrade(momTrade, 0);
            }
            catch (Exception ex)
            {
                Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnRspQryTrade)},{ex.Message}");
            }
        }

        if (isLast)
        {
            Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(OnRspQryTrade)},lastTradeId:{_lastTradeId}");
            _queryTradeEvent.Set();
        }
    }

    /// <summary>
    /// 查询mom持仓
    /// </summary>
    /// <param name="momPosition"></param>
    /// <param name="rspInfo"></param>
    /// <param name="isLast"></param>
    private void OnRspQryPosition(MomPosition momPosition, MomRspInfo rspInfo, bool isLast)
    {
        if (IsError(rspInfo))
        {
            _queryPositionEvent.Set();
            return;
        }

        if (momPosition != null && momPosition.GetPosition() != 0)
        {
            _momPosition.AddOrUpdate(momPosition.PositionId, momPosition);
        }

        if (isLast)
        {
            _queryPositionEvent.Set();
            _querySyncDataEvent.Set();
        }
    }

    /// <summary>
    /// 撤单只有出错才给响应
    /// </summary>
    /// <param name="momAction"></param>
    /// <param name="rspInfo"></param>
    private void OnRspOrderAction(MomInputOrderAction momAction, MomRspInfo rspInfo)
    {
        if (!IsError(rspInfo))
            return;

        if (momAction == null)
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnRspOrderAction)},MomInputOrderAction为空");
            return;
        }

        if (!_orderData.TryGetValue(momAction.OrderRef, out var orderData))
        {
            Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(OnRspOrderAction)},撤单目标未找到,ref:{momAction.OrderRef}");
            return;
        }

        if (orderData.Order == null)
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnRspOrderAction)},策略订单未找到,ref:{momAction.OrderRef}");
            return;
        }

        Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(OnRspOrderAction)},撤单失败,id:{orderData.Order.Id},ref:{momAction.OrderRef},{rspInfo.ErrorMsg}");
    }


    private void OnRspExchangeAccount(MomFundAccount account, MomRspInfo rspInfo, bool isLast)
    {
        if (IsError(rspInfo))
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnRspExchangeAccount)},{rspInfo.ErrorMsg}");
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
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnRspExchangeOrder)},{rspInfo.ErrorMsg}");
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
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnRspExchangePosition)},{rspInfo.ErrorMsg}");
            _queryExchangeOrderEvent.Set();
            return;
        }

        _qryExchangePositionData.Add(position);

        if (isLast)
        {
            _queryExchangePositionEvent.Set();
        }
    }
    /// <summary>
    /// 查询mom资金账户
    /// </summary>
    /// <param name="account"></param>
    /// <param name="rspInfo"></param>
    /// <param name="isLast"></param>
    private void OnRspQryAccount(MomAccount? account, MomRspInfo rspInfo, bool isLast)
    {
        if (IsError(rspInfo))
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnRspQryAccount)},{rspInfo.ErrorMsg}");
            _queryAccountEvent.Set();
            return;
        }

        UpdateBrokerageAccountInfo(account!);

        if (account != null && account.AccountType != "All")
        {
            _momAccounts.AddOrUpdate(account);
        }

        if (isLast)
        {
            _queryAccountEvent.Set();
        }
    }

    private Order CreateOrder(OrderField field)
    {
        Order order;
        switch (field.OrderPriceType)
        {
            case MomOrderPriceTypeType.AnyPrice:
                order = new MarketOrder();
                break;
            case MomOrderPriceTypeType.StopLimit:
                order = new StopLimitOrder
                {
                    LimitPrice = field.LimitPrice,
                    StopPrice = field.StopPrice
                };
                break;
            case MomOrderPriceTypeType.StopMarket:
                order = new StopMarketOrder
                {
                    StopPrice = field.StopPrice
                };
                break;
            default:
                order = new LimitOrder
                {
                    LimitPrice = field.LimitPrice
                };
                break;
        }

        order.Time = DateTime.UtcNow;
        order.Id = field.OrderRef;
        order.Status = GetOrderStatus(field.OrderStatus);
        order.BrokerId.Add(field.OrderRef.ToString());
        order.Quantity = field.Direction == MomDirectionType.Buy
            ? field.VolumeTotalOriginal
            : -1 * field.VolumeTotalOriginal;

        if (!_symbols.TryGetValue(field.InstrumentId, out var symbol))
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},合约未找到,{field.InstrumentId}");
        }

        order.Symbol = symbol;
        if (symbol != null && !SupportOffset.IsSupportOffset(symbol))
        {
            order.Offset = OrderOffset.None;
        }

        return order;
    }

    /// <summary>
    /// 推送报单
    /// </summary>
    /// <param name="momOrder"></param>
    /// <param name="index"></param>
    public void OnReturnOrder(MomOrder momOrder, uint index)
    {
        Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(OnReturnOrder)},{index},ref:{momOrder.OrderRef},{ConstantHelper.GetName<MomOrderStatusType>(momOrder.OrderStatus)}");
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

                orderData = new MomCryptoOrderData
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

                var newStatus = GetOrderStatus(momOrder.OrderStatus);
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

                if (TryParseDate(momOrder.InsertDate, momOrder.UpdateTime, out var lastUpdateTime))
                {
                    orderData.Order.LastUpdateTime = lastUpdateTime;
                }

                if (orderData.Order.Status == OrderStatus.Canceled)
                {
                    if (TryParseDate(momOrder.InsertDate, momOrder.CancelTime, out var cancelTime))
                    {
                        orderData.Order.CanceledTime = cancelTime;
                    }
                }

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
                        GetTradingTime(orderData.Order.Symbol).DateTime,
                        OrderFee.Zero,
                        $"{nameof(MomCryptoBrokerage)},ref:{momOrder.OrderRef}")
                    {
                        Status = orderData.Order.Status,
                    };

                    OnOrderEvent(orderEvent);
                    OnOrderOccurred(new[] { new OrderRecord { order = orderData.Order } });
                }
            }
            else
            {
                orderData.Order = CreateOrder(momOrder);
                Log.Trace($"{nameof(MomCryptoBrokerage)},{_tradeUser.UserId},导入未完成订单,id:{orderData.Order.Id},ref:{momOrder.OrderRef}");
            }
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(ProcessOrder)},{e}");
        }
    }

    /// <summary>
    /// 推送成交
    /// </summary>
    /// <param name="momTrade"></param>
    /// <param name="index"></param>
    public void OnReturnTrade(MomTrade momTrade, uint index)
    {
        if (index != 0)
        {
            Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(OnReturnTrade)},{index},tid:{momTrade.TradeId},ref:{momTrade.OrderRef},{momTrade.InstrumentId},volume:{momTrade.GetVolume()},price:{momTrade.Price}");
        }

        try
        {
            var tradeIdKey = $"{momTrade.OrderRef}_{momTrade.TradeId}";
            if (_momTradeIds.ContainsKey(tradeIdKey))
            {
                Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(OnReturnTrade)},重复的成交,tid:{momTrade.TradeId},ref:{momTrade.OrderRef}");
                return;
            }

            var initQuery = UseSyncData ? _initDataSyncing : _initQueryTrade;
            // 说明是查询回来的成交 说明推送遗漏
            if (index == 0 && !initQuery)
            {
                Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(OnReturnTrade)},查询到未推送成交,tid:{momTrade.TradeId},ref:{momTrade.OrderRef},{momTrade.InstrumentId},volume:{momTrade.GetVolume()},price:{momTrade.Price}");
                //_dingding.SendMessage($"查询到未推送成交,tid:{momTrade.TradeId},ref:{momTrade.OrderRef}");
                _historyDataApi.SendDingMsg($"查询到未推送成交,{_mdUser.UserId},tid:{momTrade.TradeId},ref:{momTrade.OrderRef},{momTrade.InstrumentId},volume:{momTrade.GetVolume()},price:{momTrade.Price}");
            }

            if (!_orderData.TryGetValue(momTrade.OrderRef, out var orderData))
            {
                Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(OnReturnTrade)},成交未找到订单,ref:{momTrade.OrderRef}");
                return;
            }

            _momTradeIds.TryAdd(tradeIdKey, 0);
            orderData.Trades.Add(momTrade);

            var volume = orderData.InputOrder?.VolumeTotalOriginal ?? (orderData.Order?.Quantity ?? (orderData.MomOrder?.VolumeTotalOriginal ?? 0));
            var partiallyFilled = orderData.GetTradeVolume() < volume;

            if (orderData.Order == null)
            {
                Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(OnReturnTrade)},成交未找到对应QC订单,ref:{momTrade.OrderRef}");
                return;
            }

            //初始化查询只添加不发订单状态
            if (index == 0 && initQuery)
            {
                return;
            }

            if (index != 0)
            {
                _lastMomTrade = momTrade;
            }

            var multi = 1;
            if (orderData.Order.Direction == OrderDirection.Sell)
            {
                multi = -1;
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
            var msg = $"{nameof(MomCryptoBrokerage)},tid:{momTrade.TradeId},ref:{momTrade.OrderRef}";
            if (index > 0)
            {
                msg += $",index:{index}";
            }

            orderData.Order!.status = orderStatus != OrderStatus.Canceled
                ? orderStatus
                : OrderStatus.PartiallyFilled;

            var orderEvent = new OrderEvent(
                orderData.Order,
                GetTradingTime(orderData.Order.Symbol).DateTime,
                GetOrderFee(momTrade, orderData.Order.Symbol),
                msg)
            {
                FillPrice = momTrade.Price,
                FillQuantity = momTrade.GetVolume() * multi
            };
            OnOrderEvent(orderEvent);

            if (orderStatus == OrderStatus.Canceled)
            {
                orderData.Order.status = orderStatus;
                OnOrderEvent(new OrderEvent(
                    orderData.Order,
                    GetTradingTime(orderData.Order.Symbol).DateTime,
                    OrderFee.Zero));
            }

            OnOrderOccurred(new[] { new OrderRecord { order = orderData.Order } });
            var record = ConvertToTradeRecord(momTrade);
            record.Tag = orderData.Order.Tag;
            OnTradeOccurred(new[] { record });
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnReturnTrade)},{e}");
        }
    }

    private OrderFee GetOrderFee(TradeField momTrade, Symbol symbol)
    {
        var currency = "";

        if (!_instruments.TryGetValue(momTrade.InstrumentId, out var inst))
        {
            Log.Error($"未找到 {momTrade.InstrumentId} MomInstrument ");
            return OrderFee.Zero;
        }

        if (symbol.ID.Market == Market.Binance)
        {
            currency = momTrade.CommissionAsset;
        }
        else if (symbol.ID.Market == Market.Deribit)
        {
            currency = inst.BaseCurrency;
        }
        else if (symbol.ID.Market == Market.FTX)
        {
            currency = "USD";
        }

        return new OrderFee(new CashAmount(momTrade.Commission, currency));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="trade"></param>
    /// <returns></returns>
    private TradeRecord ConvertToTradeRecord(TradeField trade)
    {
        var item = new TradeRecord();

        if (!_symbols.TryGetValue(trade.InstrumentId, out var symbol))
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(ConvertToTradeRecord)},没有找到合约,{trade.InstrumentId}");
        }
        item.Symbol = symbol;
        item.TradeId = trade.TradeId;
        item.OrderId = trade.OrderSysId;
        item.Status = OrderStatus.Filled;
        item.Direction = GetDirection(trade.Direction);

        if (symbol != null && !SupportOffset.IsSupportOffset(symbol))
        {
            item.Offset = OrderOffset.None;
        }

        if (TryParseDate(trade.TradeDate, trade.TradeTime, out var time))
        {
            item.Time = time;
        }
        else
        {
            Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(ConvertToTradeRecord)},tid:{trade.TradeId},{trade.TradeDate},{trade.TradeTime}");
            item.Time = DateTime.MinValue;
        }
        item.Amount = trade.GetVolume();
        item.Price = trade.Price;
        return item;
    }

    private void UpdateInstrument(MomInstrument inst)
    {
        if (!_symbols.TryGetValue(inst.Symbol, out var symbol))
            return;

        if (symbol.SecurityType is SecurityType.Future or SecurityType.Crypto)
        {
            var lotSize = inst.MinLimitOrderVolume > inst.MinMarketOrderVolume
                ? inst.MinLimitOrderVolume
                : inst.MinMarketOrderVolume;
            if (lotSize <= 0)
            {
                return;
            }

            //最小下单市值
            var minNotional = inst.VolumeMultiple;
            symbol.SymbolProperties = new SymbolProperties(string.Empty, inst.QuoteCurrency, 1, inst.PriceTick, lotSize, minNotional);
        }
        else if (symbol.SecurityType == SecurityType.Option)
        {
            var market = inst.Market ?? inst.Exchange;
            var quoteCurrency = inst.QuoteCurrency;
            var lotSize = inst.MinLimitOrderVolume > inst.MinMarketOrderVolume
                ? inst.MinLimitOrderVolume
                : inst.MinMarketOrderVolume;
            if (lotSize <= 0)
            {
                return;
            }

            //最小下单市值
            var minNotional = inst.VolumeMultiple;
            if (market == Market.Deribit)
            {
                quoteCurrency = inst.BaseCurrency;
            }
            symbol.SymbolProperties = new SymbolProperties(string.Empty, quoteCurrency, inst.VolumeMultiple, (decimal)inst.PriceTick, lotSize, minNotional);
        }

        var symbolKey = MomCryptoSymbolMapper.GetSymbolKey(symbol);
        if (_symbolsMapper.TryGetValue(symbolKey, out var mappedSymbol))
        {
            if (_subscribeSymbol.TryGetValue(mappedSymbol, out _))
            {
                var p = _subscribeSymbol[mappedSymbol].SubscribeSymbol.SymbolProperties;
                if (p.ContractMultiplier != symbol.SymbolProperties.ContractMultiplier
                    || p.MinNotional != symbol.SymbolProperties.MinNotional
                    || p.LotSize != symbol.SymbolProperties.LotSize)
                {
                    _subscribeSymbol[mappedSymbol].SubscribeSymbol.SymbolProperties = symbol.SymbolProperties;
                }
            }
        }
    }

    private static string GetSymbolKey(MomInstrument instrument, Symbol symbol)
    {
        if (instrument.Market == Market.Deribit && symbol.SecurityType == SecurityType.Crypto)
        {
            return symbol.Value;
        }

        return instrument.Symbol;
    }

    private void AddInstrument(MomInstrument instrument)
    {
        Log.Trace($"{nameof(MomCryptoBrokerage)},收到合约,{instrument.InstrumentName}");

        if (_symbols.ContainsKey(instrument.Symbol))
        {
            Log.Trace($"{nameof(MomCryptoBrokerage)},合约已经存在,{instrument.Symbol}");
            return;
        }

        try
        {
            var symbol = MomCryptoSymbolMapper.CreateSymbol(instrument);
            SymbolCache.Set(GetSymbolKey(instrument, symbol), symbol);
            _symbols.TryAdd(instrument.Symbol, symbol);
            var key = MomCryptoSymbolMapper.GetSymbolKey(symbol);
            if (_symbolsMapper.ContainsKey(key))
            {
                Log.Trace(key);
            }

            _symbolsMapper.TryAdd(key, instrument.Symbol);
            if (symbol.SecurityType == SecurityType.Option)
            {
                if (!string.IsNullOrEmpty(instrument.UnderlyingSymbol))
                {
                    _optionChainProvider.Add(instrument.UnderlyingSymbol!, symbol);
                }
                else
                {
                    throw new ArgumentException("Lack of definition of underlying contract in option contract");
                }
            }

            if (symbol.SecurityType == SecurityType.Future)
            {
                if (symbol.ID.Date < SecurityIdentifier.PerpetualExpiration)
                {
                    _futureChainProvider.Add(
                        instrument.Market == "deribit" ? instrument.BaseCurrency : instrument.UnderlyingSymbol,
                        symbol);
                }
                else
                {
                    _futureChainProvider.Add(symbol.Value, symbol);
                }
            }

            if (symbol.SecurityType == SecurityType.Crypto)
            {
                _cryptoChainProvider.Add(symbol);
                SymbolPropertiesDatabase.FromDataFolder().Add(symbol);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"合约解析错误:{instrument.ExchangeSymbol},{ex.Message}");
        }
    }

    /// <summary>
    /// 合约查询回调
    /// </summary>
    /// <param name="instrument"></param>
    /// <param name="isLast"></param>
    private void OnInstrumentReady(MomInstrument? instrument, bool isLast)
    {
        if (instrument == null || instrument.Symbol.Length == 0)
            return;

        try
        {
            if (InstrumentIsReady)
            {
                UpdateInstrument(instrument);
                if (isLast)
                {
                    Log.Trace($"{nameof(MomCryptoBrokerage)},完成合约更新");
                }
                return;
            }

            if (!_instruments.ContainsKey(instrument.Symbol))
            {
                _instruments.TryAdd(instrument.Symbol, instrument);
            }

            //if (instrument.GetExpiredDateTime() <= DateTime.UtcNow)
            if (instrument.IsExpired())
            {
                _delisting.TryAdd(instrument.Symbol, instrument);
                return;
            }

            AddInstrument(instrument);

            if (isLast)
            {
                foreach (var c in _optionChainProvider.OptionChain)
                {
                    Log.Trace($"{nameof(MomCryptoBrokerage)},获取期权链,{c.Key},期权个数:{c.Value.Count}");
                }

                InstrumentIsReady = true;
                _futureChainProvider.SetReady();
                _optionChainProvider.SetReady();
                _cryptoChainProvider.SetReady();

                Log.Trace($"{nameof(MomCryptoBrokerage)},合约接收完成, 合约数量:{_instruments.Count}");
            }
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(OnInstrumentReady)},{e}");
        }
    }

    /// <summary>
    /// 行情登录回调
    /// </summary>
    /// <param name="userLogin"></param>
    /// <param name="rspInfo"></param>
    public void OnMarketRspUserLogin(MomRspUserLogin userLogin, MomRspInfo rspInfo)
    {
        if (IsError(rspInfo))
        {
            MarketDisconnect();
            return;
        }

        Log.Trace($"{nameof(MomCryptoBrokerage)},行情接口登录成功");
        ResetSubscribe();
    }

    /// <summary>
    /// 行情连接成功回调
    /// </summary>
    public void OnMarketConnected()
    {
        _marketApi.Login(new MomReqUserLogin { UserID = _mdUser.UserId });
        MarketConnected = true;
        _mdConnectEvent.Set();
        var msg = $"{nameof(MomCryptoBrokerage)},行情连接成功";
        Log.Trace(msg);
        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Reconnect, 0, msg));
    }

    /// <summary>
    /// 行情断开连接回调
    /// </summary>
    /// <param name="active"></param>
    public void OnMarketDisconnected(bool active)
    {
        if (!MarketConnected)
        {
            return;
        }

        MarketConnected = false;

        var msg = $"{nameof(MomCryptoBrokerage)},{_mdUser.UserId},行情连接中断({active})";
        Log.Trace(msg);
        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Disconnect, -1, msg));

        if (!active)
        {
            msg = $"{nameof(MomCryptoBrokerage)},行情连接意外中断,稍后尝试重新连接.";
            Log.Trace(msg);
            msg = "行情连接意外中断,稍后尝试重新连接.";
            NotifyException(msg);
            Task.Delay(TimeSpan.FromSeconds(5), _exitToken.Token).ContinueWith(_ =>
            {
                MarketConnect();
            });
        }
        else
        {
            _mdConnectEvent.Set();
        }
    }

    /// <summary>
    /// 行情错误回调
    /// </summary>
    /// <param name="rspInfo"></param>
    private void OnMarketRspError(MomRspInfo? rspInfo)
    {
        if (rspInfo == null)
            return;
        Log.Error($"{nameof(MomCryptoBrokerage)},行情接口错误,{rspInfo.ErrorMsg}");
    }

    /// <summary>
    /// 行情推送
    /// </summary>
    /// <param name="data"></param>
    private void OnDepthMarketData(ref MomDepthMarketData data)
    {
        _mdLog.Debug($"OnData: {data.ExchangeSymbol}, {data.UpdateTime}, last:{data.LastPrice:F3}, {data.Volume}, bid:{data.BidPrice1:F3}, {data.BidVolume1}, ask:{data.AskPrice1:F3}, {data.AskVolume1}");

        if (!_subscribeSymbol.TryGetValue(ConvertToSymbol(data.Symbol), out var subscribeData))
            return;

        var tick = ConvertTick(data, subscribeData.SubscribeSymbol);
        if (_algorithm!.SimulationMode)
        {
            _exchangeTimeManager.SetExchangeTime(tick.Symbol, tick.Time);
        }
        if (tick.Price < 0)
        {
            Log.Error($"{data.Symbol} 价格小于零,{data.GetDateTime()}");
            Log.Error(JsonConvert.SerializeObject(data, Formatting.Indented));
            return;
        }

        //lock (_ticks)
        try
        {
            _ticks.Enqueue(tick);
            _lastTicks[tick.Symbol.Value] = tick;
        }
        catch (Exception e)
        {
            Log.Error(e);
        }
        //Log.Trace($"{data.UpdateTime},{data.Symbol},{data.LastPrice}");
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    internal static string ConvertToMomSymbol(string symbol)
    {
        return symbol;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    internal static string ConvertToSymbol(string symbol)
    {
        return symbol;
    }

    internal static string ConvertToUnderlyingSymbol(string market, string underlying)
    {
        var symbol = underlying;
        if (market == "deribit" && !string.IsNullOrEmpty(underlying))
        {
            symbol = underlying.Replace("_", "").ToUpper();
        }
        return symbol;
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
            Log.Trace($"{nameof(MomCryptoBrokerage)},{inst.InstrumentId},{rspInfo.ErrorMsg}");
            return;
        }

        var symbol = ConvertToSymbol(inst.InstrumentId);
        if (_subscribeSymbol.TryGetValue(symbol, out var subscribeData))
        {
            subscribeData.SubscribeStatus = 1;
        }
        Log.Trace($"{nameof(MomCryptoBrokerage)},行情订阅成功,{symbol}");
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

        var symbol = ConvertToSymbol(inst.InstrumentId);
        _subscribeSymbol.TryRemove(symbol, out var subscribeData);
        Log.Trace($"{nameof(MomCryptoBrokerage)},行情订阅取消成功,{symbol}");
    }

    /// <summary>
    /// 交易是否连接
    /// </summary>
    /// <returns></returns>
    public bool IsTraderConnected()
    {
        Trace.Assert(TradingConnected == _traderApi.Connected);
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
        Log.Trace($"{nameof(MomCryptoBrokerage)},开始连接交易接口");

        Task.Delay(TimeSpan.FromSeconds(30), _exitToken.Token).ContinueWith(_ =>
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
                    Log.Trace($"{nameof(MomCryptoBrokerage)},连接交易超时");
                }

                if (!InstrumentIsReady)
                {
                    Log.Trace($"{nameof(MomCryptoBrokerage)},接收合约超时");
                }

                NotifyException("交易连接超时,尝试重新连接");
                TraderDisconnect();
                TraderConnect();
            }

            if (!MarketConnected)
            {
                Log.Trace($"{nameof(MomCryptoBrokerage)},行情连接超时");
                NotifyException("行情连接超时,尝试重新连接");
                MarketDisconnect();
                MarketConnect();
            }
        });
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
    private void MarketConnect()
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
    }

    /// <summary>
    /// 行情是否连接
    /// </summary>
    /// <returns></returns>
    private bool IsMarketConnected()
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
        var subscribe = _subscribeSymbol.Keys.ToArray();
        foreach (var symbol in subscribe)
        {
            _marketApi.Subscribe(symbol);
            Log.Trace($"{nameof(MomCryptoBrokerage)},自动订阅合约,{symbol})");
        }
    }

    /// <summary>
    /// 重新连接api
    /// </summary>
    public bool ResetConnection()
    {
        Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(ResetConnection)}");
        Disconnect();
        Connect();
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
    public IEnumerable<Symbol> LookupSymbols(string lookupName, SecurityType securityType, string securityCurrency = null, string securityExchange = null)
    {
        Log.Trace($"{nameof(MomCryptoBrokerage)}.LookupSymbols(): Requesting symbol list for " + lookupName + " ...");

        var symbols = new List<Symbol>();

        if (securityType == SecurityType.Option)
        {
            var underlyingSymbol = Symbol.Create(lookupName, SecurityType.Crypto, Market.Deribit);
            symbols.AddRange(_algorithm.OptionChainProvider.GetOptionContractList(underlyingSymbol, DateTime.Today));
        }
        else if (securityType == SecurityType.Future)
        {
            var underlyingSymbol = Symbol.Create(lookupName, SecurityType.Crypto, Market.Deribit);
            symbols.AddRange(_algorithm.FutureChainProvider.GetFutureContractList(underlyingSymbol, DateTime.Today));
        }
        else
        {
            throw new ArgumentException($"{nameof(MomCryptoBrokerage)}.LookupSymbols() not support securityType:" + securityType);
        }

        Log.Trace($"{nameof(MomCryptoBrokerage)}.LookupSymbols(): Returning {0} contract(s) for {1}", symbols.Count, lookupName);

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
        var ignoreList = new List<string> { "USDTUSD", "USDT_FUSD", "BUSDUSD", "BUSD_FUSD" };
        if (ignoreList.Contains(request.Symbol.ID.Symbol) ||
            (request.Symbol.SecurityType == SecurityType.Crypto
             && request.Symbol.ID.Market == Market.Deribit))
        {
            yield break;
        }

        var qryHistoryData = new MomQryHistoryData();
        switch (request.Symbol.SecurityType)
        {
            case SecurityType.Crypto:
                qryHistoryData.Market = "crypto";
                break;
            case SecurityType.Future:
                qryHistoryData.Market = "future";
                break;
            default:
                Log.Error($"{nameof(MomCryptoBrokerage)}:GetHistory(),不支持的证券类型:{request.Symbol.SecurityType}");
                yield break;
        }

        switch (request.Resolution)
        {
            case Resolution.Minute:
                qryHistoryData.DataType = (int)MomHistoryDataType.OneMinute;
                break;
            case Resolution.Hour:
                qryHistoryData.DataType = (int)MomHistoryDataType.OneHour;
                break;
            case Resolution.Daily:
                qryHistoryData.DataType = (int)MomHistoryDataType.OneDay;
                if (request.EndTimeUtc.Date != DateTime.UtcNow.Date)
                {
                    request.EndTimeUtc = DateTime.UtcNow.Date;
                }
                break;
        }
        qryHistoryData.InstrumentId = request.Symbol.ID.Symbol;
        if (request.Symbol.SecurityType == SecurityType.Crypto)
        {
            qryHistoryData.InstrumentId = qryHistoryData.InstrumentId.Split(new[] { '/', '_', '-' })[0].RemoveFromEnd(request.Symbol.SymbolProperties.QuoteCurrency)
                                          + "_" + request.Symbol.SymbolProperties.QuoteCurrency;
        }
        /* if (qryHistoryData.InstrumentId.EndsWith("USD"))
         {
             yield break;
         }*/

        //   11 / 6 / 2020 12:00:00 AM

        qryHistoryData.TimeStart = request.StartTimeUtc.ToString("yyyyMMddHHmmss", CultureInfo.CurrentCulture);

        qryHistoryData.TimeEnd = request.EndTimeUtc.ToString("yyyyMMddHHmmss", CultureInfo.CurrentCulture);

        string? data = _historyDataApi.QryHistoryData(qryHistoryData, request.Symbol.ID.Market);

        if (data.IndexOf("错误", StringComparison.Ordinal) >= 0)
        {
            Log.Error($"{qryHistoryData.InstrumentId} {data}");
            yield break;
        }

        var period = request.Resolution.ToTimeSpan();
        var lines = data.Split(new[] { "\n" }, StringSplitOptions.None).ToList();
        if (lines.Count > 1)
        {
            lines.RemoveAt(0);
            lines = lines.Where(x => x.Length != 0).ToList();
            var i = 0;
            foreach (var line in lines)
            {
                var csv = line.Split(',');
                var tick = new Tick();
                if (!DateTime.TryParseExact(csv[0], "yyyyMMddHHmmss", null, DateTimeStyles.None, out var openTime))
                {
                    continue;
                }

                tick.Time = openTime;
                tick.Symbol = request.Symbol;
                tick.Value = System.Convert.ToDecimal(csv[4]);
                tick.Quantity = System.Convert.ToDecimal(csv[5]);


                if (request.Symbol.SecurityType is SecurityType.Future or SecurityType.Option)
                {
                    tick.TickType = TickType.Quote;
                }
                else
                {
                    tick.TickType = TickType.Trade;
                }

                tick.BidPrice = tick.Value;
                tick.BidSize = tick.Quantity;
                tick.AskPrice = tick.Value;
                tick.AskSize = tick.Quantity;

                tick.MarkPrice = tick.Value;
                tick.SettlementPrice = tick.Value;
                yield return tick;
            }
        }
        else
        {
            yield break;
        }
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
            if (!_usdtFirstSend)
            {
                yield return tick;
                if (_underlying.ContainsKey(tick.Symbol))
                {
                    var underlyingTick = tick.Clone();
                    underlyingTick.Symbol = _underlying[tick.Symbol];
                    yield return underlyingTick;
                }
            }
            else
            {
                _usdtFirstSend = false;
            }
        }
    }

    /// <summary>
    /// Adds the specified symbols to the subscription
    /// </summary>
    /// <param name="job"></param>
    /// <param name="symbols"></param>
    public void Subscribe(LiveNodePacket job, IEnumerable<Symbol> symbols)
    {
        var subscribes = new List<string>();

        foreach (var symbol in symbols)
        {
            if (symbol.IsCanonical())
            {
                //SubscribeCanonicalSymbol(symbol);
                return;
            }

            if (!SupportSymbol(symbol))
                continue;

            _exchangeTimeManager.AddSymbol(symbol);

            lock (_locker)
            {
                var symbolKey = MomCryptoSymbolMapper.GetSymbolKey(symbol);
                var isCanonical = symbol.ID.SecurityType == SecurityType.Option && symbol.IsCanonical();
                if (isCanonical)
                {
                    symbolKey = MomCryptoSymbolMapper.GetSymbolKey(symbol.Underlying);
                }

                if (!_symbolsMapper.TryGetValue(symbolKey, out var mappedSymbol))
                {
                    continue;
                }

                if (_subscribeSymbol.TryGetValue(mappedSymbol, out var subscribeData))
                {
                    if (subscribeData.SubscribeStatus == 1)
                        continue;
                }
                else
                {
                    var key = MomCryptoSymbolMapper.GetSymbolKey(symbol);
                    if (_symbolsMapper.ContainsKey(key))
                    {
                        var instSymbol = _symbolsMapper[key];
                        if (_symbols.ContainsKey(instSymbol))
                        {
                            var p = _symbols[instSymbol].SymbolProperties;
                            if (p.ContractMultiplier != symbol.SymbolProperties.ContractMultiplier
                                || p.MinNotional != symbol.SymbolProperties.MinNotional
                                || p.LotSize != symbol.SymbolProperties.LotSize)
                            {
                                symbol.SymbolProperties = p;
                            }

                            if (symbol.SecurityType == SecurityType.Crypto
                                || symbol.IsPerpetual())
                            {
                                var security = _algorithm.Securities[symbol];
                                if (security != null)
                                {
                                    var sp = security.symbolProperties;
                                    if (!sp.Equel(p))
                                    {
                                        sp.SetProperties(p);
                                    }
                                }
                            }

                            if (symbol.SecurityType == SecurityType.Option)
                            {
                                var Option = (Option)_algorithm.Securities[symbol];
                                if (Option != null)
                                {
                                    TimeSpan settlementTime = DateTime.SpecifyKind(_instruments[instSymbol].GetExpiredDateTime(), DateTimeKind.Utc).TimeOfDay;
                                    Option.SettlementTime = settlementTime;
                                }
                                else
                                {
                                    Log.Trace($"WARNING！- The momcrypto brokerage trying to update settlement time of an option {symbol} that is not added in Algorithm.Securities!");
                                }
                            }
                        }
                    }

                    subscribeData = new SubscribeData
                    {
                        InstrumentId = mappedSymbol,
                        SubscribeSymbol = isCanonical ? symbol.Underlying : symbol,
                        SubscribeStatus = 0
                    };
                    _subscribeSymbol.TryAdd(mappedSymbol, subscribeData);
                }

                mappedSymbol = ConvertToMomSymbol(mappedSymbol);
                subscribes.Add(mappedSymbol);

                if (isCanonical)
                {
                    var underlying = symbol.Underlying;
                    _underlying.TryAdd(underlying, symbol);
                }
            }
        }

        if (!subscribes.Any())
            return;

        foreach (var symbol in subscribes.ToArray())
        {
            _marketApi.Subscribe(symbol);
            Log.Trace($"{nameof(MomCryptoBrokerage)},订阅合约,{symbol})");
        }
    }

    /// <summary>
    /// Removes the specified symbols to the subscription
    /// </summary>
    /// <param name="job"></param>
    /// <param name="symbols"></param>
    public void Unsubscribe(LiveNodePacket job, IEnumerable<Symbol> symbols)
    {
        var unsubscribes = new List<string>();

        foreach (var symbol in symbols)
        {
            if (!SupportSymbol(symbol))
                continue;

            lock (_locker)
            {
                var symbolKey = MomCryptoSymbolMapper.GetSymbolKey(symbol);
                if (!_symbolsMapper.TryGetValue(symbolKey, out var mappedSymbol))
                {
                    mappedSymbol = symbolKey;
                }

                if (_subscribeSymbol.TryGetValue(mappedSymbol, out var subscribeData))
                {
                    subscribeData.SubscribeStatus = 2;
                }

                if (symbol.ID.SecurityType == SecurityType.Option && symbol.ID.StrikePrice == 0.0m)
                {
                    _underlying.TryRemove(symbol.Underlying, out _);
                }

                mappedSymbol = ConvertToMomSymbol(mappedSymbol);
                unsubscribes.Add(mappedSymbol);
            }
        }

        if (!unsubscribes.Any())
            return;

        var strings = unsubscribes.ToArray();
        _marketApi.Unsubscribe(strings);

        Log.Trace($"{nameof(MomCryptoBrokerage)}: unsubscribe request ({string.Join(",", strings)})");
    }

    #endregion

    #region IBrokerage implementation
    /// <summary>
    /// Places a new momOrder and assigns a new broker ID to the momOrder
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public override bool PlaceOrder(Order order)
    {
        if (!IsTraderConnected())
        {
            if (!ResetConnection())
            {
                Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(PlaceOrder)},id:{order.Id}");
                return false;
            }
        }

        Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(PlaceOrder)},id:{order.Id},{order.Symbol.Value}");

        _tradingAction.Post(new TradingAction(order));

        return true;
    }

    private DateTimeOffset GetTradingTime(Symbol symbol)
    {
        return _algorithm!.SimulationMode ? _exchangeTimeManager.GetExchangeTime(symbol) : DateTimeOffset.Now;
    }

    private void InternalPlace(Order order)
    {
        try
        {
            if (_cancelPendingList.Contains(order.Id))
            {
                var cancelEvent = new OrderEvent(
                    order,
                    GetTradingTime(order.Symbol).DateTime,
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

            var orderData = new MomCryptoOrderData
            {
                OrderRef = inputOrder.OrderRef,
                InputOrder = inputOrder,
                Order = order.Clone()
            };
            orderData.Order.Status = OrderStatus.Submitted;
            _orderData.TryAdd(inputOrder.OrderRef, orderData);

            Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(InternalPlace)},id:{order.Id},ref:{inputOrder.OrderRef},{order.Symbol.Value},volume:{order.Quantity}");

            _traderApi.InputOrder(inputOrder);

            var submittedEvent = new OrderEvent(
                order,
                GetTradingTime(order.symbol).DateTime,
                OrderFee.Zero,
                $"ref:{inputOrder.OrderRef}")
            {
                Status = OrderStatus.Submitted,
            };

            OnOrderEvent(submittedEvent);
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(InternalPlace)},{e}");
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
        //inputOrder.Advanced = order.GetSlippage().NormalizeToStr();
        if (!_symbolsMapper.TryGetValue(MomCryptoSymbolMapper.GetSymbolKey(order.Symbol), out var instrumentId))
        {
            instrumentId = order.Symbol.Value;
        }

        inputOrder.InstrumentId = instrumentId;
        inputOrder.Direction = GetMomDirection(order);
        inputOrder.OrderPriceType = GetMomOrderPriceType(order);
        if (inputOrder.IsStopOrder())
        {
            if (order is StopLimitOrder stopLimitOrder)
            {
                inputOrder.StopPrice = stopLimitOrder.StopPrice;
                inputOrder.LimitPrice = stopLimitOrder.LimitPrice;
                inputOrder.StopWorkingType = ConvertToMomStopWorkingTypeType(stopLimitOrder.StopPriceTriggerType);
            }
            else
            {
                if (order is StopMarketOrder stopMarketOrder)
                {
                    inputOrder.StopPrice = stopMarketOrder.StopPrice;
                    inputOrder.LimitPrice = 0;
                    inputOrder.StopWorkingType = ConvertToMomStopWorkingTypeType(stopMarketOrder.StopPriceTriggerType);
                }
                else
                {
                    Log.Error("MomDeribitBrokerage.PlaceOrder(): 止损单报单错误");
                    //return null;
                }
            }
        }
        else
        {
            var limit = order as LimitOrder;
            inputOrder.LimitPrice = limit?.LimitPrice ?? 0;
        }

        inputOrder.TimeCondition = GetMomTimeCondition(order);
        inputOrder.VolumeTotalOriginal = Math.Abs(order.Quantity);
        inputOrder.UserId = _tradeUser.UserId;
        return inputOrder;
    }

    private bool TryGetOrderData(Order order, out MomCryptoOrderData? orderData)
    {
        orderData = null;
        if (order.BrokerId.Count == 0)
        {
            return false;
        }

        var id = long.Parse(order.BrokerId.First());
        return _orderData.TryGetValue(id, out orderData) && IsOpenOrder(orderData.Order!);
    }

    /// <summary>
    /// 改单，可实现为先撤后发
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public override bool UpdateOrder(Order order)
    {
        try
        {
            if (!TryGetOrderData(order, out var orderData))
            {
                Log.Trace($"MomDeribitBrokerage.UpdateOrder(),Order not found:{order.Id}");
                return false;
            }

            _cancelEvent.Reset();
            CancelOrder(orderData!.Order);
            _cancelEvent.WaitOne(2000);

            if (orderData.MomOrder.OrderStatus != MomOrderStatusType.Canceled)
            {
                return false;
            }

            return PlaceOrder(order);
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomCryptoBrokerage)}.UpdateOrder() UpdateOrder failed，{e}");
        }

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
                Log.Error($"{nameof(MomCryptoBrokerage)} TradeConnect failed - OrderId: {order.Id}");
                return false;
            }
        }

        if (order.BrokerId.Count > 0)
        {
            Log.Trace($"{nameof(MomCryptoBrokerage)},CancelOrder,id:{order.Id},ref:{order.BrokerId[0]},{order.Symbol.Value}");
        }
        else
        {
            Log.Trace($"{nameof(MomCryptoBrokerage)},CancelOrder,id:{order.Id},{order.Symbol.Value}");
        }

        _tradingAction.Post(new TradingAction(order, TradingActionType.CancelOrder));

        return true;
    }

    private void InternalCancel(Order order)
    {
        try
        {
            if (order.BrokerId.Count == 0)
            {
                _cancelPendingList.Add(order.Id);
                Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(InternalCancel)},登记 CancelPending,id:{order.Id}");
                return;
            }

            if (!TryGetOrderData(order, out var orderData))
            {
                Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(InternalCancel)},没有找到订单,id:{order.Id}");
                return;
            }

            if (orderData.Order.Status.IsClosed())
            {
                Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(InternalCancel)},策略订单已完成,id:{order.Id},ref:{orderData.OrderRef},{orderData.Order}");
                return;
            }
            orderData.Order.Status = order.Status;

            var orderAction = CreateOrderAction(order);
            _traderApi.CancelOrder(orderAction);
            Log.Trace($"{nameof(MomCryptoBrokerage)},{nameof(InternalCancel)},id:{order.Id},ref:{orderAction.OrderRef},{order.Symbol.Value}");
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(InternalCancel)},{e}");
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

            Log.Trace($"{nameof(MomCryptoBrokerage)},Connect");

            _tsConnectEvent.Reset();
            TraderConnect();
            _tsConnectEvent.WaitOne(EventTimeout);

            _mdConnectEvent.Reset();
            MarketConnect();
            _mdConnectEvent.WaitOne(EventTimeout);

            var waitTime = TimeSpan.Zero;
            while (!IsConnected)
            {
                if (waitTime >= TimeSpan.FromSeconds(5) && TradingConnected && !InstrumentIsReady)
                {
                    waitTime = TimeSpan.Zero;
                    _traderApi.Login(_tradeUser.UserId, _tradeUser.Password);
                }

                Thread.Sleep(1000);
                waitTime.Add(TimeSpan.FromMilliseconds(1000));
            }
        }
    }

    /// <summary>
    /// Disconnects the client
    /// </summary>
    public override void Disconnect()
    {
        if (!IsMarketConnected() && !IsTraderConnected())
            return;

        Log.Trace($"{nameof(MomCryptoBrokerage)},连接断开");
        TraderDisconnect();
        MarketDisconnect();
    }

    /// <summary>
    /// 
    /// </summary>
    public override void Dispose()
    {
        Log.Trace($"{nameof(MomCryptoBrokerage)},对象销毁");
        Disconnect();
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool IsConnected => TradingConnected && InstrumentIsReady && MarketConnected;

    /// <summary>
    /// 未完成单
    /// </summary>
    /// <param name="order"></param>
    /// <returns></returns>
    public static bool IsOpenOrder(Order order)
    {
        return order.Status is OrderStatus.New or OrderStatus.CancelPending or OrderStatus.Submitted or OrderStatus.PartiallyFilled or OrderStatus.Triggered or OrderStatus.Untriggered;
    }

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
                Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(GetOpenOrders)},trader not connected");
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
                    orders.Add(orderData.Order.Clone());
                }
                else
                {
                    Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(GetOpenOrders)},策略订单ID为零");
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
                Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(GetAccountHoldings)},trader not connected");
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

        try
        {
            foreach (var item in _momPosition)
            {
                var position = item.Value;
                if (!_symbols.TryGetValue(position.InstrumentId, out var symbol))
                {
                    Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(GetAccountHoldings)}.合约未找到,{position.InstrumentId}");
                    continue;
                }

                var holding = ConvertPosition(position, symbol);
                if (holding != null)
                    holdings.Add(holding);
            }
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(GetAccountHoldings)},{e}");
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
                Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(GetCashBalance)},trader not connected");
                return balances;
            }
        }
        if (UseSyncData)
        {
            _initDataSyncing = true;
            _querySyncDataEvent.Reset();
            _traderApi.DataSync();
            _querySyncDataEvent.WaitOne(EventTimeout);
            _initDataSyncing = false;
        }
        else
        {
            _queryAccountEvent.Reset();
            _traderApi.QueryAccount(_tradeUser.UserId);
            _queryAccountEvent.WaitOne(EventTimeout);
        }

        foreach (var (currency, accounts) in _momAccounts)
        {
            foreach (var account in accounts.Values)
            {
                var security = AddConversionRateSecurity(account);
                _momAccounts.AddConversionRateSecurity(currency, security);
            }
        }

        return _momAccounts.GetCashBalance();
    }

    private Symbol? GetConversionRateSymbol(AccountField account)
    {
        var tickers = new List<string>();
        if (account.Market == "deribit")
        {
            tickers.Add($"{account.CurrencyType}_USD".ToLower());
        }
        else
        {
            tickers.Add($"{account.CurrencyType}BUSD.sptbin");
            tickers.Add($"{account.CurrencyType}USDT.sptbin");
        }

        foreach (var ticker in tickers)
        {
            if (_symbols.TryGetValue(ticker, out var symbol))
            {
                return symbol;
            }
        }

        return null;
    }

    private Security? AddConversionRateSecurity(AccountField account)
    {
        if (account.CurrencyType is "USD")
        {
            return null;
        }

        if (CashBook.CryptoUsdToken.Contains(account.ChannelType))
        {
            return null;
        }

        var symbol = GetConversionRateSymbol(account);
        if (symbol == null)
        {
            return null;
        }

        if (_algorithm!.Securities.ContainsKey(symbol))
        {
            return _algorithm.Securities[symbol];
        }

        if (symbol.SecurityType == SecurityType.Future)
        {
            return _algorithm!.AddFutureContract(symbol, Resolution.Tick);
        }

        return _algorithm!.AddSecurity(
            symbol.SecurityType,
            symbol.Value,
            Resolution.Tick,
            symbol.ID.Market,
            true,
            0m,
            false,
            symbol.SymbolProperties,
            DataNormalizationMode.Raw);
    }

    public override List<OrderRecord> GetHistoryOrders()
    {
        var list = new List<OrderRecord>();
        foreach (var item in _orderData)
        {
            if (item.Value.Order != null)
            {
                list.Add(new OrderRecord { order = item.Value.Order });
            }
        }
        return list;
    }

    public override List<TradeRecord> GetHistoryTrades()
    {
        var list = new List<TradeRecord>();
        //foreach (var item in _orderData)
        //{
        //    if (item.Value.Order != null)
        //    {
        //        foreach (var VARIABLE in COLLECTION)
        //        {

        //        }
        //        list.Add(new TradeRecord() { order = item.Value.Order });
        //    }
        //}
        return list;
    }

    #endregion

    /// <summary>
    /// 交易连接状态
    /// </summary>
    public volatile bool TradingConnected;

    /// <summary>
    /// 合约接收是否完成
    /// </summary>
    public volatile bool InstrumentIsReady;

    /// <summary>
    /// 行情连接状态
    /// </summary>
    public volatile bool MarketConnected;

    public string FtxKey { get; set; }
    public string FtxSecret { get; set; }
    public string FtxSubAccount { get; set; }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override void QueryOpenOrders(string requestId)
    {

    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override void QueryAccountHoldings(string requestId)
    {

    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override void QueryCashBalance(string requestId)
    {

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

    public override void QueryExchangeOpenOrders(string uId, string requestId)
    {
        var task = new Task(
            () =>
            {
                var orders = GetExchangeOpenOrders(uId);
                foreach (var order in orders)
                {
                    var last = orders.IndexOf(order) == orders.Count - 1;
                    OnQueryOpenOrderEvent(
                        new AlgorithmQueryArgs
                        {
                            Type = AlgorithmQueryType.ExchangeOpenOrder,
                            RequestId = requestId,
                            Order = order,
                            IsLast = last
                        }
                    );
                }
            });
        task.Start();
    }

    public override void QueryExchangeAccountHoldings(string uId, string requestId)
    {
        var task = new Task(
            () =>
            {
                var holdings = GetExchangePosition(uId);
                foreach (var holding in holdings)
                {
                    var last = holdings.IndexOf(holding) == holdings.Count - 1;
                    OnQueryOpenOrderEvent(
                        new AlgorithmQueryArgs { Type = AlgorithmQueryType.ExchangeHolding, RequestId = requestId, Holding = holding, IsLast = last }
                    );
                }
            });
        task.Start();
    }

    public override void QueryExchangeCashBalance(string uId, string requestId)
    {
        var task = new Task(
            () =>
            {
                var balances = GetExchangeCashBalance(uId);
                foreach (var balance in balances)
                {
                    var last = balances.IndexOf(balance) == balances.Count - 1;
                    OnQueryOpenOrderEvent(
                        new AlgorithmQueryArgs { Type = AlgorithmQueryType.ExchangeCashBalance, RequestId = requestId, Balance = balance, IsLast = last }
                    );
                }
            });
        task.Start();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="symbol"></param>
    /// <returns></returns>
    public Tick? GetTicker(string symbol)
    {
        if (_lastTicks.ContainsKey(symbol))
        {
            _lastTicks.TryGetValue(symbol, out var tick);
            return tick;
        }

        return null;
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
                .Where(x => x.Order is { Status: OrderStatus.CancelPending or OrderStatus.PartiallyFilled })
                .Select(x => x.OrderRef)
                .ToList();

            foreach (var id in pendingList)
            {
                QueryOrder(id);
            }
        }
        catch (Exception e)
        {
            Log.Error($"{nameof(MomCryptoBrokerage)},{nameof(InternalCheckOrderAndTrade)},{e.Message}");
        }
    }

    private void DeleteClosedOrder()
    {
        try
        {
            //删除过多的订单
            //if (_orderData.Count < MaxOrdersToKeep)
            //    return;

            var deleted = new List<long>();

            foreach (var item in _orderData)
            {
                var order = item.Value.Order;
                //如果订单状态还没结束，不能删除
                if (order == null)
                {
                    continue;
                }

                if (order.Status.IsClosed() && DateTime.Now - order.CreatedTime > TimeSpan.FromHours(24))
                {
                    deleted.Add(item.Key);
                    Log.Trace($"{nameof(MomCryptoBrokerage)},清除已关闭的策略订单,id:{order.Id},ref:{item.Key}");
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

    private void QueryOrder(long orderRef = 0)
    {
        _traderApi.QueryOrder(_tradeUser.UserId, orderRef);
    }

    private void QueryTrade(long tradeId = 0)
    {
        _traderApi.QueryTrade(_tradeUser.UserId, tradeId);
    }

    public override void ChangeLeverage(string symbol, int leverage, string exchange)
    {
        _traderApi.ChangeLeverage(symbol, leverage);
    }

    public override void Transfer(decimal amount, UserTransferType type = UserTransferType.Spot2UmFuture, string currency = "USDT")
    {
        if (type == UserTransferType.Spot2UmFuture)
        {
            _traderApi.SpotToFutures(amount, currency: currency);
        }
        else
        {
            _traderApi.FuturesToSpot(amount, currency: currency);
        }
    }

    public override bool TradingIsReady()
    {
        return _tradingIsReady;
    }

    private FtxRestApi GetFtxApi()
    {
        var client = new Client(FtxKey, FtxSecret, FtxSubAccount);
        return new FtxRestApi(client);
    }

    private static OrderDirection? GetRequestDirection(string? side)
    {
        return side switch
        {
            "buy" => OrderDirection.Buy,
            "sell" => OrderDirection.Sell,
            _ => null
        };
    }

    private static OrderStatus GetRequestStatus(string status)
    {
        switch (status)
        {
            case "cancelled":
                return OrderStatus.Canceled;
            default:
                return OrderStatus.New;
        }
    }

    private static OptionType ToOptionType(OptionRight type)
    {
        if (type == OptionRight.Put)
        {
            return OptionType.put;
        }

        return OptionType.call;
    }

    private static SideType ToRequestSide(OrderDirection? side)
    {
        return side == OrderDirection.Buy ? SideType.buy : SideType.sell;
    }

    private static OptionInfo? GetOptionInfo(FtxApi.Rest.Models.FtxOption? option)
    {
        if (option == null)
        {
            return null;
        }

        var info = new OptionInfo();
        info.Expiry = option.Expiry;
        info.Strike = option.Strike;
        info.Type = option.Type == "call" ? OptionRight.Call : OptionRight.Put;
        info.Underlying = option.Underlying;
        return info;
    }

    private static Quote GetQuote(FtxApi.Rest.Models.OptionQuote quote)
    {
        var result = new Quote();
        result.Collateral = quote.Collateral;
        result.Id = quote.Id;
        result.Option = GetOptionInfo(quote.Option!);
        result.Price = quote.Price;
        result.QuoteExpiry = quote.QuoteExpiry;
        result.QuoterSide = GetRequestDirection(quote.QuoterSide);
        result.RequestId = quote.RequestId;
        result.RequestSide = GetRequestDirection(quote.RequestSide);
        result.Size = quote.Size;
        result.Status = GetRequestStatus(quote.Status);
        result.Time = quote.Time;
        return result;
    }

    public override RequestResult<Quote> AcceptRequestQuote(string quoteId)
    {
        var result = GetFtxApi().AcceptRequestQuoteAsync(quoteId).Result;
        if (result?.Success != true)
        {
            return new RequestResult<Quote>(null!, result!.Error);
        }

        return new RequestResult<Quote>(GetQuote(result.Result));
    }

    private static QuoteRequest GetQuoteRequest(FtxApi.Rest.Models.QuoteRequest request)
    {
        var result = new QuoteRequest();
        result.Id = request.Id;
        result.Option = GetOptionInfo(request.Option);
        result.Side = GetRequestDirection(request.Side);
        result.Size = request.Size;
        result.Time = request.Time;
        result.RequestExpiry = request.RequestExpiry;
        result.Status = GetRequestStatus(request.Status);
        result.HideLimitPrice = request.HideLimitPrice;
        result.LimitPrice = request.LimitPrice;
        if (request.Quotes is { Length: > 0 })
        {
            foreach (var quote in request.Quotes)
            {
                result.Quotes.Add(GetQuote(quote));
            }
        }
        return result;
    }

    public override RequestResult<QuoteRequest> CancelQuoteRequest(string requestId)
    {
        var result = GetFtxApi().CancelQuoteRequestAsync(requestId).Result;
        return result?.Success == false
            ? new RequestResult<QuoteRequest>(null!, result!.Error)
            : new RequestResult<QuoteRequest>(QuoteRequest.Empty);
    }

    public override RequestResult<QuoteRequest> SendQuoteRequest(QuoteRequest request)
    {
        var result = GetFtxApi().CreateQuoteRequestAsync(
            ToRequestSide(request.Side),
            ToOptionType(request.Option.Type),
            request.Option.Strike,
            request.Size,
            request.Option.Expiry).Result;
        if (result?.Success != true)
        {
            return new RequestResult<QuoteRequest>(null!, result!.Error);
        }
        return new RequestResult<QuoteRequest>(GetQuoteRequest(result!.Result));
    }

    public override RequestResult<List<Quote>> QueryOptionQuote(string requestId)
    {
        var result = GetFtxApi().GetRequestQuotesAsync(requestId).Result;
        if (result?.Success != true)
        {
            return new RequestResult<List<Quote>>(null!, result!.Error);
        }

        var list = new List<Quote>();
        foreach (var quote in result.Result)
        {
            list.Add(GetQuote(quote));
        }
        return new RequestResult<List<Quote>>(list);
    }

    private static OptionPosition GetOptionPosition(FtxApi.Rest.Models.OptionPosition position)
    {
        var result = new OptionPosition();
        result.Option = GetOptionInfo(position.Option);
        result.EntryPrice = position.EntryPrice;
        result.Side = GetRequestDirection(position.Side);
        result.NetSize = position.NetSize;
        result.Size = position.Size;
        result.PessimisticIndexPrice = position.PessimisticIndexPrice;
        result.PessimisticValuation = position.PessimisticValuation;
        result.PessimisticVol = position.PessimisticVol;
        return result;
    }

    public override RequestResult<List<OptionPosition>> QueryOptionPosition()
    {
        var result = GetFtxApi().GetOptionsPositionsAsync().Result;
        if (result?.Success != true)
        {
            return new RequestResult<List<OptionPosition>>(null!, result!.Error);
        }

        var list = new List<OptionPosition>();
        foreach (var position in result.Result)
        {
            list.Add(GetOptionPosition(position));
        }
        return new RequestResult<List<OptionPosition>>(list);
    }
}