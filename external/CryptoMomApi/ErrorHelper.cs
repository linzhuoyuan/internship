using System.Runtime.CompilerServices;

namespace MomCrypto.Api
{
    public static class ErrorHelper
    {
        public static MomRspInfo Error(string msg, int id = -1)
        {
            return new() { ErrorID = id, ErrorMsg = msg };
        }

        public static readonly MomRspInfo Ok = new() { ErrorID = 0, ErrorMsg = "OK" };
        public static readonly MomRspInfo InternalError = Error("内部错误");
        public static readonly MomRspInfo InvalidLogin = Error("不合法的登录", 3);
        public static readonly MomRspInfo SingleUserSessionExceedLimit = Error("用户会话连接数超出上限");
        public static readonly MomRspInfo NotLoginYet = Error("还没有登录", 6);
        public static readonly MomRspInfo BadField = Error("报单字段有误", 15);
        public static readonly MomRspInfo BadPriceField = Error("报单价格为零", 98);
        public static readonly MomRspInfo InstrumentNotFound = Error("找不到合约", 16);
        public static readonly MomRspInfo AccountNotFound = Error("系统没有为用户分配交易账户", 17);
        public static readonly MomRspInfo ChannelNotFound = Error("系统没有为用户分配交易通道", 18);
        public static readonly MomRspInfo ChannelIndexError = Error("交易通道索引错误", 19);
        public static readonly MomRspInfo DuplicateSubscribe = Error("重复订阅", 1);
        public static readonly MomRspInfo InstrumentNotTrading = Error("合约不能交易", 2);
        public static readonly MomRspInfo UserNotFound = Error("找不到该用户", 11);
        public static readonly MomRspInfo UserNotActive = Error("用户不活跃", 4);
        public static readonly MomRspInfo OverClosePosition = Error("平仓量超过持仓量", 30);
        public static readonly MomRspInfo InsufficientMoney = Error("资金不足", 31);
        public static readonly MomRspInfo NoValidTraderAvailable = Error("交易通道不可用", 21);
        public static readonly MomRspInfo OverRequestPerSecond = Error("每秒发送请求数超过许可数", 41);
        public static readonly MomRspInfo OrderNotFound = Error("撤单找不到相应报单", 25);
        public static readonly MomRspInfo ShortSell = Error("现货交易不能卖空", 37);
        public static readonly MomRspInfo UnsupportedFunction = Error("不支持的功能", 27);
        public static readonly MomRspInfo DuplicateOrderRef = Error("不允许重复报单", 22);
        public static readonly MomRspInfo PriceOverRange = Error("价格超出涨跌停", 23);
        public static readonly MomRspInfo UnsuitableOrderStatus = Error("报单已全成交或已撤销，不能再撤", 26);
        public static readonly MomRspInfo MarketClosed = Error("交易市场已关闭", 99);
        public static readonly MomRspInfo OrderCancelPending = Error("不允许重复撤单", 97);


        public static MomResponse NewRspError(byte[] identity, MomRspInfo info)
        {
            return new()
            {
                Identity = identity,
                MsgId = MomMessageType.RspError,
                RspInfo = info,
                Last = true
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOk(MomRspInfo rsp)
        {
            return rsp.ErrorID == 0;
        }
    }
}
