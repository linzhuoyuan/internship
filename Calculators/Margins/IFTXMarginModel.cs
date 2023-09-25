using System.Collections.Generic;
using Calculators.DataType;

namespace Calculators.Margins
{
    public interface IFTXMarginModel
    {
        bool IsAtRiskForLiquidation(IList<Holding> holdings, decimal riskControlMultiplier);
        decimal GetAvailableCollateral(IList<Holding> holdings);
        decimal CalculateMarginFraction(IList<Holding> holdings, bool isInitial);
        void TryUpdateCollateralParameters(string coin, decimal totalWeight);
        decimal GetCollateral(IList<Holding> holdings, bool isInitial);
    }
}