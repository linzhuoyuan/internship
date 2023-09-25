using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public sealed class MomFundInputOrder : InputOrderField
    {
        public MomFundInputOrder()
        {
        }
        
        public override string ToString()
        {
            return $"{inputLocalId},{strategyInputId},{fundAccountId},{instrumentId},{exchangeSymbol}," +
                   $"{ConstantHelper.GetName<MomDirectionType>(direction)}," +
                   $"{ConstantHelper.GetName<MomOrderPriceTypeType>(orderPriceType)}," +
                   $"{limitPrice:F4},{volumeTotalOriginal}," +
                   $"{ConstantHelper.GetName<MomOrderStatusType>(orderStatus)}";
        }

        public MomInputOrder ToInputOrder()
        {
            var inputOrder = new MomInputOrder
            {
                userId = userId,
                fundAccountId = fundAccountId,
                fundChannelType = fundChannelType,
                inputLocalId = inputLocalId,
                instrumentId = instrumentId,
                productClass = ProductClass,
                exchangeSymbol = ExchangeSymbol,
                exchangeId = ExchangeId,
                orderStatus = OrderStatus,
                orderSubmitStatus = OrderSubmitStatus,
                orderSysId = OrderSysId,
                orderPriceType = OrderPriceType,
                direction = Direction,
                limitPrice = LimitPrice,
                volumeTotalOriginal = VolumeTotalOriginal,
                openVolume = OpenVolume,
                closeVolume = CloseVolume,
                volumeTraded = VolumeTraded,
                timeCondition = TimeCondition,
                volumeCondition = VolumeCondition,
                contingentCondition = ContingentCondition,
                stopPrice = StopPrice,
                frozenCommission = FrozenCommission,
                frozenMargin = FrozenMargin,
                frozenPremium = FrozenPremium
            };
            return inputOrder;
        }

        public MomFundInputOrder Clone()
        {
            return (MomFundInputOrder)MemberwiseClone();
        }
    }
}
