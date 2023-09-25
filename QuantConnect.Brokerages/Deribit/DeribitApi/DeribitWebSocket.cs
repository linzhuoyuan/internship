using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WebSocketSharp;
using System.Threading.Tasks.Dataflow;
using System.Collections.Concurrent;
using Logger = NLog.Logger;
using qcLog = QuantConnect.Logging.Log;

namespace TheOne.Deribit
{

    public class JsonObject
    {
        public readonly Dictionary<string, object> Data = new Dictionary<string, object>();

        public void Add(string name, object value)
        {
            Data[name] = value;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(Data);
        }
    }

    public class DeribitWebSocket:IDisposable
    {
        /// <summary>
        /// The api secret
        /// </summary>
        protected string ApiSecret;

        /// <summary>
        /// The api key
        /// </summary>
        protected string ApiKey;

        public bool IsConnected { get; set; }

        public bool IsLogined { get; set; }

        public bool IsOpen => WebSocket.IsAlive;

        protected ConcurrentDictionary<string, DeribitChannel> ChannelList = new ConcurrentDictionary<string, DeribitChannel>();
        private object _channelLock = new object();

        protected WebSocket WebSocket;

        protected JsonSerializerSettings JsonSettings = new JsonSerializerSettings
            {FloatParseHandling = FloatParseHandling.Decimal};

        private NLog.Logger _logger;

        protected DateTime LastHeartbeatUtcTime = DateTime.UtcNow;
        private Thread _connectionMonitorThread;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly object _lockerConnectionMonitor = new object();
        protected volatile bool _connectionLost=false;
        protected const int _connectionTimeout = 30000;

        private System.Timers.Timer _refreshAuthTokenTimer;
        //虽然已经设置了每10秒服务器进行心跳问询，但是为了方式应答缓慢，我们在这里没5秒再发送一次
        private System.Timers.Timer _SendTestTimer;
        private ulong _recvMsgCount = 0;

        private readonly ActionBlock<string> _msgAction;
        private readonly ActionBlock<JObject> _marketDataAction;
        private readonly ConcurrentQueue<JObject> _messageBuffer = new ConcurrentQueue<JObject>();

        private CancellationTokenSource _subscribeThreadToken = new CancellationTokenSource();
        private Thread _subscribeThread;

        private DeribitMessages.Auth _auth;
        private bool _recvBook;
        private bool _recvTick;
        private bool _recvIndexTick;
        private bool _recvTrade;
        private bool _recvUserOrder;
        private bool _recvUserTrade;
        private bool _recvUserCash;

        public event EventHandler<string> OnErr;
        public event EventHandler OnConnected;
        public event EventHandler OnClosed;
        public event EventHandler<DeribitMessages.Tick> OnTick;
        public event EventHandler<DeribitMessages.Trade[]> OnTrade;
        public event EventHandler<DeribitMessages.BookDepthLimitedData> OnBook;
        public event EventHandler<DeribitMessages.IndexTick> OnIndexTick;
        public event EventHandler<DeribitMessages.Order> OnUserOrder;
        public event EventHandler<DeribitMessages.Trade[]> OnUserTrade;
        public event EventHandler<DeribitMessages.Portfolio> OnUserAccount;


        public void SetRecvIndexTick(bool flag)
        {
            _recvIndexTick = flag;
        }

        public void SetRecvBook(bool flag)
        {
            _recvBook = flag;
        }

        public void SetRecvTick(bool flag)
        {
            _recvTick = flag;
        }

        public void SetRecvTrade(bool flag)
        {
            _recvTrade = flag;
        }

        public void SetRecvUserOrder(bool flag)
        {
            _recvUserOrder = flag;
        }

        public void SetRecvUserTrade(bool flag)
        {
            _recvUserTrade = flag;
        }

        public void SetRecvUserCash(bool flag)
        {
            _recvUserCash = flag;
        }

        protected void Wait(int timeout, Func<bool> state)
        {
            var StartTime = Environment.TickCount;
            do
            {
                if (Environment.TickCount > StartTime + timeout)
                {
                    throw new Exception("Websockets connection timeout.");
                }

                Thread.Sleep(1);
            } while (!state());
        }

        public DeribitWebSocket(string wssUrl, string apiKey, string apiSecret,Logger logger=null)
        {
            if (logger == null)
            {
                _logger = NLog.LogManager.GetLogger("DeribitWebSocket");
            }
            else
            {
                _logger = logger;
            }

            WebSocket = new WebSocket(wssUrl)
            {
                Log = {Output = (data, file) => { _logger.Trace(data.Message); }}
            };

            WebSocket.OnOpen += (sender, args) => OnOpen();
            WebSocket.OnMessage += (sender, args) => OnMessage(args.Data);
            WebSocket.OnError += (sender, args) => OnError(args.Message, args.Exception);
            WebSocket.OnClose += (sender, args) => OnClose(args.Reason);
            WebSocket.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;

            ApiSecret = apiSecret;
            ApiKey = apiKey;

            _msgAction = new ActionBlock<string>((Action<string>)ProcessWebMessage);
            _marketDataAction = new ActionBlock<JObject>((Action<JObject>)OnMessageImpl);


            _subscribeThread = new Thread(SubscribeProcess);
            _subscribeThread.Start();

            _refreshAuthTokenTimer = new System.Timers.Timer()
            {
                Interval = TimeSpan.FromMinutes(DeribitConstents.RefreshAuthTokenLoopPeriodMins).TotalMilliseconds,
                Enabled = false,
            };

            _refreshAuthTokenTimer.Elapsed += (sender, e) =>
            {
                SendSubscribeRefreshToken();
            };

            _SendTestTimer = new System.Timers.Timer()
            {
                Interval = TimeSpan.FromSeconds(DeribitConstents.ACTIVE_TEST).TotalMilliseconds,
                Enabled = false,
            };
            _SendTestTimer.Elapsed += (sender, e) =>
            {
                SendSubscribeTest($"active. RecvMsgCount:{_recvMsgCount}");
            };
        }

        public void OnError(string msg,Exception e)
        {
            _logger.Error("DeribitWebSocket Web Exception:{msg}");
            OnErr?.Invoke(this,msg);
        }

        public void OnMessage(string data)
        {
            LastHeartbeatUtcTime = DateTime.UtcNow;
            _msgAction.Post(data);
        }

        public void OnClose(string reason)
        {
            OnClosed?.Invoke(this,EventArgs.Empty);
            _logger.Trace($"WebApi OnClose {reason}");
            IsLogined = false;
            IsConnected = false;
            _connectionLost = true;
            _refreshAuthTokenTimer.Stop();
            _SendTestTimer.Stop();
        }

        public void OnOpen()
        {
            LastHeartbeatUtcTime = DateTime.Now;
            OnConnected?.Invoke(this, EventArgs.Empty);
            _logger.Trace($"WebApi OnOpen");
            IsConnected = true;
            _connectionLost = false;
            _SendTestTimer.Start();
            SendSubscribeAuth();
        }

        public void Send(string data)
        {
            WebSocket.Send(data);
        }

        public void Connect()
        {
            if (IsConnected)
                return;
            _logger.Trace("BaseWebSocketsBrokerage.Connect(): Connecting...");
            WebSocket.Connect();
            Wait(_connectionTimeout, () => IsOpen);

            _cancellationTokenSource = new CancellationTokenSource();
            _connectionMonitorThread = new Thread(() =>
            {
                var nextReconnectionAttemptUtcTime = DateTime.UtcNow;

                try
                {
                    while (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        if (_connectionLost || (DateTime.Now - LastHeartbeatUtcTime).TotalMinutes>2)
                        {
                            try
                            {
                                Reconnect();
                            }
                            catch (Exception err)
                            {

                                _logger.Error(err);
                            }
                        }

                        Thread.Sleep(5000);
                    }
                }
                catch (Exception exception)
                {
                    _logger.Error(exception);
                }
            })
            { IsBackground = true };
            _connectionMonitorThread.Start();
            while (!_connectionMonitorThread.IsAlive)
            {
                Thread.Sleep(1);
            }
        }

        protected void Reconnect()
        {
            if (IsOpen)
            {
                // connection is still good
                LastHeartbeatUtcTime = DateTime.UtcNow;
                return;
            }

            if (!_connectionLost)
            {
                return;
            }

            _logger.Trace($"Reconnecting... IsConnected: {IsConnected}  {_connectionLost}");

            //WebSocket.OnError -= this.OnError;
            try
            {
                //try to clean up state
                if (IsConnected)
                {
                    WebSocket.Close();
                    Wait(_connectionTimeout, () => !IsOpen);
                }

                if (!IsConnected)
                {
                    WebSocket.Connect();
                    Wait(_connectionTimeout, () => IsOpen);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
            finally
            {
                //WebSocket.OnError += (sender, args) => OnError(args.Message, args.Exception);
                //this.Subscribe(subscribed);
            }
        }

        private void ProcessWebMessage(string msg)
        {
            try
            {
                var result = JObject.Parse(msg);
                if (result.Properties().Any(p => p.Name == "id"))
                { }
                else
                {
                    if ((string)result["method"] == "heartbeat")
                    {
                        SendSubscribeTest("echo in ProcessWebMessage");
                        return;
                    }
                }

                //if (_streamLocked)
                //{
                //    _messageBuffer.Enqueue(result);
                //    return;
                //}



                //OnMessageImpl(result);
                //_logger.Trace(msg);
                _recvMsgCount++;
                //if (_lastDateTime == DateTime.MinValue)
                //{
                //    _lastDateTime = DateTime.Now;
                //}
                //else
                //{
                //    var last = _lastDateTime;
                //    _lastDateTime = DateTime.Now;
                //    if ((_lastDateTime - last).TotalSeconds > 60)
                //    {
                //        _lastDateTime = DateTime.MinValue;
                //        WebSocket.Close();
                //    }
                //}
                _marketDataAction.Post(result);
            }
            catch (Exception err)
            {
                _logger.Error(err);
            }
        }

        public const string JsonRPC = "2.0";
        private void AddHeader(JsonObject payload, string method, int id)
        {
            payload.Add("jsonrpc", JsonRPC);
            if (id == 0)
            {
                payload.Add("id", Thread.CurrentThread.ManagedThreadId);
            }
            else
            {
                payload.Add("id", id);
            }
            payload.Add("method", method);
        }

        private void SendSubscribeTest(string flag)
        {
            try
            {
                var method = "public/test";
                JsonObject payload = new JsonObject();
                AddHeader(payload, method, DeribitConstents.TEST_ID);
                JsonObject param = new JsonObject();
                payload.Add("params", param);
                if (!IsConnected) return;
                WebSocket.Send(payload.ToString());
                //_logger.Trace($"SendSubscribeTest(): Sent Test request. {flag}");
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }


        private void OnMessageImpl(JObject result)
        {
            try
            {
                //var result = JObject.Parse(e.Message);
                if (result.Properties().Any(p => p.Name == "id"))
                {
                    if ((int)result["id"] == DeribitConstents.SUBSCRIBE_ID)
                    {
                        OnSubscribe((JArray)result["result"]);
                    }
                    else if ((int)result["id"] == DeribitConstents.UNSUBSCRIBE_ID)
                    {
                        OnUnsubscribe((JArray)result["result"]);
                    }
                    else if ((int)result["id"] == DeribitConstents.AUTHENTICATE_ID)
                    {
                        OnAuth(result);
                    }
                    else if ((int)result["id"] == DeribitConstents.REFRESH_TOKEN_ID)
                    {
                        OnRereshToken(result);
                    }
                    else if ((int)result["id"] == DeribitConstents.SET_HEART_BEAT_ID)
                    {
                        OnSetHeartBeat(result);
                    }
                    else if ((int)result["id"] == DeribitConstents.TEST_ID)
                    {
                        OnTest(result);
                    }
                }
                else
                {
                    if ((string)result["method"] == "subscription")
                    {
                        JObject param = (JObject)result["params"];
                        string channelId = (string)param["channel"];
                        DeribitChannel channel;
                        if (ChannelList.ContainsKey(channelId))
                        {
                            channel = ChannelList[channelId];
                        }
                        else
                        {
                            _logger.Warn($"Message recieved from unknown channel Id {channelId}");
                            return;
                        }
                        try
                        {
                            //_logger.Trace($"receive data:{result.ToString()}");

                            switch (channelId.Split('.')[0])
                            {
                                case "book":
                                    //ProcessOrderBookSnapshot(channel, (JObject)param["data"]);
                                    ProcessOrderBookGroup(channel, (JObject)param["data"]);
                                    return;
                                case "trades":
                                    ProcessTradesSnapshot(channel, (JArray)param["data"]);
                                    return;
                                case "ticker":
                                    ProcessTickSnapshot(channel, (JObject)param["data"]);
                                    return;
                                case "deribit_price_index":
                                    ProcessIndexSnapshot(channel, (JObject)param["data"]);
                                    return;
                                case "user":
                                    if (channelId.Split('.')[1] == "orders")
                                    {
                                        ProcessOrderChange((JObject)param["data"]);
                                    }
                                    else if (channelId.Split('.')[1] == "trades")
                                    {
                                        ProcessTradeChange((JArray)param["data"]);
                                    }
                                    else if (channelId.Split('.')[1] == "portfolio")
                                    {
                                        ProcessPortfolioChange((JObject)param["data"]);
                                    }
                                    return;
                                default:
                                    return;
                            }
                        }
                        catch (Exception e1)
                        {
                            _logger.Error(e1);
                            throw;
                        }
                    }
                    else if ((string)result["method"] == "heartbeat")
                    {
                        JObject param = (JObject)result["params"];
                        if ((string)param["type"] == "test_request")
                        {
                            SendSubscribeTest("echo in OnMessageImpl");
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                _logger.Error($"OnMessageImpl Exception: {exception}");
                throw;
            }
        }

        private void SubscribeProcess()
        {
            int count = 0;
            while (!_subscribeThreadToken.IsCancellationRequested)
            {
                var channels = new List<string>();
                lock (_channelLock)
                {
                    
                    foreach (var item in ChannelList)
                    {
                        var c1 = (DateTime.Now - item.Value.LastSendTime).TotalSeconds > 10 &&
                                 item.Value.Status == DeribitChannelStatus.Subscribing;

                        var c2 = c1 && string.IsNullOrEmpty(item.Value.Symbol);
                        if ((c1 || item.Value.Status == DeribitChannelStatus.Reset) && !string.IsNullOrEmpty(item.Value.Symbol))
                        {
                            channels.Add(item.Value.Channel);
                            item.Value.Status = DeribitChannelStatus.Subscribing;
                        }

                        if (c2)
                        {
                            if (item.Value.Channel.Contains("user.orders"))
                            {
                                SendSubOrderChange();
                            }
                            if (item.Value.Channel.Contains("user.trades"))
                            {
                                SendSubTradeChange();
                            }
                            if (item.Value.Channel.Contains("user.portfolio"))
                            {
                                SendSubAccountChange();
                            }
                        }
                    }
                }

                if (channels.Count > 0)
                {
                    List<string> cs = new List<string>();
                    foreach (var c in channels)
                    {
                        cs.Add(c);
                        if (cs.Count > 100)
                        {
                            DoSubscribe(cs);
                            Thread.Sleep(1000);
                            cs.Clear();
                        }
                    }
                    DoSubscribe(cs);
                }

                Thread.Sleep(3000);
            }
        }

        private void DoSubscribe(IEnumerable<string> channels)
        {
            var method = "public/subscribe";
            JsonObject payload = new JsonObject();
            AddHeader(payload, method, DeribitConstents.SUBSCRIBE_ID);
            JsonObject param = new JsonObject();
            param.Add("channels", channels.ToArray());
            payload.Add("params", param);
            WebSocket.Send(payload.ToString());
            _logger.Trace($"DoSubscribe: Send channels, symbols count:{ channels.Count()}");
        }

        public void SubscribeMD(IEnumerable<string> symbols)
        {
            lock (_channelLock)
            {
                foreach (var symbol in symbols)
                {
                    _logger.Trace($"Subscribe: -- {symbol}");
                    
                    if (symbol == "btc_usd" || symbol == "eth_usd"  && _recvIndexTick)
                    {
                        var channel = $"deribit_price_index.{symbol}";
                        if (ChannelList.ContainsKey(symbol))
                        {
                            ChannelList[channel].Status = DeribitChannelStatus.Reset;
                        }
                        else
                        {
                            ChannelList[channel] = new DeribitChannel() { Symbol = symbol, Status = DeribitChannelStatus.Reset, Channel = channel };
                        }
                    }
                    else
                    {
                        if (_recvTick)
                        {
                            var channel = $"ticker.{symbol}.100ms";
                            if (ChannelList.ContainsKey(symbol))
                            {
                                ChannelList[channel].Status = DeribitChannelStatus.Reset;
                            }
                            else
                            {
                                ChannelList[channel] = new DeribitChannel()
                                    {Symbol = symbol, Status = DeribitChannelStatus.Reset, Channel = channel};
                            }
                        }

                        if (_recvBook)
                        {
                            var channel = $"book.{symbol}.none.10.100ms";
                            if (ChannelList.ContainsKey(symbol))
                            {
                                ChannelList[channel].Status = DeribitChannelStatus.Reset;
                            }
                            else
                            {
                                ChannelList[channel] = new DeribitChannel()
                                    {Symbol = symbol, Status = DeribitChannelStatus.Reset, Channel = channel};
                            }
                        }

                        if (_recvTrade)
                        {
                            var channel = $"trade.{symbol}.100ms";
                            if (ChannelList.ContainsKey(symbol))
                            {
                                ChannelList[channel].Status = DeribitChannelStatus.Reset;
                            }
                            else
                            {
                                ChannelList[channel] = new DeribitChannel()
                                    {Symbol = symbol, Status = DeribitChannelStatus.Reset, Channel = channel};
                            }
                        }
                    }
                }
            }
        }


        private void OnSubscribe(JArray results)
        {
            var datas = results.ToArray();            
            try
            {
                lock (_channelLock)
                {
                    foreach (string channel in datas)
                    {
                        _logger.Trace($"Subscribe: recv subscribe {channel}");
                        string symbol = "";
                        if (channel.StartsWith("user."))
                        {
                            ;
                        }
                        else
                        {
                            symbol = channel.Split('.')[1];
                        }

                        DeribitChannel item;
                        if (ChannelList.TryGetValue(channel, out item))
                        {
                            item.Status = DeribitChannelStatus.Subscribed;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error(e);
                throw;
            }
        }

        private void OnUnsubscribe(JArray results)
        {
            
        }

        private void OnAuth(JObject result)
        {
            if (result.Property("error") != null)
            {
                _logger.Error("用户登录失败: " + result["error"]["message"]);
                throw new Exception("用户登录失败: " + result["error"]["message"]);
            }
            else
            {
                _logger.Trace("用户登录成功");
                var data = (JObject)result["result"];
                _auth = JsonConvert.DeserializeObject<DeribitMessages.Auth>(data.ToString());
                _refreshAuthTokenTimer.Start();
                IsLogined = true;

                //默认订阅所有的订单变化事件
                if (_recvUserOrder)
                {
                    SendSubOrderChange();
                }
                if (_recvUserTrade)
                {
                    SendSubTradeChange();
                }
                if (_recvUserCash)
                {
                    SendSubAccountChange();
                }

                ResetSubscribe();
            }
        }

        private void SendSubOrderChange()
        {
            var method = "private/subscribe";
            JsonObject payload = new JsonObject();
            AddHeader(payload, method, DeribitConstents.SUBSCRIBE_ID);
            JsonObject param = new JsonObject();
            string[] channel = { "user.orders.any.any.raw" };   //订阅所有
            param.Add("channels", channel);
            payload.Add("params", param);
            WebSocket.Send(payload.ToString());
            _logger.Trace($"Subscribe: Sent user order any change at BTC&ETH");

            lock (_channelLock)
            {
                if (ChannelList.ContainsKey(channel[0]))
                {
                    ChannelList[channel[0]].Status = DeribitChannelStatus.Reset;
                }
                else
                {
                    ChannelList[channel[0]] =new DeribitChannel(){Symbol = "",Status = DeribitChannelStatus.Subscribing, Channel = channel[0],LastSendTime = DateTime.Now};
                }
            }
        }
        private void SendSubTradeChange()
        {
            var method = "private/subscribe";
            JsonObject payload = new JsonObject();
            AddHeader(payload, method, DeribitConstents.SUBSCRIBE_ID);
            JsonObject param = new JsonObject();
            string[] channel = { "user.trades.any.any.raw" };
            param.Add("channels", channel);
            payload.Add("params", param);
            WebSocket.Send(payload.ToString());
            _logger.Trace($"Subscribe: Sent user order any change at BTC&ETH");

            lock (_channelLock)
            {
                if (ChannelList.ContainsKey(channel[0]))
                {
                    ChannelList[channel[0]].Status = DeribitChannelStatus.Reset;
                }
                else
                {
                    ChannelList[channel[0]] = new DeribitChannel() { Symbol = "", Status = DeribitChannelStatus.Subscribing, Channel = channel[0], LastSendTime = DateTime.Now };
                }
            }
        }
        private void SendSubAccountChange()
        {
            var method = "private/subscribe";
            JsonObject payload = new JsonObject();
            AddHeader(payload, method, DeribitConstents.SUBSCRIBE_ID);
            JsonObject param = new JsonObject();
            string[] channels = { "user.portfolio.btc", "user.portfolio.eth" };   //订阅所有
            param.Add("channels", channels);
            payload.Add("params", param);
            WebSocket.Send(payload.ToString());
            _logger.Trace($"Subscribe: Sent user portfolio any change at BTC&ETH");

            lock (_channelLock)
            {
                foreach (var channel in channels)
                {
                    if (ChannelList.ContainsKey(channel))
                    {
                        ChannelList[channel].Status = DeribitChannelStatus.Reset;
                    }
                    else
                    {
                        ChannelList[channel] = new DeribitChannel() { Symbol = "", Status = DeribitChannelStatus.Subscribing, Channel = channel, LastSendTime = DateTime.Now };
                    }
                }
            }
        }

        public void ResetSubscribe()
        {
            lock (_channelLock)
            {
                foreach (var item in ChannelList)
                {
                    item.Value.Status = DeribitChannelStatus.Reset;
                }
            }
        }

        private void ProcessTickSnapshot(DeribitChannel channel, JObject data)
        {
            try
            {
                var tick = JsonConvert.DeserializeObject<DeribitMessages.Tick>(data.ToString());
                OnTick?.Invoke(this, tick);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
        }

        private void ProcessIndexSnapshot(DeribitChannel channel, JObject data)
        {
            try
            {
                var symbol = channel.Symbol;
                var time = DeribitUtility.UnixTimeStampToDateTime(((double)data["timestamp"]) / 1000);
                var price = (decimal)data["price"];

                var indextick = JsonConvert.DeserializeObject<DeribitMessages.IndexTick>(data.ToString());
                OnIndexTick?.Invoke(this, indextick);

            }
            catch (Exception ex)

            {
                _logger.Error(ex);
                throw;
            }
        }

        private void ProcessTradesSnapshot(DeribitChannel channel, JArray data)
        {
            try
            {
                var trades = JsonConvert.DeserializeObject<DeribitMessages.Trade[]>(data.ToString());
                OnTrade?.Invoke(this,trades);
            }
            catch (Exception e)
            {
                _logger.Error(e);
                throw;
            }
        }

        private void ProcessOrderBookGroup(DeribitChannel channel, JObject data)
        {
            try
            {
                var bookDepth = new DeribitMessages.BookDepthLimitedData();
                bookDepth.timestamp = Convert.ToInt64(data["timestamp"]);
                bookDepth.instrument_name = data["instrument_name"].ToString();
                int i = 1;
                bookDepth.bids =new List<DeribitMessages.BookOrder>();
                foreach (JArray entry in data["bids"])
                {
                    decimal price = (decimal)entry[0];
                    decimal amount = (decimal)entry[1];
                    bookDepth.bids.Add(new DeribitMessages.BookOrder() { price = price, amount = amount });
                }
                bookDepth.asks =new List<DeribitMessages.BookOrder>();
                foreach (JArray entry in data["asks"])
                {
                    decimal price = (decimal)entry[0];
                    decimal amount = (decimal)entry[1];
                    bookDepth.asks.Add(new DeribitMessages.BookOrder() { price = price, amount = amount });
                }

                OnBook?.Invoke(this, bookDepth);
            }
            catch (Exception e)
            {
                var json = data.ToString();
                _logger.Error(e, json);
                throw;
            }
        }

        private void ProcessOrderChange(JObject data)
        {
            try
            {
                var content = data.ToString();
                _logger.Trace($"ProcessOrderChange: {content}");
                qcLog.Trace($"ProcessOrderChange: {content}");

                if (data["price"].ToString() == "market_price")
                {
                    data["price"] = 0;
                }
                content = data.ToString();
                var order = JsonConvert.DeserializeObject<DeribitMessages.Order>(content);
                OnUserOrder?.Invoke(this, order);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
        }

        private void ProcessTradeChange(JArray data)
        {
            try
            {
                var content = data.ToString();
                _logger.Trace($"ProcessTradeChange: {content}");
                qcLog.Trace($"ProcessTradeChange: {content}");
                var trades = JsonConvert.DeserializeObject<DeribitMessages.Trade[]>(content);
                OnUserTrade?.Invoke(this,trades);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
        }

        private void ProcessPortfolioChange(JObject data)
        {
            try
            {
                var portfolio = JsonConvert.DeserializeObject<DeribitMessages.Portfolio>(data.ToString());
                OnUserAccount?.Invoke(this, portfolio);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                throw;
            }
        }

        private void SendSubscribeAuth()
        {
            var method = "public/auth";
            JsonObject payload = new JsonObject();
            AddHeader(payload, method, DeribitConstents.AUTHENTICATE_ID);
            JsonObject param = new JsonObject();
            param.Add("grant_type", "client_credentials");
            param.Add("client_id", ApiKey);
            param.Add("client_secret", ApiSecret);
            payload.Add("params", param);
            WebSocket.Send(payload.ToString());
            _logger.Trace("SubscribeAuth(): Sent authentication request.");
        }

        private void OnRereshToken(JObject result)
        {
            if (result.Property("error") != null)
            {
                _logger.Trace("RereshToken失败");
                SendSubscribeRefreshToken();
            }
            else
            {
                _logger.Trace("RereshToken成功");
                var data = (JObject)result["result"];

                _auth = JsonConvert.DeserializeObject<DeribitMessages.Auth>(data.ToString());
                //refresh token
                //SetTimeout((_auth.expires_in-10)*1000,SendSubscribeRefreshToken);
            }
        }

        private void SendSubscribeRefreshToken()
        {
            var method = "public/auth";
            JsonObject payload = new JsonObject();
            AddHeader(payload, method, DeribitConstents.REFRESH_TOKEN_ID);
            JsonObject param = new JsonObject();
            param.Add("grant_type", "refresh_token");
            param.Add("refresh_token", _auth.refresh_token);
            payload.Add("params", param);
            WebSocket.Send(payload.ToString());
            _logger.Trace("SendSubscribeRefreshToken: Sent RefreshToken request.");
        }
        private void OnSetHeartBeat(JObject result)
        {
            if (result.Property("error") != null)
            {
                _logger.Trace("SetHeartBeat失败");
                SendSubscribeSetHeartBeat();
            }
            else
            {
                _logger.Trace("SetHeartBeat " + result["result"].ToString());
            }
        }

        private void SendSubscribeSetHeartBeat()
        {
            var method = "public/set_heartbeat";
            JsonObject payload = new JsonObject();
            AddHeader(payload, method, DeribitConstents.SET_HEART_BEAT_ID);
            JsonObject param = new JsonObject();
            param.Add("interval", DeribitConstents.HEARTBEAT_SPAN);
            payload.Add("params", param);
            WebSocket.Send(payload.ToString());
            _logger.Trace("SendSubscribeRefreshToken(): Sent SetHeartBeat request.");
        }

        private void OnTest(JObject result)
        {
            if (result.Property("error") != null)
            {
                _logger.Trace("Test失败");
                SendSubscribeTest("send Test failed, resend");
            }
            else
            {
                //_logger.Trace("Test " + result["result"].ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            CloseSocket();
            _cancellationTokenSource.Cancel();
            if(_connectionMonitorThread !=null)_connectionMonitorThread.Join();
            _subscribeThreadToken.Cancel();
            if (_subscribeThread != null) _subscribeThread.Join();
            _SendTestTimer.Stop();
            _refreshAuthTokenTimer.Stop();
        }

        /// <summary>
        /// 
        /// </summary>
        public void CloseSocket()
        {
            WebSocket.Close();
        }
    }
}
