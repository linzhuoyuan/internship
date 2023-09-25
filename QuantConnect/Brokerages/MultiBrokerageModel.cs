using System;
using System.Collections.Generic;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Orders.Fills;
using QuantConnect.Orders.Fees;
using System.Linq;
using QuantConnect.Util;

using QuantConnect.Configuration;


namespace QuantConnect.Brokerages
{
    class MultiBrokerageModel :DefaultBrokerageModel
    {

        private const decimal _maxLeverage = 3.3m;

        private Dictionary<string, List<IBrokerageModel>> _mapModels;

        //public static readonly IReadOnlyDictionary<SecurityType, string> DefaultMarketMap = new Dictionary<SecurityType, string>
        //{
        //    {SecurityType.Base, Market.USA},
        //    {SecurityType.Equity, Market.USA},
        //    {SecurityType.Option, Market.USA},
        //    {SecurityType.Future, Market.USA},
        //    {SecurityType.Forex, Market.FXCM},
        //    {SecurityType.Cfd, Market.FXCM},
        //    {SecurityType.Crypto, Market.GDAX}
        //}.ToReadOnlyDictionary();

        public MultiBrokerageModel(Dictionary<string, List<IBrokerageModel>>models, AccountType accountType = AccountType.Margin)
         :base(accountType)
        {
            _mapModels = models;
        }

        public override IReadOnlyDictionary<SecurityType, string> DefaultMarkets { 
            get 
            {
                var map = new Dictionary<SecurityType, string>();
                var market = Config.Get("multi-default-market-equity");
                map[SecurityType.Equity] = market;
                market = Config.Get("multi-default-market-option");
                map[SecurityType.Option] = market;
                market = Config.Get("multi-default-market-crypto");
                map[SecurityType.Crypto] = market;
                market = Config.Get("multi-default-market-future");
                map[SecurityType.Future] = market;

                return map.ToReadOnlyDictionary();
            } 
        }

        public override IBuyingPowerModel GetBuyingPowerModel(Security security)
        {
            if (_mapModels.ContainsKey(security.Symbol.ID.Market)){
                return _mapModels[security.Symbol.ID.Market].First<IBrokerageModel>().GetBuyingPowerModel(security);
            }
            else
            {
                return AccountType == AccountType.Cash
                ? (IBuyingPowerModel)new CashBuyingPowerModel()
                : new SecurityMarginModel(_maxLeverage);
            }
        }

        public override decimal GetLeverage(Security security)
        {
            if (_mapModels.ContainsKey(security.Symbol.ID.Market))
            {
                return _mapModels[security.Symbol.ID.Market].First<IBrokerageModel>().GetLeverage(security);
            }
            else
            {
                if (AccountType == AccountType.Cash || security.IsInternalFeed())
                {
                    return 1m;
                }

                switch (security.Type)
                {
                    case SecurityType.Crypto:
                        return _maxLeverage;
                    case SecurityType.Option:
                        return _maxLeverage;
                    case SecurityType.Future:
                        return _maxLeverage;
                    default:
                        throw new Exception($"Invalid security type: {security.Type}");
                }
            }
        }

        /// <summary>
        /// Provides Bitfinex fee model
        /// </summary>
        /// <param name="security"></param>
        /// <returns></returns>
        public override IFeeModel GetFeeModel(Security security)
        {
            if (_mapModels.ContainsKey(security.Symbol.ID.Market))
            {
                return _mapModels[security.Symbol.ID.Market].First<IBrokerageModel>().GetFeeModel(security);
            }
            else
            {
                throw new Exception($"No Fee Model Object");
                //return new MultiFeeModel();
            }
        }
    }
}
