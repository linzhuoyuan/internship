using QuantConnect.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Brokerages.Mom
{
    /// <summary>
    /// 
    /// </summary>
    public class MomFutureChainProvider : IFutureChainProvider
    {


        /// <summary>
        /// 
        /// </summary>
        /// <param name="momBrokerage"></param>
        public MomFutureChainProvider(MomBrokerage momBrokerage)
        {

        }

        /// <summary>
        /// Gets the list of future contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public IEnumerable<Symbol> GetFutureContractList(Symbol symbol, DateTime date)
        {
            // 暂不实现
            return new List<Symbol>();
        }


    }
}
