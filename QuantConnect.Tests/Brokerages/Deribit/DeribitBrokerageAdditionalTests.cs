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
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

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
using Order = QuantConnect.Orders.Order;

namespace QuantConnect.Tests.Brokerages.Deribit
{
    [TestFixture]
    public class DeribitBrokerageAdditionalTests
    {
        private readonly List<Order> _orders = new List<Order>();

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

        internal static Security CreateSecurity(Symbol symbol)
        {
            return new Security(
                SecurityExchangeHours.AlwaysOpen(TimeZones.NewYork),
                new SubscriptionDataConfig(
                    typeof(TradeBar),
                    symbol,
                    Resolution.Tick,
                    TimeZones.NewYork,
                    TimeZones.NewYork,
                    false,
                    false,
                    false
                ),
                new Cash(Currencies.USD, 0, 1m),
                SymbolProperties.GetDefault(Currencies.USD),
                ErrorCurrencyConverter.Instance
            );
        }

        [Test]
        public void TestRateLimiting()
        {
            using (var brokerage = GetBrokerage())
            {
                Assert.IsTrue(brokerage.IsConnected);
                //GetTick(OptionSymbol);
                var method = brokerage.GetType().GetMethod("GetTick", BindingFlags.Public | BindingFlags.Instance);

                var parameters = new object[] { OptionSymbol };

                var result = Parallel.For(1, 100, x =>
                {
                    var stopwatch = Stopwatch.StartNew();
                    var value = (Tick)method.Invoke(brokerage, parameters);
                    stopwatch.Stop();
                    Console.WriteLine($"{DateTime.UtcNow:O} Response time: {stopwatch.Elapsed}");
                });
                while (!result.IsCompleted) Thread.Sleep(1000);
            }
        }


        private DeribitBrokerage GetBrokerage()
        {
            var securities = new SecurityManager(new TimeKeeper(DateTime.UtcNow, new[] { TimeZones.NewYork }));
            securities.Add(OptionSymbol, CreateSecurity(OptionSymbol));
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

            var deribitBrokerage = new DeribitBrokerage(Config.Get("deribit-url"),
                Config.Get("deribit-rest"),
                Config.Get("deribit-api-key"),
                Config.Get("deribit-api-secret"),
                algorithm.Object,
                priceProvider.Object);
            deribitBrokerage.Connect();

            return deribitBrokerage;
        }
    }
}
