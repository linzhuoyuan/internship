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

using System.Collections.Generic;
using QuantConnect.OptionQuote;
using QuantConnect.Orders;

namespace QuantConnect.Securities
{
    public enum UserTransferType
    {
        Spot2UmFuture,
        UmFuture2Spot
    }

    /// <summary>
    /// Represents a type capable of processing orders
    /// </summary>
    public interface IOrderProcessor : IOrderProvider
    {
        /// <summary>
        /// Adds the specified order to be processed
        /// </summary>
        /// <param name="request">The <see cref="OrderRequest"/> to be processed</param>
        /// <returns>The <see cref="OrderTicket"/> for the corresponding <see cref="OrderRequest.OrderId"/></returns>
        OrderTicket Process(OrderRequest request);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <param name="requestId"></param>
        void Query(AlgorithmQueryType type, string requestId);

        void ExchangeQuery(AlgorithmQueryType type, string uId, string requestId);

        void ChangeLeverage(string symbol, int leverage, string exchange = "");

        void Transfer(decimal amount, UserTransferType type = UserTransferType.Spot2UmFuture, string currency = "USDT");

        bool TradingIsReady();

        RequestResult<QuoteRequest> SendQuoteRequest(QuoteRequest request);
        RequestResult<Quote> AcceptRequestQuote(string quoteId);
        RequestResult<QuoteRequest> CancelQuoteRequest(string requestId);
        RequestResult<List<Quote>> QueryOptionQuote(string requestId);
        RequestResult<List<OptionPosition>> QueryOptionPosition();
    }
}