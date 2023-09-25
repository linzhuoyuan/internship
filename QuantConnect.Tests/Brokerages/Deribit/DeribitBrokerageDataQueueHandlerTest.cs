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
using System.Threading;
using Moq;
using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.Brokerages;
using QuantConnect.Brokerages.Deribit;

using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.TransactionHandlers;
using QuantConnect.Securities;

namespace QuantConnect.Tests.Brokerages.Deribit
{
    [TestFixture]
    public class DeribitBrokerageDataQueueHandlerTest
    {
        /// <summary>
        /// Gets the symbol to be traded, must be shortable
        /// </summary>
        protected Symbol OptionSymbol => Symbol.CreateOption(
            "BTCUSD",
            Market.Deribit,
            OptionStyle.European,
            OptionRight.Call,
            10000,
            new DateTime(2020, 12, 25),
            "BTC-25DEC20-10000-C"
        );

        protected Symbol FutureSymbol => Symbol.CreateFuture(
            "BTC-PERPETUAL",
            Market.Deribit,
            new DateTime(2030, 12, 31),
            "BTC-PERPETUAL"
        );

        internal static Security CreateOption(Symbol symbol)
        {
            var cash = symbol.Value.Substring(0, 3).ToUpper();
            return new Security(
                SecurityExchangeHours.AlwaysOpen(TimeZones.Utc),
                new SubscriptionDataConfig(
                    typeof(TradeBar),
                    symbol,
                    Resolution.Tick,
                    TimeZones.Utc,
                    TimeZones.Utc,
                    false,
                    false,
                    false
                ),
                new Cash(cash, 0, 1m),
                SymbolProperties.GetDefault(cash),
                ErrorCurrencyConverter.Instance
            );
        }

        internal static Security CreateFuture(Symbol symbol)
        {
            return new Security(
                SecurityExchangeHours.AlwaysOpen(TimeZones.Utc),
                new SubscriptionDataConfig(
                    typeof(TradeBar),
                    symbol,
                    Resolution.Tick,
                    TimeZones.Utc,
                    TimeZones.Utc,
                    false,
                    false,
                    false
                ),
                new Cash(Currencies.USD, 0, 1m),
                SymbolProperties.GetDefault(Currencies.USD),
                ErrorCurrencyConverter.Instance
            );
        }

        [Test, Description("测试订阅函数")]
        public void GetNextTicksTest()
        {
            var securities = new SecurityManager(new TimeKeeper(DateTime.UtcNow, new[] { TimeZones.NewYork }));
            securities.Add(OptionSymbol, CreateOption(OptionSymbol));
            securities.Add(FutureSymbol, CreateFuture(FutureSymbol));
            var transactions = new SecurityTransactionManager(null, securities);
            transactions.SetOrderProcessor(new BrokerageTransactionHandler());
            var algorithm = new Mock<IAlgorithm>();
            algorithm.Setup(a => a.Transactions).Returns(transactions);
            algorithm.Setup(a => a.BrokerageModel).Returns(new DeribitBrokerageModel(AccountType.Margin));
            algorithm.Setup(a => a.Portfolio).Returns(new SecurityPortfolioManager(securities, transactions));
            algorithm.Setup(a => a.OptionChainProvider).Returns(new LiveDeribitOptionChainProvider());
            algorithm.Setup(a => a.FutureChainProvider).Returns(new LiveDeribitFutureChainProvider());

            var priceProvider = new Mock<IPriceProvider>();
            priceProvider.Setup(a => a.GetLastPrice(It.IsAny<Symbol>())).Returns(1.234m);

            using (var deribit = new DeribitBrokerage(Config.Get("deribit-url"),
                Config.Get("deribit-rest"),
                Config.Get("deribit-api-key"),
                Config.Get("deribit-api-secret"),
                algorithm.Object,
                priceProvider.Object))
            {
                deribit.Connect();
                while (!deribit.IsConnected)
                {
                    Thread.Sleep(1000);
                }

                deribit.Subscribe(null, new List<Symbol> { FutureSymbol});

                Thread.Sleep(10000);

                bool subscribeResult = false;
                for (int i = 0; i < 20; i++)
                {
                    foreach (var tick in deribit.GetNextTicks())
                    {
                        if (tick.Symbol.Value == FutureSymbol.Value)
                        {
                            subscribeResult = true;
                            break;
                        }
                    }
                    Thread.Sleep(1000);
                }

                Assert.IsTrue(subscribeResult);
            }
        }
        
        [Test]
        public void GetsTickDataAfterDisconnectionConnectionCycle()
        {
            var securities = new SecurityManager(new TimeKeeper(DateTime.UtcNow, new[] { TimeZones.NewYork }));
            securities.Add(OptionSymbol, CreateOption(OptionSymbol));
            securities.Add(FutureSymbol, CreateFuture(FutureSymbol));
            var transactions = new SecurityTransactionManager(null, securities);
            transactions.SetOrderProcessor(new BrokerageTransactionHandler());
            var algorithm = new Mock<IAlgorithm>();
            algorithm.Setup(a => a.Transactions).Returns(transactions);
            algorithm.Setup(a => a.BrokerageModel).Returns(new DeribitBrokerageModel(AccountType.Margin));
            algorithm.Setup(a => a.Portfolio).Returns(new SecurityPortfolioManager(securities, transactions));
            algorithm.Setup(a => a.OptionChainProvider).Returns(new LiveDeribitOptionChainProvider());
            algorithm.Setup(a => a.FutureChainProvider).Returns(new LiveDeribitFutureChainProvider());

            var priceProvider = new Mock<IPriceProvider>();
            priceProvider.Setup(a => a.GetLastPrice(It.IsAny<Symbol>())).Returns(1.234m);
            using (var deribit = new DeribitBrokerage(Config.Get("deribit-url"),
                  Config.Get("deribit-rest"),
                  Config.Get("deribit-api-key"),
                  Config.Get("deribit-api-secret"),
                  algorithm.Object,
                  priceProvider.Object))
            {
                deribit.Connect();
                while (!deribit.IsConnected)
                {
                    Thread.Sleep(1000);
                }
                deribit.Subscribe(null, new List<Symbol> { FutureSymbol});

                deribit.Disconnect();
                Thread.Sleep(2000);

                
                deribit.Connect();
                Thread.Sleep(10000);

                bool subscribeResult = false;
                for (int i = 0; i < 20; i++)
                {
                    foreach (var tick in deribit.GetNextTicks())
                    {
                        if (tick.Symbol.Value == FutureSymbol.Value)
                        {
                            subscribeResult = true;
                            break;
                        }
                    }
                    Thread.Sleep(1000);
                }
                Assert.IsTrue(subscribeResult);
            }
        }
    }
}
