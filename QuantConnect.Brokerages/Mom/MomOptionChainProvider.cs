using QuantConnect.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace QuantConnect.Brokerages.Mom
{
    /// <summary>
    /// 
    /// </summary>
    public class MomOptionChainProvider : IOptionChainProvider
    {
        public readonly ManualResetEvent _readyEvent = new(false);
        /// <summary>
        /// 按标的合约建立期权链
        /// </summary>
        public readonly Dictionary<string, List<Symbol>> OptionChain;
        private readonly MomBrokerage _momBrokerage;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="momBrokerage"></param>
        public MomOptionChainProvider(MomBrokerage momBrokerage)
        {
            _momBrokerage = momBrokerage;
            OptionChain = new Dictionary<string, List<Symbol>>();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Clear()
        {
            OptionChain.Clear();
            _readyEvent.Reset();
        }

        public void SetReady()
        {
            _readyEvent.Set();
        }

        public void Add(string underlying, Symbol symbol)
        {
            if (!OptionChain.TryGetValue(underlying, out var options))
            {
                options = new List<Symbol>();
                OptionChain.Add(underlying, options);
            }
            options.Add(symbol);
        }

        /// <summary>
        /// Gets the list of option contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public IEnumerable<Symbol> GetOptionContractList(Symbol symbol, DateTime date)
        {
            var key = symbol.IsCanonical() ? symbol.Underlying.Value : symbol.Value;
            _readyEvent.WaitOne();
            return OptionChain.TryGetValue(key, out var symbols) ? symbols : new List<Symbol>();
        }
    }
}
