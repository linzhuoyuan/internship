using System.Collections.Generic;
using System.Linq;
using MomCrypto.Api;
using Order = QuantConnect.Orders.Order;

namespace QuantConnect.Brokerages.MomCrypto
{
    /// <summary>
    /// 订阅状态
    /// </summary>
    internal class SubscribeData
    {
        /// 合约编号
        public string InstrumentId { get; set; }

        /// 
        public Symbol SubscribeSymbol { get; set; }

        /// 订阅状态 0：订阅请求发出 1：订阅成功 2：准备退订
        public int SubscribeStatus { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    public class MomCryptoOrderData
    {
        /// <summary>
        /// 
        /// </summary>
        public MomCryptoOrderData()
        {
            Trades = new List<MomTrade>();
        }
        /// <summary>
        /// 唯一编码
        /// </summary>
        public long InputLocalId { get; set; }

        /// <summary>
        /// 报单发送编号
        /// </summary>
        public long OrderRef { get; set; }

        /// <summary>
        /// 输入报单
        /// </summary>
        public MomInputOrder InputOrder;

        /// <summary>
        /// 回报报单
        /// </summary>
        public MomOrder? MomOrder;

        /// <summary>
        /// 成交记录
        /// </summary>
        public List<MomTrade> Trades;

        /// <summary>
        /// qc原始order
        /// </summary>
        public Order? Order;

        public decimal GetTradeVolume()
        {
            return Trades.Sum(x => x.GetVolume());
        }
    }
}
