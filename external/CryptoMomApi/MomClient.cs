using System;
using System.Threading.Tasks.Dataflow;
using NLog;

namespace MomCrypto.Api
{
    public class MomClient
    {
        protected readonly ILogger logger;
        private readonly MomConnector _connector;
        public MomRspUserLogin? UserInfo;

        protected MomClient(string address, ILogger logger, bool debugMode = false)
        {
            this.logger = logger;
            _connector = new MomConnector(address, logger, debugMode);
            var rspAction = new ActionBlock<MomResponse?>(ProcessResponse_);
            _connector.ReceiveTransformQueue.LinkTo(rspAction, rsp => rsp != null);
            _connector.ReceiveTransformQueue.LinkTo(DataflowBlock.NullTarget<MomResponse?>());
        }

        private void ProcessResponse_(MomResponse? rsp)
        {
            switch (rsp!.MsgId)
            {
                case MomMessageType.Connected:
                    OnConnected?.Invoke();
                    break;
                case MomMessageType.Disconnected:
                    OnDisconnected?.Invoke(rsp.Data.IntValue == 0);
                    break;
                case MomMessageType.RspUserLogin:
                    UserInfo = rsp.Data.AsRspUserLogin!;
                    OnRspUserLogin?.Invoke(UserInfo, rsp.RspInfo);
                    break;
                case MomMessageType.RspError:
                    OnRspError?.Invoke(rsp.RspInfo);
                    break;
            }

            OnResponse?.Invoke(rsp);
            ProcessResponse(rsp);
        }

        protected virtual void ProcessResponse(MomResponse rsp)
        {
        }

        protected void Send(MomRequest request)
        {
            _connector.SendTransformQueue.Post(request);
        }

        public string Address => _connector.Address;
        public bool Connected => _connector.Connected;

        public event Action? OnConnected;
        public event Action<bool>? OnDisconnected;
        public event Action<MomRspUserLogin, MomRspInfo>? OnRspUserLogin;
        public event Action<MomRspInfo>? OnRspError;
        public event Action<MomResponse>? OnResponse;

        public void Init()
        {
            _connector.Connect();
        }

        public void Release()
        {
            _connector.Disconnect();
            OnDisconnected?.Invoke(true);
        }

        public void Login(string username, string password)
        {
            Login(new MomReqUserLogin { UserID = username, Password = password });
        }

        public void Login(MomReqUserLogin req)
        {
            Send(new MomRequest { MsgId = MomMessageType.UserLogin, Data = new MomAny(req) });
        }
    }
}
