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
using QuantConnect.Data;
using QuantConnect.Securities;
using QuantConnect.Logging;
using System.Threading;

namespace QuantConnect.Lean.Engine.DataFeeds
{
    /// <summary>
    /// DTO for storing data and the time at which it should be synchronized
    /// </summary>
    public class SubscriptionData
    {
        internal readonly BaseData data;
        internal readonly DateTime emitTimeUtc;

        /// <summary>
        /// Gets the data
        /// </summary>
        public BaseData Data => data;

        /// <summary>
        /// Gets the UTC emit time for this data
        /// </summary>
        public DateTime EmitTimeUtc => emitTimeUtc;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionData"/> class
        /// </summary>
        /// <param name="data">The base data</param>
        /// <param name="emitTimeUtc">The emit time for the data</param>
        public SubscriptionData(BaseData data, DateTime emitTimeUtc)
        {
            this.data = data;
            this.emitTimeUtc = emitTimeUtc;
        }

        /// <summary>
        /// Clones the data, computes the utc emit time and performs exchange round down behavior, storing the result in a new <see cref="SubscriptionData"/> instance
        /// </summary>
        /// <param name="configuration">The subscription's configuration</param>
        /// <param name="exchangeHours">The exchange hours of the security</param>
        /// <param name="offsetProvider">The subscription's offset provider</param>
        /// <param name="data">The data being emitted</param>
        /// <returns>A new <see cref="SubscriptionData"/> containing the specified data</returns>
        public static SubscriptionData Create(
            SubscriptionDataConfig configuration,
            SecurityExchangeHours exchangeHours,
            TimeZoneOffsetProvider offsetProvider,
            BaseData data)
        {
            if (data == null)
            {
                return null;
            }

            //string id = data.MID;
            data = data.Clone(data.isFillForward);
            var emitTimeUtc = offsetProvider.ConvertToUtc(data.endTime);
            if (configuration.Resolution != Resolution.Tick)
                data.Time = data.time.ExchangeRoundDownInTimeZone(
                    configuration.Increment,
                    exchangeHours,
                    configuration.DataTimeZone,
                    configuration.ExtendedMarketHours);
            //data.MID = id;

            //if(BaseData.IsTestData() && data.Symbol.Value == BaseData.TestSymbolValue())
            //{
            //    Log.Trace($"thread:{Thread.CurrentThread.ManagedThreadId.ToString()} testtheone:-->SubscriptionData.Create ID:{data.MID} Time:{time.ToString("MM/dd/yyyy hh:mm:ss.fff tt")} emitTimeUtc:{emitTimeUtc.ToString("MM/dd/yyyy hh:mm:ss.fff tt")} data.Time:{data.Time.ToString("MM/dd/yyyy hh:mm:ss.fff tt")} {data.Symbol.Value}");
            //}

            return new SubscriptionData(data, emitTimeUtc);
        }

        /// <summary>
        /// Wraps an existing <see cref="IEnumerator{BaseData}"/> to produce an <see cref="IEnumerator{SubscriptionData}"/>.
        /// </summary>
        /// <param name="configuration">The subscription's configuration</param>
        /// <param name="security">The subscription's security</param>
        /// <param name="offsetProvider">The subscription's time zone offset provider</param>
        /// <param name="enumerator">The underlying data enumerator</param>
        /// <returns>A subscription data enumerator</returns>
        public static IEnumerator<SubscriptionData> Enumerator(SubscriptionDataConfig configuration, Security security, TimeZoneOffsetProvider offsetProvider, IEnumerator<BaseData> enumerator)
        {
            while (enumerator.MoveNext())
            {
                yield return Create(configuration, security.Exchange.Hours, offsetProvider, enumerator.Current);
            }
        }
    }
}