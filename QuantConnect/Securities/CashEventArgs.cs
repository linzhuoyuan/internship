using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Securities
{
    /// <summary>
    /// 
    /// </summary>
    public class CashEventArgs: EventArgs
    {
        /// <summary>
        /// 
        /// </summary>
        public string Cash { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cash"></param>
        public CashEventArgs(string cash)
        {
            Cash = cash;
        }
    }
}
