using System;
using System.ComponentModel;
using NetMQ;
using NetMQ.Sockets;

namespace Monitor.Model.Sessions
{
    public class StreamSession : JsonSession
    {
        private readonly string _host;
        private readonly int _port;

        protected override void DoWork(object sender, DoWorkEventArgs e)
        {
            void SendHeartbeat(IOutgoingSocket socket)
            {
                var msg = new NetMQMessage();
                msg.AppendEmptyFrame();
                msg.Append("heartbeat");
                socket.SendMultipartMessage(msg);
            }

            var lastSendTime = DateTime.Now;
            //using (var socket = new PullSocket(_host + _port))
            using (var dealer = new DealerSocket(_host + _port))
            {
                dealer.Options.Identity = Guid.NewGuid().ToByteArray();
                dealer.ReceiveReady += DealerOnReceiveReady;
                //dealer.Connect();
                SendHeartbeat(dealer);

                var poller = new NetMQPoller();
                var timer = new NetMQTimer(TimeSpan.FromSeconds(1));
                timer.Elapsed += (_, args) =>
                {
                    if (eternalQueueListener.CancellationPending)
                    {
                        poller.Stop();
                    }
                    else
                    {
                        if ((DateTime.Now - lastSendTime).Seconds > 10)
                        {
                            SendHeartbeat(dealer);
                            lastSendTime = DateTime.Now;
                        }
                    }
                };

                poller.Add(dealer);
                poller.Add(timer);
                poller.Run();

                dealer.Close();
                resetEvent.Set();
            }
        }
        
        private void DealerOnReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            var message = new NetMQMessage();
            e.Socket.TryReceiveMultipartMessage(ref message);
            if (message.FrameCount < 2) return;
            var payload = message[1].ConvertToString();
            ProcessPacket(payload);
        }

        public StreamSession(ISessionHandler sessionHandler, IResultConverter resultConverter, StreamSessionParameters parameters) :
            base(sessionHandler, resultConverter)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            closeAfterCompleted = parameters.CloseAfterCompleted;
            _host = parameters.Host;
            _port = parameters.Port;

        }

        public override string Name => $"{_host}:{_port}";

    }
}