using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Concurrent;
using QuantConnect.Orders;


namespace QuantConnect.Securities
{
    /// <summary>
    /// not be used,nothing to do
    /// </summary>
    public class SecurityHoldingOrdersItem
    {
        /// <summary>
        /// 
        /// </summary>
        public int OrderId;
        
        /// <summary>
        /// 
        /// </summary>
        public decimal PreValue;

        /// <summary>
        /// 
        /// </summary>
        public decimal Filled;

        /// <summary>
        /// 
        /// </summary>
        public OrderStatus Status;
    }

    /// <summary>
    /// 
    /// </summary>
    class SecurityHoldingOrders
    {
        /// <summary>
        /// 
        /// </summary>
        private ConcurrentDictionary<int, SecurityHoldingOrdersItem> _orders;

        /// <summary>
        /// 
        /// </summary>
        public SecurityHoldingOrders()
        {
            _orders = new ConcurrentDictionary<int, SecurityHoldingOrdersItem>();
        }
    }
}
