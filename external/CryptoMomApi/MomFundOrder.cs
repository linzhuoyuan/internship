using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public sealed class MomFundOrder : OrderField
    {
        public override string ToString()
        {
            return $"{inputLocalId},{strategyInputId},{fundAccountId},{orderSysId},{instrumentId},{exchangeSymbol}," +
                   $"{ConstantHelper.GetName<MomDirectionType>(direction)}," +
                   $"{ConstantHelper.GetName<MomOrderPriceTypeType>(orderPriceType)}," +
                   $"{limitPrice:F4},{volumeTotalOriginal}," +
                   $"{ConstantHelper.GetName<MomOrderStatusType>(orderStatus)}";
        }

        public MomFundOrder Clone()
        {
            return (MomFundOrder)MemberwiseClone();
        }
    }
}
