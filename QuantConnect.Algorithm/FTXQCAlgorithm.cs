using System;
using Calculators.Margins;
using QuantConnect.Util;

namespace QuantConnect.Algorithm
{
    public class FTXQCAlgorithm : QCAlgorithm
    {
        private IFTXMarginModel _ftxMarginModel;

        public override void OnWarmupFinished()
        {
            if (_ftxMarginModel is null)
            {
                throw new Exception($"FTX margin model not set! Please call SetFTXMarginModel in IAlgorithm.Initialize()!");
            }
        }

        protected void SetFTXMarginModel(string location, bool enableSpotMargin)
        {
            FTXMarginModelFactory.Init(location);
            _ftxMarginModel = FTXMarginModelFactory.Resolve(enableSpotMargin);
        }

        protected decimal GetCollateral(bool isInitial)
        {
            return _ftxMarginModel.GetCollateral(Converters.GetHoldings(Portfolio), isInitial);
        }

        protected decimal GetMarginFraction(bool isInitial)
        {
            return _ftxMarginModel.CalculateMarginFraction(Converters.GetHoldings(Portfolio), isInitial);
        }

        protected bool IsAtRiskForLiquidation(decimal riskControlMultiplier)
        {
            return _ftxMarginModel.IsAtRiskForLiquidation(Converters.GetHoldings(Portfolio), riskControlMultiplier);
        }
    }
}
