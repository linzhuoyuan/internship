using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomOrder : OrderField
    {
        public override string ToString()
        {
            return $"{inputLocalId},{orderRef},{userId},{orderSysId},{instrumentId},{exchangeSymbol}," +
                   $"{ConstantHelper.GetName<MomDirectionType>(direction)}," +
                   $"{ConstantHelper.GetName<MomOrderPriceTypeType>(orderPriceType)}," +
                   $"{limitPrice:F4},{volumeTotalOriginal}," +
                   $"{ConstantHelper.GetName<MomOrderStatusType>(orderStatus)}";
        }

        public MomOrder Clone()
        {
            return (MomOrder)MemberwiseClone();
        }

        public MomFundOrder ToFundOrder()
        {
            var order = new MomFundOrder
            {
                fundAccountId = fundAccountId,
                userId = userId,
                orderLocalId = orderLocalId,
                inputLocalId = inputLocalId,
                orderSysId = orderSysId,
                instrumentId = instrumentId,
                productClass = productClass,
                exchangeSymbol = exchangeSymbol,
                exchangeId = exchangeId,
                orderPriceType = orderPriceType,
                direction = direction,
                offsetFlag = offsetFlag,
                limitPrice = limitPrice,
                volumeTotalOriginal = volumeTotalOriginal,
                timeCondition = timeCondition,
                volumeCondition = volumeCondition,
                contingentCondition = contingentCondition,
                stopPrice = stopPrice,
                orderStatus = orderStatus,
                orderSubmitStatus = orderSubmitStatus,
                volumeTraded = volumeTraded,
                averagePrice = averagePrice,
                insertDate = insertDate,
                insertTime = insertTime,
                updateTime = updateTime,
                cancelTime = cancelTime,
                statusMsg = statusMsg
            };
            return order;
        }
    }
}
