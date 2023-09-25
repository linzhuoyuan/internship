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
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuantConnect.Securities
{
    /// <summary>
    /// Provides an implementation of <see cref="JsonConverter"/> that can deserialize SecurityHolding
    /// </summary>
    public class SecurityHoldingJsonConverter : JsonConverter
    {
        /// <summary>
        /// Gets a value indicating whether this <see cref="T:Newtonsoft.Json.JsonConverter"/> can write JSON.
        /// </summary>
        /// <value>
        /// <c>true</c> if this <see cref="T:Newtonsoft.Json.JsonConverter"/> can write JSON; otherwise, <c>false</c>.
        /// </value>
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns>
        /// <c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.
        /// </returns>
        public override bool CanConvert(Type objectType)
        {
            return typeof(SecurityHolding).IsAssignableFrom(objectType);
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer">The <see cref="T:Newtonsoft.Json.JsonWriter"/> to write to.</param><param name="value">The value.</param><param name="serializer">The calling serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException("The SecurityHoldingJsonConverter does not implement a WriteJson method;.");
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader">The <see cref="T:Newtonsoft.Json.JsonReader"/> to read from.</param><param name="objectType">Type of the object.</param><param name="existingValue">The existing value of object being read.</param><param name="serializer">The calling serializer.</param>
        /// <returns>
        /// The object value.
        /// </returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var jObject = JObject.Load(reader);

            var securityHolding = CreateSecurityHoldingFromJObject(jObject);

            return securityHolding;
        }

        /// <summary>
        /// Create an  from a simple JObject
        /// </summary>
        /// <param name="jObject"></param>
        /// <returns>SecurityHolding Object</returns>
        public static SecurityHolding CreateSecurityHoldingFromJObject(JObject jObject)
        {
            var symbol = Symbol.Empty;
            var underlyingSymbol = Symbol.Empty;
            var securityHolding = new SecurityHolding();
            var averagePrice = jObject["AveragePrice"].Value<decimal>();
            var quantity = jObject["Quantity"].Value<decimal>();
            var quantityT0 = jObject["QuantityT0"].Value<decimal>();
            securityHolding.SetHoldings(averagePrice, quantity, quantityT0);
            if (jObject.SelectTokens("Symbol.ID").Any())
            {
                var sid = SecurityIdentifier.Parse(jObject.SelectTokens("Symbol.ID").Single().Value<string>());
                var ticker = jObject.SelectTokens("Symbol.Value").Single().Value<string>();
                var symbolCache = SymbolCache.GetSymbol(ticker);
                symbol = symbolCache == null ? new Symbol(sid, ticker) : symbolCache;
            }
            securityHolding.Symbol = symbol;
            securityHolding.AddNewFee(jObject["TotalFees"].Value<decimal>());
            securityHolding.AddNewProfit(jObject["Profit"].Value<decimal>());
            securityHolding.AddNewSale(jObject["TotalSaleVolume"].Value<decimal>());
            securityHolding.SetLastTradeProfit(jObject["LastTradeProfit"].Value<decimal>());
            securityHolding.HoldingType = (SecurityHoldingType)jObject["HoldingType"].Value<int>();
            securityHolding.UpdateMarketPrice(jObject["Price"].Value<decimal>());
            return securityHolding;
        }
    }
}
