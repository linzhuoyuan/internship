using System;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace QuantConnect.Securities.Equity
{
    public class SseEquityMarginModel : SecurityMarginModel
    {
        private readonly decimal _initialMarginRequirement = 1m;
        private readonly decimal _maintenanceMarginRequirement = 1m;
        /// <summary>
        /// Gets the total margin required to execute the specified order in units of the account currency including fees
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the order</param>
        /// <returns>The total margin in terms of the currency quoted in the order</returns>
        protected override decimal GetInitialMarginRequiredForOrder(
            InitialMarginRequiredForOrderParameters parameters)
        {
            var fees = parameters.Security.FeeModel.GetOrderFee(
                new OrderFeeParameters(parameters.Security,
                    parameters.Order)).Value;
            var feesInAccountCurrency = parameters.CurrencyConverter.
                ConvertToAccountCurrency(fees).Amount;

            var value = parameters.Order.GetValue(parameters.Security);
            var orderValue = parameters.Order.Direction == OrderDirection.Sell ? 0 : value * GetInitialMarginRequirement(parameters.Security);

            return orderValue + Math.Sign(orderValue) * feesInAccountCurrency;
        }

        protected override decimal GetInitialMarginRequirement(Security security)
        {
            return _initialMarginRequirement;
        }

        protected override decimal GetMaintenanceMargin(Security security)
        {
            return (security.Holdings.AbsoluteHoldingsCost + security.LongHoldings.AbsoluteHoldingsCost + security.ShortHoldings.AbsoluteHoldingsCost) * GetMaintenanceMarginRequirement(security);
        }

        public override decimal GetMaintenanceMarginRequirement(Security security)
        {
            return _maintenanceMarginRequirement;
        }
    }
}
