using System;
using System.Collections.Generic;
using QuantConnect.Securities;

namespace QuantConnect.Util
{
    public class Converters
    {
        // TODO: remove hardcode margin requriement
        public static decimal InitialMarginRequirement { get; set; } = 0.1m;
        private const decimal _maintenanceMarginRequirement = 0.03m;

        public static IList<Calculators.DataType.Holding> GetHoldings(SecurityPortfolioManager portfolio)
        {
            var holdings = new List<Calculators.DataType.Holding>();
            foreach (var security in portfolio.Securities.Values)
            {
                var coin = security.Symbol.ID.Symbol.Split('-')[0].Split('_')[0];
                Calculators.SecurityType securityType;
                decimal maintenanceMarginRequirement; 
                decimal initialMarginRequirement;
                decimal quantity;
                switch (security.Symbol.SecurityType)
                {
                    case SecurityType.Crypto:
                        securityType = Calculators.SecurityType.Spot;
                        maintenanceMarginRequirement = 0;
                        initialMarginRequirement = 0;
                        quantity = portfolio.CashBook[coin].Amount;
                        break;
                    case SecurityType.Future:
                        securityType = Calculators.SecurityType.Futures;
                        maintenanceMarginRequirement = _maintenanceMarginRequirement;
                        initialMarginRequirement = InitialMarginRequirement;
                        quantity = security.Holdings.Quantity;
                        break;
                    default:
                        throw new ArgumentException(
                            $"Security type {security.Symbol.SecurityType} not supported in FTXMarginModel!");
                }

                holdings.Add(new Calculators.DataType.Holding(coin,
                    quantity, security.Price, security.QuoteCurrency.Symbol, securityType,
                    maintenanceMarginRequirement, initialMarginRequirement));
            }

            holdings.Add(new Calculators.DataType.Holding(portfolio.CashBook.AccountCurrency,
                portfolio.CashBook[portfolio.CashBook.AccountCurrency].Amount, 1, portfolio.CashBook.AccountCurrency,
                Calculators.SecurityType.Spot, 0, 0));
            return holdings;

        }
    }
}