using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Securities;

namespace QuantConnect
{
    /// <summary>
    /// 
    /// </summary>
    public class GreekPnlData
    {
        ///
        public Decimal DeltaPnl;
        ///
        public Decimal GammaPnl;
        ///
        public Decimal VegaPnl;
        ///
        public Decimal ThetaPnl;
        ///
        public Decimal RhoPnl;
        ///
        public Decimal TotalPnl;
        ///
        public Decimal NoImvPnl;

        ///
        public void Add(GreekPnlData data)
        {
            DeltaPnl += data.DeltaPnl;
            GammaPnl += data.GammaPnl;
            VegaPnl += data.VegaPnl;
            ThetaPnl += data.ThetaPnl;
            RhoPnl += data.RhoPnl;
            TotalPnl += data.TotalPnl;
            NoImvPnl += data.NoImvPnl;
        }

        ///
        public void CopyData(GreekPnlData data)
        {
            DeltaPnl = data.DeltaPnl;
            GammaPnl = data.GammaPnl;
            VegaPnl = data.VegaPnl;
            ThetaPnl = data.ThetaPnl;
            RhoPnl = data.RhoPnl;
            TotalPnl = data.TotalPnl;
            NoImvPnl = data.NoImvPnl;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class HoldingPnlData : GreekPnlData
    { 
        ///
        public string holdingSymbol;

        public SecurityHoldingType holdingType;
    }

    /// <summary>
    /// 
    /// </summary>
    public class GreeksPnlChartData : GreekPnlData
    {
        ///
        public DateTime DataTime { get; set; }
        ///
        public List<HoldingPnlData> HoldingsPnl = new List<HoldingPnlData>();
    }
}
