using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks.Dataflow;
using NLog;
using Quantmom.Frontend;

namespace Quantmom.Api
{
    public class MomConnector
    {
        private const int HeartbeatTime = 1;
        private const int CloseTime = 10;
        private readonly HeartbeatChecker _checker;

        protected ILogger logger;

        private void CheckerOnError(Exception e)
        {
            logger.Error(e);
        }

        private void CheckerOnClose()
        {
            logger.Warn("心跳超时退出");
            OnConnectorDisconnected(MomMessageType.Close);
        }

        private void CheckerOnHeartbeat()
        {
            SendPing();
        }

        public MomConnector(string address, ILogger logger, bool debugMode = false)
        {
            this.logger = logger;
            _checker = new HeartbeatChecker(TimeSpan.FromSeconds(HeartbeatTime), TimeSpan.FromSeconds(CloseTime));
            _checker.OnHeartbeat += CheckerOnHeartbeat;
            _checker.OnClose += CheckerOnClose;
            _checker.OnError += CheckerOnError;
            Address = address;
            SendTransformQueue = new TransformBlock<MomRequest, FrontendEvent>(ProcessSend);
            ReceiveTransformQueue = new TransformBlock<FrontendEvent, MomResponse?>(ProcessReceive);
        }

        protected virtual void OnConnectorDisconnected(byte reason)
        {
            Connected = false;
        }

        private static FrontendEvent ProcessSend(MomRequest req)
        {
            return ApiMessage.RequestToEvent(req);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void SendPong()
        {
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void SendPing()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected internal virtual void SendInit()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void OnConnected()
        {
            _checker.Start();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected virtual void OnDisconnected()
        {
            _checker.Stop();
        }

        private MomResponse? ProcessReceive(FrontendEvent e)
        {
            if (e.MsgData.Count == 0 || e.MsgData[0] == null || e.MsgData[0].Length == 0)
            {
                return null;
            }
            
            _checker.Heartbeat(e);

            try
            {
                var type = e.MsgData[0][0];
                switch (type)
                {
                    case MomMessageType.Init:
                        if (Connected)
                        {
                            return null;
                        }
                        Connected = true;
                        OnConnected();
                        return ApiMessage.Connected;
                    case MomMessageType.Pong:
                        return null;
                    case MomMessageType.Ping:
                        SendPong();
                        return null;
                    case MomMessageType.Disconnected:
                        {
                            OnDisconnected();
                            var msg = e.MsgData[0];
                            return new MomResponse
                            {
                                MsgId = msg[0],
                                Last = ApiMessage.ByteToBool(msg[1]),
                                Data = new MomAny(msg[2])
                            };
                        }
                    case MomMessageType.Close:
                        {
                            OnConnectorDisconnected(e.MsgData[0][1]);
                            return null;
                        }
                }
                return ApiMessage.EventToResponse(e);
            }
            catch (Exception exception)
            {
                return new MomResponse
                {
                    RspInfo = new MomRspInfo
                    {
                        ErrorID = -1,
                        ErrorMsg = $"{exception.Message},receive:{BitConverter.ToString(e.MsgData[0])}"
                    },
                    MsgId = MomMessageType.RspError,
                    Last = true
                };
            }
        }

        public virtual void Connect()
        {
            Connected = true;
        }

        public virtual void Disconnect()
        {
            Connected = false;
        }

        public bool Connected { get; protected set; }
        public string Address { get; }

        public readonly TransformBlock<FrontendEvent, MomResponse?> ReceiveTransformQueue;
        public readonly TransformBlock<MomRequest, FrontendEvent> SendTransformQueue;
    }
}
