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
 *
*/

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NodaTime;
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.DataFeeds.Enumerators;
using QuantConnect.Securities;
using QuantConnect.Securities.Equity;
using QuantConnect.Securities.Future;
using QuantConnect.Securities.Option;
using QuantConnect.Util;

namespace QuantConnect.Tests.Engine.DataFeeds
{
    [TestFixture]
    public class SubscriptionCollectionTests
    {
        [Test]
        public void EnumerationWhileUpdatingDoesNotThrow()
        {
            var cts = new CancellationTokenSource();
            var subscriptions = new SubscriptionCollection();
            var start = DateTime.UtcNow;
            var end = start.AddSeconds(10);
            var config = new SubscriptionDataConfig(typeof(TradeBar), Symbols.SPY, Resolution.Minute, DateTimeZone.Utc, DateTimeZone.Utc, true, false, false);
            var security = new Equity(
                Symbols.SPY,
                SecurityExchangeHours.AlwaysOpen(DateTimeZone.Utc),
                new Cash(Currencies.USD, 0, 1),
                SymbolProperties.GetDefault(Currencies.USD),
                ErrorCurrencyConverter.Instance
            );
            var timeZoneOffsetProvider = new TimeZoneOffsetProvider(DateTimeZone.Utc, start, end);
            var enumerator = new EnqueueableEnumerator<BaseData>();
            var subscriptionDataEnumerator = SubscriptionData.Enumerator(config, security, timeZoneOffsetProvider, enumerator);
            var subscriptionRequest = new SubscriptionRequest(false, null, security, config, start, end);
            var subscription = new Subscription(subscriptionRequest, subscriptionDataEnumerator, timeZoneOffsetProvider);

            var addTask = new TaskFactory().StartNew(() =>
            {
                Console.WriteLine("Add task started");

                while (DateTime.UtcNow < end)
                {
                    if (!subscriptions.Contains(config))
                    {
                        subscriptions.TryAdd(subscription);
                    }

                    Thread.Sleep(1);
                }

                Console.WriteLine("Add task ended");
            }, cts.Token);

            var removeTask = new TaskFactory().StartNew(() =>
            {
                Console.WriteLine("Remove task started");

                while (DateTime.UtcNow < end)
                {
                    Subscription removed;
                    subscriptions.TryRemove(config, out removed);

                    Thread.Sleep(1);
                }

                Console.WriteLine("Remove task ended");
            }, cts.Token);

            var readTask = new TaskFactory().StartNew(() =>
            {
                Console.WriteLine("Read task started");

                while (DateTime.UtcNow < end)
                {
                    foreach (var sub in subscriptions) { }

                    Thread.Sleep(1);
                }

                Console.WriteLine("Read task ended");
            }, cts.Token);

            Task.WaitAll(addTask, removeTask, readTask);
        }
        [Test]
        public void DefaultFillForwardResolution()
        {
            var subscriptionColletion = new SubscriptionCollection();
            var defaultFillForwardResolutio = subscriptionColletion.UpdateAndGetFillForwardResolution();
            Assert.AreEqual(defaultFillForwardResolutio.Value, new TimeSpan(0, 1, 0));
        }

        [Test]
        public void UpdatesFillForwardResolutionOverridesDefaultWhenNotAdding()
        {
            var subscriptionColletion = new SubscriptionCollection();
            var subscription = CreateSubscription(Resolution.Daily);

            var fillForwardResolutio = subscriptionColletion.UpdateAndGetFillForwardResolution(subscription.Configuration);
            Assert.AreEqual(fillForwardResolutio.Value, new TimeSpan(1, 0, 0, 0));
        }

        [Test]
        public void UpdatesFillForwardResolutionSuccessfullyWhenNotAdding()
        {
            var subscriptionColletion = new SubscriptionCollection();
            var subscription = CreateSubscription(Resolution.Second);

            var fillForwardResolutio = subscriptionColletion.UpdateAndGetFillForwardResolution(subscription.Configuration);
            Assert.AreEqual(fillForwardResolutio.Value, new TimeSpan(0, 0, 1));
        }

        [Test]
        public void UpdatesFillForwardResolutionSuccessfullyWhenAdding()
        {
            var subscriptionColletion = new SubscriptionCollection();
            var subscription = CreateSubscription(Resolution.Second);

            subscriptionColletion.TryAdd(subscription);
            Assert.AreEqual(subscriptionColletion.UpdateAndGetFillForwardResolution().Value, new TimeSpan(0, 0, 1));
        }

        [Test]
        public void UpdatesFillForwardResolutionSuccessfullyOverridesDefaultWhenAdding()
        {
            var subscriptionColletion = new SubscriptionCollection();
            var subscription = CreateSubscription(Resolution.Daily);

            subscriptionColletion.TryAdd(subscription);
            Assert.AreEqual(subscriptionColletion.UpdateAndGetFillForwardResolution().Value, new TimeSpan(1, 0, 0, 0));
        }

        [Test]
        public void DoesNotUpdateFillForwardResolutionWhenAddingBiggerResolution()
        {
            var subscriptionColletion = new SubscriptionCollection();
            var subscription = CreateSubscription(Resolution.Second);
            var subscription2 = CreateSubscription(Resolution.Minute);

            subscriptionColletion.TryAdd(subscription);
            Assert.AreEqual(subscriptionColletion.UpdateAndGetFillForwardResolution().Value, new TimeSpan(0, 0, 1));
            subscriptionColletion.TryAdd(subscription2);
            Assert.AreEqual(subscriptionColletion.UpdateAndGetFillForwardResolution().Value, new TimeSpan(0, 0, 1));
        }

        [Test]
        public void UpdatesFillForwardResolutionWhenRemoving()
        {
            var subscriptionCollection = new SubscriptionCollection();
            var subscription = CreateSubscription(Resolution.Second);
            var subscription2 = CreateSubscription(Resolution.Daily);

            subscriptionCollection.TryAdd(subscription);
            subscriptionCollection.TryAdd(subscription2);
            Assert.AreEqual(subscriptionCollection.UpdateAndGetFillForwardResolution().Value, new TimeSpan(0, 0, 1));
            subscriptionCollection.TryRemove(subscription.Configuration, out subscription);
            Assert.AreEqual(subscriptionCollection.UpdateAndGetFillForwardResolution().Value, new TimeSpan(1, 0, 0, 0));
            subscriptionCollection.TryRemove(subscription2.Configuration, out subscription2);
            Assert.AreEqual(subscriptionCollection.UpdateAndGetFillForwardResolution().Value, new TimeSpan(0, 1, 0));
        }

        [Test]
        public void FillForwardResolutionIgnoresTick()
        {
            //todo 支持 Tick 回测后，单元测试无法通过
            var subscriptionCollection = new SubscriptionCollection();
            var subscription = CreateSubscription(Resolution.Tick);

            subscriptionCollection.TryAdd(subscription);
            Assert.AreEqual(new TimeSpan(0, 1, 0), subscriptionCollection.UpdateAndGetFillForwardResolution().Value);

            subscriptionCollection.TryRemove(subscription.Configuration, out subscription);
            Assert.AreEqual(new TimeSpan(0, 1, 0), subscriptionCollection.UpdateAndGetFillForwardResolution().Value);
        }

        [Test]
        public void FillForwardResolutionIgnoresInternalFeed()
        {
            var subscriptionCollection = new SubscriptionCollection();
            var subscription = CreateSubscription(Resolution.Second, "AAPL", true);

            subscriptionCollection.TryAdd(subscription);
            Assert.AreEqual(subscriptionCollection.UpdateAndGetFillForwardResolution().Value, new TimeSpan(0, 1, 0));
            subscriptionCollection.TryRemove(subscription.Configuration, out subscription);
            Assert.AreEqual(subscriptionCollection.UpdateAndGetFillForwardResolution().Value, new TimeSpan(0, 1, 0));
        }

        [Test]
        public void DoesNotUpdateFillForwardResolutionWhenRemovingDuplicateResolution()
        {
            var subscriptionCollection = new SubscriptionCollection();
            var subscription = CreateSubscription(Resolution.Second);
            var subscription2 = CreateSubscription(Resolution.Second, "SPY");

            subscriptionCollection.TryAdd(subscription);
            Assert.AreEqual(subscriptionCollection.UpdateAndGetFillForwardResolution().Value, new TimeSpan(0, 0, 1));
            subscriptionCollection.TryAdd(subscription2);
            Assert.AreEqual(subscriptionCollection.UpdateAndGetFillForwardResolution().Value, new TimeSpan(0, 0, 1));
            subscriptionCollection.TryRemove(subscription.Configuration, out subscription);
            Assert.AreEqual(subscriptionCollection.UpdateAndGetFillForwardResolution().Value, new TimeSpan(0, 0, 1));
            subscriptionCollection.TryRemove(subscription2.Configuration, out subscription2);
            Assert.AreEqual(subscriptionCollection.UpdateAndGetFillForwardResolution().Value, new TimeSpan(0, 1, 0));
        }

        [Test]
        public void SubscriptionsAreSortedWhenAdding()
        {
            //todo ConcurrentDictionary 的导出顺序不是固定的，单元测试方式错误。
            var subscriptionCollection = new SubscriptionCollection();
            var subscription = CreateSubscription(Resolution.Second, "GC", false, SecurityType.Future);
            var subscription2 = CreateSubscription(Resolution.Second, "SPY");
            var subscription3 = CreateSubscription(Resolution.Second, "AAPL", false, SecurityType.Option);
            var subscription4 = CreateSubscription(Resolution.Second, "EURGBP");
            var subscription5 = CreateSubscription(Resolution.Second, "AAPL", false, SecurityType.Option, TickType.OpenInterest);
            var subscription6 = CreateSubscription(Resolution.Second, "AAPL", false, SecurityType.Option, TickType.Quote);

            subscriptionCollection.TryAdd(subscription);
            Assert.AreEqual(new[] { subscription }, subscriptionCollection.ToArray());
            subscriptionCollection.TryAdd(subscription2);
            Assert.AreEqual(new[] { subscription2, subscription }, subscriptionCollection.ToArray());
            subscriptionCollection.TryAdd(subscription3);
            Assert.AreEqual(new[] { subscription2, subscription3, subscription }, subscriptionCollection.ToArray());
            subscriptionCollection.TryAdd(subscription4);
            Assert.AreEqual(new[] { subscription4, subscription2, subscription3, subscription }, subscriptionCollection.ToArray());
            subscriptionCollection.TryAdd(subscription5);
            Assert.AreEqual(new[] { subscription4, subscription2, subscription3, subscription5, subscription }, subscriptionCollection.ToArray());
            subscriptionCollection.TryAdd(subscription6);
            Assert.AreEqual(new[] { subscription4, subscription2, subscription3, subscription6, subscription5, subscription }, subscriptionCollection.ToArray());


            Assert.AreEqual(new[] { SecurityType.Equity, SecurityType.Equity, SecurityType.Option,
                SecurityType.Option, SecurityType.Option, SecurityType.Future }, subscriptionCollection.Select(x => x.Configuration.SecurityType).ToArray());
        }

        [Test]
        public void SubscriptionsAreSortedWhenAdding2()
        {
            var subscriptionCollection = new SubscriptionCollection();
            var subscription = CreateSubscription(Resolution.Second, "GC", false, SecurityType.Future);
            var subscription2 = CreateSubscription(Resolution.Second, "SPY");
            var subscription3 = CreateSubscription(Resolution.Second, "AAPL", false, SecurityType.Option);
            var subscription4 = CreateSubscription(Resolution.Second, "EURGBP");

            subscriptionCollection.TryAdd(subscription);
            Assert.AreEqual(subscriptionCollection.ToList(), new[] { subscription });
            subscriptionCollection.TryAdd(subscription2);
            Assert.AreEqual(subscriptionCollection.ToList(), new[] { subscription2, subscription });
            subscriptionCollection.TryAdd(subscription3);
            Assert.AreEqual(subscriptionCollection.ToList(), new[] { subscription2, subscription3, subscription });
            subscriptionCollection.TryAdd(subscription4);

            Assert.AreEqual(subscriptionCollection.ToList(), new[] { subscription4, subscription2, subscription3, subscription });
            Assert.AreEqual(subscriptionCollection.Select(x => x.Configuration.SecurityType).ToList(), new[] { SecurityType.Equity, SecurityType.Equity, SecurityType.Option, SecurityType.Future });
        }

        [Test]
        public void SubscriptionsAreSortedWhenRemoving()
        {
            var subscriptionCollection = new SubscriptionCollection();
            var subscription = CreateSubscription(Resolution.Second, "BTCEUR", false, SecurityType.Future);
            var subscription2 = CreateSubscription(Resolution.Second, "SPY");
            var subscription3 = CreateSubscription(Resolution.Second, "AAPL", false, SecurityType.Option);
            var subscription4 = CreateSubscription(Resolution.Second, "EURGBP");
            var subscription5 = CreateSubscription(Resolution.Second, "AAPL", false, SecurityType.Option, TickType.OpenInterest);
            var subscription6 = CreateSubscription(Resolution.Second, "AAPL", false, SecurityType.Option, TickType.Quote);

            subscriptionCollection.TryAdd(subscription);
            subscriptionCollection.TryAdd(subscription2);
            subscriptionCollection.TryAdd(subscription3);
            subscriptionCollection.TryAdd(subscription4);
            subscriptionCollection.TryAdd(subscription5);
            subscriptionCollection.TryAdd(subscription6);
            Assert.AreEqual(subscriptionCollection.ToList(), new[] { subscription4, subscription2, subscription3, subscription6, subscription5, subscription });

            subscriptionCollection.TryRemove(subscription2.Configuration, out subscription2);
            Assert.AreEqual(subscriptionCollection.Select(x => x.Configuration.SecurityType).ToList(), new[] { SecurityType.Equity, SecurityType.Option,
                            SecurityType.Option, SecurityType.Option, SecurityType.Future });

            subscriptionCollection.TryRemove(subscription3.Configuration, out subscription3);
            Assert.AreEqual(subscriptionCollection.Select(x => x.Configuration.SecurityType).ToList(), new[] { SecurityType.Equity, SecurityType.Option, SecurityType.Option, SecurityType.Future });

            subscriptionCollection.TryRemove(subscription.Configuration, out subscription);
            Assert.AreEqual(subscriptionCollection.Select(x => x.Configuration.SecurityType).ToList(), new[] { SecurityType.Equity, SecurityType.Option, SecurityType.Option });
            Assert.AreEqual(subscriptionCollection.ToList(), new[] { subscription4, subscription6, subscription5 });

            subscriptionCollection.TryRemove(subscription6.Configuration, out subscription6);
            Assert.AreEqual(subscriptionCollection.Select(x => x.Configuration.SecurityType).ToList(), new[] { SecurityType.Equity, SecurityType.Option });
            Assert.AreEqual(subscriptionCollection.ToList(), new[] { subscription4, subscription5 });

            subscriptionCollection.TryRemove(subscription5.Configuration, out subscription5);
            Assert.AreEqual(subscriptionCollection.Select(x => x.Configuration.SecurityType).ToList(), new[] {SecurityType.Equity});

            subscriptionCollection.TryRemove(subscription4.Configuration, out subscription4);
            Assert.IsTrue(subscriptionCollection.Select(x => x.Configuration.SecurityType).ToList().IsNullOrEmpty());
        }

        private Subscription CreateSubscription(Resolution resolution, string symbol = "AAPL", bool isInternalFeed = false,
                                                SecurityType type = SecurityType.Equity, TickType tickType = TickType.Trade)
        {
            var start = DateTime.UtcNow;
            var end = start.AddSeconds(10);
            Security security;
            Symbol _symbol;
            if (type == SecurityType.Equity)
            {
                _symbol = new Symbol(SecurityIdentifier.GenerateEquity(DateTime.Now, symbol, Market.USA), symbol);
                security = new Equity(
                    _symbol,
                    SecurityExchangeHours.AlwaysOpen(DateTimeZone.Utc),
                    new Cash(Currencies.USD, 0, 1),
                    SymbolProperties.GetDefault(Currencies.USD),
                    ErrorCurrencyConverter.Instance
                );
            }
            else if (type == SecurityType.Option)
            {
                _symbol = new Symbol(SecurityIdentifier.GenerateOption(DateTime.Now,
                    SecurityIdentifier.GenerateEquity(DateTime.Now, symbol, Market.USA),
                    Market.USA, 0.0m, OptionRight.Call, OptionStyle.American), symbol);
                security = new Option(
                    _symbol,
                    SecurityExchangeHours.AlwaysOpen(DateTimeZone.Utc),
                    new Cash(Currencies.USD, 0, 1),
                    new OptionSymbolProperties(SymbolProperties.GetDefault(Currencies.USD)),
                    ErrorCurrencyConverter.Instance
                );
            }
            else if (type == SecurityType.Future)
            {
                _symbol = new Symbol(SecurityIdentifier.GenerateFuture(DateTime.Now, symbol, Market.USA), symbol);
                security = new Future(
                    _symbol,
                    SecurityExchangeHours.AlwaysOpen(DateTimeZone.Utc),
                    new Cash(Currencies.USD, 0, 1),
                    SymbolProperties.GetDefault(Currencies.USD),
                    ErrorCurrencyConverter.Instance
                );
            }
            else
            {
                throw new Exception("SecurityType not implemented");
            }
            var config = new SubscriptionDataConfig(typeof(TradeBar), _symbol, resolution, DateTimeZone.Utc, DateTimeZone.Utc, true, false, isInternalFeed, false, tickType);
            var timeZoneOffsetProvider = new TimeZoneOffsetProvider(DateTimeZone.Utc, start, end);
            var enumerator = new EnqueueableEnumerator<BaseData>();
            var subscriptionDataEnumerator = SubscriptionData.Enumerator(config, security, timeZoneOffsetProvider, enumerator);
            var subscriptionRequest = new SubscriptionRequest(false, null, security, config, start, end);
            return new Subscription(subscriptionRequest, subscriptionDataEnumerator, timeZoneOffsetProvider);
        }
    }
}
