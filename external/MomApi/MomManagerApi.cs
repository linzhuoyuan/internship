using System;
using NLog;

namespace Quantmom.Api
{
    public class MomManagerApi : MomClient
    {
        private const string All = "*";

        protected override void ProcessResponse(MomResponse rsp)
        {
            switch (rsp.MsgId)
            {
                case MomMessageType.RspQryInstrument:
                    InstrumentReady?.Invoke(rsp.Data.AsInstrument, rsp.Last);
                    break;
                case MomMessageType.RtnOrder:
                    ReturnOrder?.Invoke(rsp.Data.AsOrder, rsp.Index);
                    break;
                case MomMessageType.RtnTrade:
                    ReturnTrade?.Invoke(rsp.Data.AsTrade, rsp.Index);
                    break;
                case MomMessageType.RtnAccount:
                    ReturnAccount?.Invoke(rsp.Data.AsAccount);
                    break;
                case MomMessageType.RtnPosition:
                    ReturnPosition?.Invoke(rsp.Data.AsPosition);
                    break;
                case MomMessageType.RtnCheckOrder:
                case MomMessageType.RtnInputOrder:
                    ReturnInputOrder?.Invoke(rsp.Data.AsInputOrder);
                    break;
                case MomMessageType.RspQryUncheckInput:
                    RspQryUncheckInput?.Invoke(rsp.Data.AsInputOrder, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryOrder:
                    RspQryOrder?.Invoke(rsp.Data.AsOrder, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryTrade:
                    RspQryTrade?.Invoke(rsp.Data.AsTrade, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryAccount:
                    RspQryAccount?.Invoke(rsp.Data.AsAccount, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryPosition:
                    RspQryPosition?.Invoke(rsp.Data.AsPosition, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryFundAccount:
                    RspQryFundAccount?.Invoke(rsp.Data.AsFundAccount, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryFundPosition:
                    RspQryFundPosition?.Invoke(rsp.Data.AsFundPosition, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryUser:
                    RspQryUser?.Invoke(rsp.Data.AsUser, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryFund:
                    RspQryFund?.Invoke(rsp.Data.AsFund, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspSubscribeResponse:
                    RspSubscribeResponse?.Invoke(rsp.Data.AsSubscribeResponse, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryTradingChannel:
                    RspQryTradingChannel?.Invoke(rsp.Data.AsTradingChannel, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryTradingRoute:
                    RspQryTradingRoute?.Invoke(rsp.Data.AsTradingRoute, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryRiskExpression:
                    RspQryRiskExpression?.Invoke(rsp.Data.AsRiskExpression, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryPerformance:
                    RspQryPerformance?.Invoke(rsp.Data.AsPerformance, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspQryFundPerformance:
                    RspQryFundPerformance?.Invoke(rsp.Data.AsFundPerformance, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspDataAction:
                    RspDataAction?.Invoke(rsp.Data.AsDataAction, rsp.RspInfo, rsp.Last);
                    break;
                case MomMessageType.RspError:
                    RspError?.Invoke(rsp.RspInfo);
                    break;
            }
        }

        public MomManagerApi(string address, ILogger logger, bool debugMode = false)
            : base(address, logger, debugMode)
        {

        }

        public event Action<MomInstrument?, bool>? InstrumentReady;
        public event Action<MomOrder?, uint>? ReturnOrder;
        public event Action<MomTrade?, uint>? ReturnTrade;
        public event Action<MomAccount?>? ReturnAccount;
        public event Action<MomPosition?>? ReturnPosition;
        public event Action<MomInputOrder?>? ReturnInputOrder;
        public event Action<MomInputOrder?, MomRspInfo, bool>? RspQryUncheckInput;
        public event Action<MomOrder?, MomRspInfo, bool>? RspQryOrder;
        public event Action<MomTrade?, MomRspInfo, bool>? RspQryTrade;
        public event Action<MomAccount?, MomRspInfo, bool>? RspQryAccount;
        public event Action<MomPosition?, MomRspInfo, bool>? RspQryPosition;
        public event Action<MomFundAccount?, MomRspInfo, bool>? RspQryFundAccount;
        public event Action<MomFundPosition?, MomRspInfo, bool>? RspQryFundPosition;
        public event Action<MomUser?, MomRspInfo, bool>? RspQryUser;
        public event Action<MomFund?, MomRspInfo, bool>? RspQryFund;
        public event Action<MomSubscribeResponse?, MomRspInfo, bool>? RspSubscribeResponse;
        public event Action<MomTradingChannel?, MomRspInfo, bool>? RspQryTradingChannel;
        public event Action<MomTradingRoute?, MomRspInfo, bool>? RspQryTradingRoute;
        public event Action<MomRiskExpression?, MomRspInfo, bool>? RspQryRiskExpression;
        public event Action<MomPerformance?, MomRspInfo, bool>? RspQryPerformance;
        public event Action<MomFundPerformance?, MomRspInfo, bool>? RspQryFundPerformance;
        public event Action<MomDataAction?, MomRspInfo, bool>? RspDataAction;
        public event Action<MomRspInfo>? RspError;
        
        public void SaveData(MomDataAction action)
        {
            Send(new MomRequest { MsgId = MomMessageType.DataAction, Data = new MomAny(action) });
        }

        public void QueryInstrument()
        {
            var req = new MomQryInstrument();
            Send(new MomRequest { MsgId = MomMessageType.QryInstrument, Data = new MomAny(req) });
        }

        public void QueryFund()
        {
            Send(new MomRequest { MsgId = MomMessageType.QryFund, Data = MomAny.Empty });
        }

        public void QueryUser()
        {
            Send(new MomRequest { MsgId = MomMessageType.QryUser, Data = MomAny.Empty });
        }

        public void QueryOrder(DateTime? starTime, DateTime? endTime) => QueryOrder(All, starTime, endTime);

        public void QueryOrder(string userId, DateTime? starDate, DateTime? endDate)
        {
            var req = new MomQryOrder
            {
                OwnerId = userId,
                InsertTimeStart = DateHelper.DateToInt(starDate ?? DateTime.Today),
                InsertTimeEnd = DateHelper.DateToInt(endDate ?? DateTime.Today.AddDays(1))
            };
            Send(new MomRequest { MsgId = MomMessageType.QryOrder, Data = new MomAny(req) });
        }

        public void QueryTrade(DateTime? starTime, DateTime? endTime) => QueryTrade(All, starTime, endTime);

        public void QueryTrade(string userId, DateTime? starDate, DateTime? endDate)
        {
            var req = new MomQryTrade
            {
                OwnerId = userId,
                TradeTimeStart = DateHelper.DateToInt(starDate ?? DateTime.Today),
                TradeTimeEnd = DateHelper.DateToInt(endDate ?? DateTime.Today.AddDays(1))
            };
            Send(new MomRequest { MsgId = MomMessageType.QryTrade, Data = new MomAny(req) });
        }

        public void QueryAccount(string userId = All)
        {
            var req = new MomQryAccount { OwnerId = userId };
            Send(new MomRequest { MsgId = MomMessageType.QryAccount, Data = new MomAny(req) });
        }

        public void QueryFundAccount(string accountId = All)
        {
            var req = new MomQryAccount { OwnerId = accountId };
            Send(new MomRequest { MsgId = MomMessageType.QryFundAccount, Data = new MomAny(req) });
        }

        public void QueryFundPosition(string accountId = All)
        {
            var req = new MomQryPosition { OwnerId = accountId };
            Send(new MomRequest { MsgId = MomMessageType.QryFundPosition, Data = new MomAny(req) });
        }

        public void QueryPosition(string userId = All)
        {
            var req = new MomQryPosition { OwnerId = userId };
            Send(new MomRequest { MsgId = MomMessageType.QryPosition, Data = new MomAny(req) });
        }

        public void SubscribeResponse(string[] users, string[] fundAccounts)
        {
            var req = new MomSubscribeResponse { UserIdList = users, FundAccountIdList = fundAccounts };
            Send(new MomRequest { MsgId = MomMessageType.SubscribeResponse, Data = new MomAny(req) });
        }

        public void QueryTradingChannel()
        {
            Send(new MomRequest { MsgId = MomMessageType.QryTradingChannel, Data = new MomAny(new MomQryField()) });
        }

        public void QueryTradingRoute()
        {
            Send(new MomRequest { MsgId = MomMessageType.QryTradingRoute, Data = new MomAny(new MomQryField()) });
        }

        public void QueryRiskExpression()
        {
            Send(new MomRequest { MsgId = MomMessageType.QryRiskExpression, Data = new MomAny(new MomQryField()) });
        }

        public void QueryPerformance(string accountId)
        {
            var req = new MomQryPerformance { AccountId = accountId };
            Send(new MomRequest { MsgId = MomMessageType.QryPerformance, Data = new MomAny(req) });
        }

        public void QueryFundPerformance(string accountId)
        {
            var req = new MomQryPerformance { AccountId = accountId };
            Send(new MomRequest { MsgId = MomMessageType.QryFundPerformance, Data = new MomAny(req) });
        }

        public void QueryUncheckInput(string userId)
        {
            var req = new MomQryUncheckInput { OwnerId = userId };
            Send(new MomRequest { MsgId = MomMessageType.QryUncheckInput, Data = new MomAny(req) });
        }
    }
}