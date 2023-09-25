using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect
{
    /// <summary>
    /// 
    /// </summary>
    public class GreeksChartData
    {
        /// data time
        public DateTime DataTime { get; set; }
        /// 
        public decimal Delta { get; set; }
        /// 
        public decimal Gamma { get; set; }
        /// 
        public decimal Vega { get; set; }
        /// 
        public decimal Theta { get; set; }
        /// 
        public decimal Rho { get; set; }
    }
}
