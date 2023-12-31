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
using QuantConnect.Brokerages;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.OptionQuote;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Interfaces
{
    /// <summary>
    /// Brokerage interface that defines the operations all brokerages must implement. The IBrokerage implementation
    /// must have a matching IBrokerageFactory implementation.
    /// </summary>
    public interface IBrokerage : IDisposable
    {
        /// <summary>
        /// Event that fires each time an order is filled
        /// </summary>
        event EventHandler<OrderEvent> OrderStatusChanged;

        /// <summary>
        /// 
        /// </summary>
        event EventHandler<IEnumerable<TradeRecord>> TradeOccurred;

        /// <summary>
        /// 
        /// </summary>
        event EventHandler<IEnumerable<OrderRecord>> OrderOccurred;

        /// <summary>
        /// Event that fires each time a short option position is assigned
        /// </summary>
        event EventHandler<OrderEvent> OptionPositionAssigned;

        /// <summary>
        /// Event that fires each time a user's brokerage account is changed
        /// </summary>
        event EventHandler<AccountEvent> AccountChanged;

        /// <summary>
        /// Event that fires when a message is received from the brokerage
        /// </summary>
        event EventHandler<BrokerageMessageEvent> Message;

        event EventHandler<AlgorithmQueryArgs> QueryEvent;

        /// <summary>
        /// Gets the name of the brokerage
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns true if we're currently connected to the broker
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Gets all open orders on the account
        /// </summary>
        /// <returns>The open orders returned from IB</returns>
        List<Order> GetOpenOrders();

        /// <summary>
        /// Gets all holdings for the account
        /// </summary>
        /// <returns>The current holdings from the account</returns>
        List<Holding> GetAccountHoldings();

        /// <summary>
        /// Gets the current cash balance for each currency held in the brokerage account
        /// </summary>
        /// <returns>The current cash balance for each currency available for trading</returns>
        List<CashAmount> GetCashBalance();

        /// <summary>
        /// Places a new order and assigns a new broker ID to the order
        /// </summary>
        /// <param name="order">The order to be placed</param>
        /// <returns>True if the request for a new order has been placed, false otherwise</returns>
        bool PlaceOrder(Order order);

        /// <summary>
        /// Updates the order with the same id
        /// </summary>
        /// <param name="order">The new order information</param>
        /// <returns>True if the request was made for the order to be updated, false otherwise</returns>
        bool UpdateOrder(Order order);

        /// <summary>
        /// Cancels the order with the specified ID
        /// </summary>
        /// <param name="order">The order to cancel</param>
        /// <returns>True if the request was made for the order to be canceled, false otherwise</returns>
        bool CancelOrder(Order order);

        /// <summary>
        /// Connects the client to the broker's remote servers
        /// </summary>
        void Connect();

        /// <summary>
        /// Disconnects the client from the broker's remote servers
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Specifies whether the brokerage will instantly update account balances
        /// </summary>
        bool AccountInstantlyUpdated { get; }

        /// <summary>
        /// Gets the history for the requested security
        /// </summary>
        /// <param name="request">The historical data request</param>
        /// <returns>An enumerable of bars covering the span specified in the request</returns>
        IEnumerable<BaseData> GetHistory(HistoryRequest request);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        List<string> SupportMarkets();


        /// <summary>
        /// 
        /// </summary>
        /// <param name="order"></param>
        void SetOrderCached(Order order);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        List<TradeRecord> GetHistoryTrades();

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        List<OrderRecord> GetHistoryOrders();

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        void QueryOpenOrders(string requestId);
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        void QueryAccountHoldings(string requestId);
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        void QueryCashBalance(string requestId);

        void QueryExchangeOpenOrders(string uId, string requestId);

        void QueryExchangeAccountHoldings(string uId, string requestId);

        void QueryExchangeCashBalance(string uId, string requestId);

        void ChangeLeverage(string symbol, int leverage, string exchange);

        void Transfer(decimal amount, UserTransferType type = UserTransferType.Spot2UmFuture, string currency = "USDT");
        
        bool TradingIsReady();
        RequestResult<QuoteRequest> SendQuoteRequest(QuoteRequest request);
        RequestResult<Quote> AcceptRequestQuote(string quoteId);
        RequestResult<QuoteRequest> CancelQuoteRequest(string requestId);
        RequestResult<List<Quote>> QueryOptionQuote(string requestId);
        RequestResult<List<OptionPosition>> QueryOptionPosition();
    }
}