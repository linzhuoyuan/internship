
namespace QuantConnect.Securities
{
    class SymbolBonus
    {
        public SymbolBonus(
            string tradeDate, 
            string contractId, 
            string contractType, 
            decimal contractMultiplier, 
            string underlyingTicker, 
            string expiryDate, 
            decimal strikePrice)
        {
            TradeDate = tradeDate;
            ContractId = contractId;
            ContractType = contractType;
            ContractMultiplier = contractMultiplier;
            UnderlyingTicker = underlyingTicker;
            StrikePrice = strikePrice;
            ExpiryDate = expiryDate;
        }

        public string TradeDate;

        public string ContractId;

        public string ContractType;

        public decimal ContractMultiplier;

        public string UnderlyingTicker;

        public decimal StrikePrice;

        public string ExpiryDate;

    }
}
