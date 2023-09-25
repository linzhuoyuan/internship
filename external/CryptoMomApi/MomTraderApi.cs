using System;
using NLog;

namespace MomCrypto.Api
{
    public class MomTraderApi : MomClient
    {
        public MomTraderApi(string address, ILogger logger, bool debugMode = false)
            : base(address, logger, debugMode)
        {

        }

        public event Action<MomInstrument, bool>? InstrumentReady;
        public event Action<MomInstrument, bool>? InstrumentListed;
        public event Action<MomInstrument, bool>? InstrumentExpired;
        public event Action<MomInputOrder, MomRspInfo>? RspInputOrder;
        public event Action<MomInputOrderAction, MomRspInfo>? RspOrderAction;
        public event Action<MomOrder, uint>? ReturnOrder;
        public event Action<MomTrade, uint>? ReturnTrade;
        public event Action<MomAccount>? ReturnAccount;
        public event Action<MomFundAccount>? ReturnFundAccount;
        public event Action<MomPosition>? ReturnPosition;
        public event Action<MomOrder?, MomRspInfo, bool>? RspQryOrder;
        public event Action<MomTrade?, MomRspInfo, bool>? RspQryTrade;
        public event Action<MomPosition?, MomRspInfo, bool>? RspQryPosition;
        public event Action<MomAccount?, MomRspInfo, bool>? RspQryAccount;
        public event Action<MomFundOrder?, MomRspInfo, bool>? RspQryExchangeOrder;
        public event Action<MomFundPosition?, MomRspInfo, bool>? RspQryExchangePosition;
        public event Action<MomFundAccount?, MomRspInfo, bool>? RspQryExchangeAccount;
        public event Action<MomRspInfo>? RspChangeLeverage;
        public event Action<MomCashJournal?, MomRspInfo>? RspCashJournal;

        protected override void ProcessResponse(MomResponse rsp)
        {
            try
            {
                switch (rsp.MsgId)
                {
                    case MomMessageType.RspQryInstrument:
                        InstrumentReady?.Invoke(rsp.Data.AsInstrument!, rsp.Last);
                        break;
                    case MomMessageType.InstrumentExpired:
                        InstrumentExpired?.Invoke(rsp.Data.AsInstrument!, rsp.Last);
                        break;
                    case MomMessageType.InstrumentListed:
                        InstrumentListed?.Invoke(rsp.Data.AsInstrument!, rsp.Last);
                        break;
                    case MomMessageType.RspInputOrder:
                        RspInputOrder?.Invoke(rsp.Data.AsInputOrder!, rsp.RspInfo);
                        break;
                    case MomMessageType.RspOrderAction:
                        RspOrderAction?.Invoke(rsp.Data.AsInputOrderAction!, rsp.RspInfo);
                        break;
                    case MomMessageType.RtnOrder:
                        ReturnOrder?.Invoke(rsp.Data.AsOrder!, rsp.Index);
                        break;
                    case MomMessageType.RtnTrade:
                        ReturnTrade?.Invoke(rsp.Data.AsTrade!, rsp.Index);
                        break;
                    case MomMessageType.RtnAccount:
                        ReturnAccount?.Invoke(rsp.Data.AsAccount!);
                        break;
                    case MomMessageType.RtnFundAccount:
                        ReturnFundAccount?.Invoke(rsp.Data.AsFundAccount!);
                        break;
                    case MomMessageType.RtnPosition:
                        ReturnPosition?.Invoke(rsp.Data.AsPosition!);
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
                    case MomMessageType.RspQryExchangeOrder:
                        RspQryExchangeOrder?.Invoke(rsp.Data.AsFundOrder, rsp.RspInfo, rsp.Last);
                        break;
                    case MomMessageType.RspQryExchangePosition:
                        RspQryExchangePosition?.Invoke(rsp.Data.AsFundPosition, rsp.RspInfo, rsp.Last);
                        break;
                    case MomMessageType.RspQryExchangeAccount:
                        RspQryExchangeAccount?.Invoke(rsp.Data.AsFundAccount, rsp.RspInfo, rsp.Last);
                        break;
                    case MomMessageType.RspChangeLeverage:
                        RspChangeLeverage?.Invoke(rsp.RspInfo);
                        break;
                    case MomMessageType.RspCashJournal:
                        RspCashJournal?.Invoke(rsp.Data.AsCashJournal, rsp.RspInfo);
                        break;
                }
            }
            catch (Exception e)
            {
                logger.Error(e);
            }

        }

        public void QueryInstrument()
        {
            var req = new MomQryInstrument();
            Send(new MomRequest { MsgId = MomMessageType.QryInstrument, Data = new MomAny(req) });
        }

        public void QueryOrder(string userId, long orderRef = 0)
        {
            var req = new MomQryOrder { UserId = userId, OrderSysId = orderRef.ToString() };
            Send(new MomRequest { MsgId = MomMessageType.QryOrder, Data = new MomAny(req) });
        }

        public void QueryTrade(string userId, long tradeLocalId = 0)
        {
            var req = new MomQryTrade { UserId = userId, TradeId = tradeLocalId.ToString() };
            Send(new MomRequest { MsgId = MomMessageType.QryTrade, Data = new MomAny(req) });
        }

        public void QueryAccount(string userId)
        {
            var req = new MomQryAccount { UserId = userId };
            Send(new MomRequest { MsgId = MomMessageType.QryAccount, Data = new MomAny(req) });
        }

        public void QueryPosition(string userId)
        {
            var req = new MomQryPosition { UserId = userId };
            Send(new MomRequest { MsgId = MomMessageType.QryPosition, Data = new MomAny(req) });
        }

        public void DataSync()
        {
            Send(new MomRequest { MsgId = MomMessageType.DataSync, Data = new MomAny(null) });
        }

        public void InputOrder(MomInputOrder order)
        {
            Send(new MomRequest { MsgId = MomMessageType.InputOrder, Data = new MomAny(order) });
        }

        public void CancelOrder(string userId, long orderRef)
        {
            var field = new MomInputOrderAction { OrderRef = orderRef, UserId = userId };
            Send(new MomRequest { MsgId = MomMessageType.OrderAction, Data = new MomAny(field) });
        }

        public void CancelOrder(MomInputOrderAction action)
        {
            Send(new MomRequest { MsgId = MomMessageType.OrderAction, Data = new MomAny(action) });
        }

        public void QueryExchangeAccount(string exchangeAccountId)
        {
            var req = new MomQryAccount { UserId = exchangeAccountId };
            Send(new MomRequest { MsgId = MomMessageType.QryExchangeAccount, Data = new MomAny(req) });
        }

        public void QueryExchangePosition(string exchangeAccountId)
        {
            var req = new MomQryPosition { UserId = exchangeAccountId };
            Send(new MomRequest { MsgId = MomMessageType.QryExchangePosition, Data = new MomAny(req) });
        }

        public void QueryExchangeOrder(string exchangeAccountId)
        {
            var req = new MomQryOrder { UserId = exchangeAccountId };
            Send(new MomRequest { MsgId = MomMessageType.QryExchangeOrder, Data = new MomAny(req) });
        }

        public void ChangeLeverage(string symbol, int leverage, string exchange = MomMarkets.BinanceFutures)
        {
            var req = new MomChangeLeverage { Exchange = exchange, Symbol = symbol, Leverage = leverage };
            Send(new MomRequest { MsgId = MomMessageType.ChangeLeverage, Data = new MomAny(req) });
        }

        public void Transfer(byte transferType, decimal amount, string userId = "", string market = MomMarkets.Binance, string currency = "USDT")
        {
            var req = new MomCashJournal();
            req.userId = string.IsNullOrEmpty(userId) ? UserInfo.UserID : userId;
            req.operatorId = UserInfo.UserID;
            req.currencyType = currency;
            req.CashJournalType = transferType;
            req.inserted = 0;
            req.actionDate = DateTime.Today.ToString("yyyyMMdd");
            req.market = market;
            req.amount = amount;
            Send(new MomRequest { MsgId = MomMessageType.CashJournal, Data = new MomAny(req) });
        }

        public void SpotToFutures(
            decimal amount,
            string userId = "",
            string market = MomMarkets.Binance,
            string currency = "USDT")
        {
            Transfer(MomCashJournalTypeType.MainToUsdtFuture, amount, userId, market, currency);
        }

        public void FuturesToSpot(
            decimal amount,
            string userId = "",
            string market = MomMarkets.Binance,
            string currency = "USDT")
        {
            Transfer(MomCashJournalTypeType.UsdtFutureToMain, amount, userId, market, currency);
        }
    }
}
