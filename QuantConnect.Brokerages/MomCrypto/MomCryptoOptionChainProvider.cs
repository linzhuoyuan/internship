using System;
using System.Collections.Generic;
using System.Threading;
using QuantConnect.Interfaces;

namespace QuantConnect.Brokerages.MomCrypto
{
    /// <summary>
    /// 
    /// </summary>
    public class MomCryptoOptionChainProvider : IOptionChainProvider
    {
        private readonly ManualResetEvent _readyEvent = new(false);

        private readonly MomCryptoBrokerage _brokerage;

        /// <summary>
        /// 
        /// </summary>
        public MomCryptoOptionChainProvider(MomCryptoBrokerage brokerage)
        {
            _brokerage = brokerage;
            OptionChain = new Dictionary<string, List<Symbol>>();
        }

        /// <summary>
        /// 按标的合约建立期权链
        /// </summary>
        public readonly Dictionary<string, List<Symbol>> OptionChain;

        /// <summary>
        /// 
        /// </summary>
        public void Clear()
        {
            OptionChain.Clear();
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetReady()
        {
            _readyEvent.Set();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="underlying"></param>
        /// <param name="symbol"></param>
        public void Add(string underlying, Symbol symbol)
        {
            underlying = MomCryptoBrokerage.ConvertToUnderlyingSymbol(symbol.ID.Market, underlying);
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
            _readyEvent.WaitOne();

            var key = symbol.ID.Symbol;
            if (OptionChain.TryGetValue(key, out var symbols))
            {
                return symbols;
            }
            return new List<Symbol>();
        }
    }
}
