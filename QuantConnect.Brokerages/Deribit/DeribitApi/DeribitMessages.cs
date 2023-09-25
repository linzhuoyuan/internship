using Newtonsoft.Json;
using System;
using RestSharp;
using System.Collections.Generic;
using System.Globalization;

namespace TheOne.Deribit
{
    public class DeribitMessages
    {

        //several simple objects to facilitate json conversion
#pragma warning disable 1591

        public class BaseMessage
        {
            public int id { get; set; }
            public string jsonrpc { get; set; }
            public long usIn { get; set; }
            public long usOut { get; set; }
            public long usDiff { get; set; }
            public bool testnet { get; set; }
        }

        public class ErrorInfo 
        {
            [JsonProperty("message")]
            public string ErrorMsg { get; set; }
            [JsonProperty("code")]
            public int ErrorCode { get; set; }
            /// <summary>
            /// 10301 : Already subscribed
            /// </summary>
            public string Level => ErrorCode == 10301 ? "Warning" : "Error";
        }

        public class ErrorMessage : BaseMessage
        {
            public ErrorInfo error { get; set; }            
        }

        public class Auth
        {
            public string access_token { get; set; }
            public int expires_in { get; set; }
            public string refresh_token { get; set; }
            public string scope { get; set; }
            public string state { get; set; }
            public string token_type { get; set; }
        }

        public class Order
        {
            //order state, "open", "filled", "rejected", "cancelled", "untriggered"
            public string order_state { get; set; }
            //Maximum amount within an order to be shown to other traders, 0 for invisible order.
            public decimal max_show { get; set; }
            //true if created with API
            public bool api { get; set; }
            //It represents the requested order size. For perpetual and futures the amount is in USD units, 
            //for options it is amount of corresponding cryptocurrency contracts, e.g., BTC or ETH.
            public decimal amount { get; set; }
            //true created via Deribit frontend(optional)
            public bool web { get; set; }
            //Unique instrument identifier
            public string instrument_name { get; set; }
            //advanced type: "usd" or "implv" (Only for options; field is omitted if not applicable).
            public string advanced { get; set; }
            //Whether the stop order has been triggered (Only for stop orders)
            public bool triggered { get; set; }
            //true if order made from block_trade trade, added only in that case.
            public bool block_trade { get; set; }
            //Original order type. Optional field
            public string original_order_type { get; set; }
            //Price in base currency
            public decimal price { get; set; }
            //Order time in force: "good_til_cancelled", "fill_or_kill", "immediate_or_cancel"
            public string time_in_force { get; set; }
            //Options, advanced orders only - true if last modification of the order was performed by the pricing engine, otherwise false
            public bool auto_replaced { get; set; }
            //Id of the stop order that was triggered to create the order (Only for orders that were created by triggered stop orders).
            public string stop_order_id { get; set; }
            //The timestamp (seconds since the Unix epoch, with millisecond precision)
            public long last_update_timestamp { get; set; }
            //true for post-only orders only
            public bool post_only { get; set; }
            //true if the order was edited (by user or - in case of advanced options orders - by pricing engine), otherwise false.
            public bool replaced { get; set; }
            //Filled amount of the order. For perpetual and futures the filled_amount is in USD units, 
            //for options - in units or corresponding cryptocurrency contracts, e.g., BTC or ETH.
            public decimal filled_amount { get; set; }
            //Average fill price of the order
            public decimal average_price { get; set; }
            //Unique order identifier
            public string order_id { get; set; }
            //true for reduce-only orders only
            public bool reduce_only { get; set; }
            //Commission paid so far (in base currency)
            public decimal commission { get; set; }
            //Name of the application that placed the order on behalf of the user (optional).
            public string app_name { get; set; }
            //stop price (Only for future stop orders)
            public decimal stop_price { get; set; }
            //user defined label (up to 32 characters)
            public string label { get; set; }
            //The timestamp (seconds since the Unix epoch, with millisecond precision)
            public long creation_timestamp { get; set; }
            //direction, buy or sell
            public string direction { get; set; }
            //true if order was automatically created during liquidation
            public bool is_liquidation { get; set; }
            //order type, "limit", "market", "stop_limit", "stop_market"
            public string order_type { get; set; }
            //Option price in USD (Only if advanced="usd")
            public decimal usd { get; set; }
            //Profit and loss in base currency.
            public decimal profit_loss { get; set; }
            //Implied volatility in percent. (Only if advanced="implv")
            public decimal implv { get; set; }
            //Trigger type (Only for stop orders). Allowed values: "index_price", "mark_price", "last_price".
            public string trigger { get; set; }


            public Order Clone() //浅CLONE
            {
                return this.MemberwiseClone() as Order;
            }
        }

        public class OrderResult : ErrorMessage
        {
            //public int id { get; set; }
            //public string jsonrpc { get; set; }
            [JsonProperty("result")]
            public Order[] orders { get; set; }
        }

        public class OrderStateResult : ErrorMessage
        {
            //public int id { get; set; }
            //public string jsonrpc { get; set; }
            [JsonProperty("result")]
            public Order order { get; set; }
        }

        public class Trade
        {
            //amount	number	Trade amount. For perpetual and futures - in USD units, for options it is amount of corresponding cryptocurrency contracts, e.g., BTC or ETH.
            public decimal amount { get; set; }
            //block_trade_id	string	Block trade id - when trade was part of block trade
            public string block_trade_id { get; set; }
            //direction string direction, buy or sell
            public string direction { get; set; }
            //fee	number	User's fee in units of the specified fee_currency
            public decimal fee { get; set; }
            //fee_currency	string	Currency, i.e "BTC", "ETH"
            public string fee_currency { get; set; }
            //index_price	number	Index Price at the moment of trade
            public decimal index_price { get; set; }
            //instrument_name	string	Unique instrument identifier
            public string instrument_name { get; set; }
            // iv	number	Option implied volatility for the price (Option only)
            public decimal iv { get; set; }
            // label	string	User defined label (presented only when previously set for order by user)
            public string label { get; set; }
            //liquidation string Optional field(only for trades caused by liquidation) : "M" when maker side of trade was under liquidation, "T" when taker side was under liquidation, "MT" when both sides of trade were under liquidation
            public string liquidation { get; set; }
            //liquidity string Describes what was role of users order: "M" when it was maker order, "T" when it was taker order
            public string liquidity { get; set; }
            //matching_id string Always null, except for a self-trade which is possible only if self-trading is switched on for the account(in that case this will be id of the maker order of the subscriber)
            public string matching_id { get; set; }
            //order_id string Id of the user order(maker or taker), i.e.subscriber's order id that took part in the trade
            public string order_id { get; set; }
            //order_type string Order type: "limit, "market", or "liquidation"
            public string order_type { get; set; }
            //post_only string	true if user order is post-only
            public string post_only { get; set; }
            //price number  Price in base currency
            public decimal price { get; set; }
            //reduce_only string	true if user order is reduce-only
            public string reduce_only { get; set; }
            //self_trade boolean	true if the trade is against own order.This can only happen when your account has self-trading enabled.Contact an administrator if you think you need that
            public bool self_trade { get; set; }
            //state    string order state, "open", "filled", "rejected", "cancelled", "untriggered" or "archive" (if order was archived)
            public string state { get; set; }
            //tick_direction integer Direction of the "tick" (0 = Plus Tick, 1 = Zero-Plus Tick, 2 = Minus Tick, 3 = Zero-Minus Tick).
            public long tick_direction { get; set; }
            //timestamp integer The timestamp of the trade
            public long timestamp { get; set; }
            //trade_id string Unique(per currency) trade identifier
            public string trade_id { get; set; }
            //trade_seq integer The sequence number of the trade within instrument
            public long trade_seq { get; set; }

            //	Profit and loss in base currency.
            public decimal? profit_loss { get; set; }
            //	Underlying price for implied volatility calculations (Options only)
            public decimal underlying_price { get; set; }
            //	Mark Price at the moment of trade
            public decimal mark_price { get; set; }
        }

        public class TradeResult
        {
            public int id { get; set; }
            public string jsonrpc { get; set; }
            [JsonProperty("result")]
            public Trades result { get; set; }
        }

        public class TradeByOrderResult
        {
            public int id { get; set; }
            public string jsonrpc { get; set; }
            [JsonProperty("result")]
            public Trade[] trades { get; set; }
        }

        public class Trades
        {
            public bool has_more { get; set; }
            public Trade[] trades { get; set; }
        }

        public class PlaceOrder
        {
            public Order order { get; set; }
            public Trade[] trades { get; set; }
        }

        public class CancelOrderResult : ErrorMessage
        {
            //public int id { get; set; }
            //public string jsonrpc { get; set; }
            [JsonProperty("result")]
            public Order order { get; set; }
        }

        public class PlaceOrderResult : ErrorMessage
        {
            //public int id { get; set; }
            //public string jsonrpc { get; set; }
            [JsonProperty("result")]
            public PlaceOrder place_order { get; set; }
        }

        public class Position
        {
            //	Average price of trades that built this position
            public decimal average_price { get; set; }
            //Only for options, average price in USD
            public decimal average_price_usd { get; set; }
            //Delta parameter
            public decimal delta { get; set; }
            //direction, buy or sell
            public string direction { get; set; }
            //Only for futures, estimated liquidation price
            public decimal estimated_liquidation_price { get; set; }
            //Floating profit or loss
            public decimal floating_profit_loss { get; set; }
            //Only for options, floating profit or loss in USD
            public decimal floating_profit_loss_usd { get; set; }
            //Current index price
            public decimal index_price { get; set; }
            //Initial margin
            public decimal initial_margin { get; set; }
            //Unique instrument identifier
            public string instrument_name { get; set; }
            //Instrument kind, "future" or "option"
            public string kind { get; set; }
            //Maintenance margin
            public decimal maintenance_margin { get; set; }
            //Current mark price for position's instrument
            public decimal mark_price { get; set; }
            //Open orders margin
            public decimal open_orders_margin { get; set; }
            //Realized profit or loss
            public decimal realized_profit_loss { get; set; }
            //Last settlement price for position's instrument 0 if instrument wasn't settled yet
            public decimal settlement_price { get; set; }
            //Position size for futures size in quote currency (e.g. USD), for options size is in base currency (e.g. BTC)
            public decimal size { get; set; }
            //Only for futures, position size in base currency
            public decimal size_currency { get; set; }
            //Profit or loss from position
            public decimal total_profit_loss { get; set; }
        }

        public class PositionResult
        {
            public int id { get; set; }
            public string jsonrpc { get; set; }
            [JsonProperty("result")]
            public Position[] positions { get; set; }
        }

        public class Account
        {
            //Options summary gamma
            public decimal options_gamma { get; set; }
            //Projected maintenance margin (for portfolio margining users)
            public decimal projected_maintenance_margin { get; set; }
            //System generated user nickname (available when parameter extended = true)
            public string system_name { get; set; }
            //The account's margin balance
            public decimal margin_balance { get; set; }
            //Whether two factor authentication is enabled (available when parameter extended = true)
            public bool tfa_enabled { get; set; }
            //Options value
            public decimal options_value { get; set; }
            //Account name (given by user) (available when parameter extended = true)
            public string username { get; set; }
            //The account's current equity
            public decimal equity { get; set; }
            //Futures profit and Loss
            public decimal futures_pl { get; set; }
            //Options session unrealized profit and Loss
            public decimal options_session_upl { get; set; }
            //Account id (available when parameter extended = true)
            public int id { get; set; }
            //Options summary vega
            public decimal options_vega { get; set; }
            //Optional identifier of the referrer (of the affiliation program, and available when parameter extended = true), which link was used by this account at registration. It coincides with suffix of the affiliation link path after /reg-
            public string referrer_id { get; set; }
            //The selected currency
            public string currency { get; set; }
            //Whether account is loginable using email and password (available when parameter extended = true and account is a subaccount)
            public bool login_enabled { get; set; }
            //Account type (available when parameter extended = true)
            public string type { get; set; }
            //Futures session realized profit and Loss
            public decimal futures_session_rpl { get; set; }
            //Options summary theta
            public decimal options_theta { get; set; }
            //true when portfolio margining is enabled for user
            public bool portfolio_margining_enabled { get; set; }
            //The sum of position deltas without positions that will expire during closest expiration
            public decimal projected_delta_total { get; set; }
            //Session realized profit and loss
            public decimal session_rpl { get; set; }
            //The sum of position deltas
            public decimal delta_total { get; set; }
            //Options profit and Loss
            public decimal options_pl { get; set; }
            //The account's available to withdrawal funds
            public decimal available_withdrawal_funds { get; set; }
            //The maintenance margin.
            public decimal maintenance_margin { get; set; }
            //The account's initial margin
            public decimal initial_margin { get; set; }
            //true when the inter-user transfers are enabled for user (available when parameter extended = true)
            public bool interuser_transfers_enabled { get; set; }
            //Futures session unrealized profit and Loss
            public decimal futures_session_upl { get; set; }
            //Options session realized profit and Loss
            public decimal options_session_rpl { get; set; }
            //The account's available funds
            public decimal available_funds { get; set; }
            //User email (available when parameter extended = true)
            public string email { get; set; }
            //Session unrealized profit and loss
            public decimal session_upl { get; set; }
            //Profit and loss
            public decimal total_pl { get; set; }
            //Options summary delta
            public decimal options_delta { get; set; }
            //The account's balance
            public decimal balance { get; set; }
            //Projected initial margin (for portfolio margining users)
            public decimal projected_initial_margin { get; set; }
            //The deposit address for the account (if available)
            public string deposit_address { get; set; }
        }
        public class AccountResult
        {
            public int id { get; set; }
            public string jsonrpc { get; set; }
            [JsonProperty("result")]
            public Account account { get; set; }
        }

        public class Portfolio
        {
            //The account's available funds
            public decimal available_funds { get; set; }
            //The account's available to withdrawal funds
            public decimal available_withdrawal_funds { get; set; }
            // The account's balance
            public decimal balance { get; set; }
            //The selected currency
            public string currency { get; set; }
            //The sum of position deltas
            public decimal delta_total { get; set; }
            //The account's current equity
            public decimal equity { get; set; }
            // Futures profit and Loss
            public decimal futures_pl { get; set; }
            // Futures session realized profit and Loss
            public decimal futures_session_rpl { get; set; }
            // Futures session unrealized profit and Loss
            public decimal futures_session_upl { get; set; }
            //  The account's initial margin
            public decimal initial_margin { get; set; }
            // The maintenance margin.
            public decimal maintenance_margin { get; set; }
            //  The account's margin balance
            public decimal margin_balance { get; set; }
            // Options summary delta
            public decimal options_delta { get; set; }
            //  Options summary gamma
            public decimal options_gamma { get; set; }
            // Options profit and Loss
            public decimal options_pl { get; set; }
            // Options session realized profit and Loss
            public decimal options_session_rpl { get; set; }
            //  number  Options session unrealized profit and Loss
            public decimal options_session_upl { get; set; }
            //  Options summary theta
            public decimal options_theta { get; set; }
            // Options summary vega
            public decimal options_value { get; set; }
            // When true portfolio margining is enabled for user
            public bool portfolio_margining_enabled { get; set; }
            //  The sum of position deltas without positions that will expire during closest expiration
            public decimal projected_delta_total { get; set; }

            // Projected initial margin
            public decimal projected_initial_margin { get; set; }
            // Projected maintenance margin
            public decimal projected_maintenance_margin { get; set; }
            // Session realized profit and loss
            public decimal session_rpl { get; set; }
            // Session unrealized profit and loss
            public decimal session_upl { get; set; }
            // Profit and loss
            public decimal total_pl { get; set; }
        }

        public class Candles
        {
            public decimal[] close { get; set; }
            public decimal[] high { get; set; }
            public decimal[] low { get; set; }
            public decimal[] open { get; set; }
            public string status { get; set; }
            public long[] ticks { get; set; }
            public decimal[] volume { get; set; }
        }
        public class CandleResult
        {
            public int id { get; set; }
            public string jsonrpc { get; set; }
            [JsonProperty("result")]
            public Candles candles { get; set; }
        }

        public class OrderData
        {
            //Type of notification: snapshot for initial, change for others. The field is only included in aggregated response (when input parameter interval != raw)
            public string jsonrpc { get; set; }
            //The timestamp of last change (seconds since the Unix epoch, with millisecond precision)
            public long timestamp { get; set; }
            //Unique instrument identifier
            public string instrument_name { get; set; }
            //Identifier of the previous notification (it's not included for the first notification)
            public long prev_change_id { get; set; }
            //Identifier of the notification
            public long change_id { get; set; }
            //array of [action, price, amount]	The first notification will contain the amounts for all price levels (a list of ["new", price, amount] tuples). All following notifications will contain a list of tuples with action, price level and new amount ([action, price, amount]). Action can be new, change or delete.
            public object[][] asks { get; set; }
            public object[][] bids { get; set; }
        }
        public class Greeks
        {
            // delta	number	(Only for option) The delta value for the option
            public decimal? delta { get; set; }
            //gamma	number	(Only for option) The gamma value for the option
            public decimal? gamma { get; set; }
            //rho	number	(Only for option) The rho value for the option
            public decimal? rho { get; set; }
            //theta	number	(Only for option) The theta value for the option
            public decimal? theta { get; set; }
            // vega	number	(Only for option) The vega value for the option
            public decimal? vega { get; set; }

        }
        public class Stats
        {
            // high	number	highest price during 24h
            public decimal? high { get; set; }
            // low	number	lowest price during 24h
            public decimal? low { get; set; }
            // volume	number	volume during last 24h in base currency
            public decimal? volume { get; set; }
        }


        public class TickResult
        {
            public int id { get; set; }
            public string jsonrpc { get; set; }
            [JsonProperty("result")]
            public Tick tick { get; set; }
        }

        public class Tick
        {
            //ask_iv	number	(Only for option) implied volatility for best ask
            public decimal? ask_iv { get; set; }
            //best_ask_amount	number	It represents the requested order size of all best asks
            public decimal? best_ask_amount { get; set; }
            //best_ask_price	number	The current best ask price, null if there aren't any asks
            public decimal? best_ask_price { get; set; }
            //best_bid_amount	number	It represents the requested order size of all best bids
            public decimal? best_bid_amount { get; set; }
            //best_bid_price	number	The current best bid price, null if there aren't any bids
            public decimal? best_bid_price { get; set; }
            //bid_iv	number	(Only for option) implied volatility for best bid
            public decimal? bid_iv { get; set; }
            //current_funding	number	Current funding (perpetual only)
            public decimal? current_funding { get; set; }
            //delivery_price	number	The settlement price for the instrument. Only when state = closed
            public decimal? delivery_price { get; set; }
            //funding_8h	number	Funding 8h (perpetual only)
            public decimal? funding_8h { get; set; }
            public Greeks greeks { get; set; }
            //index_price	number	Current index price
            public decimal? index_price { get; set; }
            //instrument_name string Unique instrument identifier
            public string instrument_name { get; set; }
            //interest_rate	number	Interest rate used in implied volatility calculations (options only)
            public decimal? interest_rate { get; set; }
            //last_price	number	The price for the last trade
            public decimal? last_price { get; set; }
            //mark_iv	number	(Only for option) implied volatility for mark price
            public decimal? mark_iv { get; set; }
            //mark_price	number	The mark price for the instrument
            public decimal? mark_price { get; set; }
            //max_price	number	The maximum price for the future. Any buy orders you submit higher than this price, will be clamped to this maximum.
            public decimal? max_price { get; set; }
            //min_price	number	The minimum price for the future. Any sell orders you submit lower than this price will be clamped to this minimum.
            public decimal? min_price { get; set; }
            //open_interest	number	The total amount of outstanding contracts in the corresponding amount units. For perpetual and futures the amount is in USD units, for options it is amount of corresponding cryptocurrency contracts, e.g., BTC or ETH.
            public decimal? open_interest { get; set; }
            //settlement_price	number	The settlement price for the instrument. Only when state = open
            public decimal? settlement_price { get; set; }
            //state	string	The state of the order book. Possible values are open and closed.
            public string state { get; set; }
            public Stats stats { get; set; }
            //timestamp	integer	The timestamp (seconds since the Unix epoch, with millisecond precision)
            public long timestamp { get; set; }
            //underlying_index	number	Name of the underlying future, or index_price (options only)
            public string underlying_index { get; set; }
            //underlying_price	number	Underlying price for implied volatility calculations (options only)
            public decimal? underlying_price { get; set; }
        }


        public class StopOrderHistoryResult
        {
            public int id { get; set; }
            public string jsonrpc { get; set; }

            [JsonProperty("result")]
            public StopOrderEntityInfo result { get; set; }
        }

        public class StopOrderEntityInfo
        {
            public string continuation { get; set; }
            public StopOrderEntity[] entries { get; set; }
        }

        public class StopOrderEntity
        {
            public decimal amount { get; set; }
            public string direction { get; set; }
            public string instrument_name { get; set; }
            public string label { get; set; }
            public long last_update_timestamp { get; set; }
            public string order_id { get; set; }
            public string order_state { get; set; }
            public string order_type { get; set; }
            public bool? post_only { get; set; }
            public decimal price { get; set; }
            public bool? reduce_only { get; set; }
            public string request { get; set; }
            public string stop_id { get; set; }
            public decimal stop_price { get; set; }
            public long timestamp { get; set; }
            public string trigger { get; set; }
        }


        public class AuthResponseMessage : BaseMessage
        {
            public string Status { get; set; }
        }

        public class Candle
        {
            public long Timestamp { get; set; }
            public decimal Open { get; set; }
            public decimal Close { get; set; }
            public decimal High { get; set; }
            public decimal Low { get; set; }
            public decimal Volume { get; set; }

            public Candle() { }

            public Candle(long msts, decimal close)
            {
                Timestamp = msts;
                Open = Close = High = Low = close;
                Volume = 0;
            }

            public Candle(object[] entries)
            {
                Timestamp = Convert.ToInt64(entries[0]);
                Open = Convert.ToDecimal(entries[1]);
                Close = Convert.ToDecimal(entries[2]);
                High = Convert.ToDecimal(entries[3]);
                Low = Convert.ToDecimal(entries[4]);
                Volume = Convert.ToDecimal(entries[5]);
            }
        }

        public class InstrumentResult
        {
            public int id { get; set; }
            public string jsonrpc { get; set; }
            [JsonProperty("result")]
            public Instrument[] instruments { get; set; }
        }

        public class Instrument
        {
            public string base_currency { get; set; }

            public decimal contract_size { get; set; }

            public long creation_timestamp { get; set; }

            public long expiration_timestamp { get; set; }

            public string instrument_name { get; set; }

            public bool is_active { get; set; }

            public string kind { get; set; }

            public decimal min_trade_amount { get; set; }

            public string option_type { get; set; }

            public string quote_currency { get; set; }

            public string settlement_period { get; set; }

            public decimal? strike { get; set; }

            public decimal tick_size { get; set; }
        }

        public class IndexTick
        {
            public string index_name { get; set; }
            public long timestamp { get; set; }
            public decimal price { get; set; }
        }

        public class BookOrder
        {
            public string action { get; set; }

            public decimal price { get; set; }

            public decimal amount { get; set; }
        }

        public class BookDepthLimitedData
        {
            public IList<BookOrder> asks { get; set; }

            public IList<BookOrder> bids { get; set; }

            public long change_id { get; set; }

            public string instrument_name { get; set; }

            public long prev_change_id { get; set; }

            public long timestamp { get; set; }

            public string type { get; set; }
        }

#pragma warning restore 1591
    }
}
