using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public sealed class MomFundTrade : TradeField
    {
        public override string ToString()
        {
            return $"{tradeId},{fundAccountId},{instrumentId},{exchangeSymbol}," +
                   $"{ConstantHelper.GetName<MomDirectionType>(direction)}," +
                   $"{price},{volume}";
        }

        public MomFundTrade Clone()
        {
            return (MomFundTrade)MemberwiseClone();
        }
    }
}
