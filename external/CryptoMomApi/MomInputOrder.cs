using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public sealed class MomInputOrder : InputOrderField
    {
        public override string ToString()
        {
            return $"{inputLocalId},{userId},{accountId},{instrumentId},{exchangeSymbol}," +
                   $"{ConstantHelper.GetName<MomDirectionType>(direction)}," +
                   $"{ConstantHelper.GetName<MomOrderPriceTypeType>(orderPriceType)}," +
                   $"{limitPrice:F4},{volumeTotalOriginal}," +
                   $"{ConstantHelper.GetName<MomOrderStatusType>(orderStatus)}";
        }

        public MomInputOrder Clone()
        {
            return (MomInputOrder)MemberwiseClone();
        }

        public MomFundInputOrder ToFundInput()
        {
            var fundInput = new MomFundInputOrder
            {
                userId = userId,
                orderRef = orderRef,
                fundAccountId = fundAccountId,
                fundChannelType = fundChannelType,
                inputLocalId = inputLocalId,
                instrumentId = instrumentId,
                productClass = productClass,
                exchangeSymbol = exchangeSymbol,
                exchangeId = exchangeId,
                orderPriceType = orderPriceType,
                direction = direction,
                limitPrice = limitPrice,
                volumeTotalOriginal = volumeTotalOriginal,
                timeCondition = timeCondition,
                volumeCondition = volumeCondition,
                contingentCondition = contingentCondition,
                stopPrice = stopPrice,
                advanced = advanced,
                triggerType = triggerType,
                timestamp1 = timestamp1,
                timestamp2 = timestamp2,
            };
            return fundInput;
        }
    }
}
