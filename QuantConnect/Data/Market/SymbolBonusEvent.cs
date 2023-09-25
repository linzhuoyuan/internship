using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Data.Market
{
    public class SymbolBonusEvent : BaseData
    {

        public decimal StrikePrice { get; set; }

        public decimal ContractMulti { get; set; }

        public SymbolBonusEvent()
        {
            DataType = MarketDataType.Auxiliary;
        }

        public SymbolBonusEvent(Symbol symbol, decimal strikePrice, decimal contractMulti, DateTime date) 
            : this()
        {
            Time = date;
            Symbol = symbol;
            StrikePrice = strikePrice;
            ContractMulti = contractMulti;
        }

        public override BaseData Clone()
        {
            return new SymbolBonusEvent(Symbol, StrikePrice, ContractMulti, Time);
        }
    }
}
