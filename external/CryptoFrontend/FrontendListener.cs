using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using AsyncIO;
using NetMQ;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using NLog;
using Skyline;

namespace MomCrypto.Frontend
{
    public class FrontendListener : LongRunningTask
    {
        private class FrontendMonitor : IDisposable
        {
            private readonly ILogger _logger;
            private readonly NetMQMonitor _monitor;
            private Task _task;
            private readonly Dictionary<AsyncSocket, string> _sessions = new Dictionary<AsyncSocket, string>(100);

            private void OnSocketErrorEvent(object sender, NetMQMonitorErrorEventArgs e)
            {
                _logger.Info($"{e.Address} {e.SocketEvent} error：{e.ErrorCode}");
            }

            private void OnSocketEvent(object s, NetMQMonitorSocketEventArgs e)
            {
                try
                {
                    if (e.Socket != null)
                    {
                        if (e.SocketEvent == SocketEvents.Accepted)
                        {
                            _sessions[e.Socket] = e.Socket.RemoteEndPoint.ToString();
                        }

                        if (_sessions.TryGetValue(e.Socket, out var remote))
                        {
                            switch (e.SocketEvent)
                            {
                                case SocketEvents.Disconnected:
                                case SocketEvents.Closed:
                                    _sessions.Remove(e.Socket);
                                    break;
                            }
                        }
                        _logger.Info($"{remote} {e.SocketEvent}");
                    }
                    else
                    {
                        _logger.Info($"{e.Address} {e.SocketEvent}");
                    }
                }
                catch (SocketException)
                {
                    _logger.Info($"{e.Address} {e.SocketEvent}");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"{e.SocketEvent}");
                }
            }

            public FrontendMonitor(NetMQSocket target, ILogger logger)
            {
                _logger = logger;
                _monitor = new NetMQMonitor(
                    target,
                    "inproc://mon.inproc_" + Guid.NewGuid().ToString("N"),
                    SocketEvents.All);

                _monitor.Listening += OnSocketEvent;
                _monitor.Accepted += OnSocketEvent;
                _monitor.Disconnected += OnSocketEvent;
                _monitor.AcceptFailed += OnSocketErrorEvent;
                _monitor.CloseFailed += OnSocketErrorEvent;
                _monitor.BindFailed += OnSocketErrorEvent;
            }

            public void Open()
            {
                if (_task == null)
                    _task = _monitor.StartAsync();
            }

            public void Close()
            {
                if (_task != null)
                {
                    _monitor.Stop();
                    _task.Wait();
                    _task = null;
                }
            }

            public void Dispose()
            {
                Close();
                _monitor.Dispose();
            }
        }

        private readonly Stopwatch _sendWatch = new Stopwatch();
        private readonly ILogger _logger;

        public int PerMaxSendBlock = 20;
        public int PerMaxRecvBlock = 20;
        protected TimeSpan pollTimerInterval = TimeSpan.FromMilliseconds(1);
        private readonly bool _debugMode;

        private static void RouterToDealer(IOutgoingSocket router, FrontendEvent e)
        {
            if (e.MsgData.Count == 0) { return; }
            if (e.MsgSize.Count <= e.MsgData.Count)
            {
                for (var i = e.MsgSize.Count; i < e.MsgData.Count; i++)
                {
                    e.MsgSize.Add(e.MsgData[i].Length);
                }
            }

            router.SendFrame(e.Identity, e.Identity.Length, true);
            for (var i = 0; i < e.MsgData.Count; i++)
            {
                router.SendFrame(e.MsgData[i], e.MsgSize[i], i < e.MsgData.Count - 1);
            }
        }

        private static bool ReceiveFormDealer(IReceivingSocket router, List<byte[]> data)
        {
            return router.TryReceiveMultipartBytes(ref data);
        }

        private void ProcessSend(IOutgoingSocket router)
        {
            try
            {
                var sendCount = 0;
                for (var i = 0; i < PerMaxSendBlock; i++)
                {
                    if (SendQueue.TryReceive(out var e))
                    {
                        if (e == null)
                            continue;
                        if (_debugMode)
                        {
                            PrintFrontendEvent(e, true);
                        }
                        ++sendCount;
                        RouterToDealer(router, e);
                    }
                    else
                    {
                        break;
                    }
                }

                if (sendCount == PerMaxSendBlock)
                {
                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"{nameof(ProcessSend)}");
            }
        }

        private void PrintFrontendEvent(FrontendEvent e, bool sendMode)
        {
            var data = e.MsgData[0];
            if (sendMode && data[0] == 11)
            {
                return;
            }

            var sb = new StringBuilder();
            sb.Append(sendMode ? "Send: " : "Recv: ");
            sb.Append(BitConverter.ToString(e.Identity));
            var size = sendMode ? e.MsgSize[0] : data.Length;
            sb.Append(", Len: ");
            sb.Append(size);
            sb.Append(", Data[0-15]: ");
            sb.Append(BitConverter.ToString(data, 0, Math.Min(size, 16)));
            _logger.Debug(sb.ToString());
        }

        private void ProcessReceive(object sender, NetMQSocketEventArgs e)
        {
            try
            {
                var data = new List<byte[]>(5);
                for (var i = 0; i < PerMaxRecvBlock; i++)
                {
                    if (ReceiveFormDealer(e.Socket, data))
                    {
                        var req = new FrontendEvent(data[0]);
                        for (var n = 1; n < data.Count; n++)
                        {
                            req.MsgData.Add(data[n]);
                        }
                        if (_debugMode)
                        {
                            PrintFrontendEvent(req, false);
                        }
                        ReceiveQueue.Post(req);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"{nameof(ProcessReceive)}");
            }
        }

        protected override void Run(CancellationToken ct)
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            try
            {
                using (var router = new RouterSocket())
                using (var monitor = new FrontendMonitor(router, _logger))
                {
                    monitor.Open();
                    try
                    {
                        //Windows only
                        //router.Options.TcpKeepalive = true;
                        //router.Options.TcpKeepaliveIdle = new TimeSpan(0, 0, 1);
                        //router.Options.TcpKeepaliveInterval = new TimeSpan(0, 0, 1);

                        router.ReceiveReady += ProcessReceive;
                        router.Bind(Address);
                        var poller = new NetMQPoller();
                        var timer = new NetMQTimer(pollTimerInterval);
                        timer.Elapsed += (sender, args) =>
                        {
                            ProcessSend(router);
                            if (ct.IsCancellationRequested)
                            {
                                poller.Stop();
                            }
                        };
                        poller.Add(router);
                        poller.Add(timer);
                        poller.Run();
                    }
                    finally
                    {
                        router.Unbind(Address);
                        monitor.Close();
                        router.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
            _logger.Info("FrontendListener Stop");
        }

        public FrontendListener(string address, ILogger logger, bool debugMode = false)
        {
            _logger = logger;
            Address = address;
            _debugMode = debugMode;
            ReceiveQueue = new BufferBlock<FrontendEvent>(DataflowHelper.SpscBlockOptions);
            SendQueue = new BufferBlock<FrontendEvent>(DataflowHelper.SpscBlockOptions);
        }

        public string Address { get; }
        public bool DebugMode => _debugMode;

        public BufferBlock<FrontendEvent> ReceiveQueue { get; }
        public BufferBlock<FrontendEvent> SendQueue { get; }
    }
}
