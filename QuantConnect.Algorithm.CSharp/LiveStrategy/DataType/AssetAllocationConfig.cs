using System;
using CsvHelper.Configuration.Attributes;

namespace QuantConnect.Algorithm.CSharp.LiveStrategy.DataType
{
    public class AssetAllocationConfig
    {
        [Name("coin")]
        public string Coin { get; set; }
        [Name("asset")]
        public decimal Asset { get; set; }
        [Name("start")]
        public DateTime StartDate { get; set; }
        [Name("end")]
        public DateTime EndDate { get; set; }
    }
}
