using System;
using System.Collections.Generic;
using System.Text;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp.qlnet.tools
{
    public class MyOrder
    {
        public Symbol TradeSymbol { get; }
        public decimal TargetVolume { get; }
        public decimal FilledVolume { get; set; }
        public OrderType OrderType { get; }
        public OrderStatus OrderStatus { get; set; }
        public bool Finished { get; set; }
        public long OrderID { get; set; }
        public decimal StopPrice { get; }
        public decimal LimitPrice { get; }

        public MyOrder(Symbol tradeSymbol, decimal targetVolume, OrderType orderType, decimal limitPrice = 0, decimal stopPrice = 0)
        {
            TradeSymbol = tradeSymbol;
            TargetVolume = targetVolume;
            OrderType = orderType;
            LimitPrice = limitPrice;
            StopPrice = stopPrice;
        }
    }
}
