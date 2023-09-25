using System;

namespace QuantConnect.Algorithm.CSharp.LiveStrategy.DataType
{
    public class LiveAssetAllocation
    {
        public string Ticker { get; set; }
        public virtual decimal Asset { get; set; }
        public double T2M { get; set; }
        public double T2MUpdateRatio { get; set; }
        public bool UpdateStrikes { get; set; }
        public decimal InitialStrikeRatio { get; set; }
        public decimal StrikeSpread{ get ; set; }
        public bool IsBull { get; set; }
        public DateTime SignalTime { get; set; } // UTC time with no timezone info
        public decimal UpdateStrikeRatio { get; set; }
        public decimal LimitPriceRatio { get; set; }

        public LiveAssetAllocation()
        { }

        public LiveAssetAllocation(string ticker, decimal asset, double t2M, 
            double t2MUpdateRatio, bool updateStrikes, decimal initialStrikeRatio, 
            decimal strikeSpread, bool isBull, DateTime signalTime, 
            decimal updateStrikeRatio, decimal limitPriceRatio)
        {
            Ticker = ticker;
            Asset = asset;
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
