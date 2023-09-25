using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomFundTrade : TradeField
    {
        public override string ToString()
        {
            return $"{tradeId},{fundAccountId},{instrumentId},{exchangeSymbol}," +
                   $"{ConstantHelper.GetName<MomOffsetFlagType>(offsetFlag)}," +
                   $"{ConstantHelper.GetName<MomDirectionType>(direction)}," +
                   $"{price},{volume}";
        }

        public MomFundTrade Clone()
        {
            return (MomFundTrade)MemberwiseClone();
        }

        public MomTrade ToTrade()
        {
            var trade = new MomTrade
            {
                userId = userId,
                fundAccountId = fundAccountId,
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
