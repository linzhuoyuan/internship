using System;
using System.Collections.Generic;
using System.Threading;
using QuantConnect.Interfaces;

namespace QuantConnect.Brokerages.MomCrypto
{
    public class MomCryptoChainProvider : ICryptoChainProvider
    {
        private readonly MomCryptoBrokerage _brokerage;
        private readonly Dictionary<string, List<Symbol>> _cryptoChain;
        private readonly ManualResetEvent _readyEvent = new(false);

        public MomCryptoChainProvider(MomCryptoBrokerage brokerage)
        {
            _cryptoChain = new Dictionary<string, List<Symbol>>();
            _brokerage = brokerage;
        }

        public void Add(Symbol symbol)
        {
            if (!_cryptoChain.TryGetValue(MomCryptoSymbolMapper.GetSymbolKey(symbol), out var symbols))
            {
                symbols = new List<Symbol>();
                _cryptoChain.Add(MomCryptoSymbolMapper.GetSymbolKey(symbol), symbols);
            }

            symbols.Add(symbol);
        }

        public void SetReady()
        { 
            _readyEvent.Set();
        }

        public IEnumerable<Symbol> GetCryptoContractList(Symbol symbol, DateTime date)
        {
            _readyEvent.WaitOne();
            return _cryptoChain.TryGetValue(MomCryptoSymbolMapper.GetSymbolKey(symbol), out var symbols) ? symbols : new List<Symbol>();
        }
    }
}