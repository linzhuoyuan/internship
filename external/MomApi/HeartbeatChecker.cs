using System;
using System.Threading.Tasks.Dataflow;
using Quantmom.Frontend;
using Timer = System.Timers.Timer;

namespace Quantmom.Api
{
internal class HeartbeatChecker
    {
        private readonly ActionBlock<FrontendEvent?> _actionHeartbeat;
        private readonly Timer _timer;
        private DateTime _lastResponseTime = DateTime.UtcNow;
        private DateTime _lastHeartbeatTime = DateTime.UtcNow;

        private void Do(FrontendEvent? rsp)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (now - _lastHeartbeatTime >= HeartbeatTime)
                {
                    OnHeartbeat?.Invoke();
                    _lastHeartbeatTime = now;
                }

                if (rsp != null)
                {
                    _lastResponseTime = now;
                }
                else
                {
                    if (now - _lastResponseTime > CloseTime)
                    {
                        OnClose?.Invoke();
                    }
                }
            }
            catch (Exception e)
            {
                OnError?.Invoke(e);
            }
        }

        public HeartbeatChecker(TimeSpan heartbeatTime, TimeSpan closeTime)
        {
            HeartbeatTime = heartbeatTime;
            CloseTime = closeTime;
            _actionHeartbeat = new ActionBlock<FrontendEvent?>(Do);
            _timer = new Timer();
            _timer.Elapsed += (_, _) => _actionHeartbeat.Post(null);
            _timer.Interval = HeartbeatTime.TotalMilliseconds;
            _timer.AutoReset = true;
        }

        public TimeSpan HeartbeatTime { get; }
        public TimeSpan CloseTime { get; }

        public event Action OnHeartbeat = null!;
        public event Action OnClose = null!;
        public event Action<Exception> OnError = null!;

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        public void Heartbeat(FrontendEvent? req)
        {
            _actionHeartbeat.Post(req);
        }
    }
}