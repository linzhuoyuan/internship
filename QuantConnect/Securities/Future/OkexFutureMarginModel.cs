/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuantConnect.Securities.Future
{
    /// <summary>
    /// Represents a simple margining model for margining futures.
    /// </summary>
    public class OkexFutureMarginModel : SecurityMarginModel
    {
      
        public static Dictionary<string, decimal> OkexFutureContractValForUSD = new Dictionary<string, decimal>
        {
            { "BTC" , 100 }, // 面值 一张合约 100美元
            { "LTC" , 10 },
            { "ETH" , 10 },
            { "ETC" , 10 },
            { "XRP" , 10 },
            { "EOS" , 10 },
            { "BCH" , 10 },
            { "BSV" , 10 },
            { "TRX" , 10 }
        };

        public static Dictionary<string, decimal> OkexFutureContractValForUSDT = new Dictionary<string, decimal>
        {
            { "BTC" , (decimal)0.0001 },    // 面值 一张合约 0.0001BTC
            { "LTC" , (decimal)0.001 },     // 面值 一张合约 0.001LTC
            { "ETH" , (decimal)0.001 },
            { "ETC" , (decimal)0.1 },
            { "XRP" , (decimal)1 },
            { "EOS" , (decimal)0.1 },       // 面值 一张合约  0.1EOS
            { "BCH" , (decimal)0.001 },
            { "BSV" , (decimal)0.001 },
            { "TRX" , (decimal)1 }
        };

        private Dictionary<string,OkFutureLeverage> _leverages = new Dictionary<string, OkFutureLeverage>();
        class OkFutureLeverage
        {
            //long_leverage String  多仓杠杆
            public string long_leverage { get; set; }
            //margin_mode String 持仓模式
            public string margin_mode { get; set; }
            //short_leverage String  空仓杠杆
            public string short_leverage { get; set; }
            //instrument_id String 合约名称
            public string instrument_id { get; set; }
        }
        private string ReadLeverageFile(string type)
        {
            string path = "okex-leverage";
            if (!Directory.Exists(path))
            {
                throw new Exception($"okex leverage directory not exist");
            }
            string filename = type + "-leverage.json";
            filename = path + "\\" + filename;
            if (!File.Exists(filename))
            {
                throw new Exception($"okex fee file {filename} not exist");
            }
            return File.ReadAllText(filename);
        }

        private void ReadLeverageFile()
        {
            var content = ReadLeverageFile("futures");
            var list = JArray.Parse(content);
            foreach (JObject i in list)
            {
                OkFutureLeverage item = i.ToObject<OkFutureLeverage>();
                _leverages.Add(item.instrument_id, item);
            }
            content = ReadLeverageFile("swap");
            list = JArray.Parse(content);
            foreach (JObject i in list)
            {
                OkFutureLeverage item = i.ToObject<OkFutureLeverage>();
                _leverages.Add(item.instrument_id, item);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FutureMarginModel"/>
        /// </summary>
        /// <param name="requiredFreeBuyingPowerPercent">The percentage used to determine the required unused buying power for the account.</param>
        public OkexFutureMarginModel(decimal requiredFreeBuyingPowerPercent = 0)
        {
            RequiredFreeBuyingPowerPercent = requiredFreeBuyingPowerPercent;
            ReadLeverageFile();
        }

        /// <summary>
        /// Gets the current leverage of the security
        /// </summary>
        /// <param name="security">The security to get leverage for</param>
        /// <returns>The current leverage in the security</returns>
        public override decimal GetLeverage(Security security)
        {
            var symbol = security.Symbol;
            if (_leverages.ContainsKey(symbol.Value))
            {
                return Convert.ToDecimal(_leverages[symbol.Value].long_leverage);
            }
            return 1;
        }

        /// <summary>
        /// Sets the leverage for the applicable securities, i.e, futures
        /// </summary>
        /// <remarks>
        /// This is added to maintain backwards compatibility with the old margin/leverage system
        /// </remarks>
        /// <param name="security"></param>
        /// <param name="leverage">The new leverage</param>
        public override void SetLeverage(Security security, decimal leverage)
        {
            // Futures are leveraged products and different leverage cannot be set by user.
            throw new InvalidOperationException("Futures are leveraged products and different leverage cannot be set by user");
        }

        /// <summary>
        /// Gets the total margin required to execute the specified order in units of the account currency including fees
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the order</param>
        /// <returns>The total margin in terms of the currency quoted in the order</returns>
        protected override decimal GetInitialMarginRequiredForOrder(
            InitialMarginRequiredForOrderParameters parameters)
        {
            //Get the order value from the non-abstract order classes (MarketOrder, LimitOrder, StopMarketOrder)
            //Market order is approximated from the current security price and set in the MarketOrder Method in QCAlgorithm.

            var fees = parameters.Security.FeeModel.GetOrderFee(
                new OrderFeeParameters(parameters.Security,
                    parameters.Order)).Value;
            var feesInAccountCurrency = parameters.CurrencyConverter.
                ConvertToAccountCurrency(fees).Amount;

            decimal value = 0;
            decimal orderValue = 0;
            //所需保证金=仓位价值／所选杠杆倍数

            var symbol = parameters.Security.Symbol;
            var key = symbol.Value.Substring(0, 3);
            if (OkexFutureHolding.IsFutureUSD(symbol.Value))
            {
                //没有介绍，应该和永续合约的USD结算合约相同
                value = OkexFutureContractValForUSD[key] * parameters.Order.Quantity;
                var btc_price = parameters.Portfolio.Securities["BTCUSD"].Price;
                orderValue = value * (1/btc_price) * GetInitialMarginRequirement(parameters.Security);
            }
            else if (OkexFutureHolding.IsFutureUSDT(symbol.Value))
            {
                //假设当前BTC价格为10000USDT/BTC，用户希望使用10倍杠杆开多1BTC等值的交割合约，用户开多张数=开多BTC数量/面值=1/0.0001=10000张。
                //用户所需保证金 = 面值 * 张数 * BTC价格 / 杠杆倍数 = 0.0001 * 10000 * 10000 / 10 = 1000USDT
                value = OkexFutureContractValForUSDT[key] * parameters.Order.Quantity;
                var btc_price = parameters.Portfolio.Securities["BTCUSD"].Price;
                orderValue = value * btc_price * GetInitialMarginRequirement(parameters.Security);
            }
            else if (OkexFutureHolding.IsSwapUSD(symbol.Value))
            {
                //假设当前BTC价格为$10000，用户希望使用10倍杠杆开多1BTC等值的永续合约，
                //用户开多张数 = 开多BTC数量 * BTC价格／面值 = 1 * 10000 / 100 = 100张。
                //用户所需保证金 = 面值 * 张数／（BTC价格* 杠杆倍数）= 100 * 100 /（10000 * 10）= 0.1BTC
                value = OkexFutureContractValForUSD[key] * parameters.Order.Quantity;
                var btc_price = parameters.Portfolio.Securities["BTCUSD"].Price;
                orderValue = value * (1 / btc_price) * GetInitialMarginRequirement(parameters.Security);
            }
            else if (OkexFutureHolding.IsSwapUSDT(symbol.Value))
            {
                //假设当前BTC价格为10000USDT/BTC，用户希望使用10倍杠杆开多1BTC等值的永续合约，用户开多张数=开多BTC数量/面值=1/0.0001=10000张。
                //用户所需保证金 = 面值 * 张数 * BTC价格 / 杠杆倍数 = 0.0001 * 10000 * 10000 / 10 = 1000USDT
                value = OkexFutureContractValForUSDT[key] * parameters.Order.Quantity;
                var btc_price = parameters.Portfolio.Securities["BTCUSD"].Price;
                orderValue = value * btc_price * GetInitialMarginRequirement(parameters.Security);
            }

            return orderValue + Math.Sign(orderValue) * feesInAccountCurrency;
        }

        /// <summary>
        /// Gets the margin currently alloted to the specified holding
        /// </summary>
        /// <param name="security">The security to compute maintenance margin for</param>
        /// <returns>The maintenance margin required for the </returns>
        protected override decimal GetMaintenanceMargin(Security security)
        {
            if (security?.GetLastData() == null || security.Holdings.HoldingsCost == 0m)
                return 0m;

            var symbol = security.Symbol;
            if (OkexFutureHolding.IsFutureUSD(symbol.Value))
            {
                //全仓保证金模式下，开仓的要求是开仓后保证金率不能低于100%。
                //成交后，则用户持有对应多空方向的仓位。全仓保证金下，用户的账户权益将根据最新成交价增加或减少；
                //全仓模式下，当用户的账户权益，10杠杆下，合约账户权益不足保证金的10%，20倍杠杆下，BTC合约账户权益不足保证金的20%时

            }
            else if (OkexFutureHolding.IsFutureUSDT(symbol.Value))
            {
                return 0;
            }
            else if (OkexFutureHolding.IsSwapUSD(symbol.Value))
            {
                return 0;
            }
            else if (OkexFutureHolding.IsSwapUSDT(symbol.Value))
            {
                return 0;
            }


            return Math.Sign(security.Holdings.HoldingsCost);
        }

        /// <summary>
        /// Gets the margin cash available for a trade
        /// </summary>
        /// <param name="portfolio">The algorithm's portfolio</param>
        /// <param name="security">The security to be traded</param>
        /// <param name="direction">The direction of the trade</param>
        /// <param name="offset">The offset of the trade</param>
        /// <param name="orderId">The orderId of the trade</param>
        /// <returns>The margin available for the trade</returns>
        protected override decimal GetMarginRemaining(SecurityPortfolioManager portfolio, Security security, OrderDirection direction, 
            OrderOffset offset = OrderOffset.None, long orderId = 0)
        {
            var totalPortfolioValue = portfolio.GetTotalPortfolioValueForCurrency(security.QuoteCurrency.Symbol);
            var result = portfolio.GetMarginRemainingForCurrency(security.QuoteCurrency.Symbol, totalPortfolioValue);
            return result;
        }

        /// <summary>
        /// The percentage of an order's absolute cost that must be held in free cash in order to place the order
        /// </summary>
        protected override decimal GetInitialMarginRequirement(Security security)
        {
            //初始保证金率：=1/杠杆倍数
            var symbol = security.Symbol;
            if (_leverages.ContainsKey(symbol.Value))
            {
                return 1 / Convert.ToDecimal(_leverages[symbol.Value].long_leverage);
            }
            return 0;
        }

        /// <summary>
        /// The percentage of the holding's absolute cost that must be held in free cash in order to avoid a margin call
        /// </summary>
        public override decimal GetMaintenanceMarginRequirement(Security security)
        {
            return 0;
        }        
        
    }
}