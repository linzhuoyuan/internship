using Calculators.DataType;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Calculators.IO;
using NLog;

namespace Calculators.Margins
{
    public class FTXMarginModel : IFTXMarginModel
    {
        protected readonly string _quoteCurrency;
        private const decimal _minUSDPosition = -30000;
        private const decimal _maxCollateralNegativeUSDRatio = 4;
        private const decimal _maintenanceMarginAjdustment = 0.002m;
        protected static ConcurrentDictionary<string, FTXCollateralParameters> _collateralParametersMap;
        protected Logger _logger;

        public FTXMarginModel(string location = "", string quoteCurrency = "USD")
        {
            _quoteCurrency = quoteCurrency;
            _logger = LogManager.GetLogger("FTXMarginModel");
            Init(location);
        }

        private void Init(string location)
        {
            if (_collateralParametersMap is null)
            {
                if (string.IsNullOrEmpty(location))
                {
                    _collateralParametersMap = new ConcurrentDictionary<string, FTXCollateralParameters>();
                }
                else
                {
                    var dataLoader = new AggregatedDataLoader();
                    _collateralParametersMap = new ConcurrentDictionary<string, FTXCollateralParameters>(dataLoader
                        .FetchData<FTXCollateralParameters>(location)
                        .ToDictionary(x => x.Coin, x => x));
                }
            }
        }

        public decimal GetCollateral(IList<Holding> holdings, bool isInitial)
        {
            return holdings.Where(h =>
                    _collateralParametersMap.Keys.Contains(h.Coin) && h.QuoteCurrency == _quoteCurrency &&
                    h.SecurityType == SecurityType.Spot)
                .Select(h => h.CalculateCollateralValue(_collateralParametersMap[h.Coin], isInitial)).Sum();
        }

        /// <summary>
        /// collateral used is equal to:
        ///     For PRESIDENT: initialMarginRequirement * openSize * (risk price)
        ///     For MOVE: initialMarginRequirement * openSize * (index price)
        ///     Otherwise: initialMarginRequirement * openSize * (mark price)
        /// </summary>
        /// <param name="holdings"></param>
        /// <returns></returns>
        public decimal GetAvailableCollateral(IList<Holding> holdings)
        {
            var collateral = GetCollateral(holdings, true);
            var frozenMargins = holdings.Sum(CalculateInitialMargin);
            return collateral - frozenMargins;
        }

        /// <summary>
        /// FTX will trade your non-USD collateral into USD if your USD balance is negative and any of the following hold:
        ///     You are close to liquidation: your account's margin fraction is less than (20bps + maintenance margin fraction requirement)
        ///     Your negative USD balance is large: over $30,000 in magnitude
        ///     Your negative USD balance is large when compared to overall collateral: its magnitude is over 4 times larger than your net account collateral
        /// From: https://help.ftx.com/hc/en-us/articles/360031149632-Non-USD-Collateral
        /// </summary>
        /// <param name="holdings"></param>
        /// <param name="riskControlMultiplier">a multiplier to adjust USD remaining requirement, should be between 0-1 to strengthen the rules</param>
        /// <returns></returns>
        public virtual bool IsAtRiskForLiquidation(IList<Holding> holdings, decimal riskControlMultiplier = 1)
        {
            if (riskControlMultiplier <= 0 || riskControlMultiplier > 1)
            {
                _logger.Error($"Invalid risk control multiplier {riskControlMultiplier}!");
            }

            var usdPosition = holdings.Single(h => h.Coin == _quoteCurrency).Quantity * riskControlMultiplier;
            var netCollateral = GetCollateral(holdings, false);
            var futuresNotional = holdings.Where(h => h.SecurityType == SecurityType.Futures)
                .Select(h => Math.Abs(h.Quantity) * h.MarkPrice).Sum();
            var spotNotional = holdings.Where(h => h.SecurityType == SecurityType.Spot)
                .Select(h => h.MarkPrice * Math.Min(0m, h.Quantity)).Sum();
            var marginFraction = netCollateral * riskControlMultiplier / spotNotional + futuresNotional;
            var weightedAverageMaintenanceMarginRequirement =
                holdings.Select(h => Math.Abs(h.Quantity) * h.MarkPrice * h.MaintenanceMarginRequirement).Sum() /
                futuresNotional;

            return IsAtRiskForLiquidation(marginFraction, weightedAverageMaintenanceMarginRequirement, usdPosition,
                netCollateral);
        }

        public bool IsAtRiskForLiquidation(decimal marginFraction,
            decimal weightedAverageMaintenanceMarginRequirement, decimal usdPosition, decimal netCollateral)
        {
            return marginFraction <= weightedAverageMaintenanceMarginRequirement + _maintenanceMarginAjdustment ||
                   usdPosition <= _minUSDPosition || usdPosition <= -netCollateral * _maxCollateralNegativeUSDRatio;
        }

        public decimal CalculateMarginFraction(IList<Holding> holdings, bool isInitial)
        {
            var totalNotional = holdings.Sum(CalculateNotional);
            return GetCollateral(holdings, isInitial) / totalNotional;
        }

        public void TryUpdateCollateralParameters(string coin, decimal totalWeight)
        {
            if (_collateralParametersMap.TryGetValue(coin, out var parameters))
            {
                if (!_collateralParametersMap.TryUpdate(coin, parameters, parameters.Copy(totalWeight)))
                {
                    _logger.Error($"Failed to update total weight of {coin}, new weight should be {totalWeight}");
                }
            }
            else
            {
                _logger.Error($"Cannot find collateral parameters for coin {coin}");
            }
        }

        protected virtual decimal CalculateInitialMargin(Holding holding)
        {
            return holding.SecurityType == SecurityType.Futures ? Math.Abs(holding.Quantity) * holding.MarkPrice * holding.InitialMarginRequirement : 0;
        }

        protected virtual decimal CalculateNotional(Holding holding)
        {
            return holding.SecurityType == SecurityType.Futures ? Math.Abs(holding.Quantity) * holding.MarkPrice : 0;
        }
    }
}
 