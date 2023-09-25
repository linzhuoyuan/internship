namespace QuantConnect.Securities
{
    public class InfiniteBuyingPowerModel : IBuyingPowerModel
    {
        public decimal GetLeverage(Security security)
        {
            return 1m;
        }

        public void SetLeverage(Security security, decimal leverage)
        {
        }

        public HasSufficientBuyingPowerForOrderResult HasSufficientBuyingPowerForOrder(
            HasSufficientBuyingPowerForOrderParameters parameters)
        {
            return new HasSufficientBuyingPowerForOrderResult(true);
        }

        public GetMaximumOrderQuantityForTargetValueResult GetMaximumOrderQuantityForTargetValue(
            GetMaximumOrderQuantityForTargetValueParameters parameters)
        {
            var currentHolding = parameters.Security.Type is SecurityType.Crypto
                ? parameters.Portfolio.CashBook[((IBaseCurrencySymbol)parameters.Security).BaseCurrencySymbol].Amount
                : parameters.Portfolio.Securities[parameters.Security.Symbol].Holdings.Quantity;
            var needPosition = parameters.Target / parameters.Security.Price - currentHolding;
            return new GetMaximumOrderQuantityForTargetValueResult(needPosition);
        }

        public ReservedBuyingPowerForPosition GetReservedBuyingPowerForPosition(ReservedBuyingPowerForPositionParameters parameters)
        {
            return parameters.ResultInAccountCurrency(0);
        }

        public BuyingPower GetBuyingPower(BuyingPowerParameters parameters)
        {
            return new BuyingPower(int.MaxValue);
        }
    }
}
