using QuantConnect.Orders;


namespace QuantConnect.Algorithm.CSharp.qlnet.tools
{
    public class PositionManager
    {
        public Symbol Symbol { get; set; }

        private decimal _virtualPosition;
        private decimal _currentPosition;

        /// <summary>
        /// 创建仓位管理实例
        /// </summary>
        public PositionManager(decimal virtualPosition, Symbol symbol)
        {
            Symbol = symbol;
            _virtualPosition = virtualPosition;

        }

        /// <summary>
        /// 在添加order时增加虚拟持仓
        /// </summary>
        public void AddPosition(decimal vol)
        {
            //下单时则累加虚拟持仓
            _virtualPosition += vol;
        }

        /// <summary>
        /// 处理现有的position
        /// </summary>
        public void ManagePosition(decimal currentPosition)
        {
            _currentPosition = currentPosition;
        }

        /// <summary>
        /// 用orderevent对position信息进行更新
        /// </summary>
        public void UpdatePosition(OrderEvent orderEvent, Order order)
        {
            if (order.Type == OrderType.StopLimit)
                return;
            //如果order被cancel或者invalid，虚拟持仓中减掉该order的量
            if (orderEvent.Status == OrderStatus.Canceled || orderEvent.Status == OrderStatus.Invalid || orderEvent.Status == OrderStatus.Filled)
            {
                _virtualPosition -= order.Quantity;
            }
            //如果order部分成交，调整虚拟持仓的量
            if (orderEvent.Status == OrderStatus.PartiallyFilled || orderEvent.Status == OrderStatus.Filled)
            {
                //virtual_position -= order.Quantity;
                _virtualPosition += orderEvent.FillQuantity;
            }

        }


        /// <summary>
        /// 检查策略记录的订单和broker返回的订单是否一致
        /// </summary>
        public bool CheckPosition()
        {
            return _virtualPosition == _currentPosition;
        }
    }
}
