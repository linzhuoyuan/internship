using System;
using System.Collections.Generic;

namespace QuantConnect.Interfaces
{
    public class BrokerageAccountInfo
    {
        public DateTime DateTime;
        public string Id;
        public bool IsFundAccount;
        public string AccountId;
        public string AccountType;
        public string Currency;
        public decimal Available;
        public decimal BuyingPower;
        public decimal CashBalance;
        public decimal MaintMargin;
        public decimal UnrealizedPnL;
        public decimal RealizedPnL;
        public decimal CashFlow;
        public decimal FinancingUsed;
        public decimal FinancingCommission;
        public decimal FinancingRate;
        public decimal OptionMarketValue;
        public decimal StockMarketValue;
        public decimal GuaranteeRate;
        public Dictionary<string, string> CustomData;
    }
}