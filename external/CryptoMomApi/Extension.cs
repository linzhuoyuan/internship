using System;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security;

namespace MomCrypto.Api
{
    public static class SafeExtensions
    {
        // convert a secure string into a normal plain text string
        public static string ToPlainString(this SecureString secureStr)
        {
            var plainStr = new NetworkCredential(string.Empty, secureStr).Password;
            return plainStr;
        }

        // convert a plain text string into a secure string
        public static SecureString ToSecureString(this string plainStr)
        {
            var secure = new SecureString();
            foreach (var c in plainStr.ToCharArray())
            {
                secure.AppendChar(c);
            }
            secure.MakeReadOnly();
            return secure;
        }
    }

    public static class PositionExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLong(this PositionField position)
        {
            return position.position >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsShort(this PositionField position)
        {
            return position.position < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal GetPosition(this PositionField position)
        {
            return position.ProductClass == MomProductClassType.CoinFutures ? position.cashPosition : position.position;
        }
    }

    public static class TradeExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLong(this TradeField trade)
        {
            return trade.direction == MomDirectionType.Buy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsShort(this TradeField trade)
        {
            return trade.direction == MomDirectionType.Sell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBuy(this TradeField trade)
        {
            return trade.direction == MomDirectionType.Buy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSell(this TradeField trade)
        {
            return trade.direction == MomDirectionType.Sell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal GetVolume(this TradeField trade)
        {
            return trade.ProductClass == MomProductClassType.CoinFutures ? trade.Amount : trade.Volume;
        }
    }

    public static class OrderExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDone(this OrderField order)
        {
            return order.orderStatus
                is MomOrderStatusType.AllTraded
                or MomOrderStatusType.Canceled
                or MomOrderStatusType.PartCanceled
                or MomOrderStatusType.Expired
                or MomOrderStatusType.Rejected;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPartCanceled(this OrderField order)
        {
            return order.orderStatus == MomOrderStatusType.PartCanceled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCancelled(this OrderField order)
        {
            return order.orderStatus == MomOrderStatusType.Canceled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFilled(this OrderField order)
        {
            return order.orderStatus == MomOrderStatusType.AllTraded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPartiallyFilled(this OrderField order)
        {
            return order.orderStatus == MomOrderStatusType.PartTradedQueueing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNoTrade(this OrderField order)
        {
            return order.orderStatus is MomOrderStatusType.NoTradeQueueing or MomOrderStatusType.PartTradedQueueing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMarket(this OrderField order)
        {
            return order.orderPriceType == MomOrderPriceTypeType.AnyPrice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStopMarket(this OrderField order)
        {
            return order.orderPriceType == MomOrderPriceTypeType.StopMarket;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStopOrder(this OrderField input)
        {
            return input.orderPriceType is MomOrderPriceTypeType.StopLimit or MomOrderPriceTypeType.StopMarket;
        }
    }

    public static class InputOrderExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (bool Buy, bool Sell) GetBuySell(this InputOrderField input)
        {
            return (input.direction == MomDirectionType.Buy, input.direction == MomDirectionType.Sell);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBuy(this InputOrderField input)
        {
            return input.direction == MomDirectionType.Buy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSell(this InputOrderField input)
        {
            return input.direction == MomDirectionType.Sell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMarket(this InputOrderField input)
        {
            return input.orderPriceType == MomOrderPriceTypeType.AnyPrice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLimit(this InputOrderField input)
        {
            return input.orderPriceType == MomOrderPriceTypeType.LimitPrice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStopLimit(this InputOrderField input)
        {
            return input.orderPriceType == MomOrderPriceTypeType.StopLimit;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStopMarket(this InputOrderField input)
        {
            return input.orderPriceType == MomOrderPriceTypeType.StopMarket;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLimitOrder(this InputOrderField input)
        {
            return input.orderPriceType is MomOrderPriceTypeType.StopLimit or MomOrderPriceTypeType.LimitPrice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStopOrder(this InputOrderField input)
        {
            return input.orderPriceType is MomOrderPriceTypeType.StopLimit or MomOrderPriceTypeType.StopMarket;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InputOrderField SetLimitPrice(this InputOrderField input, decimal price)
        {
            input.orderPriceType = MomOrderPriceTypeType.LimitPrice;
            input.limitPrice = price;
            return input;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InputOrderField SetMarketPrice(this InputOrderField input)
        {
            input.orderPriceType = MomOrderPriceTypeType.AnyPrice;
            input.limitPrice = 0;
            return input;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDone(this InputOrderField input)
        {
            return input.orderStatus
                is MomOrderStatusType.AllTraded
                or MomOrderStatusType.Canceled
                or MomOrderStatusType.Rejected
                or MomOrderStatusType.Expired
                or MomOrderStatusType.PartCanceled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPartCanceled(this InputOrderField input)
        {
            return input.orderStatus == MomOrderStatusType.PartCanceled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCanceled(this InputOrderField input)
        {
            return input.orderStatus == MomOrderStatusType.Canceled;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRejected(this InputOrderField input)
        {
            return input.orderStatus == MomOrderStatusType.Rejected;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAllTraded(this InputOrderField input)
        {
            return input.orderStatus == MomOrderStatusType.AllTraded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNoSend(this InputOrderField input)
        {
            return input.orderStatus == MomOrderStatusType.NoTradeNotQueueing
                   && input.orderSubmitStatus == MomOrderSubmitStatusType.InsertRejected;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Buy(this InputOrderField input)
        {
            input.direction = MomDirectionType.Buy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sell(this InputOrderField input)
        {
            input.direction = MomDirectionType.Sell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSettlement(this InputOrderField input)
        {
            return IsUserSettlement(input) || IsMomSettlement(input);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsUserSettlement(this InputOrderField input)
        {
            return input.OrderPriceType == MomOrderPriceTypeType.SettlementPrice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMomSettlement(this InputOrderField input)
        {
            return input.OrderPriceType == MomOrderPriceTypeType.MomSettlementPrice;
        }
    }

    public static class InstrumentExtension
    {
        /// <summary>
        /// 可以交易
        /// </summary>
        private const int EnableTradingMark = 1;
        /// <summary>
        /// 优先平今
        /// </summary>
        private const int CloseTodayFirstMark = 2;
        /// <summary>
        /// 锁仓（用反向开仓替换平今）
        /// </summary>
        private const int LockCloseTodayMark = 4;
        /// <summary>
        /// 允许做空
        /// </summary>
        private const int EnableShortMark = 8;
        /// <summary>
        /// 能够平今仓(T+0)
        /// </summary>
        private const int EnableCloseTodayMark = 16;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EnableTrading(this MomInstrument instrument)
        {
            return (instrument.tradingRules & EnableTradingMark) == EnableTradingMark;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetEnableTrading(this MomInstrument instrument, bool value = true)
        {
            if (value)
            {
                instrument.tradingRules |= EnableTradingMark;
            }
            else
            {
                instrument.tradingRules &= ~EnableTradingMark;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CloseTodayFirst(this MomInstrument instrument)
        {
            return (instrument.tradingRules & CloseTodayFirstMark) == CloseTodayFirstMark;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCloseTodayFirst(this MomInstrument instrument, bool value = true)
        {
            if (value)
            {
                instrument.tradingRules |= CloseTodayFirstMark;
            }
            else
            {
                instrument.tradingRules &= ~CloseTodayFirstMark;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LockCloseToday(this MomInstrument instrument)
        {
            return (instrument.tradingRules & LockCloseTodayMark) == LockCloseTodayMark;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetLockCloseToday(this MomInstrument instrument, bool value = true)
        {
            if (value)
            {
                instrument.tradingRules |= LockCloseTodayMark;
            }
            else
            {
                instrument.tradingRules &= ~LockCloseTodayMark;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EnableShort(this MomInstrument instrument)
        {
            return (instrument.tradingRules & EnableShortMark) == EnableShortMark;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetEnableShort(this MomInstrument instrument, bool value = true)
        {
            if (value)
            {
                instrument.tradingRules |= EnableShortMark;
            }
            else
            {
                instrument.tradingRules &= ~EnableShortMark;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EnableCloseToday(this MomInstrument instrument)
        {
            return (instrument.tradingRules & EnableCloseTodayMark) == EnableCloseTodayMark;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetEnableCloseToday(this MomInstrument instrument, bool value = true)
        {
            if (value)
            {
                instrument.tradingRules |= EnableCloseTodayMark;
            }
            else
            {
                instrument.tradingRules &= ~EnableCloseTodayMark;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOption(this MomInstrument instrument)
        {
            return instrument.productClass switch {
                MomProductClassType.CoinOptions => true,
                MomProductClassType.FuturesOptions => true,
                MomProductClassType.IndexOptions => true,
                MomProductClassType.Options => true,
                _ => false
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCall(this MomInstrument instrument)
        {
            return IsOption(instrument) && instrument.optionsType == MomOptionsTypeType.CallOptions;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPut(this MomInstrument instrument)
        {
            return IsOption(instrument) && instrument.optionsType == MomOptionsTypeType.PutOptions;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime GetExpiredDateTime(this MomInstrument instrument)
        {
            if (string.IsNullOrEmpty(instrument.expireDate))
            {
                return DateTime.MaxValue;
            }

            if (instrument.expireDate!.Length == 8)
            {
                return DateTime.TryParseExact(instrument.expireDate, "yyyyMMdd", null, DateTimeStyles.None, out var date)
                    ? date
                    : DateTime.MaxValue;
            }

            return DateTime.TryParseExact(instrument.expireDate, "yyyyMMddHHmmss", null, DateTimeStyles.None, out var datetime)
                ? datetime
                : DateTime.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetExpiredDateTime(this MomInstrument instrument, DateTime datetime)
        {
            instrument.expireDate = datetime.ToString("yyyyMMddHHmmss");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStarted(this MomInstrument instrument)
        {
            return instrument.InstLifePhase == MomInstLifePhaseType.Started;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Started(this MomInstrument instrument)
        {
            instrument.InstLifePhase = MomInstLifePhaseType.Started;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExpired(this MomInstrument instrument)
        {
            return instrument.InstLifePhase == MomInstLifePhaseType.Expired;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Expired(this MomInstrument instrument)
        {
            instrument.InstLifePhase = MomInstLifePhaseType.Expired;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime GetDateTime(this MomDepthMarketData data)
        {
            var updateTime = data.UpdateTime.Length == 5 ? "0" + data.UpdateTime : data.UpdateTime;
            var dataTime = data.ActionDay + updateTime;

            if (DateTime.TryParseExact(dataTime, "yyyyMMddHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            {
                return dateTime;
            }
            return DateTime.Now;
        }
    }

    public static class Convert
    {
        public static byte DirectionToPositionDirection(byte direction)
        {
            return direction switch {
                MomDirectionType.Sell => MomPosiDirectionType.Short,
                _ => MomPosiDirectionType.Long
            };
        }
    }
}
