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

using QuantConnect.Securities;
using QuantConnect.Securities.Option;

namespace QuantConnect.Orders.Fees
{
    /// <summary>
    /// Provides an implementation of <see cref="FeeModel"/> that models Deribit order fees
    /// </summary>
    public class DeribitFeeModel : FeeModel
    {
        //  1.永续合约：
        //  a) 主动吃单：0.075%（比如您开多/空一个BTC的仓位，那么手续费则为0.00075 BTC）
        //  b) 做市返佣：0.025%
        //  c) 资金费率是系统根据永续溢价情况自动调节，是多空双方相互付给对方，平台不收取任何费用。当资金费率为正时，多方付给空方；当资金费率为0时，多空双方不付钱；当资金费率为负时，空方付给多方。
        public const decimal MakerFee = 0m;
        public const decimal TakerFee = 0.0005m;

        //  2.期货：
        //  a) 主动吃单：0.05%
        //  b) 做市返佣：0.02%
        public const decimal FutureMakerFee = 0m;
        public const decimal FutureTakerFee = 0.0005m;

        //  3.期权：
        //  a) 期权合约对应标的数量的0.04%（比如您开多/空了一张BTC看涨/看跌合约，那么手续费则为1BTC*0.04%=0.0004BTC）
        //  注意：期权手续费不会高于期权费的12.5%。例如，某期权合约的价格若为0.0001 BTC，手续费则为0.0001 BTC* 12.5% = 0.0000125 BTC，而不是0.0004 BTC。
        public const decimal OptionFee = 0.0003m;
        public const decimal OptionFeeRate = 0.125m;
        public const decimal OptionDeliveryFee = 0.00015m;

        /// <summary>
        /// Get the fee for this order in quote currency
        /// </summary>
        /// <param name="parameters">A <see cref="OrderFeeParameters"/> object
        /// containing the security and order</param>
        /// <returns>The cost of the order in quote currency</returns>
        public override OrderFee GetOrderFee(OrderFeeParameters parameters)
        {
            var order = parameters.Order;
            var security = parameters.Security;
            var props = order.Properties as OrderProperties;
            decimal fee = 0;
            decimal unitPrice = 1;
            if (security.Type == SecurityType.Option)
            {
                var fee1 = order.Type == OrderType.OptionExercise ? OptionDeliveryFee : OptionFee;

                // get order value in quote currency
                unitPrice = order.Direction == OrderDirection.Buy ? security.AskPrice : security.BidPrice;
                if (order.Type == OrderType.Limit)
                {
                    // limit order posted to the order book
                    unitPrice = ((LimitOrder)order).LimitPrice;
                }
                else if (order.Type == OrderType.OptionExercise)
                {
                    var underlyingPrice = (security as Option).Underlying.Cache.MarkPrice;
                    if (underlyingPrice == 0)
                    {
                        Logging.Log.Error($"underlying mark price is 0, security: {security.Symbol.Value}, underlying: {(security as Option).Underlying.Symbol.Value}, underlyingPrice: {(security as Option).Underlying.Price}");
                        underlyingPrice = (security as Option).Underlying.Price;
                    }
                    unitPrice = (security as Option).GetIntrinsicValue(underlyingPrice) / underlyingPrice;
                }
                unitPrice *= security.SymbolProperties.ContractMultiplier;
                var fee2 = unitPrice * OptionFeeRate;
                fee = System.Math.Min(fee1, fee2);

                return new OrderFee(new CashAmount(
                    order.AbsoluteQuantity * fee,
                    security.QuoteCurrency.Symbol));
            }
            else if(security.Type==SecurityType.Future)
            {
                if(security.Symbol.Value.Contains("PERPETUAL"))
                {
                    if(order.Type == OrderType.Market)
                    {
                        fee = TakerFee;
                    }
                    else
                    {
                        fee = MakerFee;
                    }                    
                }
                else
                {
                    if (order.Type == OrderType.Market)
                    {
                        fee = FutureTakerFee;
                    }
                    else
                    {
                        fee = FutureMakerFee;
                    }
                }

                unitPrice = 1;

                unitPrice *= security.SymbolProperties.ContractMultiplier;
                if (order.Type == OrderType.Market)
                {
                    return new OrderFee(new CashAmount(unitPrice * order.AbsoluteQuantity * fee, security.QuoteCurrency.Symbol));
                }
                // apply fee factor, currently we do not model 30-day volume, so we use the first tier
                var price = security.Cache.MarkPrice;
                if (price == 0)
                {
                    return new OrderFee(new CashAmount(unitPrice * order.AbsoluteQuantity * fee, security.QuoteCurrency.Symbol));
                }
                return new OrderFee(new CashAmount(
                    unitPrice * order.AbsoluteQuantity * fee / price,
                    security.Symbol.Value.Split(new []{' ', '-', '_', '/'})[0].RemoveFromEnd("USD")));
            }

            return new OrderFee(new CashAmount(0,security.QuoteCurrency.Symbol));
        }
    }
}
