using QuantConnect.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuantConnect.Brokerages.Mom;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// 
    /// </summary>
    public class LiveMomFutureChainProvider : IFutureChainProvider
    {
        /// <summary>
        /// 
        /// </summary>
        private MomBrokerage _momBrokerage;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="momBrokerage"></param>
        public LiveMomFutureChainProvider(MomBrokerage momBrokerage)
        {
            _momBrokerage = momBrokerage;
        }

        /// <summary>
        /// Gets the list of future contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public IEnumerable<Symbol> GetFutureContractList(Symbol symbol, DateTime date)
        {
            return _momBrokerage.GetFutureContractList(symbol, date);
        }
    }
}
