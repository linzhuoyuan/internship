using System.Runtime.Serialization;

namespace MomCrypto.Api
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
    }
}
