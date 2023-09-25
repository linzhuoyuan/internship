using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using QuantConnect.Interfaces;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    public class LiveMultiOptionChainProvider : IOptionChainProvider
    {

        private Dictionary<string, List<Object>>  _subBrokers =null;

        private Dictionary<string, IOptionChainProvider> _subOptionChainProvider = new Dictionary<string, IOptionChainProvider>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="subBrokers"></param>
        public LiveMultiOptionChainProvider(Dictionary<string, List<Object>> subBrokers)
        {
            _subBrokers = subBrokers;

            foreach(var i in subBrokers)
            {
                var brokerage = i.Value.First<Object>() as IBrokerage;
                if (brokerage.Name == "Deribit")
                {
                    var subOptionChainProvider = new CachingOptionChainProvider(new LiveDeribitOptionChainProvider());
                    _subOptionChainProvider[Market.Deribit] = subOptionChainProvider;
                }
                else if (brokerage.Name == "Okex")
                {
                    var subOptionChainProvider = new CachingOptionChainProvider(new LiveOkexOptionChainProvider());
                    _subOptionChainProvider[Market.Okex] = subOptionChainProvider;
                }
                else
                {
                    throw new Exception($"LiveMultiOptionChainProvider not Support brokerage : {brokerage.Name}");
                }
            }
        }

        /// <summary>
        /// Gets the list of option contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol">The underlying symbol</param>
        /// <param name="date">The date for which to request the option chain (only used in backtesting)</param>
        /// <returns>The list of option contracts</returns>
        public IEnumerable<Symbol> GetOptionContractList(Symbol symbol, DateTime date)
        {
            if (!_subOptionChainProvider.ContainsKey(symbol.ID.Market))
            {
                throw new Exception($"LiveMultiOptionChainProvider.GetOptionContractList not Support symbol : {symbol.Value}");
            }
            List<Symbol> lst =new  List<Symbol>();
            int count = 0;
            do
            {
                lst.Clear();
                count++;
                var symbols = _subOptionChainProvider[symbol.ID.Market].GetOptionContractList(symbol, date);
                if (symbols.Count() > 0)
                {
                    lst.AddRange(symbols);
                    break;
                }
            } while (count < 4);
            if(count ==4 && lst.Count == 0)
            {
                throw new Exception($"LiveMultiOptionChainProvider.GetOptionContractList can not find symbol: {symbol.Value}");
            }
            return lst.ToArray();
        }
    }
}
