using QuantConnect.Orders;

namespace QuantConnect.OptionQuote
{
    public class OptionPosition
    {
        public decimal NetSize;
        public decimal EntryPrice;
        public decimal Size;
        public OptionInfo? Option;
        public OrderDirection? Side;
        public decimal? PessimisticValuation;
        public decimal? PessimisticIndexPrice;
        public decimal? PessimisticVol;

        public override string ToString()
        {
            return $"{Option}-{NetSize}-{EntryPrice}-{Side}";
        }
    }

}