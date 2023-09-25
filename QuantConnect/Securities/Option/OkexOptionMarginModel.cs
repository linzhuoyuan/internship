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
using System.IO;
using System.Collections.Generic;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.Securities.Future;

namespace QuantConnect.Securities.Option
{
    /// <summary>
    /// okex 期权规则
    /// 合约乘数 0.1 btc
    /// 平台展示的期权合约账户信息包括账户权益、账户余额、可用保证金、已用保证金、维持保证金、已实现盈亏、未实现盈亏和期权市值。
    /// 账户权益：是指用户在期权合约账户中实际拥有的全部资产。
    /// 账户权益 = 账户余额 + 期权市值。
    /// 账户余额：账户中数字资产现货的数量，包括冻结的保证金部分。
    /// 保证金：为确保交易双方能够进行期权交易活动（买入期权、卖出期权、持有期权、到期日结算行权等），而暂时在账户中被冻结的一部分资金，不能用作其他用途。
    ///        买方与卖方冻结的保证金类型不同，买方只在挂单的时候冻结保证金，持仓不冻结保证金；卖方要冻结挂单保证金、持仓保证金作为履约的保证。
    /// 可用保证金：当前可以用于开仓的金额。
    /// 已用保证金：用户当前持仓所需的保证金和所有开仓委托所冻结的保证金。
    /// 维持保证金：维持该账户内的所有仓位所需的最少的保证金，若账户余额低于最低维持保证金，投资者仓位将触发强制减仓。
    /// 已实现盈亏：以上一个结算时间为起始，当前时间为结点的用户已平仓位产生的盈亏。已实现盈亏计入账户权益，可作为保证金但不可提现，
    ///            在当期结算后，账户所产生的已实现盈亏才可以提出交易账户。
    /// 未实现盈亏：以上一个结算时间为起始，当前时间为结点的用户未平仓位产生的盈亏，实际上也已实时体现在期权市值里。
    /// 期权市值：当前持有的期权仓位在市场上的价值。
    /// 期权市值 = 标记价格* 合约乘数* 持仓张数。
    /// 
    /// 
    /// 合约已实现盈亏：以上一个结算时间为起始，当前时间为结点的用户已平仓位产生的盈亏。已实现盈亏计入账户权益，可作为保证金但不可提现，
    ///               在当期结算后，账户所产生的已实现盈亏才可以提出交易账户。
    /// 多头平仓：（平仓成交价- 结算基准价）* 合约乘数* 成交张数；
    /// 举例说明：某用户以0.02 BTC的价格买入2张BTC合约，合约乘数为0.1，结算基准价为0.03 BTC，然后以价格0.04 BTC卖出平多1张合约，则合约已实现盈亏=（0.04 - 0.03）*0.1*1=0.001 BTC。
    /// 空头平仓：（结算基准价- 平仓成交价）* 合约乘数* 成交张数；
    /// 举例说明：某用户以结算基准价为0.03 BTC卖出开空10张合约，然后以价格0.01BTC买入平空8张合约，则合约已实现盈亏=（0.03 - 0.01）*0.1*8=0.016 BTC。
    /// 
    /// 合约未实现盈亏：以上一个结算时间为起始，当前时间为结点的用户未平仓位产生的盈亏，实际上也已实时体现在期权市值里。
    /// 多头持仓：未实现盈亏=标记价格* 合约乘数* 持仓张数 -开仓均价(或结算价格)* 合约乘数* 持仓张数；
    /// 举例说明：某用户以0.03 BTC的结算基准价买入2张BTC合约，标记价格为0.04BTC，则合约未实现盈亏= 0.04*0.1*2- 0.03*0.1*2 = 0.002 BTC。
    /// 空头持仓：未实现盈亏=开仓均价(或结算价格)* 合约乘数* 持仓张数 - 标记价格* 合约乘数* 持仓张数；
    /// 举例说明：某用户以0.03 BTC的结算基准价卖出5张BTC合约，标记价格为0.02BTC，则合约未实现盈亏= 0.03*0.1*5- 0.02*0.1*5 = 0.005 BTC。
    /// 
    /// 
    /// 期权交易过程中涉及四种保证金，挂单保证金、持仓保证金、维持保证金、已用保证金。

    /// 挂单保证金：每笔挂单成交前，需要冻结的保证金。挂单保证金是为了确保用户能有足够的资金购买在挂合约、或考虑期权费后仍有足够的资金履约，
    ///            且假设成交后针对此挂单合约有对应的持仓保证金。

    /// 开仓：
    /// 买入开仓挂单保证金= (挂单价格x合约乘数+ 手续费) x挂单张数；
    /// 卖出开仓挂单保证金= max(持仓保证金- 挂单价格x合约乘数+ 手续费，最低挂单保证金x合约乘数) x挂单张数。卖出期权订单成交时，卖方支付手续费、收到期权费，冻结持仓保证金。

    /// 平仓：
    /// 卖出来平仓的挂单保证金：(max (手续费- 挂单价格, 0) x合约乘数) x挂单张数；
    /// 买入来平仓的挂单保证金：(max (挂单价格- 持仓保证金/合约乘数 + 手续费, 0 ) x合约乘数) x挂单张数。卖方想要平仓需要支付期权费、手续费，可以释放卖方当前持仓的保证金。
    /// 持仓保证金：持有当前仓位需要占用的保证金。

    /// 买方的持仓保证金= 0；
    /// 看涨期权的卖方：持仓保证金= [max(0.1, 0.15 - 虚值程度 / 同到期日交割合约的标记价格) + 期权标记价格] x合约乘数x持仓张数；
    /// 看跌期权的卖方：持仓保证金= [max(0.1 x(1 + 期权标记价格), 0.15 - 虚值程度 / 同到期日交割合约的标记价格) + 期权标记价格] x合约乘数x持仓张数。
    /// 虚值程度：此处虚值程度仅用于计算账户风险。以看涨期权为例，当远期价格（即同到期日交割合约的标记价格）低于执行价格时，此时的期权为虚值期权，
    ///          行权对买方不利(因为买方没有必要用更高的执行价格去买入标的资产)，则买方会选择不行权。看涨期权的虚值程度就代表了远期价格低于执行价格的程度
    ///          ，二者相差越大，虚值程度越深。看跌期权同理。 
    /// 注：“期权标记价格”以BTC计价、“持仓张数”是实际持仓张数的绝对值，无正负号。
    /// 看涨期权虚值程度=执行价格-同期交割合约标记价格      
    /// 看跌期权虚值程度=同期交割合约标记价格-执行价格

    /// 维持保证金：为了维持当前持仓仓位安全，账户所需的最低保证金要求。当保证金余额低于维持保证金时，账户将触发强制减仓。
    /// 买方维持保证金 = 0；
    /// 看涨期权的卖方：维持保证金= (0.075 + 标记价格) x合约乘数x持仓张数；
    /// 看跌期权的卖方：维持保证金= (0.075 x(1 + 标记价格) + 标记价格) x合约乘数x持仓张数。
    /// 注：“标记价格”以BTC计价、“持仓张数”是实际持仓张数的绝对值，无正负号。
    /// 已用保证金：已用保证金等于挂单保证金与持仓保证金之和。
    /// </summary>

    public class OkexOptionMarginModel : SecurityMarginModel
    {
        private SecurityPortfolioManager _portfolio = null;
        Dictionary<string, OkOptionLeverage> _leverages = new Dictionary<string, OkOptionLeverage>();
        class OkOptionLeverage
        {
            //leverage String  杠杆
            public string leverage { get; set; }
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
            var content = ReadLeverageFile("option");
            var list = JArray.Parse(content);
            foreach (JObject i in list)
            {
                OkOptionLeverage item = i.ToObject<OkOptionLeverage>();
                _leverages.Add(item.instrument_id, item);
            }
        }


        // initial margin
        private const decimal OptionMarginRequirement = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionMarginModel"/>
        /// </summary>
        /// <param name="requiredFreeBuyingPowerPercent">The percentage used to determine the required unused buying power for the account.</param>
        public OkexOptionMarginModel(decimal requiredFreeBuyingPowerPercent = 0)
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
            // Options are not traded on margin
            return 1;
        }

        /// <summary>
        /// Sets the leverage for the applicable securities, i.e, options.
        /// </summary>
        /// <param name="security"></param>
        /// <param name="leverage">The new leverage</param>
        public override void SetLeverage(Security security, decimal leverage)
        {
            // Options are leveraged products and different leverage cannot be set by user.
            throw new InvalidOperationException("Options are leveraged products and different leverage cannot be set by user");
        }

        /// <summary>
        /// Gets the total margin required to execute the specified order in units of the account currency including fees
        /// </summary>
        /// <param name="parameters">An object containing the portfolio, the security and the order</param>
        /// <returns>The total margin in terms of the currency quoted in the order</returns>
        protected override decimal GetInitialMarginRequiredForOrder(
            InitialMarginRequiredForOrderParameters parameters)
        {
            decimal margin = 0;
            var buy = parameters.Order.Quantity > 0 ? true : false;
            if (parameters.Security.Holdings.Quantity == 0)
            {
                if (buy) //买入开仓
                {
                    margin = GetInitialMarginRequiredForOrder_BuyOpen(parameters);
                }
                else //卖出开仓
                {
                    margin = GetInitialMarginRequiredForOrder_SellOpen(parameters);
                }
            }
            else if (parameters.Security.Holdings.IsLong)
            {
                if (buy) //买入开仓
                {
                    margin = GetInitialMarginRequiredForOrder_BuyOpen(parameters);
                }
                else //卖出平仓
                {
                    margin = GetInitialMarginRequiredForOrder_SellClose(parameters);
                }
            }
            else if (parameters.Security.Holdings.IsShort)
            {
                if (buy) //买入平仓
                {
                    margin = GetInitialMarginRequiredForOrder_BuyClose(parameters);
                }
                else //卖出开仓
                {
                    margin = GetInitialMarginRequiredForOrder_SellOpen(parameters);
                }
            }
            return margin;
        }

        /// <summary>
        /// Gets the margin currently alloted to the specified holding
        /// </summary>
        /// <param name="security">The security to compute maintenance margin for</param>
        /// <returns>The maintenance margin required for the </returns>
        protected override decimal GetMaintenanceMargin(Security security)
        {
            return security.Holdings.AbsoluteHoldingsCost * GetMaintenanceMarginRequirement(security, security.Holdings.HoldingsCost);
        }

        /// <summary>
        /// Gets the margin cash available for a trade 剩余保证金
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
            _portfolio = portfolio;
            var totalPortfolioValue = portfolio.GetTotalPortfolioValueForCurrency(security.QuoteCurrency.Symbol);
            var result = portfolio.GetMarginRemainingForCurrency(security.QuoteCurrency.Symbol, totalPortfolioValue);
            return result;
        }

        /// <summary>
        /// The percentage of an order's absolute cost that must be held in free cash in order to place the order
        /// </summary>
        protected override decimal GetInitialMarginRequirement(Security security)
        {
            return GetInitialMarginRequirement(security, security.Holdings.HoldingsValue);
        }

        /// <summary>
        /// The percentage of the holding's absolute cost that must be held in free cash in order to avoid a margin call
        /// </summary>
        public override decimal GetMaintenanceMarginRequirement(Security security)
        {
            //return GetMaintenanceMarginRequirement(security, security.Holdings.HoldingsValue);
            return 1;
        }

        /// <summary>
        /// The percentage of an order's absolute cost that must be held in free cash in order to place the order
        /// </summary>
        private decimal GetInitialMarginRequirement(Security security, decimal value)
        {
            var option = (Option)security;

            if (value > 0m)
            {
                return OptionMarginRequirement;
            }
            decimal optionMargin = 0;
            var absValue = -value;
            var optionProperties = (OptionSymbolProperties)option.SymbolProperties;
            var underlying = option.Underlying;
            
            
            return optionMargin;
        }

        private string GetFutureUnderlying(string okexSymbol)
        {
            var len = okexSymbol.LastIndexOf('-');

            var substr = okexSymbol.Substring(0, len).ToUpper();

            if (substr.Contains("USDT"))
            {
                substr = substr.Replace("USDT", "UST");
            }
            // return as it is due to Okex has similar Symbol format
            return substr.Replace("-", "").ToUpper();
        }

        /// <summary>
        /// The percentage of the holding's absolute cost that must be held in free cash in order to avoid a margin call
        /// </summary>
        private decimal GetMaintenanceMarginRequirement(Security security, decimal value)
        {
            var option = (Option)security;

            if (value > 0m)
            {
                return OptionMarginRequirement;
            }

            decimal optionMargin = 0;
            var absValue = -value;
            var optionProperties = (OptionSymbolProperties)option.SymbolProperties;
            var underlying = option.Underlying;

            if (option.Right == OptionRight.Call)
            {
                optionMargin = ((decimal)0.075 + option.Cache.MarkPrice);
            }
            else
            {
                optionMargin = ((decimal)0.075 * (1 + option.Cache.MarkPrice) *option.Cache.MarkPrice);
            }
            //合约乘数x持仓张数
            optionMargin = optionMargin * optionProperties.ContractMultiplier * option.Holdings.AbsoluteQuantity * option.QuoteCurrency.ConversionRate;
            return optionMargin/absValue;
        }

        /// <summary>
        /// Private method takes option security and its holding and returns required margin. Method considers all short positions naked.
        /// </summary>
        /// <param name="portfolio">portfolio</param>
        /// <param name="security">Option security</param>
        /// <param name="value">Holding value</param>
        /// <returns></returns>
        /// 持仓保证金
        private decimal GetHoldingMargin(SecurityPortfolioManager portfolio, Security security, decimal value)
        {
            var option = (Option)security;

            if (value == 0m ||
                option.Close == 0m ||
                option.StrikePrice == 0m ||
                option.Underlying == null ||
                option.Underlying.Close == 0m)
            {
                return 0m;
            }
            // value :market value,value>0, is buy. so return 1
            if (value > 0m)
            {
                return 0;
            }
            //value < 0

            decimal optionMargin = 0;
            var absValue = -value;
            var optionProperties = (OptionSymbolProperties)option.SymbolProperties;
            var underlying = option.Underlying;

            /// 看涨期权的卖方：持仓保证金= [max(0.1, 0.15 - 虚值程度 / 同到期日交割合约的标记价格) + 期权标记价格] x合约乘数x持仓张数；
            /// 看跌期权的卖方：持仓保证金= [max(0.1 x(1 + 期权标记价格), 0.15 - 虚值程度 / 同到期日交割合约的标记价格) + 期权标记价格] x合约乘数x持仓张数。

            /// 看涨期权虚值程度=执行价格-同期交割合约标记价格      
            /// 看跌期权虚值程度=同期交割合约标记价格-执行价格

            if (portfolio == null)
            {
                throw new Exception($"OkexOptionMarginModel _portfolio == null");
            }


            decimal future_markprice = 0;
            //获取交割合约的标记价格
            foreach (var item in _portfolio.Securities)
            {
                if (item.Key.SecurityType == SecurityType.Future && !item.Key.Value.Contains("SWAP"))
                {
                    //标的相同
                    if (GetFutureUnderlying(item.Key.Value) == underlying.Symbol.Value)
                    {
                        //到期日相同
                        var future = (Future.Future)item.Value;
                        if (option.Expiry == future.Expiry)
                        {
                            future_markprice = future.Cache.MarkPrice;
                        }
                    }
                }
            }

            if (option.Right == OptionRight.Call)
            {
                var vitualRatio = option.StrikePrice - future_markprice;
                optionMargin = (Math.Max((decimal)0.1, ((decimal)0.15 - (vitualRatio / future_markprice))) + option.Cache.MarkPrice);
            }
            else
            {
                var vitualRatio = future_markprice - option.StrikePrice;
                optionMargin = (Math.Max(((decimal)0.1 * (1 + option.Cache.MarkPrice)), ((decimal)0.15 - (vitualRatio / future_markprice))) + option.Cache.MarkPrice);
            }
            //合约乘数x持仓张数
            optionMargin = optionMargin * optionProperties.ContractMultiplier * option.Holdings.AbsoluteQuantity * option.QuoteCurrency.ConversionRate;

            return optionMargin;
        }

        private decimal GetInitialMarginRequiredForOrder_BuyOpen(
            InitialMarginRequiredForOrderParameters parameters)
        {
            //买入开仓挂单保证金 = (挂单价格x合约乘数 + 手续费) x挂单张数；
            var fees = parameters.Security.FeeModel.GetOrderFee(
                new OrderFeeParameters(parameters.Security,
                    parameters.Order)).Value;
            var feesInAccountCurrency = parameters.CurrencyConverter.
                ConvertToAccountCurrency(fees).Amount;

            var value = parameters.Order.GetValue(parameters.Security);
            var orderValue = value * parameters.Security.QuoteCurrency.ConversionRate * parameters.Security.SymbolProperties.ContractMultiplier;
            // Math.Sign only return 1 or -1
            return orderValue + Math.Sign(orderValue) * feesInAccountCurrency;
        }

        private decimal GetInitialMarginRequiredForOrder_BuyClose(
            InitialMarginRequiredForOrderParameters parameters)
        {
            //买入来平仓的挂单保证金 =( max (挂单价格- 持仓保证金/合约乘数 + 手续费, 0 ) x合约乘数) x挂单张数。
            //卖方想要平仓需要支付期权费、手续费，可以释放卖方当前持仓的保证金。

            var fees = parameters.Security.FeeModel.GetOrderFee(
                new OrderFeeParameters(parameters.Security,
                    parameters.Order)).Value;
            var feesInAccountCurrency = parameters.CurrencyConverter.
                ConvertToAccountCurrency(fees).Amount;

            var value = parameters.Order.GetValue(parameters.Security);
            var orderValue = value * parameters.Security.QuoteCurrency.ConversionRate * parameters.Security.SymbolProperties.ContractMultiplier;

            var holdingMargin = GetHoldingMargin(parameters.Portfolio, parameters.Security, parameters.Security.Holdings.HoldingsValue);

            return Math.Max((orderValue - holdingMargin + feesInAccountCurrency), 0);
        }

        private decimal GetInitialMarginRequiredForOrder_SellOpen(
            InitialMarginRequiredForOrderParameters parameters)
        {
            //卖出开仓挂单保证金= max (持仓保证金- 挂单价格x合约乘数+ 手续费，最低挂单保证金x合约乘数) x挂单张数。
            //最低挂单保证金为0.1btc 固定的
            //卖出期权订单成交时，卖方支付手续费、收到期权费，冻结持仓保证金。

            var fees = parameters.Security.FeeModel.GetOrderFee(
               new OrderFeeParameters(parameters.Security,
                   parameters.Order)).Value;
            var feesInAccountCurrency = parameters.CurrencyConverter.
                ConvertToAccountCurrency(fees).Amount;

            var value = parameters.Order.GetValue(parameters.Security);
            var orderValue = value * parameters.Security.QuoteCurrency.ConversionRate * parameters.Security.SymbolProperties.ContractMultiplier;

            var holdingMargin = GetHoldingMargin(parameters.Portfolio, parameters.Security, parameters.Security.Holdings.HoldingsValue);
            var minimumMargin = (decimal)0.1 * parameters.Order.Quantity * parameters.Security.QuoteCurrency.ConversionRate * parameters.Security.SymbolProperties.ContractMultiplier;

            return Math.Max((orderValue - holdingMargin + feesInAccountCurrency), 0);

        }

        private decimal GetInitialMarginRequiredForOrder_SellClose(
            InitialMarginRequiredForOrderParameters parameters)
        {
            //卖出来平仓的挂单保证金 =( max (手续费- 挂单价格, 0) x合约乘数) x挂单张数；

            var fees = parameters.Security.FeeModel.GetOrderFee(
                new OrderFeeParameters(parameters.Security,
                    parameters.Order)).Value;
            var feesInAccountCurrency = parameters.CurrencyConverter.
                ConvertToAccountCurrency(fees).Amount;

            var value = parameters.Order.GetValue(parameters.Security);
            var orderValue = value * parameters.Security.QuoteCurrency.ConversionRate * parameters.Security.SymbolProperties.ContractMultiplier;
            // Math.Sign only return 1 or -1
            return Math.Max((feesInAccountCurrency - orderValue),0);
        }
    }
}