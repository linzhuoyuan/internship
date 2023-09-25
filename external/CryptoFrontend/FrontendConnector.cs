using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NetMQ;
using NetMQ.Monitoring;
using NetMQ.Sockets;
using NLog;
using Skyline;

namespace MomCrypto.Frontend
{
    public class FrontendConnector : LongRunningTask
    {
        private class ConnectMonitor : IDisposable
        {
            private readonly NetMQMonitor _monitor;
            private readonly ILogger _logger;
            private Task _task;

            public ConnectMonitor(NetMQSocket target, FrontendConnector service, ILogger logger)
            {
                _logger = logger;
                _monitor = new NetMQMonitor(
                    target,
                    "inproc://mon.inproc_" + Guid.NewGuid().ToString("N"), SocketEvents.All);
                _monitor.Timeout = TimeSpan.FromMilliseconds(1000);
                _monitor.CloseFailed += OnMonitorErrorEvent;
                _monitor.ConnectDelayed += OnMonitorErrorEvent;
                _monitor.ConnectRetried += OnMonitorConnectRetried;
                _monitor.Closed += service.OnDealerClosed;
                _monitor.Connected += service.OnDealerConnected;
                _monitor.Disconnected += service.OnDealerDisconnected;
            }

            private void OnMonitorConnectRetried(object sender, NetMQMonitorIntervalEventArgs e)
            {
                _logger.Debug($"monitor {e.SocketEvent}, interval:{e.Interval}");
            }

            private void OnMonitorErrorEvent(object sender, NetMQMonitorErrorEventArgs e)
            {
                _logger.Debug($"monitor {e.SocketEvent}, error:{e.ErrorCode}");
            }

            private void OnMonitorEvent(object sender, NetMQMonitorSocketEventArgs e)
            {
                _logger.Debug($"monitor {e.SocketEvent}");
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
                    try
                    {
                        _monitor.Stop();
                        _task.Wait();
                    }
                    catch
                    {
                        // ignored
                    }
                    _task = null;
                }
            }

            public void Dispose()
            {
                Close();
                _monitor.Dispose();
            }
        }

        private readonly ILogger _logger;
        private byte[] _identity;
        protected TimeSpan pollTimerInterval = TimeSpan.FromMilliseconds(10);
        public int PerMaxSendBlock = 20;
        public int PerMaxRecvBlock = 20;
        private readonly bool _debugMode;
        private bool _dealerClosed;

        protected void OnDealerDisconnected(object sender, NetMQMonitorSocketEventArgs e)
        {
            _logger.Info($"{e.Address}, socket:{e.Socket.GetHashCode()}, OnDealerDisconnected");
            OnDisconnected?.Invoke(this, 1);
        }

        protected void OnDealerConnected(object sender, NetMQMonitorSocketEventArgs e)
        {
            if (_dealerClosed)
            {
                _dealerClosed = false;
            }
            _logger.Info($"{e.Address}, socket:{e.Socket.GetHashCode()}, OnDealerConnected");
            OnSocketConnected?.Invoke(this);
        }

        private void OnDealerClosed(object sender, NetMQMonitorSocketEventArgs e)
        {
            _dealerClosed = true;
            _logger.Info($"{e.Address}, socket:{e.Socket.GetHashCode()}, OnDealerClosed");
        }

        private static void DealerToRouter(IOutgoingSocket dealer, FrontendEvent e)
        {
            if (e.MsgData.Count == 0) { return; }
            if (e.MsgSize.Count <= e.MsgData.Count)
            {
                for (var i = e.MsgSize.Count; i < e.MsgData.Count; i++)
                {
                    e.MsgSize.Add(e.MsgData[i].Length);
                }
            }
            for (var i = 0; i < e.MsgData.Count; i++)
            {
                dealer.SendFrame(e.MsgData[i], e.MsgSize[i], i < e.MsgData.Count - 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ReceiveFormRouter(IReceivingSocket dealer, List<byte[]> data)
        {
            return dealer.TryReceiveMultipartBytes(ref data);
        }

        private void ProcessSend(IOutgoingSocket dealer)
        {
            try
            {
                for (var i = 0; i < PerMaxSendBlock; i++)
                {
                    if (SendQueue.TryReceive(out var e))
                    {
                        if (_debugMode)
                        {
                            e.Identity = _identity;
                            PrintFrontendEvent(e, true);
                        }
                        DealerToRouter(dealer, e);
                    }
                    else
                    {
                        break;
                    }
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
            if (!sendMode && data[0] == 11)
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

        private void ProcessResponse(object sender, NetMQSocketEventArgs e)
        {
            try
            {
                var data = new List<byte[]>();
                for (var i = 0; i < PerMaxRecvBlock; i++)
                {
                    if (ReceiveFormRouter(e.Socket, data))
                    {
                        var rsp = new FrontendEvent(_identity);
                        rsp.MsgData.AddRange(data);
                        if (_debugMode)
                        {
                            PrintFrontendEvent(rsp, false);
                        }
                        ReceiveQueue.Post(rsp);
                    }
                    else
                    {
                        break;
                    }
                    data.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"{nameof(ProcessResponse)}");
            }
        }

        protected override void Run(CancellationToken ct)
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            try
            {
                using (var dealer = new DealerSocket())
                using (var monitor = new ConnectMonitor(dealer, this, _logger))
                {
                    monitor.Open();
                    _logger.Debug("Connector Monitor Open");
                    //等待Monitor启动
                    Utility.Sleep(500, ct);
                    try
                    {
                        _identity = Guid.NewGuid().ToByteArray();
                        dealer.Options.Identity = _identity;
                        dealer.ReceiveReady += ProcessResponse;
                        dealer.Connect(Address);
                        _logger.Debug("Connector Dealer Connect " + Address);
                        var poller = new NetMQPoller();
                        var timer = new NetMQTimer(pollTimerInterval);
                        timer.Elapsed += (sender, args) =>
                        {
                            ProcessSend(dealer);

                            if (ct.IsCancellationRequested)
                            {
                                poller.Stop();
                            }
                        };

                        poller.Add(dealer);
                        poller.Add(timer);
                        _logger.Debug("Connector Poller Run");
                        poller.Run();
                        ProcessSend(dealer);
                    }
                    finally
                    {
                        _logger.Debug("Connector Monitor Close");
                        monitor.Close();
                        dealer.Disconnect(Address);
                        OnDisconnected?.Invoke(this, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        public FrontendConnector(string address, ILogger logger, bool debugMode = false)
        {
            _logger = logger;
            Address = address;
            _debugMode = debugMode;
            ReceiveQueue = new BufferBlock<FrontendEvent>(DataflowHelper.SpscBlockOptions);
            SendQueue = new BufferBlock<FrontendEvent>(DataflowHelper.SpscBlockOptions);
        }

        public string Address { get; }
        public bool DebugMode => _debugMode;

        public event Action<FrontendConnector, byte> OnDisconnected;
        public event Action<FrontendConnector> OnSocketConnected;

        public BufferBlock<FrontendEvent> ReceiveQueue { get; }
        public BufferBlock<FrontendEvent> SendQueue { get; }
    }
}
