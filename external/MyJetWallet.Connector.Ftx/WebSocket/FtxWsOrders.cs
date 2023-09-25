using System;
using System.Globalization;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using FtxApi.Rest.Models;
using FtxApi.WsEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace FtxApi.WebSocket
{
    public class FtxWsOrders : IDisposable
    {
        private readonly WebsocketEngine _engine;
        private readonly Client _client;
        private readonly ILogger _logger;

        private static readonly JsonSerializer DefaultSerializer = JsonSerializer.Create(
            new JsonSerializerSettings
            {
                DateTimeZoneHandling = DateTimeZoneHandling.Utc,
                Culture = CultureInfo.InvariantCulture
            });
        public static string Url { get; set; } = "wss://ftx.com/ws/";

        public FtxWsOrders(Client client, ILogger logger)
        {
            _client = client;
            _logger = logger;
            _engine = new WebsocketEngine(nameof(FtxWsOrders), Url, 5000, 10000, logger);
            _engine.SendPing = SendPing;
            _engine.OnReceive = Receive;
            _engine.OnConnect = Connect;
        }

        private async Task Connect(ClientWebSocket webSocket)
        {
            //await Task.Delay(1000);
            await webSocket.FtxAuthentication(_client);
            await webSocket.SubscribeFtxChannel("fills");
            await webSocket.SubscribeFtxChannel("orders");
        }

        private async Task Receive(ClientWebSocket webSocket, string msg)
        {
            var token = JToken.Parse(msg);
            var type = (string?)token["type"];
            var channel = (string?)token["channel"];

            if (type == "update")
            {
                if (channel == "fills")
                {
                    var fill = token["data"]?.ToObject<Fill>(DefaultSerializer);
                    if (fill != null && FillUpdates != null)
                    {
                        await FillUpdates.Invoke(fill);
                    }
                }
                else if (channel == "orders")
                {
                    var order = token["data"]?.ToObject<Order>(DefaultSerializer);
                    if (order != null && OrderUpdates != null)
                    {
                        await OrderUpdates.Invoke(order);
                    }
                }
            }
            else if (type == "pong")
            {

            }
            else if (type == "subscribed")
            {
                if (channel == "orders")
                {
                    _logger.Debug("订阅订单推送成功");
                }
                else if(channel == "fills")
                {
                    _logger.Debug("订阅成交推送成功");
                }
            }
            else if (type == "error")
            {
                if ((string?) token["msg"] == "Invalid login credentials")
                {
                    await Connect(webSocket);
                }
                _logger.Error(msg);
            }
        }

        private static async Task SendPing(ClientWebSocket webSocket)
        {
            await webSocket.SendFtxPing();
        }

        public void Start(CancellationToken? ct = null)
        {
            _engine.Start();
            if (ct.HasValue)
            {
                while (!ct.Value.IsCancellationRequested)
                {
                    var socket = _engine.GetClientWebSocket();
                    if (socket?.State == WebSocketState.Open)
                    {
                        break;
                    }
                    Thread.Sleep(100);
                }
            }
        }

        public void Stop()
        {
            _engine.Stop();
        }

        public Func<Order, Task>? OrderUpdates;
        public Func<Fill, Task>? FillUpdates;

        public void Dispose()
        {
            _engine.Stop();
            _engine.Dispose();
        }
    }
}