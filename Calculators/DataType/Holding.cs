using System;

namespace Calculators.DataType
{
    public class Holding
    {
        public string Coin { get; }
        public decimal Quantity { get; }
        public decimal MarkPrice { get; }
        public string QuoteCurrency { get; }
        public SecurityType SecurityType { get; }
        public decimal MaintenanceMarginRequirement { get; }
        public decimal InitialMarginRequirement { get; }

        public Holding(string coin, decimal quantity, decimal markPrice, string quoteCurrency, SecurityType securityType, decimal maintenanceMarginRequirement, decimal initialMarginRequirement)
        {
            Coin = coin;
            Quantity = quantity;
            MarkPrice = markPrice;
            QuoteCurrency = quoteCurrency;
            SecurityType = securityType;
            MaintenanceMarginRequirement = maintenanceMarginRequirement;
            InitialMarginRequirement = initialMarginRequirement;
        }

        public decimal CalculateCollateralValue(FTXCollateralParameters collateralConfig, bool isInitial)
        {
            if (Quantity > 0)
            {
                var weight = isInitial ? collateralConfig.InitialWeight : collateralConfig.TotalWeight;
                return Quantity * MarkPrice * (decimal) Math.Min((double) weight,
                    1.1 / (1 + (double) collateralConfig.IMFFactor * Math.Sqrt((double) Quantity)));
            }

            return Quantity * MarkPrice;
        }

        public override string ToString()
        {
            return $"{Coin}-{SecurityType}-{Quantity}-{MarkPrice}-{QuoteCurrency}"; 
        }
    }
}
