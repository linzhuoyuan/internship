using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TheOne.Deribit
{
    public static class DeribitEnums
    {
        public static class OrderType
        {
            public static string limit = "limit";
            public static string market = "market";
            public static string stop_limit = "stop_limit";
            public static string stop_market = "stop_market";
        }

        public static class OrderTriggerType
        {
            public static string index_price = "index_price";
            public static string mark_price = "mark_price";
            public static string last_price = "last_price";
        }

        public static class InstrumentKind
        {
            public static string any = "any";
            public static string future = "future";
            public static string option = "option";
        }

        public static class OrderState
        {
            public static string open = "open";
            public static string filled = "filled";
            public static string rejected = "rejected";
            public static string cancelled = "cancelled";
            public static string untriggered = "untriggered";
            public static string triggered = "triggered";
            public static string closed = "closed";
        }

        public static class BuySell
        {
            public static string buy = "buy";
            public static string sell = "sell";
        }

        public static class CurrencyCode
        {
            public static string any = "any";
            public static string BTC = "BTC";
            public static string ETH = "ETH";
        }

        public static class BookOrderAction
        {
            public static string @new = "new";
            public static string change = "change";
            public static string delete = "delete";
        }

    }
}