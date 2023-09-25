using System;

namespace QuantConnect.Data.Market
{
    public class SymbolBonusEvents : DataDictionary<SymbolBonusEvent>
    {
        public SymbolBonusEvents()
        {
        }

        public SymbolBonusEvents(DateTime frontier)
            : base(frontier)
        {
        }

        public new SymbolBonusEvent this[Symbol symbol] { get { return base[symbol]; } set { base[symbol] = value; } }

    }
}
