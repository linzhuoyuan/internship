﻿/*
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
using System.Linq;
using QuantConnect.Algorithm.Framework.Portfolio;
using QuantConnect.Data.UniverseSelection;

namespace QuantConnect.Algorithm.Framework.Execution
{
    /// <summary>
    /// Provides an implementation of <see cref="IExecutionModel"/> that immediately submits
    /// market orders to achieve the desired portfolio targets
    /// </summary>
    public class ImmediateExecutionModel : ExecutionModel
    {
        private readonly PortfolioTargetCollection _targetsCollection = new PortfolioTargetCollection();

        /// <summary>
        /// Immediately submits orders for the specified portfolio targets.
        /// </summary>
        /// <param name="algorithm">The algorithm instance</param>
        /// <param name="targets">The portfolio targets to be ordered</param>
        public override void Execute(QCAlgorithm algorithm, IPortfolioTarget[] targets)
        {
            _targetsCollection.AddRange(targets);
            // for performance we check count value, OrderByMarginImpact and ClearFulfilled are expensive to call
            if (_targetsCollection.Count > 0)
            {
                foreach (var target in _targetsCollection.OrderByMarginImpact(algorithm))
                {
                    if (target.Symbol.IsChinaMarket() && algorithm.LiveMode)
                    {
                        var existing_long = algorithm.Securities[target.Symbol].LongHoldings.Quantity
                            + algorithm.Transactions.GetOpenOrderTickets(target.Symbol)
                                .Aggregate(0m, (d, ticket) => d + ticket.Quantity - ticket.QuantityFilled);

                        var existing_short = algorithm.Securities[target.Symbol].ShortHoldings.Quantity
                            + algorithm.Transactions.GetOpenOrderTickets(target.Symbol)
                                .Aggregate(0m, (d, ticket) => d + ticket.Quantity - ticket.QuantityFilled);

                        var quantity = target.Quantity - (existing_long + existing_short);

                        if (quantity != 0)
                        {
                            if (quantity > 0)
                            {
                                algorithm.CloseBuy(target.Symbol, existing_short);
                                algorithm.OpenBuy(target.Symbol, quantity);
                            }
                            else
                            {
                                algorithm.CloseSell(target.Symbol, existing_long);
                                algorithm.OpenSell(target.Symbol, quantity);
                            }
                        }
                    }
                    else
                    {
                        var existing = algorithm.Securities[target.Symbol].Holdings.Quantity
                            + algorithm.Transactions.GetOpenOrderTickets(target.Symbol)
                                .Aggregate(0m, (d, ticket) => d + ticket.Quantity - ticket.QuantityFilled);
                        var quantity = target.Quantity - existing;
                        if (quantity != 0)
                        {
                            algorithm.MarketOrder(target.Symbol, quantity);
                        }
                    }
                }

                _targetsCollection.ClearFulfilled(algorithm);
            }
        }

        /// <summary>
        /// Event fired each time the we add/remove securities from the data feed
        /// </summary>
        /// <param name="algorithm">The algorithm instance that experienced the change in securities</param>
        /// <param name="changes">The security additions and removals from the algorithm</param>
        public override void OnSecuritiesChanged(QCAlgorithm algorithm, SecurityChanges changes)
        {
        }
    }
}