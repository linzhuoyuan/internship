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
using NUnit.Framework;
using QuantConnect.Brokerages.Deribit;

namespace QuantConnect.Tests.Brokerages.Deribit
{
    [TestFixture]
    public class DeribitSymbolMapperTests
    {
        [Test]
        public void ReturnsCorrectLeanSymbol()
        {
            var mapper = new DeribitSymbolMapper();
           
           var symbol = mapper.GetLeanSymbol("BTC-USD", SecurityType.Crypto, Market.Deribit);
            Assert.AreEqual("BTC-USD", symbol.Value);
            Assert.AreEqual(SecurityType.Crypto, symbol.ID.SecurityType);
            Assert.AreEqual(Market.Deribit, symbol.ID.Market);
        }

        [Test]
        public void ReturnsCorrectBrokerageSymbol()
        {
            var mapper = new DeribitSymbolMapper();

            var symbol = Symbol.CreateOption(
            "BTCUSD",
            Market.Deribit,
            OptionStyle.European,
            OptionRight.Call,
            9250,
            new DateTime(2020, 5, 22),
            "BTC-22MAY20-9250-C"
        );
            var brokerageSymbol = mapper.GetBrokerageSymbol(symbol);
            Assert.AreEqual("BTC-22MAY20-9250-C", brokerageSymbol);
        }

        [Test]
        public void ReturnsCorrectUnderlyingSymbol()
        {
            var mapper = new DeribitSymbolMapper();

            var underlyingSymbol = mapper.GetUnderlyingSymbol("BTC-22MAY20-9250-C");
            Assert.AreEqual("BTCUSD", underlyingSymbol.Value);
            Assert.AreEqual(SecurityType.Crypto, underlyingSymbol.ID.SecurityType);
        }

        [Test]
        public void ReturnsCorrectBrokerageSecurityType()
        {
            var mapper = new DeribitSymbolMapper();

            var brokerageSecurityType = mapper.GetBrokerageSecurityType("BTC-22MAY20-9250-C");
            Assert.AreEqual(SecurityType.Option, brokerageSecurityType);
        }

        [Test]
        public void ThrowsOnNullOrEmptyOrInvalidSymbol()
        {
            var mapper = new DeribitSymbolMapper();

            Assert.Throws<ArgumentException>(() => mapper.GetLeanSymbol(null, SecurityType.Option, Market.Deribit));

            Assert.Throws<ArgumentException>(() => mapper.GetLeanSymbol("", SecurityType.Option, Market.Deribit));

            var symbol = Symbol.Empty;
            Assert.Throws<ArgumentException>(() => mapper.GetBrokerageSymbol(symbol));

            symbol = null;
            Assert.Throws<ArgumentException>(() => mapper.GetBrokerageSymbol(symbol));

            symbol = Symbol.Create("", SecurityType.Option, Market.Deribit);
            Assert.Throws<ArgumentException>(() => mapper.GetBrokerageSymbol(symbol));

        }

    }
}
