using System;
using System.Collections.Generic;
using System.Threading;
using QuantConnect.Interfaces;

namespace QuantConnect.Brokerages.MomCrypto
{
    public class MomCryptoFutureChainProvider : IFutureChainProvider
    {
        private readonly MomCryptoBrokerage _brokerage;
        private readonly Dictionary<string, List<Symbol>> _futureChain;
        private readonly ManualResetEvent _readyEvent = new(false);

        /// <summary>
        /// 
        /// </summary>
        public MomCryptoFutureChainProvider(MomCryptoBrokerage brokerage)
        {
            _futureChain = new Dictionary<string, List<Symbol>>();
            _brokerage = brokerage;
        }

        private static string GetKey(string? underlying, Symbol symbol)
        {
            return $"{symbol.id.Market}_{underlying}";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="underlying"></param>
        /// <param name="symbol"></param>
        public void Add(string? underlying, Symbol symbol)
        {
            var key = GetKey(underlying, symbol);
            if (!_futureChain.TryGetValue(key, out var futures))
            {
                futures = new List<Symbol>();
                _futureChain.Add(key, futures);
            }

            futures.Add(symbol);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SetReady()
        {
            _readyEvent.Set();
        }

        /// <summary>
        /// Gets the list of future contracts for a given underlying symbol
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="date"></param>
        /// <returns></returns>
        public IEnumerable<Symbol> GetFutureContractList(Symbol symbol, DateTime date)
        {
            return FindFutureContracts(symbol);
        }

        private IEnumerable<Symbol> FindFutureContracts(Symbol symbol)
        {
            _readyEvent.WaitOne();
            var underlying = symbol.value;
            if (underlying.StartsWith("/"))
            {
                underlying = underlying[1..];
            }

            var key = GetKey(underlying, symbol);
            return _futureChain.TryGetValue(key, out var symbols) ? symbols : new List<Symbol>();
        }
    }
}
