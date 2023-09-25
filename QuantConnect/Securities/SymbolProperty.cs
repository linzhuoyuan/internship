

namespace QuantConnect.Securities
{
    // 分红调整改动：更新行权价和合约乘数； writter:lh
    public class SymbolProperty
    {
        public SymbolProperty(
            string contractId,
            string underlyingTicker,
            string contractType,
            string optionRight,
            decimal strikePrice,
            string expiryDate,
            string tradeDate,
            decimal contractMultiplier)
        {
            ContractId = contractId;
            UnderlyingTicker = underlyingTicker;
            ContractType = contractType;
            OptionRight = optionRight;
            StrikePrice = strikePrice;
            ExpiryDate = expiryDate;
            OptionType = "european";
            TradeDate = tradeDate;
            ContractMultiplier = contractMultiplier;
        }

        public string ContractId
        {
            get;
            private set;
        }

        public string UnderlyingTicker
        {
            get;
            private set;
        }

        public string ContractType
        {
            get;
            private set;
        }

        public string OptionType
        {
            get;
            private set;
        }

        public string OptionRight
        {
            get;
            private set;
        }

        public decimal StrikePrice
        {
            get;
            private set;
        }

        public string ExpiryDate
        {
            get;
            private set;
        }

        public string TradeDate
        {
            get;
            private set;
        }

        public decimal ContractMultiplier
        {
            get;
            private set;
        }
    }
}
