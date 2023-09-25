using System;
using System.Threading.Tasks.Dataflow;
using NLog;
using Quantmom.Frontend;

namespace Quantmom.Api
{
    public sealed class MomSocketConnector : MomConnector
    {
        private readonly FrontendConnector _connector;

        private void OnConnectorDisconnected(FrontendConnector connector, byte reason)
        {
            if (!Connected)
            {
                return;
            }

            Connected = false;
            connector.ReceiveQueue.Post(ApiMessage.MakeDisconnected(reason));
        }

        private void OnConnectorConnected(FrontendConnector connector)
        {
            SendPing();
        }

        protected override void OnConnectorDisconnected(byte reason)
        {
            OnConnectorDisconnected(_connector, reason);
        }

        protected override void SendPong()
        {
            _connector.SendQueue.Post(ApiMessage.MakePong());
        }

        protected override void SendPing()
        {
            if (Connected)
            {
                _connector.SendQueue.Post(ApiMessage.MakePing());
            }
        }

        protected internal override void SendInit()
        {
            _connector.SendQueue.Post(ApiMessage.MakeInit());
        }

        public MomSocketConnector(string address, ILogger logger, bool debugMode = false)
            : base(address, logger)
        {
            _connector = new FrontendConnector(address, logger, debugMode);
            _connector.OnDisconnected += OnConnectorDisconnected;
            _connector.OnSocketConnected += OnConnectorConnected;
            _connector.ReceiveQueue.LinkTo(ReceiveTransformQueue);
            SendTransformQueue.LinkTo(_connector.SendQueue);
        }

        public override void Connect()
        {
            SendInit();
            _connector.Start();
        }

        public override void Disconnect()
        {
            if (Connected)
            {
                _connector.SendQueue.Post(ApiMessage.MakeClose());
            }

            Connected = false;
            _connector.Stop();
        }
    }
}