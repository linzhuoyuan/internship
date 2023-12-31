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
using NUnit.Framework;
using QuantConnect.Algorithm;
using QuantConnect.Brokerages;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Securities;
using QuantConnect.Tests.Engine.DataFeeds;
using QuantConnect.Util;

namespace QuantConnect.Tests.Common.Securities
{
    [TestFixture]
    public class SecurityMarginModelTests
    {
        private static Symbol _symbol;
        private static readonly string _cashSymbol = Currencies.USD;
        private static FakeOrderProcessor _fakeOrderProcessor;

        [Test]
        public void ZeroTargetWithZeroHoldingsIsNotAnError()
        {
            var algorithm = GetAlgorithm();
            var security = InitAndGetSecurity(algorithm, 0);

            var model = new SecurityMarginModel();
            var result = model.GetMaximumOrderQuantityForTargetValue(algorithm.Portfolio, security, 0);

            Assert.AreEqual(0, result.Quantity);
            Assert.IsTrue(result.Reason.IsNullOrEmpty());
            Assert.IsFalse(result.IsError);
        }

        [Test]
        public void ZeroTargetWithNonZeroHoldingsReturnsNegativeOfQuantity()
        {
            var algorithm = GetAlgorithm();
            var security = InitAndGetSecurity(algorithm, 0);
            security.Holdings.SetHoldings(200, 10);

            var model = new SecurityMarginModel();
            var result = model.GetMaximumOrderQuantityForTargetValue(algorithm.Portfolio, security, 0);

            Assert.AreEqual(-10, result.Quantity);
            Assert.IsTrue(result.Reason.IsNullOrEmpty());
            Assert.IsFalse(result.IsError);
        }

        [Test]
        public void SetHoldings_ZeroToFullLong()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            var security = InitAndGetSecurity(algo, 5);
            var actual = algo.CalculateOrderQuantity(_symbol, 1m * security.BuyingPowerModel.GetLeverage(security));
            // (100000 * 2 * 0.9975 setHoldingsBuffer) / 25 - fee ~=7979m
            Assert.AreEqual(7979m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
        }

        [Test]
        public void SetHoldings_ZeroToFullLong_NonAccountCurrency_ZeroQuoteCurrency()
        {
            var algorithm = GetAlgorithm();
            algorithm.Portfolio.CashBook.Clear();
            algorithm.Portfolio.SetAccountCurrency("EUR");
            algorithm.Portfolio.SetCash(10000);
            // We don't have quote currency - we will get a "loan"
            algorithm.Portfolio.SetCash(Currencies.USD, 0, 0.88m);
            var security = InitAndGetSecurity(algorithm, 5);

            var actual = algorithm.CalculateOrderQuantity(_symbol, 1m * security.BuyingPowerModel.GetLeverage(security));
            // (10000 * 2 * 0.9975 setHoldingsBuffer) / 25 * 0.88 conversion rate - 5 USD fee * 0.88 conversion rate ~=906m
            Assert.AreEqual(906m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algorithm));
        }

        [TestCase("Long")]
        [TestCase("Short")]
        public void GetReservedBuyingPowerForPosition_NonAccountCurrency_ZeroQuoteCurrency(string position)
        {
            var algorithm = GetAlgorithm();
            algorithm.Portfolio.CashBook.Clear();
            algorithm.Portfolio.SetAccountCurrency("EUR");
            algorithm.Portfolio.SetCash(10000);
            algorithm.Portfolio.SetCash(Currencies.USD, 0, 0.88m);
            var security = InitAndGetSecurity(algorithm, 5);
            security.Holdings.SetHoldings(security.Price,
                (position == "Long" ? 1 : -1) * 100);

            var actual = security.BuyingPowerModel.GetReservedBuyingPowerForPosition(new ReservedBuyingPowerForPositionParameters(security));
            // 100quantity * 25price * 0.88rate * 0.5 MaintenanceMarginRequirement = 1100
            Assert.AreEqual(1100, actual.Value);
        }

        [Test]
        public void SetHoldings_ZeroToFullLong_NonAccountCurrency()
        {
            var algorithm = GetAlgorithm();
            algorithm.Portfolio.CashBook.Clear();
            algorithm.Portfolio.SetAccountCurrency("EUR");
            algorithm.Portfolio.SetCash(10000);
            // We have 1000 USD too
            algorithm.Portfolio.SetCash(Currencies.USD, 1000, 0.88m);
            var security = InitAndGetSecurity(algorithm, 5);

            var actual = algorithm.CalculateOrderQuantity(_symbol, 1m * security.BuyingPowerModel.GetLeverage(security));
            // ((10000 + 1000 USD * 0.88 rate) * 2 * 0.9975 setHoldingsBuffer) / 25 * 0.88 rate - 5 USD fee * 0.88 rate ~=986m
            Assert.AreEqual(986m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algorithm));
        }

        [Test]
        public void SetHoldings_Long_TooBigOfATarget()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            var security = InitAndGetSecurity(algo, 5);
            var actual = algo.CalculateOrderQuantity(_symbol, 1m * security.BuyingPowerModel.GetLeverage(security) + 0.1m);
            // (100000 * 2.1* 0.9975 setHoldingsBuffer) / 25 - fee ~=8378m
            Assert.AreEqual(8378m, actual);
            Assert.IsFalse(HasSufficientBuyingPowerForOrder(actual, security, algo));
        }

        [Test]
        public void SetHoldings_Long_TooBigOfATarget_NonAccountCurrency()
        {
            var algorithm = GetAlgorithm();
            algorithm.Portfolio.CashBook.Clear();
            algorithm.Portfolio.SetAccountCurrency("EUR");
            algorithm.Portfolio.SetCash(10000);
            // We don't have quote currency - we will get a "loan"
            algorithm.Portfolio.SetCash(Currencies.USD, 0, 0.88m);
            var security = InitAndGetSecurity(algorithm, 5);

            var actual = algorithm.CalculateOrderQuantity(_symbol, 1m * security.BuyingPowerModel.GetLeverage(security) + 0.1m);
            // (10000 * 2.1 * 0.9975 setHoldingsBuffer) / 25 * 0.88 conversion rate - 5 USD fee * 0.88 conversion rate ~=951m
            Assert.AreEqual(951m, actual);
            Assert.IsFalse(HasSufficientBuyingPowerForOrder(actual, security, algorithm));
        }

        [Test]
        public void SetHoldings_ZeroToFullShort()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            var security = InitAndGetSecurity(algo, 5);
            var actual = algo.CalculateOrderQuantity(_symbol, -1m * security.BuyingPowerModel.GetLeverage(security));
            // (100000 * 2 * 0.9975 setHoldingsBuffer) / 25 - fee~=-7979m
            Assert.AreEqual(-7979m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
        }

        [Test]
        public void SetHoldings_ZeroToFullShort_NonAccountCurrency_ZeroQuoteCurrency()
        {
            var algorithm = GetAlgorithm();
            algorithm.Portfolio.CashBook.Clear();
            algorithm.Portfolio.SetAccountCurrency("EUR");
            algorithm.Portfolio.SetCash(10000);
            algorithm.Portfolio.SetCash(Currencies.USD, 0, 0.88m);
            var security = InitAndGetSecurity(algorithm, 5);

            var actual = algorithm.CalculateOrderQuantity(_symbol, - 1m * security.BuyingPowerModel.GetLeverage(security));
            // (10000 * - 2 * 0.9975 setHoldingsBuffer) / 25 * 0.88 conversion rate - 5 USD fee * 0.88 conversion rate ~=906m
            Assert.AreEqual(-906m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algorithm));
        }

        [Test]
        public void SetHoldings_ZeroToFullShort_NonAccountCurrency()
        {
            var algorithm = GetAlgorithm();
            algorithm.Portfolio.CashBook.Clear();
            algorithm.Portfolio.SetAccountCurrency("EUR");
            algorithm.Portfolio.SetCash(10000);
            algorithm.Portfolio.SetCash(Currencies.USD, 1000, 0.88m);
            var security = InitAndGetSecurity(algorithm, 5);

            var actual = algorithm.CalculateOrderQuantity(_symbol, -1m * security.BuyingPowerModel.GetLeverage(security));
            // ((10000 + 1000 * 0.88)* - 2 * 0.9975 setHoldingsBuffer) / 25 * 0.88 conversion rate - 5 USD fee * 0.88 conversion rate ~=986m
            Assert.AreEqual(-986m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algorithm));
        }

        [Test]
        public void SetHoldings_Short_TooBigOfATarget()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            var security = InitAndGetSecurity(algo, 5);
            var actual = algo.CalculateOrderQuantity(_symbol, -1m * security.BuyingPowerModel.GetLeverage(security) - 0.1m);
            // (100000 * - 2.1m * 0.9975 setHoldingsBuffer) / 25 - fee~=-8378m
            Assert.AreEqual(-8378m, actual);
            Assert.IsFalse(HasSufficientBuyingPowerForOrder(actual, security, algo));
        }

        [Test]
        public void SetHoldings_Short_TooBigOfATarget_NonAccountCurrency()
        {
            var algorithm = GetAlgorithm();
            algorithm.Portfolio.CashBook.Clear();
            algorithm.Portfolio.SetAccountCurrency("EUR");
            algorithm.Portfolio.SetCash(10000);
            algorithm.Portfolio.SetCash(Currencies.USD, 0, 0.88m);
            var security = InitAndGetSecurity(algorithm, 5);

            var actual = algorithm.CalculateOrderQuantity(_symbol, -1m * security.BuyingPowerModel.GetLeverage(security) - 0.1m);
            // (10000 * - 2.1 * 0.9975 setHoldingsBuffer) / 25 * 0.88 conversion rate - 5 USD fee * 0.88 conversion rate ~=951m
            Assert.AreEqual(-951m, actual);
            Assert.IsFalse(HasSufficientBuyingPowerForOrder(actual, security, algorithm));
        }

        [Test]
        public void SetHoldings_ZeroToFullLong_NoFee()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            var security = InitAndGetSecurity(algo, 0);
            var actual = algo.CalculateOrderQuantity(_symbol, 1m * security.BuyingPowerModel.GetLeverage(security));
            // (100000 * 2 * 0.9975 setHoldingsBuffer) / 25 =7980m
            Assert.AreEqual(7980m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
        }

        [Test]
        public void SetHoldings_Long_TooBigOfATarget_NoFee()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            var security = InitAndGetSecurity(algo, 0);
            var actual = algo.CalculateOrderQuantity(_symbol, 1m * security.BuyingPowerModel.GetLeverage(security) + 0.1m);
            // (100000 * 2.1m* 0.9975 setHoldingsBuffer) / 25 = 8379m
            Assert.AreEqual(8379m, actual);
            Assert.IsFalse(HasSufficientBuyingPowerForOrder(actual, security, algo));
        }

        [Test]
        public void SetHoldings_ZeroToFullShort_NoFee()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            var security = InitAndGetSecurity(algo, 0);
            var actual = algo.CalculateOrderQuantity(_symbol, -1m * security.BuyingPowerModel.GetLeverage(security));
            var order = new MarketOrder(_symbol, actual, DateTime.UtcNow);
            // (100000 * 2 * 0.9975 setHoldingsBuffer) / 25 = -7980m
            Assert.AreEqual(-7980m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
        }

        [Test]
        public void SetHoldings_Short_TooBigOfATarget_NoFee()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            var security = InitAndGetSecurity(algo, 0);
            var actual = algo.CalculateOrderQuantity(_symbol, -1m * security.BuyingPowerModel.GetLeverage(security) - 0.1m);
            // (100000 * -2.1 * 0.9975 setHoldingsBuffer) / 25 =  -8379m
            Assert.AreEqual(-8379m, actual);
            Assert.IsFalse(HasSufficientBuyingPowerForOrder(actual, security, algo));
        }

        [Test]
        public void FreeBuyingPowerPercentDefault_Equity()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            var security = InitAndGetSecurity(algo, 5, SecurityType.Equity);
            var model = security.BuyingPowerModel;

            var actual = algo.CalculateOrderQuantity(_symbol, 1m * model.GetLeverage(security));
            // (100000 * 2 * 0.9975) / 25 - 1 order due to fees
            Assert.AreEqual(7979m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
            Assert.AreEqual(algo.Portfolio.Cash, model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value);
        }

        [Test]
        public void FreeBuyingPowerPercentAppliesForCashAccount_Equity()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            algo.SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage, AccountType.Cash);
            var security = InitAndGetSecurity(algo, 5, SecurityType.Equity);
            var requiredFreeBuyingPowerPercent = 0.05m;
            var model = security.BuyingPowerModel = new SecurityMarginModel(1, requiredFreeBuyingPowerPercent);

            var actual = algo.CalculateOrderQuantity(_symbol, 1m * model.GetLeverage(security));
            // (100000 * 1 * 0.95 * 0.9975) / 25 - 1 order due to fees
            Assert.AreEqual(3790m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual * 1.0025m, security, algo));
            Assert.IsFalse(HasSufficientBuyingPowerForOrder(actual * 1.0025m + security.SymbolProperties.LotSize + 9, security, algo));
            var expectedBuyingPower = algo.Portfolio.Cash * (1 - requiredFreeBuyingPowerPercent);
            Assert.AreEqual(
                expectedBuyingPower, 
                model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value);
        }

        [Test]
        public void FreeBuyingPowerPercentAppliesForMarginAccount_Equity()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            algo.SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage, AccountType.Margin);
            var security = InitAndGetSecurity(algo, 5, SecurityType.Equity);
            var requiredFreeBuyingPowerPercent = 0.05m;
            var model = security.BuyingPowerModel = new SecurityMarginModel(2, requiredFreeBuyingPowerPercent);

            var actual = algo.CalculateOrderQuantity(_symbol, 1m * model.GetLeverage(security));
            // (100000 * 2 * 0.95 * 0.9975) / 25 - 1 order due to fees
            Assert.AreEqual(7580m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual * 1.0025m, security, algo));
            Assert.IsFalse(HasSufficientBuyingPowerForOrder(actual * 1.0025m + security.SymbolProperties.LotSize + 9, security, algo));
            var expectedBuyingPower = algo.Portfolio.Cash * (1 - requiredFreeBuyingPowerPercent);
            Assert.AreEqual(expectedBuyingPower, model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value);
        }

        [Test]
        public void FreeBuyingPowerPercentCashAccountWithLongHoldings_Equity()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            algo.SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage, AccountType.Cash);
            var security = InitAndGetSecurity(algo, 5, SecurityType.Equity);
            var requiredFreeBuyingPowerPercent = 0.05m;
            var model = security.BuyingPowerModel = new SecurityMarginModel(1, requiredFreeBuyingPowerPercent);
            security.Holdings.SetHoldings(25, 2000);
            security.SettlementModel.ApplyFunds(
                algo.Portfolio, security, DateTime.UtcNow.AddDays(-10), _cashSymbol, - 2000 * 25);

            // Margin remaining 50k + used 50k + initial margin 50k - 5k free buying power percent (5% of 100k)
            Assert.AreEqual(145000, model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Sell)).Value);
            // Margin remaining 50k - 5k free buying power percent (5% of 100k)
            Assert.AreEqual(45000 ,model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value);

            var actual = algo.CalculateOrderQuantity(_symbol, - 1m * model.GetLeverage(security));
            // ((100k - 5) * -1 * 0.95 * 0.9975 - (50k holdings)) / 25 - 1 order due to fees
            Assert.AreEqual(-5790m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
            Assert.IsFalse(HasSufficientBuyingPowerForOrder(actual * 1.0025m, security, algo));
        }

        [Test]
        public void FreeBuyingPowerPercentMarginAccountWithLongHoldings_Equity()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            algo.SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage, AccountType.Margin);
            var security = InitAndGetSecurity(algo, 5, SecurityType.Equity);
            var requiredFreeBuyingPowerPercent = 0.05m;
            var model = security.BuyingPowerModel = new SecurityMarginModel(2, requiredFreeBuyingPowerPercent);
            security.Holdings.SetHoldings(25, 2000);
            security.SettlementModel.ApplyFunds(
                algo.Portfolio, security, DateTime.UtcNow.AddDays(-10), _cashSymbol, -2000 * 25);

            // Margin remaining 75k + used 25k + initial margin 25k - 5k free buying power percent (5% of 100k)
            Assert.AreEqual(120000, model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Sell)).Value);
            // Margin remaining 75k - 5k free buying power percent
            Assert.AreEqual(70000, model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value);

            var actual = algo.CalculateOrderQuantity(_symbol, -1m * model.GetLeverage(security));
            // ((100k - 5) * -2 * 0.95 * 0.9975 - (50k holdings)) / 25 - 1 order due to fees
            Assert.AreEqual(-9580m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
            Assert.IsFalse(HasSufficientBuyingPowerForOrder(actual * 1.0025m, security, algo));
        }

        [Test]
        public void FreeBuyingPowerPercentMarginAccountWithShortHoldings_Equity()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            algo.SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage, AccountType.Margin);
            var security = InitAndGetSecurity(algo, 5, SecurityType.Equity);
            var requiredFreeBuyingPowerPercent = 0.05m;
            var model = security.BuyingPowerModel = new SecurityMarginModel(2, requiredFreeBuyingPowerPercent);
            security.Holdings.SetHoldings(25, -2000);
            security.SettlementModel.ApplyFunds(
                algo.Portfolio, security, DateTime.UtcNow.AddDays(-10), _cashSymbol, 2000 * 25);

            // Margin remaining 75k + used 25k + initial margin 25k - 5k free buying power percent (5% of 100k)
            Assert.AreEqual(120000, model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value);
            // Margin remaining 75k - 5k free buying power percent
            Assert.AreEqual(70000, model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Sell)).Value);

            var actual = algo.CalculateOrderQuantity(_symbol, 1m * model.GetLeverage(security));
            // ((100k - 5) * 2 * 0.95 * 0.9975 - (-50k holdings)) / 25 - 1 order due to fees
            Assert.AreEqual(9580m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
            Assert.IsFalse(HasSufficientBuyingPowerForOrder(actual * 1.0025m, security, algo));
        }

        [Test]
        public void FreeBuyingPowerPercentDefault_Option()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            var security = InitAndGetSecurity(algo, 5, SecurityType.Option);
            var model = security.BuyingPowerModel;

            var actual = algo.CalculateOrderQuantity(_symbol, 1m * model.GetLeverage(security));
            // (100000 * 1) / (25 * 100 contract multiplier) - 1 order due to fees
            Assert.AreEqual(39m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
            Assert.AreEqual(algo.Portfolio.Cash, model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value);
        }

        [Test]
        public void FreeBuyingPowerPercentAppliesForCashAccount_Option()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            algo.SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage, AccountType.Cash);
            var security = InitAndGetSecurity(algo, 5, SecurityType.Option);
            var requiredFreeBuyingPowerPercent = 0.05m;
            var model = security.BuyingPowerModel = new SecurityMarginModel(1, requiredFreeBuyingPowerPercent);

            var actual = algo.CalculateOrderQuantity(_symbol, 1m * model.GetLeverage(security));
            // (100000 * 1 * 0.95) / (25 * 100 contract multiplier) - 1 order due to fees
            Assert.AreEqual(37m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
            var expectedBuyingPower = algo.Portfolio.Cash * (1 - requiredFreeBuyingPowerPercent);
            Assert.AreEqual(expectedBuyingPower, model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value);
        }

        [Test]
        public void FreeBuyingPowerPercentAppliesForMarginAccount_Option()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            algo.SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage, AccountType.Margin);
            var security = InitAndGetSecurity(algo, 5, SecurityType.Option);
            var requiredFreeBuyingPowerPercent = 0.05m;
            var model = security.BuyingPowerModel = new SecurityMarginModel(2, requiredFreeBuyingPowerPercent);

            var actual = algo.CalculateOrderQuantity(_symbol, 1m * model.GetLeverage(security));
            // (100000 * 2 * 0.95) / (25 * 100 contract multiplier) - 1 order due to fees
            Assert.AreEqual(75m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
            var expectedBuyingPower = algo.Portfolio.Cash * (1 - requiredFreeBuyingPowerPercent);
            Assert.AreEqual(expectedBuyingPower, model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value);
        }

        [Test]
        public void FreeBuyingPowerPercentDefault_Future()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            var security = InitAndGetSecurity(algo, 5, SecurityType.Future);
            var model = security.BuyingPowerModel;

            var actual = algo.CalculateOrderQuantity(_symbol, 1m * model.GetLeverage(security));
            // (100000 * 1 * 0.9975 ) / 25 - 1 order due to fees
            Assert.AreEqual(3989m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
            Assert.AreEqual(algo.Portfolio.Cash, model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value);
        }

        [Test]
        public void FreeBuyingPowerPercentAppliesForCashAccount_Future()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            algo.SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage, AccountType.Cash);
            var security = InitAndGetSecurity(algo, 5, SecurityType.Future);
            var requiredFreeBuyingPowerPercent = 0.05m;
            var model = security.BuyingPowerModel = new SecurityMarginModel(1, requiredFreeBuyingPowerPercent);

            var actual = algo.CalculateOrderQuantity(_symbol, 1m * model.GetLeverage(security));
            // ((100000 - 5) * 1 * 0.95 * 0.9975 / (25)
            Assert.AreEqual(3790m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
            var expectedBuyingPower = algo.Portfolio.Cash * (1 - requiredFreeBuyingPowerPercent);
            Assert.AreEqual(
                expectedBuyingPower, 
                model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value);
        }

        [Test]
        public void FreeBuyingPowerPercentAppliesForMarginAccount_Future()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            algo.SetBrokerageModel(BrokerageName.InteractiveBrokersBrokerage, AccountType.Margin);
            var security = InitAndGetSecurity(algo, 5, SecurityType.Future);
            var requiredFreeBuyingPowerPercent = 0.05m;
            var model = security.BuyingPowerModel = new SecurityMarginModel(2, requiredFreeBuyingPowerPercent);

            var actual = algo.CalculateOrderQuantity(_symbol, 1m * model.GetLeverage(security));
            // ((100000 - 5) * 2 * 0.95 * 0.9975 / (25)
            Assert.AreEqual(7580m, actual);
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(actual, security, algo));
            var expectedBuyingPower = algo.Portfolio.Cash * (1 - requiredFreeBuyingPowerPercent);
            Assert.AreEqual(
                expectedBuyingPower, 
                model.GetBuyingPower(new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value);
        }

        [Test]
        public void NonAccountCurrencyFees()
        {
            var algo = GetAlgorithm();
            algo.SetCash(100000);
            var security = InitAndGetSecurity(algo, 0);
            algo.SetCash("EUR", 0, 100);
            security.FeeModel = new NonAccountCurrencyCustomFeeModel();

            // ((100000 - 100 * 100) * 2 * 0.9975 / (25)
            var actual = algo.CalculateOrderQuantity(_symbol, 1m * security.BuyingPowerModel.GetLeverage(security));
            Assert.AreEqual(7182m, actual);
            // ((100000 - 100 * 100) * 2 / (25)
            var quantity = security.BuyingPowerModel.GetMaximumOrderQuantityForTargetValue(
                algo.Portfolio, security, 2m).Quantity;
            Assert.AreEqual(7200m, quantity);

            // the maximum order quantity can be executed
            Assert.IsTrue(HasSufficientBuyingPowerForOrder(quantity, security, algo)); ;
        }

        [TestCase(0)]
        [TestCase(10000)]
        public void NonAccountCurrency_GetBuyingPower(decimal nonAccountCurrencyCash)
        {
            var algo = GetAlgorithm();
            algo.Portfolio.CashBook.Clear();
            algo.Portfolio.SetAccountCurrency("EUR");
            algo.Portfolio.SetCash(10000);
            algo.Portfolio.SetCash(Currencies.USD, nonAccountCurrencyCash, 0.88m);
            var security = InitAndGetSecurity(algo, 0);
            Assert.AreEqual(10000m + algo.Portfolio.CashBook[Currencies.USD].ValueInAccountCurrency,
                algo.Portfolio.TotalPortfolioValue);

            var quantity = security.BuyingPowerModel.GetBuyingPower(
                new BuyingPowerParameters(algo.Portfolio, security, OrderDirection.Buy)).Value;

            Assert.AreEqual(10000m + algo.Portfolio.CashBook[Currencies.USD].ValueInAccountCurrency,
                quantity);
        }

        private static QCAlgorithm GetAlgorithm()
        {
            SymbolCache.Clear();
            // Initialize algorithm
            var algo = new QCAlgorithm();
            algo.SetFinishedWarmingUp();
            _fakeOrderProcessor = new FakeOrderProcessor();
            algo.Transactions.SetOrderProcessor(_fakeOrderProcessor);
            return algo;
        }

        private static Security InitAndGetSecurity(QCAlgorithm algo, decimal fee, SecurityType securityType = SecurityType.Equity, string symbol = "SPY")
        {
            algo.SubscriptionManager.SetDataManager(new DataManagerStub(algo));
            Security security;
            if (securityType == SecurityType.Equity)
            {
                security = algo.AddEquity(symbol);
                _symbol = security.Symbol;
            }
            else if (securityType == SecurityType.Option)
            {
                security = algo.AddOption(symbol);
                _symbol = security.Symbol;
            }
            else if (securityType == SecurityType.Future)
            {
                security = algo.AddFuture(symbol);
                _symbol = security.Symbol;
            }
            else
            {
                throw new Exception("SecurityType not implemented");
            }

            security.FeeModel = new ConstantFeeModel(fee);
            Update(algo.Portfolio.CashBook, security, 25);
            return security;
        }

        private static void Update(CashBook cashBook, Security security, decimal close)
        {
            security.SetMarketPrice(new TradeBar
            {
                Time = DateTime.Now,
                Symbol = security.Symbol,
                Open = close,
                High = close,
                Low = close,
                Close = close
            });
        }

        private bool HasSufficientBuyingPowerForOrder(decimal orderQuantity, Security security, IAlgorithm algo)
        {
            var order = new MarketOrder(security.Symbol, orderQuantity, DateTime.UtcNow);
            _fakeOrderProcessor.AddTicket(order.ToOrderTicket(algo.Transactions));
            var hashSufficientBuyingPower = security.BuyingPowerModel.HasSufficientBuyingPowerForOrder(algo.Portfolio,
                security, new MarketOrder(security.Symbol, orderQuantity, DateTime.UtcNow));
            return hashSufficientBuyingPower.IsSufficient;
        }

        internal class NonAccountCurrencyCustomFeeModel : FeeModel
        {
            public string FeeCurrency = "EUR";
            public decimal FeeAmount = 100m;

            public override OrderFee GetOrderFee(OrderFeeParameters parameters)
            {
                return new OrderFee(new CashAmount(FeeAmount, FeeCurrency));
            }
        }
    }
}