using Calculators.DataType;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Calculators.Margins
{
    public class FTXMarginModelWithSpotMargin: FTXMarginModel
    {
        public FTXMarginModelWithSpotMargin(string location = "", string quoteCurrency = "USD") : base(location,
            quoteCurrency)
        {
        }

        /// <summary>
        /// The auto-close margin--the point at which you are not just liquidated but in fact closed down off-exchange--is 50% of the maintenance margin requirement
        /// From: https://help.ftx.com/hc/en-us/articles/360053007671
        /// for safety, we send liquidation warning once reach mantenance margin
        /// </summary>
        /// <param name="holdings"></param>
        /// <param name="riskControlMultiplier"></param>
        /// <returns></returns>
        public override bool IsAtRiskForLiquidation(IList<Holding> holdings, decimal riskControlMultiplier = 1)
        {
            var totalMaintenanceMargin = holdings.Select(CalculateMaintenanceMargin).Sum();
            var collateral = GetCollateral(holdings, false);
            return collateral * riskControlMultiplier <= totalMaintenanceMargin * 0.5m;
        }

        private decimal CalculateMaintenanceMargin(Holding holding)
        {
            switch (holding.SecurityType)
            {
                case SecurityType.Spot:
                    return holding.Quantity < 0
                        ? -GetMaintenanceMarginRequirementForCrypto(holding) * holding.MarkPrice * holding.Quantity
                        : 0;
                case SecurityType.Futures:
                    return holding.MarkPrice * Math.Abs(holding.Quantity) * holding.MaintenanceMarginRequirement;
                default:
                    _logger.Error($"Security type {holding.SecurityType} not supported in FTXMarginModel!");
                    return 0;
            }
        }

        protected override decimal CalculateInitialMargin(Holding holding)
        {
            switch (holding.SecurityType)
            {
                case SecurityType.Spot:
                    return holding.Quantity < 0 
                        ? -GetInitialMarginRequirementForCrypto(holding) * holding.MarkPrice * holding.Quantity
                        : 0;
                case SecurityType.Futures:
                    return holding.MarkPrice * Math.Abs(holding.Quantity) * holding.InitialMarginRequirement;
                default:
                    _logger.Error($"Security type {holding.SecurityType} not supported in FTXMarginModel!");
                    return 0;
            }
        }

        protected override decimal CalculateNotional(Holding holding)
        {
            switch (holding.SecurityType)
            {
                case SecurityType.Spot:
                    return holding.Quantity > 0
                        ? holding.MarkPrice * holding.Quantity
                        : 0;
                case SecurityType.Futures:
                    return holding.MarkPrice * Math.Abs(holding.Quantity);
                default:
                    _logger.Error($"Security type {holding.SecurityType} not supported in FTXMarginModel!");
                    return 0;
            }
        }

        public decimal GetInitialMarginRequirementForCrypto(Holding holding)
        {
            return Math.Max(1.1m / _collateralParametersMap[holding.Coin].InitialWeight - 1,
                _collateralParametersMap[holding.Coin].IMFFactor *
                (decimal) Math.Sqrt(Math.Abs((double) holding.Quantity)));
        }

        public decimal GetMaintenanceMarginRequirementForCrypto(Holding holding)
        {
            return Math.Max(1.03m / _collateralParametersMap[holding.Coin].TotalWeight - 1,
                0.6m * _collateralParametersMap[holding.Coin].IMFFactor *
                (decimal) Math.Sqrt(Math.Abs((double) holding.Quantity)));
        }
    }
}
 