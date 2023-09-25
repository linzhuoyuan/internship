using System;
using NLog;

namespace MomCrypto.Api
{
    public delegate void MarketDataAction(ref MomDepthMarketData data);
    public class MomMarketDataApi : MomClient
    {
        public MomMarketDataApi(string address, ILogger logger, bool debugMode = false) :
            base(address, logger, debugMode)
        {
        }

        protected override void ProcessResponse(MomResponse rsp)
        {
            switch (rsp.MsgId)
            {
                case MomMessageType.RtnDepthMarketData:
                    {
                        rsp.Data.GetMarketData(out var data);
                        ReturnData?.Invoke(ref data);
                    }
                    break;
                case MomMessageType.RspSubscribe:
                    RspSubscribe?.Invoke(rsp.Data.AsSpecificInstrument, rsp.RspInfo);
                    break;
                case MomMessageType.RspUnsubscribe:
                    RspUnsubscribe?.Invoke(rsp.Data.AsSpecificInstrument, rsp.RspInfo);
                    break;
                default:
                    break;
            }
        }

        public void Subscribe(string[] symbols)
        {
            Send(new MomRequest
            {
                MsgId = MomMessageType.Subscribe,
                Data = new MomAny(symbols)
            });
        }

        public void Subscribe(string symbol)
        {
            Subscribe(new[] { symbol });
        }

        public void Unsubscribe(string[] symbols)
        {
            Send(new MomRequest
            {
                MsgId = MomMessageType.Unsubscribe,
                Data = new MomAny(symbols)
            });
        }

        public void Unsubscribe(string symbol)
        {
            Unsubscribe(new[] { symbol });
        }

        public event MarketDataAction ReturnData;
        public event Action<MomSpecificInstrument, MomRspInfo> RspSubscribe;
        public event Action<MomSpecificInstrument, MomRspInfo> RspUnsubscribe;
    }
}
