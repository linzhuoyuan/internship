using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomFundInputOrder : InputOrderField
    {
        public override string ToString()
        {
            return $"{fundAccountId},{exchangeSymbol}," +
                   $"{ConstantHelper.GetName<MomDirectionType>(direction)}," +
                   $"{ConstantHelper.GetName<MomOffsetFlagType>(offsetFlag)}," +
                   $"{limitPrice:F4},{volumeTotalOriginal.NormalizeToStr()}," +
                   $"{ConstantHelper.GetName<MomOrderStatusType>(orderStatus)}";
        }

        public MomInputOrder ToInputOrder()
        {
            var inputOrder = new MomInputOrder
            {
                userId = UserId,
                fundId = FundId,
                fundAccountId = FundAccountId,
                fundChannelType = FundChannelType,
                inputLocalId = InputLocalId,
                instrumentId = InstrumentId,
                productClass = ProductClass,
                exchangeSymbol = ExchangeSymbol,
                exchangeId = ExchangeId,
                orderStatus = OrderStatus,
                orderSubmitStatus = OrderSubmitStatus,
                orderSysId = OrderSysId,
                orderPriceType = OrderPriceType,
                direction = Direction,
                offsetFlag = OffsetFlag,
                limitPrice = LimitPrice,
                volumeTotalOriginal = VolumeTotalOriginal,
                openVolume = OpenVolume,
                closeVolume = CloseVolume,
                closeTodayVolume = CloseTodayVolume,
                volumeTraded = VolumeTraded,
                timeCondition = TimeCondition,
                volumeCondition = VolumeCondition,
                contingentCondition = ContingentCondition,
                stopPrice = StopPrice,
                frozenCommission = FrozenCommission,
                frozenMargin = FrozenMargin,
                frozenPremium = FrozenPremium,
                shortKey = ShortKey
            };
            return inputOrder;
        }

        public MomFundInputOrder Clone()
        {
            return (MomFundInputOrder)MemberwiseClone();
        }
    }
}
