using System;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FtxApi.WebSocket
{
    public static class FtxSenderClientWebSocket
    {
        public static async Task SendFtxPing(this ClientWebSocket webSocket)
        {
            var msg = JsonConvert.SerializeObject(new { op = "ping" });

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)),
                WebSocketMessageType.Text, true, new CancellationTokenSource(3000).Token);
        }

        public static async Task FtxAuthentication(this ClientWebSocket webSocket, Client client)
        {
            var hashMaker = new HMACSHA256(Encoding.UTF8.GetBytes(client.ApiSecret));
            var hash = hashMaker.ComputeHash(Encoding.UTF8.GetBytes($"{FtxHelper.GetMillisecondsFromEpochStart()}websocket_login"));
            var sign = BitConverter.ToString(hash).Replace("-", string.Empty).ToLower();
            var args = new
            {
                key = client.ApiKey, 
                sign, 
                time = FtxHelper.GetMillisecondsFromEpochStart(),
                subaccount = client.SubAccount
            };
            var msg = JsonConvert.SerializeObject(new { op = "login", args });
            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public static async Task SubscribeFtxChannel(this ClientWebSocket webSocket, string channel, string market)
        {
            var msg = JsonConvert.SerializeObject(new { op = "subscribe", channel, market });

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public static async Task SubscribeFtxChannel(this ClientWebSocket webSocket, string channel)
        {
            var msg = JsonConvert.SerializeObject(new { op = "subscribe", channel });

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public static async Task UnsubscribeFtxChannel(this ClientWebSocket webSocket, string channel)
        {
            var msg = JsonConvert.SerializeObject(new { op = "unsubscribe", channel });

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        public static async Task UnsubscribeFtxChannel(this ClientWebSocket webSocket, string channel, string market)
        {
            var msg = JsonConvert.SerializeObject(new { op = "unsubscribe", channel, market });

            await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}