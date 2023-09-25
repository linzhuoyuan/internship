using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Securities;
using QuantConnect.Securities.Future;
using QuantConnect.Securities.Option;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Provides properties specific to Mom
    /// </summary>
    public  class MomCryptoBrokerageModel :  DefaultBrokerageModel
    {
        private bool _enableSpotMarginForFTX;
        /// <summary>
        /// 
        /// </summary>
        public override IReadOnlyDictionary<SecurityType, string> DefaultMarkets { get; } = GetDefaultMarkets();
        /// <summary>
        /// Initializes a new instance of the <see cref="MomDeribitBrokerageModel"/> class
        /// </summary>
        /// <param name="accountType">The type of account to be modelled, defaults to
        /// <see cref="QuantConnect.AccountType.Margin"/></param>
        public MomCryptoBrokerageModel(bool enableSpotMarginForFTX, AccountType accountType = AccountType.Margin)
             : base(accountType)
        {
            _enableSpotMarginForFTX = enableSpotMarginForFTX;
        }

        /// <summary>
        /// Gets a new fee model that represents this brokerage's fee structure
        /// </summary>
        /// <param name="security"></param>
        /// <returns></returns>
        public override IFeeModel GetFeeModel(Security security)
        {
            return new DeribitFeeModel();
        }

        /// <summary>
        /// Gets a new buying power model for the security, returning the default model with the security's configured leverage.
        /// For cash accounts, leverage = 1 is used.
        /// For margin trading, max leverage = 3.3
        /// </summary>
        /// <param name="security">The security to get a buying power model for</param>
        /// <returns>The buying power model for this brokerage/security</returns>
        public override IBuyingPowerModel GetBuyingPowerModel(Security security)
        {
            if (security.Symbol.ID.Market == Market.Binance)
            {
                return new InfiniteBuyingPowerModel();
            }

            switch (security.Type)
            {
                case SecurityType.Forex:
                    return new CashBuyingPowerModel();
                case SecurityType.Crypto:
                    return new CashBuyingPowerModel();
                case SecurityType.Option:
                    return new DeribitOptionMarginModel(RequiredFreeBuyingPowerPercent);
                case SecurityType.Future:
                    if (security.Symbol.ID.Market == Market.Deribit)
                        return new DeribitFutureMarginModel(RequiredFreeBuyingPowerPercent);
                    return new FutureMarginModel(RequiredFreeBuyingPowerPercent);
    
                default:
                    throw new Exception($"Invalid security type: {security.Type}");
            }
        }

        private static IReadOnlyDictionary<SecurityType, string> GetDefaultMarkets()
        {
            var map = DefaultMarketMap.ToDictionary();
            map[SecurityType.Crypto] = Market.Binance;
            map[SecurityType.Future] = Market.Binance;
            map[SecurityType.Option] = Market.Deribit;
            return map.ToReadOnlyDictionary();
        }

        public override ISettlementModel GetSettlementModel(Security security)
        {
            return new ImmediateSettlementModel();
        }



    }
}
