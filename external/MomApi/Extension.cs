using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks.Dataflow;
using NetMQ;

namespace Quantmom.Api
{
    public static class Helper
    {
        /// <summary>
        /// 过滤SQL非法字符串
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string GetSafeSql(this string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            value = Regex.Replace(value, @";", string.Empty);
            value = Regex.Replace(value, @"'", string.Empty);
            //value = Regex.Replace(value, @"&", string.Empty);
            value = Regex.Replace(value, @"%20", string.Empty);
            //value = Regex.Replace(value, @"--", string.Empty);
            //value = Regex.Replace(value, @"%", string.Empty);
            value = Regex.Replace(value, @"\\", "\\\\");
            value = Regex.Replace(value, "{", "{{");
            value = Regex.Replace(value, "}", "}}");
            return value;
        }

        public static void DataflowBlockClose(IDataflowBlock block)
        {
            block.Complete();
            block.Completion.Wait();
        }

        /// <summary>
        /// Will remove any trailing zeros for the provided decimal input
        /// </summary>
        /// <param name="input">The <see cref="decimal"/> to remove trailing zeros from</param>
        /// <returns>Provided input with no trailing zeros</returns>
        /// <remarks>Will not have the expected behavior when called from Python,
        /// since the returned <see cref="decimal"/> will be converted to python float,
        /// <see cref="NormalizeToStr"/></remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal Normalize(this decimal input)
        {
            // http://stackoverflow.com/a/7983330/1582922
            return input / 1.000000000000000000000000000000000m;
        }

        /// <summary>
        /// Will remove any trailing zeros for the provided decimal and convert to string.
        /// Uses <see cref="Normalize"/>.
        /// </summary>
        /// <param name="input">The <see cref="decimal"/> to convert to <see cref="string"/></param>
        /// <returns>Input converted to <see cref="string"/> with no trailing zeros</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string NormalizeToStr(this decimal input)
        {
            return Normalize(input).ToString(CultureInfo.InvariantCulture);
        }
    }

    public static class AccountExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PositionOnly(this AccountField account)
        {
            return account.channelType == MomChannelTypeType.PositionOnly;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CapitalPosition(this AccountField account)
        {
            return account.channelType == MomChannelTypeType.CapitalPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFuture(this AccountField account)
        {
            return account.accountType == MomAccountTypeType.Futures;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStock(this AccountField account)
        {
            return account.accountType == MomAccountTypeType.Stocks;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStockOption(this AccountField account)
        {
            return account.accountType == MomAccountTypeType.StockOptions;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CreditAccount(this AccountField account)
        {
            return account.FundChannelType == MomChannelTypeType.CreditAccount;
        }
    }

    public static class DepthMarketDataExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsValid(this MomDepthMarketData data)
        {
            return data.InstrumentIndex >= 0 && !string.IsNullOrEmpty(data.Symbol);
        }

        public static DateTime? GetDateTime(this MomDepthMarketData marketData)
        {
            var updateTime = marketData.UpdateTime.Length == 5 ? $"0{marketData.UpdateTime}" : marketData.UpdateTime;
            var dataTime = marketData.TradingDay + updateTime;

            if (DateTime.TryParseExact(dataTime, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            {
                return dateTime.AddMilliseconds(marketData.UpdateMillisec);
            }

            return null;
        }
    }

    public static class InstrumentHelper
    {
        public const string StocksMarket = "stocks";
        public const string OptionsMarket = "options";
        public const string FuturesMarket = "futures";
        public const string InteractiveBrokersMarket = "ib";

        public static string GetCatsExchange(string exchangeId)
        {
            switch (exchangeId)
            {
                case "XSHE":
                    return "SZ";
                case "XSHG":
                    return "SH";
                case "CCFX":
                    return "CFFEX";
                case "XDCE":
                    return "DCE";
                case "XZCE":
                    return "CZCE";
                case "XSGE":
                    return "SHFE";
                case "XSIE":
                    return "INE";
                default:
                    return exchangeId;
            }
        }

        public static string GetCatsExchange(this MomInstrument instrument)
        {
            return GetCatsExchange(instrument.exchange);
        }

        public static string GetCatsSymbol(this MomInstrument instrument)
        {
            return $"{instrument.ExchangeSymbol}.{GetCatsExchange(instrument.Exchange)}";
        }

        public static string GetHisSymbol(this MomInstrument instrument)
        {
            return $"{GetCatsExchange(instrument.exchange)}{instrument.ExchangeSymbol}".ToLower();
        }

        public static string GetHisMarket(this MomInstrument instrument)
        {
            return GetCatsExchange(instrument.exchange).ToLower() switch
            {
                "sh" => "sse",
                _ => "szse"
            };
        }

        public static string GetHisProduct(this MomInstrument instrument)
        {
            return instrument.market.ToLower() switch
            {
                OptionsMarket => "option",
                _ => "equity"
            };
        }

        public static string GetHisUnderlying(this MomInstrument instrument)
        {
            if (!string.IsNullOrEmpty(instrument.underlyingSymbol))
            {
                var items = instrument.underlyingSymbol.Split('.');
                if (items.Length > 1)
                {
                    return $"{items[1]}{items[0]}".ToLower();
                }
            }
            return instrument.underlyingSymbol;
        }

        public static string GetPutCallName(this MomInstrument instrument)
        {
            return instrument.optionsType == MomOptionsTypeType.CallOptions ? "call" : "put";
        }

        public static string GetUnderlyingExchangeSymbol(this MomInstrument instrument)
        {
            if (!string.IsNullOrEmpty(instrument.underlyingSymbol))
            {
                var items = instrument.underlyingSymbol.Split('.');
                if (items.Length > 1)
                {
                    return items[0];
                }
            }
            return instrument.underlyingSymbol;
        }
    }

    public static class DateHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime? StrToDate(string date)
        {
            if (DateTime.TryParseExact(date, "yyyyMMdd", null, DateTimeStyles.None, out var datetime))
            {
                return datetime;
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToDateStr(DateTime dateTime)
        {
            return dateTime.ToString("yyyyMMdd");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToTimeStr(DateTime dateTime)
        {
            return dateTime.ToString(@"HH\:mm\:ss");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DateToInt(DateTime date)
        {
            return date.Year * 10000 + date.Month * 100 + date.Day;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime IntToDate(int value)
        {
            return new DateTime(value / 10000, (value % 10000) / 100, value % 100);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TimeToInt(string time)
        {
            return int.Parse(time.Replace(":", string.Empty));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TimeToInt(TimeSpan time)
        {
            return time.Hours * 10000 + time.Minutes * 100 + time.Seconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TimeToInt(DateTime time)
        {
            return TimeToInt(time.TimeOfDay);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan IntToTime(int value)
        {
            return new TimeSpan(value / 1000000, value % 1000000 / 10000, (value % 10000) / 100, value % 100);
        }
    }

    public static class PositionExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLong(this PositionField position)
        {
            return position.posiDirection == MomPosiDirectionType.Long;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsShort(this PositionField position)
        {
            return position.posiDirection == MomPosiDirectionType.Short;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLong(this DetailField detail)
        {
            return detail.direction == MomPosiDirectionType.Long;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsShort(this DetailField detail)
        {
            return detail.direction == MomPosiDirectionType.Short;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetOpenClose(this PositionField position, TradeField trade)
        {
            return GetOpenClose(position, trade.direction);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetOpenClose(this PositionField position, byte direction)
        {
            var offsetFlag = MomOffsetFlagType.Open;
            if (position.IsLong() && (direction is MomDirectionType.Sell or MomDirectionType.RepaymentSell)
                || position.IsShort() && direction is MomDirectionType.Buy or MomDirectionType.FinancingBuy)
            {
                offsetFlag = MomOffsetFlagType.Close;
            }

            return offsetFlag;
        }
    }

    public static class TradeExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLong(this TradeField trade)
        {
            return trade.direction is MomDirectionType.Buy or MomDirectionType.FinancingBuy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsShort(this TradeField trade)
        {
            return trade.direction is MomDirectionType.Sell or MomDirectionType.RepaymentSell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAuto(this TradeField trade)
        {
            return trade.offsetFlag == MomOffsetFlagType.AutoOpenClose;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOpen(this TradeField trade)
        {
            return trade.offsetFlag == MomOffsetFlagType.Open;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsClose(this TradeField trade)
        {
            return trade.offsetFlag == MomOffsetFlagType.Close;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCloseToday(this TradeField trade)
        {
            return trade.offsetFlag == MomOffsetFlagType.CloseToday;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBuy(this TradeField trade)
        {
            return trade.direction is MomDirectionType.Buy or MomDirectionType.FinancingBuy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsFinancingBuy(this TradeField trade)
        {
            return trade.direction == MomDirectionType.FinancingBuy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSell(this TradeField trade)
        {
            return trade.direction is MomDirectionType.Sell or MomDirectionType.RepaymentSell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte GetPositionDirection(this TradeField trade)
        {
            switch (trade.direction)
            {
                case MomDirectionType.FinancingBuy:
                case MomDirectionType.Buy:
                    switch (trade.offsetFlag)
                    {
                        case MomOffsetFlagType.Open:
                            return MomPosiDirectionType.Long;
                        default:
                            return MomPosiDirectionType.Short;
                    }
                case MomDirectionType.RepaymentSell:
                case MomDirectionType.Sell:
                    switch (trade.offsetFlag)
                    {
                        case MomOffsetFlagType.Open:
                            return MomPosiDirectionType.Short;
                        default:
                            return MomPosiDirectionType.Long;
                    }
            }
            throw new InvalidException("成交的买卖方向或开平仓错误.");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime GetTradeTime(this TradeField trade)
        {
            return DateHelper.IntToDate(trade.tradeDate).Add(DateHelper.IntToTime(trade.tradeTime));
        }
    }

    public static class OrderExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void AddTrade(this OrderField order, TradeField trade)
        {
            if (order.volumeTraded == 0)
            {
                order.averagePrice = trade.price;
                order.volumeTraded = trade.volume;
            }
            else
            {
                order.averagePrice = (order.volumeTraded * order.averagePrice + trade.volume * trade.price) / (order.volumeTraded + trade.volume);
                order.volumeTraded += trade.volume;
            }

            if (order.IsDone())
            {
                return;
            }

            order.updateTime = trade.tradeTime;
            order.orderStatus = order.volumeTraded == order.volumeTotalOriginal
                ? MomOrderStatusType.AllTraded
                : MomOrderStatusType.PartTradedQueueing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDone(this OrderField order)
        {
            return order.orderStatus
                is MomOrderStatusType.AllTraded
                or MomOrderStatusType.Canceled
                or MomOrderStatusType.PartCanceled
                or MomOrderStatusType.Rejected;
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
        public static bool IsPartCanceled(this OrderField order)
        {
            return order.orderStatus == MomOrderStatusType.PartCanceled;
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
        public static DateTime GetInsertTime(this OrderField order)
        {
            return DateHelper.IntToDate(order.insertDate).Add(DateHelper.IntToTime(order.insertTime));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime GetCancelTime(this OrderField order)
        {
            return DateHelper.IntToDate(order.insertDate).Add(DateHelper.IntToTime(order.cancelTime));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime GetUpdateTime(this OrderField order)
        {
            return DateHelper.IntToDate(order.insertDate).Add(DateHelper.IntToTime(order.updateTime));
        }
    }

    public static class InputOrderExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOpen(this InputOrderField input)
        {
            return input.offsetFlag == MomOffsetFlagType.Open;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsClose(this InputOrderField input)
        {
            return input.offsetFlag == MomOffsetFlagType.Close;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCloseToday(this InputOrderField input)
        {
            return input.offsetFlag == MomOffsetFlagType.CloseToday;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (bool Open, bool Close, bool CloseToday, bool Auto) GetOpenClose(this InputOrderField input)
        {
            var value = input.offsetFlag;
            return (
                value == MomOffsetFlagType.Open,
                value == MomOffsetFlagType.Close,
                value == MomOffsetFlagType.CloseToday,
                value == MomOffsetFlagType.AutoOpenClose);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (bool Buy, bool Sell) GetBuySell(this InputOrderField input)
        {
            return (input.IsBuy(), input.IsSell());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAuto(this InputOrderField input)
        {
            return input.offsetFlag == MomOffsetFlagType.AutoOpenClose;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsBuy(this InputOrderField input)
        {
            return input.direction is MomDirectionType.Buy or MomDirectionType.FinancingBuy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSell(this InputOrderField input)
        {
            return input.direction is MomDirectionType.Sell or MomDirectionType.RepaymentSell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsMarket(this InputOrderField input)
        {
            return input.orderPriceType == MomOrderPriceTypeType.AnyPrice;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InputOrderField SetOffsetFlag(this InputOrderField input, byte value)
        {
            input.offsetFlag = value;
            return input;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InputOrderField SetOpen(this InputOrderField input)
        {
            return SetOffsetFlag(input, MomOffsetFlagType.Open);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InputOrderField SetClose(this InputOrderField input)
        {
            return SetOffsetFlag(input, MomOffsetFlagType.Close);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InputOrderField SetCloseToday(this InputOrderField input)
        {
            return SetOffsetFlag(input, MomOffsetFlagType.CloseToday);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static InputOrderField SetAutoOpenClose(this InputOrderField input)
        {
            return SetOffsetFlag(input, MomOffsetFlagType.AutoOpenClose);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsLimit(this InputOrderField input)
        {
            return input.orderPriceType == MomOrderPriceTypeType.LimitPrice;
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
            return input.orderStatus is MomOrderStatusType.AllTraded
                or MomOrderStatusType.Canceled
                or MomOrderStatusType.Rejected
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
        public static bool IsPartTraded(this InputOrderField input)
        {
            return input.orderStatus == MomOrderStatusType.PartTradedQueueing;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNoSend(this InputOrderField input)
        {
            return input.orderStatus == MomOrderStatusType.NoTradeNotQueueing
                   && input.orderSubmitStatus == MomOrderSubmitStatusType.InsertRejected;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsChecked(this InputOrderField input)
        {
            return input.orderStatus == MomOrderStatusType.Checked
                   && input.orderSubmitStatus == MomOrderSubmitStatusType.InsertRejected;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Buy(this InputOrderField input)
        {
            input.direction = MomDirectionType.Buy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FinancingBuy(this InputOrderField input)
        {
            input.direction = MomDirectionType.FinancingBuy;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Sell(this InputOrderField input)
        {
            input.direction = MomDirectionType.Sell;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RepaymentSell(this InputOrderField input)
        {
            input.direction = MomDirectionType.RepaymentSell;
        }
    }

    public static class InstrumentExtension
    {
        /// <summary>
        /// 允许交易
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
        /// <summary>
        /// 美式期权
        /// </summary>
        private const int EnableAmericanOptionMark = 32;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EnableTrading(this MomInstrument inst)
        {
            return (inst.tradingRules & EnableTradingMark) == EnableTradingMark;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetEnableTrading(this MomInstrument inst, bool value = true)
        {
            if (value)
            {
                inst.tradingRules |= EnableTradingMark;
            }
            else
            {
                inst.tradingRules &= ~EnableTradingMark;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CloseTodayFirst(this MomInstrument inst)
        {
            return (inst.tradingRules & CloseTodayFirstMark) == CloseTodayFirstMark;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCloseTodayFirst(this MomInstrument inst, bool value = true)
        {
            if (value)
            {
                inst.tradingRules |= CloseTodayFirstMark;
            }
            else
            {
                inst.tradingRules &= ~CloseTodayFirstMark;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LockCloseToday(this MomInstrument inst)
        {
            return (inst.tradingRules & LockCloseTodayMark) == LockCloseTodayMark;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetLockCloseToday(this MomInstrument inst, bool value = true)
        {
            if (value)
            {
                inst.tradingRules |= LockCloseTodayMark;
            }
            else
            {
                inst.tradingRules &= ~LockCloseTodayMark;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EnableShort(this MomInstrument inst)
        {
            return (inst.tradingRules & EnableShortMark) == EnableShortMark;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetEnableShort(this MomInstrument inst, bool value = true)
        {
            if (value)
            {
                inst.tradingRules |= EnableShortMark;
            }
            else
            {
                inst.tradingRules &= ~EnableShortMark;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EnableCloseToday(this MomInstrument inst)
        {
            return (inst.tradingRules & EnableCloseTodayMark) == EnableCloseTodayMark;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetEnableCloseToday(this MomInstrument inst, bool value = true)
        {
            if (value)
            {
                inst.tradingRules |= EnableCloseTodayMark;
            }
            else
            {
                inst.tradingRules &= ~EnableCloseTodayMark;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AmericanOption(this MomInstrument inst)
        {
            return (inst.tradingRules & EnableAmericanOptionMark) == EnableAmericanOptionMark;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetAmericanOption(this MomInstrument inst, bool value = true)
        {
            if (value)
            {
                inst.tradingRules |= EnableAmericanOptionMark;
            }
            else
            {
                inst.tradingRules &= ~EnableAmericanOptionMark;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DateTime GetExpiredDateTime(this MomInstrument instrument)
        {
            if (string.IsNullOrEmpty(instrument.expireDate))
            {
                return DateTime.MaxValue;
            }

            var format = instrument.expireDate.Length switch
            {
                14 => "yyyyMMddHHmmss",
                19 => "yyyy-MM-ddTHH:mm:ss",
                _ => "yyyyMMdd"
            };

            return DateTime.TryParseExact(instrument.expireDate, format, null, DateTimeStyles.None, out var datetime)
                ? datetime
                : DateTime.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCall(this MomInstrument inst)
        {
            return inst.optionsType == MomOptionsTypeType.CallOptions;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPut(this MomInstrument inst)
        {
            return inst.optionsType == MomOptionsTypeType.PutOptions;
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
        public static bool IsOption(this MomInstrument instrument)
        {
            return instrument.productClass switch
            {
                MomProductClassType.FuturesOptions => true,
                MomProductClassType.IndexOptions => true,
                MomProductClassType.Options => true,
                _ => false
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStock(this MomInstrument instrument)
        {
            return instrument.productClass switch
            {
                MomProductClassType.Stocks => true,
                MomProductClassType.Etf => true,
                _ => false
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEtf(this MomInstrument instrument)
        {
            return instrument.productClass switch
            {
                MomProductClassType.Etf => true,
                _ => false
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool NetPosition(this MomInstrument instrument)
        {
            return instrument.positionType == MomPositionTypeType.Net;
        }
    }

    public static class ConstantHelper
    {
        private const string Undefined = "__Undefined__";
        private class CacheItem
        {
            public readonly IDictionary<byte, string> NameMap = new Dictionary<byte, string>(10);
            public readonly IDictionary<string, byte> ValueMap = new Dictionary<string, byte>(10);
        }

        private static readonly Dictionary<Type, CacheItem> Cache = new Dictionary<Type, CacheItem>(10);

        static ConstantHelper()
        {
            RegisterType(typeof(MomPosiDirectionType));
            RegisterType(typeof(MomProductClassType));
            RegisterType(typeof(MomOrderStatusType));
            RegisterType(typeof(MomOrderSubmitStatusType));
            RegisterType(typeof(MomDirectionType));
            RegisterType(typeof(MomOffsetFlagType));
            RegisterType(typeof(MomTimeConditionType));
            RegisterType(typeof(MomVolumeConditionType));
            RegisterType(typeof(MomOrderPriceTypeType));
            RegisterType(typeof(MomContingentConditionType));
            RegisterType(typeof(MomTradeTypeType));
            RegisterType(typeof(MomTradeSourceType));
        }

        public static void RegisterType(Type type)
        {
            var item = new CacheItem();
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (var field in fields)
            {
                var description = field.GetCustomAttribute<DescriptionAttribute>();
                if (description == null)
                {
                    continue;
                }

                var value = (byte)field.GetRawConstantValue();
                item.NameMap.Add(value, description.Description);
                item.ValueMap.Add(description.Description, value);
            }
            Cache[type] = item;
        }

        public static List<byte> GetValues<T>()
        {
            var type = typeof(T);
            if (Cache.TryGetValue(type, out var item))
            {
                return item.ValueMap.Values.ToList();
            }
            var list = new List<byte>();
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (var field in fields)
            {
                list.Add((byte)field.GetRawConstantValue());
            }
            return list;
        }

        public static byte GetValue<T>(string name)
        {
            var type = typeof(T);
            if (Cache.TryGetValue(type, out var item))
            {
                if (item.ValueMap.TryGetValue(name, out var value))
                {
                    return value;
                }
                throw new IndexOutOfRangeException($"not found {name} in {typeof(T).Name}");
            }

            var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (var field in fields)
            {
                var description = field.GetCustomAttribute<DescriptionAttribute>();
                if (description != null && name == description.Description)
                {
                    return (byte)field.GetRawConstantValue();
                }
            }
            throw new IndexOutOfRangeException($"not found {name} in {typeof(T).Name}");
        }

        public static List<byte> GetValues<T>(string names)
        {
            var items = names.Split(',');
            var type = typeof(T);
            if (Cache.TryGetValue(type, out var cache))
            {
                return items
                    .Select(name =>
                    {
                        if (cache.ValueMap.TryGetValue(name, out var value))
                        {
                            return value;
                        }
                        throw new IndexOutOfRangeException($"not found {name} in {typeof(T).Name}");
                    }).ToList();
            }

            var list = new List<byte>();
            foreach (var name in items)
            {
                list.Add(GetValue<T>(name));
            }
            return list;
        }

        public static IDictionary<byte, string> GetNames<T>()
        {
            var type = typeof(T);
            if (Cache.TryGetValue(type, out var item))
            {
                return item.NameMap;
            }

            var names = new Dictionary<byte, string>();
            var fields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            foreach (var field in fields)
            {
                var description = field.GetCustomAttribute<DescriptionAttribute>();
                if (description != null)
                {
                    names.Add((byte)field.GetRawConstantValue(), description.Description);
                }
            }

            return names;
        }

        public static string GetName<T>(byte value)
        {
            var names = GetNames<T>();
            return !names.TryGetValue(value, out var name) ? Undefined : name;
        }

        public static string GetNames<T>(IEnumerable<byte> values)
        {
            var names = GetNames<T>();
            return string.Join(",", values.Select(value => !names.TryGetValue(value, out var name) ? Undefined : name));
        }

        public static string GetNames<T>(params byte[] values)
        {
            return GetNames<T>((IEnumerable<byte>)values);
        }
    }
}
