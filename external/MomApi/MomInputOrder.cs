using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomInputOrder : InputOrderField
    {
        public override string ToString()
        {
            return $"{userId},{accountId},{exchangeSymbol}," +
                   $"{ConstantHelper.GetName<MomDirectionType>(direction)}," +
                   $"{ConstantHelper.GetName<MomOffsetFlagType>(offsetFlag)}," +
                   $"{limitPrice:F4},{volumeTotalOriginal.NormalizeToStr()}," +
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
                channelIndex = channelIndex,
                fundId = fundId,
                userId = userId,
                accountId = accountId,
                fundAccountId = fundAccountId,
                fundChannelType = fundChannelType,
                strategyInputId = inputLocalId,
                orderRef = orderRef,
                instrumentId = instrumentId,
                productClass = productClass,
                exchangeSymbol = exchangeSymbol,
                exchangeId = exchangeId,
                orderPriceType = orderPriceType,
                direction = direction,
                offsetFlag = offsetFlag,
                openVolume = openVolume,
                closeTodayVolume = closeTodayVolume,
                closeVolume = closeVolume,
                limitPrice = limitPrice,
                volumeTotalOriginal = volumeTotalOriginal,
                timeCondition = timeCondition,
                volumeCondition = volumeCondition,
                contingentCondition = contingentCondition,
                stopPrice = stopPrice,
                timestamp1 = timestamp1,
                timestamp2 = timestamp2,
            };
            return fundInput;
        }
    }
}
