using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using FtxApi.WebSocket.Models;
using FtxApi.WsEngine;
using Newtonsoft.Json;
using NLog;

namespace FtxApi.WebSocket
{
    public class FtxWsMarkets: IDisposable
    {
        private readonly ILogger _logger;
        private readonly WebsocketEngine _engine;
        public static string Url { get; set; } = "wss://ftx.com/ws/";
        public FtxWsMarkets(ILogger logger)
        {
            _logger = logger;
            _engine = new WebsocketEngine(nameof(FtxWsMarkets), Url, 5000, 10000, logger);
            _engine.SendPing = SendPing;
            _engine.OnReceive = Receive;
            _engine.OnConnect = Connect;
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

        public Func<List<MarketState>, Task>? ReceiveUpdates;

        public void Dispose()
        {
            _engine.Stop();
            _engine.Dispose();
        }

        private static async Task Connect(ClientWebSocket webSocket)
        {
            await webSocket.SubscribeFtxChannel("markets");
        }

        private async Task Receive(ClientWebSocket webSocket, string msg)
        {
            var packet = JsonConvert.DeserializeObject<FtxWebsocketReceive<DataAction<Dictionary<string, MarketState>>>>(msg);
            
            if (packet is {Channel: "markets", Type: FtxWebsocketReceive.Partial or FtxWebsocketReceive.Update})
            {
                await OnReceiveUpdates(packet.Data.Data.Values.ToList());
            }
        }

        private static async Task SendPing(ClientWebSocket webSocket)
        {
            await webSocket.SendFtxPing();
        }

        private async Task OnReceiveUpdates(List<MarketState> markets)
        {
            try
            {
                var action = ReceiveUpdates;
                if (action != null)
                    await action.Invoke(markets);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception from method OnReceiveUpdates from client code");
            }
        }
    }
}