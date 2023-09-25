using System;

namespace QuantConnect.Algorithm.CSharp.LiveStrategy.DataType
{
    public class BinanceLiveAssetAllocation: LiveAssetAllocation
    {
        public decimal SpotAsset { get; set; }
        public decimal LeverageAsset { get; set; }
        public decimal FuturesAsset { get; set; }

        public override decimal Asset => SpotAsset + LeverageAsset + FuturesAsset;

        public BinanceLiveAssetAllocation()
        { }

        public BinanceLiveAssetAllocation(string ticker, decimal spotAsset, decimal leverageAsset, decimal futuresAsset, double t2M, double t2MUpdateRatio, bool updateStrikes, decimal initialStrikeRatio, decimal strikeSpread, bool isBull, DateTime signalTime, decimal updateStrikeRatio, decimal limitPriceRatio)
        {
            Ticker = ticker;
            SpotAsset = spotAsset;
            LeverageAsset = leverageAsset;
            FuturesAsset = futuresAsset;
            T2M = t2M;
            T2MUpdateRatio = t2MUpdateRatio;
            UpdateStrikes = updateStrikes;
            InitialStrikeRatio = initialStrikeRatio;
            StrikeSpread = strikeSpread;
            IsBull = isBull;
            SignalTime = signalTime;
            UpdateStrikeRatio = updateStrikeRatio;
            LimitPriceRatio = limitPriceRatio;
        }
    }
}
