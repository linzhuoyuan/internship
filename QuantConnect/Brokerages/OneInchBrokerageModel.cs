using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Securities;

namespace QuantConnect.Brokerages;

public class OneInchBrokerageModel : DefaultBrokerageModel
{
    public OneInchBrokerageModel()
    {

    }

    public override AccountType AccountType => AccountType.Cash;
    public override bool CanUpdateOrder(Security security, Order order, UpdateOrderRequest request, out BrokerageMessageEvent message)
    {
        message = null;
        return false;
    }

    public override IBuyingPowerModel GetBuyingPowerModel(Security security)
    {
        return new InfiniteBuyingPowerModel();
    }

    public override decimal GetLeverage(Security security)
    {
        return 1m;
    }

    public override IFeeModel GetFeeModel(Security security)
    {
        return new FeeModel();
    }
}