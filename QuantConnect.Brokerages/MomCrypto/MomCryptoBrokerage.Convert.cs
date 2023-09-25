using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using MomCrypto.Api;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Securities;
using Order = QuantConnect.Orders.Order;

namespace QuantConnect.Brokerages.MomCrypto
{
    /// <summary>
    /// 
    /// </summary>
    public partial class MomCryptoBrokerage
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

            throw new ArgumentException($"MOMDeribit OrderSide Not Supported :{order.Direction}");
        }

        /// <summary>
        /// MOM买卖方向转为QC买卖方向
        /// </summary>
        /// <param name="momDirection"></param>
        /// <returns></returns>
        public static OrderDirection GetDirection(byte momDirection)
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

        /// <summary>
        /// QC开平转为MOM开平
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        public static byte GetMomOrderOffset(Order order)
        {
            switch (order.Offset)
            {
                case OrderOffset.Open:
                    return MomOffsetFlagType.Open;
                case OrderOffset.Close:
                    {
                        // 如何判断平今平昨？
                        return MomOffsetFlagType.Close;
                    }
            }

            throw new ArgumentException($" MOMDeribit OrderOffset Not Supported:{order.Offset}");
        }

        /// <summary>
        /// MOM开平转为QC开平
        /// </summary>
        /// <param name="momOpenClose"></param>
        /// <returns></returns>
        public static OrderOffset GetOrderOffset(byte momOpenClose)
        {
            switch (momOpenClose)
            {
                case MomOffsetFlagType.Open:
                    return OrderOffset.Open;
                case MomOffsetFlagType.Close:
                case MomOffsetFlagType.CloseToday:
                    return OrderOffset.Close;
            }

            throw new ArgumentException($"QC OrderOffset Not Supported :{momOpenClose}");
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
                case OrderType.StopLimit:
                    return MomOrderPriceTypeType.StopLimit;
                case OrderType.StopMarket:
                    return MomOrderPriceTypeType.StopMarket;
                case OrderType.OptionExercise:
                    return MomOrderPriceTypeType.SettlementPrice;
            }

            throw new ArgumentException($"MomCrypto PriceType Not Supported:{order.Type}");
        }

        /// <summary>
        /// MOM价格类型转为QC价格类型
        /// </summary>
        /// <param name="momOrderPriceType"></param>
        /// <returns></returns>
        public static OrderType GetOrderPriceType(byte momOrderPriceType)
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
        public static OrderStatus GetOrderStatus(byte momOrderStatus)
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
                case MomOrderStatusType.Expired:
                    return OrderStatus.Canceled;
                case MomOrderStatusType.Untriggered:
                    return OrderStatus.Untriggered;
                case MomOrderStatusType.Triggered:
                    return OrderStatus.Triggered;
                case MomOrderStatusType.Closed:
                    return OrderStatus.Closed;

            }

            throw new ArgumentException($"QC OrderStatus Not Supported:{momOrderStatus}");
        }

        /// <summary>
        /// MOM持仓方向转为QC持仓方向
        /// </summary>
        /// <param name="momDirectionType"></param>
        /// <returns></returns>
        public static SecurityHoldingType GetHoldingType(byte momDirectionType)
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
        public static SecurityType GetSecurityType(byte momProductClassType)
        {
            switch (momProductClassType)
            {
                case MomProductClassType.All:
                    return SecurityType.Base;
                case MomProductClassType.Futures:
                case MomProductClassType.CoinFutures:
                    return SecurityType.Future;
                case MomProductClassType.CoinOptions:
                case MomProductClassType.Options:
                case MomProductClassType.FuturesOptions:
                case MomProductClassType.IndexOptions:
                    return SecurityType.Option;
                case MomProductClassType.Stock:
                case MomProductClassType.Etf:
                    return SecurityType.Equity;
                case MomProductClassType.Index:
                    return SecurityType.Crypto;
                case MomProductClassType.Crypto:
                    return SecurityType.Crypto;
            }

            throw new ArgumentException($"QC SecurityType Not Supported:{momProductClassType}");
        }

        /// <summary>
        /// Mom期权类型转为QC期权类型
        /// </summary>
        /// <param name="momOptionType"></param>
        /// <returns></returns>
        public static OptionRight GetOptionRight(byte momOptionType)
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
        /// <param name="market"></param>
        /// <returns></returns>
        public static string ConvertMarket(string market)
        {
            return market;
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
            tick.Value = marketData.LastPrice;
            tick.Quantity = marketData.Volume;
            tick.LocalTime = DateTime.Now;

            // 包含成交和行情，不知用哪个
            if (symbol.SecurityType is SecurityType.Future or SecurityType.Option or SecurityType.Crypto)
            {
                tick.TickType = TickType.Quote;
            }
            else
            {
                tick.TickType = TickType.Trade;
            }

            if (_algorithm!.SimulationMode
                && symbol.SecurityType is SecurityType.Crypto
                && symbol.ID.Market == Market.Deribit)
            {
                tick.TickType = TickType.Trade;
            }

            var updateTime = marketData.UpdateTime.Length == 5 ? "0" + marketData.UpdateTime : marketData.UpdateTime;
            var dataTime = marketData.ActionDay + updateTime;

            tick.Time = DateTime.TryParseExact(dataTime, "yyyyMMddHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime)
                ? DateTime.SpecifyKind(dateTime.AddMilliseconds(marketData.UpdateMillisec), DateTimeKind.Utc)
                : DateTime.UtcNow;

            tick.BidPrice = marketData.BidPrice1;
            tick.BidSize = marketData.BidVolume1;
            tick.AskPrice = marketData.AskPrice1;
            tick.AskSize = marketData.AskVolume1;

            tick.BidPrice1 = marketData.BidPrice1;
            tick.BidSize1 = marketData.BidVolume1;
            tick.AskPrice1 = marketData.AskPrice1;
            tick.AskSize1 = marketData.AskVolume1;

            tick.BidPrice2 = marketData.BidPrice2;
            tick.BidSize2 = marketData.BidVolume2;
            tick.AskPrice2 = marketData.AskPrice2;
            tick.AskSize2 = marketData.AskVolume2;

            tick.BidPrice3 = marketData.BidPrice3;
            tick.BidSize3 = marketData.BidVolume3;
            tick.AskPrice3 = marketData.AskPrice3;
            tick.AskSize3 = marketData.AskVolume3;

            tick.BidPrice4 = marketData.BidPrice4;
            tick.BidSize4 = marketData.BidVolume4;
            tick.AskPrice4 = marketData.AskPrice4;
            tick.AskSize4 = marketData.AskVolume4;

            tick.BidPrice5 = marketData.BidPrice5;
            tick.BidSize5 = marketData.BidVolume5;
            tick.AskPrice5 = marketData.AskPrice5;
            tick.AskSize5 = marketData.AskVolume5;

            tick.MarkPrice = marketData.MarkPrice;
            tick.SettlementPrice = marketData.SettlementPrice;
            tick.AskIV = marketData.AskIV;
            tick.BidIV = marketData.BidIV;
            tick.MarkIV = marketData.MarkIV;
            tick.Delta = marketData.CurrDelta;
            tick.Vega = marketData.Vega;
            tick.Theta = marketData.Theta;
            tick.Rho = marketData.Rho;
            tick.Gamma = marketData.Gamma;

            return tick;
        }

        private static DateTime MomStringToDateTime(string date, string time)
        {
            if (time == null)
            {
                return DateTime.MinValue;
            }

            var dataTime = date + time;

            if (!DateTime.TryParseExact(dataTime, "yyyy-MM-ddHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
            {
                throw new Exception("时间格式不对");
            }

            return dateTime;
        }

        private static Holding ConvertExchangePosition(PositionField momPosition)
        {
            var deribit = true;// momPosition.ExchangeSymbol.IndexOf(".") < 0;
            var holding = new Holding();

            // 没有字段用此字段返回合约代码
            holding.CurrencySymbol = momPosition.ExchangeSymbol;
            if (deribit)
            {
                holding.Type = SecurityType.Base;
                holding.HoldingType = SecurityHoldingType.Net;

                holding.Quantity = momPosition.GetPosition();
                holding.AveragePrice = momPosition.PositionCost;
            }
            else
            {
                holding.Type = SecurityType.Base;
                holding.HoldingType = SecurityHoldingType.Net;
                holding.Quantity = momPosition.GetPosition();
                holding.AveragePrice = momPosition.PositionCost;
                holding.RealizedPnL = momPosition.CloseProfit;
                holding.Commission = momPosition.Commission;
            }

            return holding;
        }

        private static Order ConvertExchangeOrder(MomFundOrder momOrder)
        {
            Order order;
            switch (momOrder.OrderPriceType)
            {
                case MomOrderPriceTypeType.AnyPrice:
                    order = new MarketOrder();
                    break;
                case MomOrderPriceTypeType.LimitPrice:
                    order = new LimitOrder();
                    break;
                case MomOrderPriceTypeType.StopLimit:
                    order = new StopLimitOrder();
                    break;
                case MomOrderPriceTypeType.StopMarket:
                    order = new StopMarketOrder();
                    break;
                default:
                    throw new ArgumentException("不识别的价格类型");
            };

            order.quantity = momOrder.VolumeTotalOriginal;
            order.price = momOrder.LimitPrice;
            order.Symbol = Symbol.Empty;

            order.id = -1;

            order.contingentId = 0;
            order.fillQuantity = momOrder.VolumeTraded;
            order.averageFillPrice = 0;
            order.commission = 0;
            order.brokerId = new List<string>();
            order.brokerId.Add(momOrder.OrderRef.ToString());
            order.priceCurrency = string.Empty;
            order.time = MomStringToDateTime(momOrder.InsertDate, momOrder.InsertTime);
            order.fillTime = DateTime.MinValue;
            order.timeZoneTime = DateTime.MinValue;
            order.lastFillTime = null;
            order.lastUpdateTime = MomStringToDateTime(momOrder.InsertDate, momOrder.UpdateTime);
            order.canceledTime = MomStringToDateTime(momOrder.InsertDate, momOrder.CancelTime);
            order.tradeValue = 0;
            order.status = GetOrderStatus(momOrder.OrderStatus);
            order.properties = null;
            order.tag = momOrder.InstrumentId;      // 没有字段用此字段返回合约代码
            order.offset = OrderOffset.None;
            order.orderSubmissionData = null;

            return order;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="momPosition"></param>
        /// <param name="symbol"></param>
        /// <returns></returns>
        private static Holding ConvertPosition(
            PositionField momPosition,
            Symbol symbol)
        {
            if (symbol.ID.Market == Market.Deribit)
            {
                return ConvertCoinPosition(momPosition, symbol);
            }

            var holding = new Holding();
            holding.Symbol = symbol;
            holding.Type = symbol.SecurityType;
            holding.HoldingType = SecurityHoldingType.Net;
            holding.CurrencySymbol = "$";

            holding.Quantity = momPosition.GetPosition();
            holding.AveragePrice = momPosition.PositionCost;
            holding.RealizedPnL = momPosition.CloseProfit;
            holding.Commission = momPosition.Commission;
            return holding;
        }

        /// <summary>
        /// mom持仓转为QC持仓
        /// </summary>
        /// <param name="momPosition"></param>
        /// <param name="symbol"></param>
        /// <returns></returns>
        private static Holding ConvertCoinPosition(
            PositionField momPosition,
            Symbol symbol)
        {
            var holding = new Holding();

            if (momPosition.GetPosition() == 0)
                return null;

            holding.Symbol = symbol;
            holding.Type = symbol.SecurityType;
            holding.HoldingType = SecurityHoldingType.Net;
            holding.CurrencySymbol = "$";
            holding.Quantity = momPosition.GetPosition();
            holding.AveragePrice = momPosition.PositionCost;

            return holding;
        }

        private static bool TryParseDate(string date, string time, out DateTime datetime)
        {
            date = date.Replace("/", string.Empty).Replace("-", string.Empty);
            if (!DateTime.TryParseExact(date, "yyyyMMdd", null, DateTimeStyles.None, out datetime))
            {
                return false;
            }

            if (string.IsNullOrEmpty(time))
            {
                return true;
            }

            time = time.Replace(":", string.Empty).Replace(".", string.Empty);
            if (time.Length < 6)
            {
                return false;
            }

            if (!int.TryParse(time.Substring(0, 2), NumberStyles.None, null, out var hours))
            {
                return false;
            }
            if (!int.TryParse(time.Substring(2, 2), NumberStyles.None, null, out var minutes))
            {
                return false;
            }
            if (!int.TryParse(time.Substring(4, 2), NumberStyles.None, null, out var seconds))
            {
                return false;
            }

            var ms = 0;
            if (time.Length > 6)
            {
                int.TryParse(time.Substring(6), NumberStyles.None, null, out ms);
            }
            var timeSpan = new TimeSpan(0, hours, minutes, seconds, ms);
            datetime = datetime.Add(timeSpan);
            return true;
        }

        public static byte ConvertToMomStopWorkingTypeType(StopPriceTriggerType type)
        {
            switch (type)
            {
                case StopPriceTriggerType.MarkPrice:
                    return MomStopWorkingTypeType.MarkPrice;
                case StopPriceTriggerType.LastPrice:
                    return MomStopWorkingTypeType.ContractPrice;
                default:
                    throw new Exception($"ConvertToMomStopWorkingTypeType {type.ToString()} Not Support");
            }
        }

        public static bool SaveCSV(string fullPath, string data)
        {
            bool re = true;
            try
            {
                FileStream FileStream = new FileStream(fullPath, FileMode.Append);
                StreamWriter sw = new StreamWriter(FileStream, System.Text.Encoding.UTF8);
                sw.WriteLine(data);
                //清空缓冲区
                sw.Flush();
                //关闭流
                sw.Close();
                FileStream.Close();
            }
            catch
            {
                re = false;
            }
            return re;
        }


        public static List<string> ReadSymbolProperties()
        {
            var list = new List<string>();
            var directory = Path.Combine(Globals.DataFolder, "symbol-properties");
            var fileName = Path.Combine(directory, "symbol-properties-database.csv");
            if (!File.Exists(fileName))
            {
                throw new FileNotFoundException("Unable to locate symbol properties file: " + fileName);
            }

            foreach (var line in File.ReadLines(fileName))
            {
                list.Add(line);
            }

            return list;
        }

        public static void SaveSymbolProperties(List<string> data)
        {
            var directory = Path.Combine(Globals.DataFolder, "symbol-properties");
            var fileName = Path.Combine(directory, "symbol-properties-database-copy.csv");
            using var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
            using var sw = new StreamWriter(fs);
            foreach (var line in data)
            {
                sw.WriteLine(line);
            }
        }

        private static byte GetMomTimeCondition(Order order)
        {
            if (order.properties is DeribitOrderProperties properties)
            {
                switch (properties.TimeInForceFlag)
                {
                    case TimeInForceFlag.FillOrKill:
                        return MomTimeConditionType.FOK;
                    case TimeInForceFlag.ImmediateOrCancel:
                        return MomTimeConditionType.IOC;
                    case TimeInForceFlag.GoodTilCancelled:
                    default:
                        break;
                }
            }

            return MomTimeConditionType.GTC;
        }
    }
}
