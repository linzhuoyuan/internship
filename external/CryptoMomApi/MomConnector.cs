using System;
using System.Threading.Tasks.Dataflow;
using MomCrypto.Frontend;
using NLog;

namespace MomCrypto.Api
{
    internal class MomConnector
    {
        private readonly FrontendConnector _connector;
        private IDisposable? _sendLink;

        private readonly ILogger _logger;

        private const int HeartbeatTime = 1;
        private const int CloseTime = 10;
        private readonly HeartbeatChecker _checker;

        private void CheckerOnError(Exception e)
        {
            _logger.Error(e);
        }

        private void CheckerOnClose()
        {
            _logger.Warn("心跳超时退出");
            OnConnectorDisconnected(_connector, MomMessageType.Close);
        }
        
        private void CheckerOnHeartbeat()
        {
            SendPing();
        }
        
        private void SendPong()
        {
            _connector.SendQueue.Post(ApiMessage.MakePong());
        }

        private void SendPing()
        {
            if (Connected)
            {
                _connector.SendQueue.Post(ApiMessage.MakePing());
            }
        }

        private void SendInit()
        {
            _connector.SendQueue.Post(ApiMessage.MakeInit());
        }

        public MomConnector(string address, ILogger logger, bool debugMode = false)
        {
            _logger = logger;
            _checker = new HeartbeatChecker(TimeSpan.FromSeconds(HeartbeatTime), TimeSpan.FromSeconds(CloseTime));
            _checker.OnHeartbeat += CheckerOnHeartbeat;
            _checker.OnClose += CheckerOnClose;
            _checker.OnError += CheckerOnError;
            Address = address;
            _connector = new FrontendConnector(address, logger, debugMode);
            _connector.OnDisconnected += OnConnectorDisconnected;
            _connector.OnSocketConnected += OnConnectorConnected;
            SendTransformQueue = new TransformBlock<MomRequest, FrontendEvent>(ProcessSend);
            ReceiveTransformQueue = new TransformBlock<FrontendEvent, MomResponse?>(ProcessReceive);
            _connector.ReceiveQueue.LinkTo(ReceiveTransformQueue);
        }

        private void OnConnectorDisconnected(FrontendConnector connector, byte reason)
        {
            if (!Connected)
            {
                return;
            }

            Connected = false;
            _connector.ReceiveQueue.Post(ApiMessage.MakeDisconnected(reason));
        }

        private void OnConnectorConnected(FrontendConnector connector)
        {
            SendPing();
        }

        private static FrontendEvent ProcessSend(MomRequest req)
        {
            return ApiMessage.RequestToEvent(req);
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
                            OnConnectorDisconnected(_connector, MomMessageType.Close);
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
                        ErrorMsg = $"{exception.Message}, receive: {BitConverter.ToString(e.MsgData[0])}"
                    },
                    MsgId = MomMessageType.RspError,
                    Last = true
                };
            }
        }

        private void OnDisconnected()
        {
            _sendLink?.Dispose();
            _checker.Stop();
        }

        private void OnConnected()
        {
            _sendLink = SendTransformQueue.LinkTo(_connector.SendQueue);
            _checker.Start();
        }

        public void Connect()
        {
            SendInit();
            _connector.Start();
        }

        public void Disconnect()
        {
            if (Connected)
            {
                _connector.SendQueue.Post(ApiMessage.MakeClose());
            }
            Connected = false;
            _connector.Stop();
        }

        public bool Connected { get; private set; }
        public string Address { get; }

        public readonly TransformBlock<FrontendEvent, MomResponse?> ReceiveTransformQueue;
        public readonly TransformBlock<MomRequest, FrontendEvent> SendTransformQueue;
    }
}
