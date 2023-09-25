using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using FtxApi.WebSocket.Models;
using FtxApi.WsEngine;
using Newtonsoft.Json;
using NLog;

namespace FtxApi.WebSocket
{
    public class FtxWsOrderBooks : IDisposable
    {
        public static string Url { set; get; } = "wss://ftx.com/ws/";
        public static string ChannelName = "orderbook";

        private readonly ILogger _logger;
        private readonly WebsocketEngine _engine;
        private readonly HashSet<string> _symbols = new();

        public FtxWsOrderBooks(ILogger logger, string? name = null)
        {
            _logger = logger;
            _engine = new WebsocketEngine(name ?? nameof(FtxWsOrderBooks), Url, 5000, 10000, logger);
            _engine.SendPing = SendPing;
            _engine.OnReceive = Receive;
            _engine.OnConnect = Connect;
        }
        
        public void Restart()
        {
            Stop();
            Restart();
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

        public Func<FtxOrderBook, Task>? ReceiveUpdates;

        public void Dispose()
        {
            _engine.Stop();
            _engine.Dispose();
        }

        public async Task Subscribe(IList<string> symbols)
        {
            var webSocket = _engine.GetClientWebSocket();
            if (webSocket == null)
                return;

            foreach (var symbol in symbols)
            {
                if (_symbols.Contains(symbol))
                {
                    continue;
                }

                _symbols.Add(symbol);
                await webSocket.SubscribeFtxChannel(ChannelName, symbol);
            }
        }

        public async Task Unsubscribe(IList<string> symbols)
        {
            var webSocket = _engine.GetClientWebSocket();
            if (webSocket == null)
                return;

            foreach (var symbol in symbols)
            {
                if (!_symbols.Contains(symbol))
                {
                    continue;
                }

                _symbols.Remove(symbol);
                await webSocket.UnsubscribeFtxChannel(ChannelName, symbol);
            }
        }

        public async Task Resubscribe(IList<string> symbols)
        {
            var webSocket = _engine.GetClientWebSocket();
            if (webSocket == null)
                return;

            foreach (var symbol in symbols)
            {
                await webSocket.UnsubscribeFtxChannel(ChannelName, symbol);
                await webSocket.SubscribeFtxChannel(ChannelName, symbol);
            }
        }

        private async Task Connect(ClientWebSocket webSocket)
        {
            foreach (var symbol in _symbols)
            {
                await webSocket.SubscribeFtxChannel(ChannelName, symbol);
            }
        }

        private async Task Receive(ClientWebSocket webSocket, string msg)
        {
            var packet = JsonConvert.DeserializeObject<FtxWebsocketReceive<FtxOrderBook>>(msg);

            if (packet != null
                && packet.Channel == ChannelName
                && packet is { Type: FtxWebsocketReceive.Partial or FtxWebsocketReceive.Update })
            {
                packet.Data.id = packet.Market;
                await OnReceiveUpdates(packet.Data);
            }
        }

        private static async Task SendPing(ClientWebSocket webSocket)
        {
            await webSocket.SendFtxPing();
        }

        private async Task OnReceiveUpdates(FtxOrderBook orderBook)
        {
            try
            {
                var action = ReceiveUpdates;
                if (action != null)
                    await action.Invoke(orderBook);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Exception from method OnReceiveUpdates from client code");
            }
        }
    }
}