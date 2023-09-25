using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace QuantConnect.Orders
{
    /// <summary>
    /// 
    /// </summary>
    public class TradeRecord
    {
        /// <summary>
        /// The symbol of the traded instrument
        /// </summary>
        public Symbol Symbol { get; set; }
        
        public string TradeId { get; set; }

        public string OrderId { get; set; }

        public OrderStatus Status { get; set; }

        public OrderDirection Direction { get; set; }

        public OrderOffset Offset { get; set; }

        /// <summary>
        /// fill time
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// fill amount
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// fill price
        /// </summary>
        public decimal Price { get; set; }
        public decimal TradeValue { get; set; }

        public decimal ProfitLoss { get; set; }

        public OrderFee Fee { get; set; }

        public decimal IndexPrice { get; set; }

        public decimal UnderlyingPrice { get; set; }

        public decimal MarkPrice { get; set; }

        public string Tag { get;  set; }

        public override string ToString()
        {
            var message = $"UtcTime Time: {Time} TradeId:{TradeId} OrderID: {OrderId} Symbol: {Symbol.Value} Status: {Status} " +
                          $"FillQuantity: {Amount} FillPrice: {Price.SmartRounding()}";

            if (Fee != null && Fee.Value.Amount != 0m) 
                message += $" Fee: {Fee}";

            return message;
        }
    }
}
