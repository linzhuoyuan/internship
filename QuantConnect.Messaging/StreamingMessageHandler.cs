using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using LiteDB;
using LiteDB.Engine;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using QLNet;
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Notifications;
using QuantConnect.Packets;

namespace QuantConnect.Messaging
{
    /// <summary>
    /// Message handler that sends messages over tcp using NetMQ.
    /// </summary>
    public class StreamingMessageHandler : IMessagingHandler
    {
        private string _port = string.Empty;
        private AlgorithmNodePacket? _job;
        private Task? _task;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private bool _tradeSaveDb = false;

        /// <summary>
        /// Gets or sets whether this messaging handler has any current subscribers.
        /// This is not used in this message handler.  Messages are sent via tcp as they arrive
        /// </summary>
        public bool HasSubscribers { get; set; }

        /// <summary>
        /// GetAlgorithmNodePacket
        /// </summary>
        public AlgorithmNodePacket GetAlgorithmNodePacket()
        {
            return _job!;
        }

        /// <summary>
        /// Initialize the messaging system
        /// </summary>
        public void Initialize()
        {
            _port = Config.Get("desktop-http-port");
            _tradeSaveDb = Config.GetBool("trade-save-db", false);
            CheckPort();
        }

        private string GetDatabaseFile()
        {
            return PathHelper.CompletionPath($"{_job.AlgorithmId}_{DateTime.Now:yyyyMMdd_HHmmss}.db");
        }

        private class ResultItem
        {
            public int Id { get; set; }
            public string Data { get; set; }
        }

        private void Do()
        {
            const int perMaxSendBlock = 20;
            var clients = new Dictionary<byte[], DateTime>(ByteArrayEqualsComparer.Instance);

            var databaseFile = GetDatabaseFile();
            if (File.Exists(databaseFile))
            {
                File.Delete(databaseFile);
            }

            ILiteCollection<ResultItem>? collection = null;
            if (_tradeSaveDb)
            {
                var db = new LiteDatabase(databaseFile);
                collection = db.GetCollection<ResultItem>("result");
            }

            var server = new RouterSocket($"@tcp://0.0.0.0:{_port}");
            server.ReceiveReady += (sender, args) =>
            {
                var msg = new NetMQMessage();
                if (!server.TryReceiveMultipartMessage(ref msg))
                {
                    return;
                }
                var id = msg.First.ToByteArray();
                if (!clients.ContainsKey(id))
                {
                    clients.Add(id, DateTime.Now);
                    if (_tradeSaveDb && collection != null)
                    {
                        foreach (var data in collection.FindAll())
                        {
                            SendToClient(id, data.Data);
                        }
                    }
                }
                else
                {
                    clients[id] = DateTime.Now;
                }
            };
            var poller = new NetMQPoller();
            var checkTimer = new NetMQTimer(TimeSpan.FromMilliseconds(60000));
            checkTimer.Elapsed += (sender, args) =>
            {
                var removed = new List<byte[]>();
                foreach (var client in clients)
                {
                    if ((DateTime.Now - client.Value).TotalMilliseconds > checkTimer.Interval)
                    {
                        removed.Add(client.Key);
                    }
                }
                removed.ForEach(n => clients.Remove(n));
            };
            var sendTimer = new NetMQTimer(TimeSpan.FromMilliseconds(1));
            sendTimer.Elapsed += (sender, args) =>
            {
                var messages = new List<ResultItem>();
                for (var i = 0; i < perMaxSendBlock; i++)
                {
                    if (_queue.TryDequeue(out var data))
                    {
                        if (_tradeSaveDb)
                        {
                            messages.Add(new ResultItem {Data = data});
                        }

                        foreach (var clientId in clients.Keys)
                        {
                            SendToClient(clientId, data);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                if (_tradeSaveDb)
                {
                    try
                    {
                        collection?.InsertBulk(messages, messages.Count);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                    }
                }
                if (_cts.IsCancellationRequested)
                {
                    poller.Stop();
                }
            };
            poller.Add(server);
            poller.Add(sendTimer);
            poller.Add(checkTimer);
            poller.Run();
            try
            {
                server.Dispose();
            }
            catch (Exception)
            {
                // ignored
            }

            void SendToClient(byte[] client, string data)
            {
                var msg = new NetMQMessage();
                msg.Append(client);
                msg.AppendEmptyFrame();
                msg.Append(data);
                server.SendMultipartMessage(msg);
            }
        }

        /// <summary>
        /// Set the user communication channel
        /// </summary>
        /// <param name="job"></param>
        public void SetAuthentication(AlgorithmNodePacket job)
        {
            _job = job;
            Transmit(_job);
            _task ??= Task.Run(Do, _cts.Token);
        }

        /// <summary>
        /// Send any notification with a base type of Notification.
        /// </summary>
        /// <param name="notification">The notification to be sent.</param>
        public void SendNotification(Notification notification)
        {
            var type = notification.GetType();
            if (type == typeof(NotificationEmail) || type == typeof(NotificationWeb) || type == typeof(NotificationSms))
            {
                Log.Error("Messaging.SendNotification(): Send not implemented for notification of type: " + type.Name);
                return;
            }
            notification.Send();
        }

        /// <summary>
        /// Send all types of packets
        /// </summary>
        public void Send(Packet packet)
        {
            Transmit(packet);

            if (StreamingApi.IsEnabled)
            {
                if (_job != null)
                    StreamingApi.Transmit(_job.UserId, _job.Channel, packet);
            }
        }

        /// <summary>
        /// Send a message to the _server using ZeroMQ
        /// </summary>
        /// <param name="packet">Packet to transmit</param>
        public void Transmit(Packet packet)
        {
            var payload = JsonConvert.SerializeObject(packet);
            _queue.Enqueue(payload);

            //var message = new NetMQMessage();
            //message.Append(payload);
            //_
            //_server.TrySendMultipartMessage(message);
        }

        /// <summary>
        /// Check if port to be used by the desktop application is available.
        /// </summary>
        private void CheckPort()
        {
            try
            {
                TcpListener tcpListener = new TcpListener(IPAddress.Any, _port.ToInt32());
                tcpListener.Start();
                tcpListener.Stop();
            }
            catch
            {
                throw new Exception("The port configured in config.json is either being used or blocked by a firewall." +
                                    "Please choose a new port or open the port in the firewall.");
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _task?.Wait();
        }
    }
}