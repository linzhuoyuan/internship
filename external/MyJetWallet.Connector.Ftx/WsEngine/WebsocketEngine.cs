using System;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace FtxApi.WsEngine
{
    public class WebsocketEngine: IDisposable
    {
        private readonly string _name;
        private readonly string _url;
        private readonly int _pingIntervalMSec;
        private readonly int _silenceDisconnectIntervalMSec;
        private readonly ILogger _logger;
        private ClientWebSocket? _clientWebSocket;

        private DateTime _lastMessageReceive = DateTime.Now;

        public static int InitBufferSize { get; set; } = 512;

        private Task? _runTask;

        private readonly CancellationTokenSource _token = new();

        public WebsocketEngine(string name, string url, int pingIntervalMSec, int silenceDisconnectIntervalMSec, ILogger logger)
        {
            _name = name;
            _url = url;
            _pingIntervalMSec = pingIntervalMSec;
            _silenceDisconnectIntervalMSec = silenceDisconnectIntervalMSec;
            _logger = logger;
        }

        public Func<ClientWebSocket, Task>? OnConnect { get; set; }
        public Func<Task>? OnDisconnect { get; set; }
        public Func<ClientWebSocket, string, Task>? OnReceive { get; set; }
        public Func<ClientWebSocket, Task>? SendPing { get; set; }

        public ClientWebSocket? GetClientWebSocket()
        {
            return _clientWebSocket;
        }
        
        public void Start()
        {
            if (_token.IsCancellationRequested)
                throw new Exception("Cannot start stopped websocket");


            _runTask = Task.Run(Run);

            _logger.Info("Web socket {name} is started", _name);
        }

        public void Stop()
        {
            _token.Cancel();
            try
            {
                _runTask?.Wait();
            }
            catch (Exception)
            {
                // ignored
            }

            _logger.Info("Web socket {name} is stopped", _name);
        }

        public async Task Run()
        {
            while (!_token.IsCancellationRequested)
            {
                try
                {
                    _clientWebSocket = new ClientWebSocket();
                    _logger.Info("Web socket {name} try to connect ...", _name);
                    _logger.Info("Web socket {name} is connected", _name);
                    await _clientWebSocket.ConnectAsync(new Uri(_url), _token.Token);

                    await Task.WhenAll(Receive(_clientWebSocket), Send(_clientWebSocket), CheckActivity(_clientWebSocket));
                }
                catch (AggregateException ex)
                {
                    if (!ex.InnerExceptions.All(e => e is TaskCanceledException))
                    {
                        _logger.Warn(ex, "Web socket {name} receive Exception", _name);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, "Web socket {name} receive Exception", _name);
                }
                finally
                {
                    _clientWebSocket?.Dispose();
                    _clientWebSocket = null;
                    _logger.Info("Web socket {name} is closed", _name);
                }

                try
                {
                    await OnDisconnectAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Web socket {name} receive Exception from method OnDisconnect", _name);
                }

                await Task.Delay(2000, _token.Token);
            }
        }

        private async Task CheckActivity(ClientWebSocket webSocket)
        {
            try
            {
                if (_silenceDisconnectIntervalMSec <= 0)
                    return;

                var delay = (int) Math.Round(_silenceDisconnectIntervalMSec / 10.0, 0);

                while (webSocket.State == WebSocketState.Open && !_token.IsCancellationRequested)
                {
                    await Task.Delay(delay, _token.Token);

                    if ((DateTime.UtcNow - _lastMessageReceive).TotalMilliseconds >= _silenceDisconnectIntervalMSec)
                    {
                        _logger.Error("Web socket {name} was closed because do not receive messages from service side", _name);

                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                        return;
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Web socket {name} receive Exception on check activity");
            }
        }

        private async Task Send(ClientWebSocket webSocket)
        {
            try
            {
                _lastMessageReceive = DateTime.UtcNow;

                await OnConnectAsync(webSocket);

                _logger.Info("Web socket {name} OnConnected is done", _name);

                if (_pingIntervalMSec <= 0)
                    return;

                while (webSocket.State == WebSocketState.Open && !_token.IsCancellationRequested)
                {
                    await SendPingMessage(webSocket);
                    await Task.Delay(_pingIntervalMSec, _token.Token);
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Web socket {name} receive Exception and closed", _name);
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            }
        }

        private async Task Receive(ClientWebSocket webSocket)
        {
            try
            {
                byte[] buffer = new byte[InitBufferSize];
                var offset = 0;

                while (webSocket.State == WebSocketState.Open && !_token.IsCancellationRequested)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, buffer.Length - offset), _token.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.Info("Web socket {name} receive close signal", _name);

                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);

                        return;
                    }

                    _lastMessageReceive = DateTime.UtcNow;

                    var count = result.Count;
                    offset += count;

                    if (!result.EndOfMessage)
                    {
                        if (buffer.Length - offset < 10)
                        {
                            var buf = buffer;
                            buffer = new byte[buf.Length * 2];
                            buf.CopyTo(buffer, 0);
                        }

                        _logger.Info("Web socket {name} increase buffer size to {size}", _name,
                            buffer.Length);

                        continue;
                    }

                    var msg = Encoding.UTF8.GetString(buffer, 0, offset);

                    try
                    {
                        await OnReceiveAsync(webSocket, msg);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warn(ex, "Web socket {name} receive Exception from method OnReceive", _name);
                        throw;
                    }

                    offset = 0;
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Web socket {name} receive Exception and closed", _name);
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            }
        }

        public async Task OnConnectAsync(ClientWebSocket clientWebSocket)
        {
            var action = OnConnect;
            if (action != null)
            {
                await action.Invoke(clientWebSocket);
            }
        }

        public async Task OnDisconnectAsync()
        {
            var action = OnDisconnect;
            if (action != null)
            {
                await action.Invoke();
            }
        }

        public async Task OnReceiveAsync(ClientWebSocket webSocket, string message)
        {
            var action = OnReceive;
            if (action != null)
            {
                await action.Invoke(webSocket, message);
            }
        }

        public async Task SendPingMessage(ClientWebSocket clientWebSocket)
        {
            var action = SendPing;
            if (action != null)
            {
                await action.Invoke(clientWebSocket);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}