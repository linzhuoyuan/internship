using System;
using System.Globalization;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Securities;
using Quantmom.Api;
using Convert = System.Convert;
using Order = QuantConnect.Orders.Order;

namespace QuantConnect.Brokerages.Mom
{
    public partial class MomBrokerage
    {
        /// <summary>
        /// QC买卖方向转换为MOM买卖方向
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        public static byte GetMomDirection(Order order)
        {
            switch (order.Direction)
            {
                case OrderDirection.Buy:
                    return MomDirectionType.Buy;
                case OrderDirection.Sell:
                    return MomDirectionType.Sell;
            }

            throw new ArgumentException($"MOM OrderSide Not Supported :{order.Direction}");
        }

        /// <summary>
        /// MOM买卖方向转为QC买卖方向
        /// </summary>
        /// <param name="momDirection"></param>
        /// <returns></returns>
        public static OrderDirection GetQCDirection(byte momDirection)
        {
            switch (momDirection)
            {
                case MomDirectionType.Buy:
                    return OrderDirection.Buy;
                case MomDirectionType.Sell:
                    return OrderDirection.Sell;
            }

            throw new ArgumentException($"QC OrderSide Not Supported:{momDirection}");
        }

        public static byte GetMomOrderOffset(Order order)
        {
            var type = order.SecurityType;
            if (type is SecurityType.Option or SecurityType.Future)
            {
                return order.Offset switch
                {
                    OrderOffset.None => MomOffsetFlagType.AutoOpenClose,
                    OrderOffset.Open => MomOffsetFlagType.Open,
                    OrderOffset.Close => MomOffsetFlagType.Close,
                    OrderOffset.CloseT0 => MomOffsetFlagType.CloseToday,
                    _ => throw new ArgumentException(
                        $" MOM OrderOffset Not Supported:Direction {type} {order.Direction}  Offset {order.Offset}")
                };
            }

            if (type == SecurityType.Equity)
            {
                return MomOffsetFlagType.AutoOpenClose;
            }

            throw new ArgumentException($" MOM OrderOffset Not Supported:Direction {type} {order.Direction}  Offset {order.Offset}");
        }

        public static OrderOffset GetQCOrderOffset(MomOrder order)
        {
            switch (order.OffsetFlag)
            {
                case MomOffsetFlagType.Open:
                    return OrderOffset.Open;
                case MomOffsetFlagType.Close:
                case MomOffsetFlagType.CloseToday:
                    return OrderOffset.Close;
                default:
                    return OrderOffset.None;
            }
        }

        public static OrderOffset GetQCTradeOffset(MomTrade trade)
        {
            switch (trade.OffsetFlag)
            {
                case MomOffsetFlagType.Open:
                    return OrderOffset.Open;
                case MomOffsetFlagType.Close:
                case MomOffsetFlagType.CloseToday:
                    return OrderOffset.Close;
                default:
                    return OrderOffset.None;
            }
        }

        /// <summary>
        /// QC价格类型转为MOM价格类型
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        public static byte GetMomOrderPriceType(Order order)
        {
            switch (order.Type)
            {
                case OrderType.Market:
                    return MomOrderPriceTypeType.AnyPrice;
                case OrderType.Limit:
                    return MomOrderPriceTypeType.LimitPrice;
            }

            throw new ArgumentException($"MOM PriceType Not Supported:{order.Type}");
        }

        /// <summary>
        /// MOM价格类型转为QC价格类型
        /// </summary>
        /// <param name="momOrderPriceType"></param>
        /// <returns></returns>
        public static OrderType GetQCOrderPriceType(byte momOrderPriceType)
        {
            switch (momOrderPriceType)
            {
                case MomOrderPriceTypeType.AnyPrice:
                    return OrderType.Market;
                case MomOrderPriceTypeType.LimitPrice:
                    return OrderType.Limit;
            }

            throw new ArgumentException($"QC PriceType Not Supported:{momOrderPriceType}");
        }

        /// <summary>
        /// MOM订单状态转为QC订单状态
        /// </summary>
        /// <param name="momOrderStatus"></param>
        /// <returns></returns>
        public static OrderStatus GetQCOrderStatus(byte momOrderStatus)
        {
            switch (momOrderStatus)
            {
                case MomOrderStatusType.AllTraded:
                    return OrderStatus.Filled;
                case MomOrderStatusType.PartTradedQueueing:
                case MomOrderStatusType.PartTradedNotQueueing:
                    return OrderStatus.PartiallyFilled;
                case MomOrderStatusType.NoTradeQueueing:
                case MomOrderStatusType.NoTradeNotQueueing:
                    return OrderStatus.Submitted;
                case MomOrderStatusType.Canceled:
                    return OrderStatus.Canceled;
                case MomOrderStatusType.Rejected:
                    return OrderStatus.Invalid;
                case MomOrderStatusType.PartCanceled:
                    return OrderStatus.Canceled;
            }

            throw new ArgumentException($"QC OrderStatus Not Supported:{momOrderStatus}");
        }

        /// <summary>
        /// MOM持仓方向转为QC持仓方向
        /// </summary>
        /// <param name="momDirectionType"></param>
        /// <returns></returns>
        public static SecurityHoldingType GetQCHoldingType(byte momDirectionType)
        {
            switch (momDirectionType)
            {
                case MomPosiDirectionType.Net:
                    return SecurityHoldingType.Net;
                case MomPosiDirectionType.Long:
                    return SecurityHoldingType.Long;
                case MomPosiDirectionType.Short:
                    return SecurityHoldingType.Short;
            }

            throw new ArgumentException($"QC HoldingType Not Supported:{momDirectionType}");
        }

        /// <summary>
        /// MOM产品类型转为QC产品类型
        /// </summary>
        /// <param name="momProductClassType"></param>
        /// <returns></returns>
        public static SecurityType GetQCSecurityType(byte momProductClassType)
        {
            switch (momProductClassType)
            {
                case MomProductClassType.All:
                    return SecurityType.Base;
                case MomProductClassType.Futures:
                    return SecurityType.Future;
                case MomProductClassType.Options:
                case MomProductClassType.FuturesOptions:
                case MomProductClassType.IndexOptions:
                    return SecurityType.Option;
                case MomProductClassType.Stocks:
                case MomProductClassType.Etf:
                    return SecurityType.Equity;

            }

            throw new ArgumentException($"QC SecurityType Not Supported:{momProductClassType}");
        }

        /// <summary>
        /// Mom期权类型转为QC期权类型
        /// </summary>
        /// <param name="momOptionType"></param>
        /// <returns></returns>
        public static OptionRight GetQCOptionRight(byte momOptionType)
        {
            switch (momOptionType)
            {
                case MomOptionsTypeType.CallOptions:
                    return OptionRight.Call;
                case MomOptionsTypeType.PutOptions:
                    return OptionRight.Put;
            }

            return 0;
            //throw new ArgumentException($"MomBrokerage QC:不支持的期权类型:{momOptionType}");
        }

        /// <summary>
        /// Mom交易所转为QC交易所
        /// </summary>
        /// <param name="momExchangeId"></param>
        /// <returns></returns>
        public static string ConvertQcExchangeId(string momExchangeId)
        {
            switch (momExchangeId)
            {
                case "SMART":
                    return Market.USA;
                case "XSHE":
                case "SZ":
                    return Market.SZSE;
                case "XSHG":
                case "SH":
                    return Market.SSE;
                case "SEHKSZSE":
                case "SEHKNTL":
                case "CHINEXT":
                    return Market.HKA;
                case "SEHK":
                    return Market.HKG;
            }

            // 根据MOM市场定义扩展
            return Market.CFFEX;
        }

        /// <summary>
        /// mom行情转tick
        /// </summary>
        /// <param name="marketData"></param>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public Tick ConvertTick(MomDepthMarketData marketData, Symbol symbol)
        {
            var tick = new Tick();
            tick.Symbol = symbol;

            var lastPrice = Convert.ToDecimal(marketData.LastPrice);
            if (lastPrice > 0)
            {
                tick.Value = lastPrice;
                tick.Quantity = Convert.ToDecimal(marketData.Volume);
            }

            // 包含成交和行情，不知用哪个
            tick.TickType = symbol.ID.SecurityType == SecurityType.Option ? TickType.Quote : TickType.Trade;
            tick.Time = marketData.GetDateTime() ?? DateTime.UtcNow;

            var bidPrice1 = Convert.ToDecimal(marketData.BidPrice1);
            if (bidPrice1 > 0)
            {
                tick.BidPrice = bidPrice1;
                tick.BidSize = Convert.ToDecimal(marketData.BidVolume1);

                tick.BidPrice1 = tick.BidPrice;
                tick.BidSize1 = tick.BidSize;
            }

            var askPrice1 = Convert.ToDecimal(marketData.AskPrice1);
            if (askPrice1 > 0)
            {
                tick.AskPrice = askPrice1;
                tick.AskSize = Convert.ToDecimal(marketData.AskVolume1);
                tick.AskPrice1 = tick.BidPrice;
                tick.AskSize1 = tick.BidSize;
            }

            var bidPrice2 = Convert.ToDecimal(marketData.BidPrice2);
            if (bidPrice2 > 0)
            {
                tick.BidPrice2 = bidPrice2;
                tick.BidSize2 = Convert.ToDecimal(marketData.BidVolume2);
            }

            var askPrice2 = Convert.ToDecimal(marketData.AskPrice2);
            if (askPrice2 > 0)
            {
                tick.AskPrice2 = askPrice2;
                tick.AskSize2 = Convert.ToDecimal(marketData.AskVolume2);
            }

            var bidPrice3 = Convert.ToDecimal(marketData.BidPrice3);
            if (bidPrice3 > 0)
            {
                tick.BidPrice3 = bidPrice3;
                tick.BidSize3 = Convert.ToDecimal(marketData.BidVolume3);
            }

            var askPrice3 = Convert.ToDecimal(marketData.AskPrice3);
            if (askPrice3 > 0)
            {
                tick.AskPrice3 = askPrice3;
                tick.AskSize3 = Convert.ToDecimal(marketData.AskVolume3);
            }

            var bidPrice4 = Convert.ToDecimal(marketData.BidPrice4);
            if (bidPrice4 > 0)
            {
                tick.BidPrice4 = bidPrice4;
                tick.BidSize4 = Convert.ToDecimal(marketData.BidVolume4);
            }

            var askPrice4 = Convert.ToDecimal(marketData.AskPrice4);
            if (askPrice4 > 0)
            {
                tick.AskPrice4 = askPrice4;
                tick.AskSize4 = Convert.ToDecimal(marketData.AskVolume4);
            }

            var bidPrice5 = Convert.ToDecimal(marketData.BidPrice5);
            if (bidPrice5 > 0)
            {
                tick.BidPrice5 = bidPrice5;
                tick.BidSize5 = Convert.ToDecimal(marketData.BidVolume5);
            }

            var askPrice5 = Convert.ToDecimal(marketData.AskPrice5);
            if (askPrice5 > 0)
            {
                tick.AskPrice5 = askPrice5;
                tick.AskSize5 = Convert.ToDecimal(marketData.AskVolume5);
            }

            return tick;
        }


        /// <summary>
        /// mom持仓转为QC持仓
        /// </summary>
        /// <param name="momPosition"></param>
        /// <param name="symbol"></param>
        /// <param name="factor"></param>
        /// <param name="lastPrice"></param>
        /// <returns></returns>
        public static Holding ConvertPosition(MomPosition momPosition, Symbol symbol, decimal lastPrice = 0m)
        {
            var holding = new Holding();

            holding.Symbol = symbol;
            holding.Type = symbol.SecurityType;
            holding.HoldingType = GetQCHoldingType(momPosition.PosiDirection);
            holding.CurrencySymbol = "¥";

            var multi = 1;
            if (holding.HoldingType == SecurityHoldingType.Short)
            {
                multi = -1;
            }

            holding.Quantity = multi * momPosition.Position;
            holding.QuantityT0 = multi * momPosition.TodayPosition;
            holding.MarketPrice = lastPrice;
            holding.AveragePrice = momPosition.OpenCost;
            holding.RealizedPnL = momPosition.RealizedPnL;
            holding.Commission = momPosition.Commission;
            return holding;
        }

        public static TradeRecord ConvertTrade(MomTrade trade, Order order)
        {
            var item = new TradeRecord();
            item.Symbol = order.Symbol;
            item.TradeId = trade.TradeId;
            item.OrderId = trade.OrderSysId;
            item.Status = order.Status;
            item.Direction = order.Direction;
            item.Offset = GetQCTradeOffset(trade);
            var multi = 1;
            if (order.Direction == OrderDirection.Sell)
            {
                multi = -1;
            }
            item.Time = trade.GetTradeTime();
            item.Amount = trade.Volume * multi;
            item.Price = Convert.ToDecimal(trade.Price);
            item.Fee = new OrderFee(new CashAmount(Convert.ToDecimal(trade.Commission), "USD"));

            return item;
        }


    }
}
