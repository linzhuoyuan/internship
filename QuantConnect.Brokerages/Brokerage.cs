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
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.OptionQuote;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Brokerages
{
    /// <summary>
    /// Represents the base Brokerage implementation. This provides logging on brokerage events.
    /// </summary>
    public abstract class Brokerage : IBrokerage
    {
        /// <summary>
        /// Event that fires each time an order is filled
        /// </summary>
        public event EventHandler<OrderEvent> OrderStatusChanged;

        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<IEnumerable<TradeRecord>> TradeOccurred;

        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<IEnumerable<OrderRecord>> OrderOccurred;

        /// <summary>
        /// Event that fires each time a short option position is assigned
        /// </summary>
        public event EventHandler<OrderEvent> OptionPositionAssigned;

        /// <summary>
        /// Event that fires each time a user's brokerage account is changed
        /// </summary>
        public event EventHandler<AccountEvent> AccountChanged;

        /// <summary>
        /// Event that fires when an error is encountered in the brokerage
        /// </summary>
        public event EventHandler<BrokerageMessageEvent> Message;

        public event EventHandler<AlgorithmQueryArgs> QueryEvent;


        /// <summary>
        /// Gets the name of the brokerage
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns true if we're currently connected to the broker
        /// </summary>
        public abstract bool IsConnected { get; }

        /// <summary>
        /// Creates a new Brokerage instance with the specified name
        /// </summary>
        /// <param name="name">The name of the brokerage</param>
        protected Brokerage(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Places a new order and assigns a new broker ID to the order
        /// </summary>
        /// <param name="order">The order to be placed</param>
        /// <returns>True if the request for a new order has been placed, false otherwise</returns>
        public abstract bool PlaceOrder(Order order);

        /// <summary>
        /// Updates the order with the same id
        /// </summary>
        /// <param name="order">The new order information</param>
        /// <returns>True if the request was made for the order to be updated, false otherwise</returns>
        public abstract bool UpdateOrder(Order order);

        /// <summary>
        /// Cancels the order with the specified ID
        /// </summary>
        /// <param name="order">The order to cancel</param>
        /// <returns>True if the request was made for the order to be canceled, false otherwise</returns>
        public abstract bool CancelOrder(Order order);

        /// <summary>
        /// Connects the client to the broker's remote servers
        /// </summary>
        public abstract void Connect();

        /// <summary>
        /// Disconnects the client from the broker's remote servers
        /// </summary>
        public abstract void Disconnect();

        /// <summary>
        /// Dispose of the brokerage instance
        /// </summary>
        public virtual void Dispose()
        {
            // NOP
        }

        /// <summary>
        /// Event invocator for the OrderFilled event
        /// </summary>
        /// <param name="e">The OrderEvent</param>
        protected virtual void OnOrderEvent(OrderEvent e)
        {
            try
            {
                Log.Debug("Brokerage.OnOrderEvent(): " + e);

                var handler = OrderStatusChanged;
                if (handler != null) handler(this, e);
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnTradeOccurred(IEnumerable<TradeRecord> e)
        {
            try
            {
                Log.Debug("Brokerage.OnTradeOccurred(): " + e);

                var handler = TradeOccurred;
                if (handler != null) handler(this, e);
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="e"></param>
        protected virtual void OnOrderOccurred(IEnumerable<OrderRecord> e)
        {
            try
            {
                Log.Debug("Brokerage.OnOrderOccurred(): " + e);

                var handler = OrderOccurred;
                if (handler != null) handler(this, e);
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

        /// <summary>
        /// Event invocator for the OptionPositionAssigned event
        /// </summary>
        /// <param name="e">The OrderEvent</param>
        protected virtual void OnOptionPositionAssigned(OrderEvent e)
        {
            try
            {
                Log.Debug("Brokerage.OptionPositionAssigned(): " + e);

                var handler = OptionPositionAssigned;
                handler?.Invoke(this, e);
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

        /// <summary>
        /// Event invocation for the AccountChanged event
        /// </summary>
        /// <param name="e">The AccountEvent</param>
        protected virtual void OnAccountChanged(AccountEvent e)
        {
            try
            {
                Log.Trace("Brokerage.OnAccountChanged(): " + e);

                var handler = AccountChanged;
                handler?.Invoke(this, e);
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

        /// <summary>
        /// Event invocation for the Message event
        /// </summary>
        /// <param name="e">The error</param>
        protected virtual void OnMessage(BrokerageMessageEvent e)
        {
            try
            {
                if (e.Type == BrokerageMessageType.Error)
                {
                    Log.Error("Brokerage.OnMessage(): " + e);
                }
                else
                {
                    Log.Trace("Brokerage.OnMessage(): " + e);
                }

                var handler = Message;
                handler?.Invoke(this, e);
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

        /// <summary>
        /// Gets all open orders on the account.
        /// NOTE: The order objects returned do not have QC order IDs.
        /// </summary>
        /// <returns>The open orders returned from IB</returns>
        public abstract List<Order> GetOpenOrders();

        /// <summary>
        /// Gets all holdings for the account
        /// </summary>
        /// <returns>The current holdings from the account</returns>
        public abstract List<Holding> GetAccountHoldings();

        /// <summary>
        /// Gets the current cash balance for each currency held in the brokerage account
        /// </summary>
        /// <returns>The current cash balance for each currency available for trading</returns>
        public abstract List<CashAmount> GetCashBalance();

        /// <summary>
        /// Specifies whether the brokerage will instantly update account balances
        /// </summary>
        public virtual bool AccountInstantlyUpdated => false;

        /// <summary>
        /// Gets the history for the requested security
        /// </summary>
        /// <param name="request">The historical data request</param>
        /// <returns>An enumerable of bars covering the span specified in the request</returns>
        public virtual IEnumerable<BaseData> GetHistory(HistoryRequest request)
        {
            return Enumerable.Empty<BaseData>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual List<string> SupportMarkets()
        {
            return new List<string>();
        }

        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="order"></param>
        public virtual void SetOrderCached(Order order)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual List<TradeRecord> GetHistoryTrades()
        {
            return new List<TradeRecord>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual List<OrderRecord> GetHistoryOrders()
        {
            return new List<OrderRecord>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual void QueryOpenOrders(string requestId)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual void QueryAccountHoldings(string requestId)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual void QueryCashBalance(string requestId)
        {
        }


        public virtual void QueryExchangeOpenOrders(string uId, string requestId)
        {

        }

        public virtual void QueryExchangeAccountHoldings(string uId, string requestId)
        {

        }

        public virtual void QueryExchangeCashBalance(string uId, string requestId)
        {

        }

        public virtual void ChangeLeverage(string symbol, int leverage, string exchange)
        {

        }

        public virtual void Transfer(decimal amount, UserTransferType type = UserTransferType.Spot2UmFuture, string currency = "USDT")
        {
        }

        public virtual bool TradingIsReady()
        {
            return true;
        }

        public virtual RequestResult<QuoteRequest> SendQuoteRequest(QuoteRequest request)
        {
            throw new NotImplementedException();
        }

        public virtual RequestResult<Quote> AcceptRequestQuote(string quoteId)
        {
            throw new NotImplementedException();
        }

        public virtual RequestResult<QuoteRequest> CancelQuoteRequest(string requestId)
        {
            throw new NotImplementedException();
        }

        public virtual RequestResult<List<Quote>> QueryOptionQuote(string requestId)
        {
            throw new NotImplementedException();
        }

        public virtual RequestResult<List<OptionPosition>> QueryOptionPosition()
        {
            throw new NotImplementedException();
        }

        protected virtual void OnQueryOpenOrderEvent(AlgorithmQueryArgs e)
        {
            try
            {
                Log.Debug("Brokerage.OnQueryOpenOrderEvent(): " + e);

                var handler = QueryEvent;
                handler?.Invoke(this, e);
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }

        protected virtual void OnQueryAccountHoldingEvent(AlgorithmQueryArgs e)
        {
            try
            {
                Log.Debug("Brokerage.OnQueryAccountHoldingEvent(): " + e);

                var handler = QueryEvent;
                handler?.Invoke(this, e);
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }
        protected virtual void OnQueryCashBalanceEvent(AlgorithmQueryArgs e)
        {
            try
            {
                Log.Debug("Brokerage.OnQueryCashBalanceEvent(): " + e);

                var handler = QueryEvent;
                handler?.Invoke(this, e);
            }
            catch (Exception err)
            {
                Log.Error(err);
            }
        }
    }
}
