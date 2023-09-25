using System.Runtime.Serialization;

namespace MomCrypto.Api
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
    }
}
