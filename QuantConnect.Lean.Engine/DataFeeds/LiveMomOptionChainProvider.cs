using QuantConnect.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using QuantConnect.Brokerages.Mom;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// 
    /// </summary>
    public class LiveMomOptionChainProvider : IOptionChainProvider
    {
        private readonly MomBrokerage _momBrokerage;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="momBrokerage"></param>
        public LiveMomOptionChainProvider(MomBrokerage momBrokerage)
        {
            _momBrokerage = momBrokerage;
        }

        /// <summary>
        /// Gets the list of option contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public IEnumerable<Symbol> GetOptionContractList(Symbol symbol, DateTime date)
        {
            return _momBrokerage.GetOptionContractList(symbol, date);
        }

    }
}
