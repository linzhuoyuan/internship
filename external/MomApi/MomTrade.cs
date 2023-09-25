using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomTrade : TradeField
    {
        public override string ToString()
        {
            return $"{tradeId},{userId},{instrumentId},{exchangeSymbol}," +
                   $"{ConstantHelper.GetName<MomDirectionType>(direction)}," +
                   $"{price},{volume}";
        }

        public MomTrade Clone()
        {
            return (MomTrade)MemberwiseClone();
        }

        public MomFundTrade ToFundTrade()
        {
            var trade = new MomFundTrade
            {
                fundAccountId = fundAccountId,
                userId = userId,
                tradeLocalId = tradeLocalId,
                inputLocalId = inputLocalId,
                orderLocalId = orderLocalId,
                instrumentId = instrumentId,
                exchangeSymbol = exchangeSymbol,
                productClass = productClass,
                exchangeId = exchangeId,
                tradeId = tradeId,
                direction = direction,
                orderSysId = orderSysId,
                offsetFlag = offsetFlag,
                price = price,
                volume = volume,
                margin = margin,
                commission = commission,
                tradeDate = tradeDate,
                tradeTime = tradeTime,
                tradeType = tradeType,
                tradeSource = tradeSource
            };
            return trade;
        }
    }
}
