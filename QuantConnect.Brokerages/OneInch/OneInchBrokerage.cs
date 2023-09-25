using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Packets;
using QuantConnect.Securities;
using MySql.Data.MySqlClient;
using System.Threading.Tasks;
using HistoryRequest = QuantConnect.Data.HistoryRequest;
using QuantConnect.Configuration;
using System.Net;
using System.IO;
using System.Text;
using System.Net.Http;
using WebSocketSharp;
using Newtonsoft.Json;
using QuantConnect.Parameters;

namespace QuantConnect.Brokerages.OneInch;


[BrokerageFactory(typeof(OneInchBrokerageFactory))]
public class OneInchBrokerage : Brokerage, IDataQueueHandler
{
    private class BarData
    {
        public DateTime time;
        public decimal open;
        public decimal close;
        public decimal high;
        public decimal low;
    }

    private readonly IAlgorithm _algorithm;
    private volatile bool _isConnected;
    private readonly ConcurrentDictionary<string, Symbol> _symbols = new();
    private MySqlConnection local_db_main_net = new MySqlConnection("server=192.168.10.243;user=liangchen;database=one_inch;port=3306;password=Wlc12588888");
    private MySqlConnection local_db_goerli = new MySqlConnection("server=192.168.10.243;user=liangchen;database=one_inch;port=3306;password=Wlc12588888");
    private MySqlConnection local_db_arbitrum = new MySqlConnection("server=192.168.10.243;user=liangchen;database=one_inch;port=3306;password=Wlc12588888");
    private MySqlConnection history_db_main_net = new MySqlConnection("server=192.168.10.243;user=liangchen;database=one_inch;port=3306;password=Wlc12588888");
    private MySqlConnection history_db_goerli = new MySqlConnection("server=192.168.10.243;user=liangchen;database=one_inch;port=3306;password=Wlc12588888");
    private MySqlConnection history_db_arbitrum = new MySqlConnection("server=192.168.10.243;user=liangchen;database=one_inch;port=3306;password=Wlc12588888");
    private MySqlConnection coin_meta_db = new MySqlConnection("server=192.168.10.243;user=liangchen;database=twitter;port=3306;password=Wlc12588888");
    private MySqlConnection asset_allocation_db = new MySqlConnection("server=192.168.10.243;user=liangchen;database=twitter;port=3306;password=Wlc12588888");
    private string _importantMessageKey;
    private string goerli_importantMessageKey = "af25e7c112eaa90fcfaa044b826346ef5a5419768212e0cdc8f03fd6a296eae6";
    private string goerli_regular_message_key = "af25e7c112eaa90fcfaa044b826346ef5a5419768212e0cdc8f03fd6a296eae6";
    private string main_net_important_message_key = "a2ab9e6fcd10a76f8d862b561442be7a6abd9e9c38f838104f69118d77791320";
    private string main_net_regular_message_key = "6d70ffb8d33e9d5a0c8d417800876acc49093269a7d42d7bb35b987cd9eb54cc";
    private string RegularMessageKey;
    private string _importantMessageToken = "info";
    private Dictionary<string, int> pair2timestamp = new Dictionary<string, int>();
    private int global_earliest_timestamp = int.MaxValue;
    private int global_last_timestamp = int.MaxValue;
    private Dictionary<string, string> symbol2address = new Dictionary<string, string>();
    private Dictionary<string, int> symbol2timestamp = new Dictionary<string, int>();
    private Dictionary<string, int> symbol2decimal = new Dictionary<string, int>();
    private Dictionary<string, string> symbol2name = new Dictionary<string, string>();
    private string websocket_port = "";
    private string http_port = "";  
    private static WebSocket? clientWebSocket;
    private static readonly WebClient clientHttp = new WebClient();
    private static readonly HttpClient httpClient = new HttpClient();
    private string wallet_addr = Config.Get("wallet_addr");
    private string base_currency = Config.Get("base_currency");
    private int chain_id = Convert.ToInt32(Config.Get("chain_id")); 
    private string[] web3_currencies = new[] {"ETH", "USDC", "USDT"};
    private int max_splits = Convert.ToInt32(Config.Get("max_splits"));
    private List<string> tickers = new List<string>();
    private int sql_read_fail_times = 0;
    private List<Order> open_orders = new List<Order>();

    [Parameter("asset-allocation-config-file")]
    public string AssetAllocationConfigFile = string.Empty;

    public OneInchBrokerage(IAlgorithm algorithm)
        : base(nameof(OneInchBrokerage)) {
        _algorithm = algorithm;

        // 加一个请求拆单查询的模块，使用http请求http server，如果超时或者报错则默认不做拆单操作
        httpClient.Timeout = TimeSpan.FromMinutes(1);
        _algorithm.RequestSplitNum = (decimal tradeVolume, string coin_pair, decimal estimate_usd, decimal eth_price) => {
            string token = coin_pair.Replace(base_currency, "");
            string quote_symbol = coin_pair.Replace(base_currency, "");
            string quote_token_address = symbol2address[quote_symbol];
            string quote_token_name = symbol2name[quote_symbol];
            string quote_token_decimal = Convert.ToString(symbol2decimal[quote_symbol]);
            string quote_amount = Convert.ToString(tradeVolume);
            string max_splits_str = Convert.ToString(max_splits);
            string estimate_usd_str = Convert.ToString(estimate_usd);
            string eth_price_str = Convert.ToString(eth_price);
            Console.WriteLine("before query split order");
            try {
                var request = new HttpRequestMessage(HttpMethod.Get, $"http://192.168.10.243:{http_port}/?wallet_addr={wallet_addr}&request_type=query_split_num&quote_symbol={quote_symbol}&quote_token_address={quote_token_address}&quote_token_name={quote_token_name}&quote_token_decimal={quote_token_decimal}&quote_amount={quote_amount}&max_splits={max_splits_str}&base_currency={base_currency}&estimate_usd={estimate_usd_str}&eth_price={eth_price_str}");
                var response = httpClient.Send(request);
                response.EnsureSuccessStatusCode();
                var stream = response.Content.ReadAsStream();
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                string read_res = reader.ReadToEnd();
                Console.WriteLine("query split order response body:" + read_res);
                return Convert.ToInt32(read_res);
            } catch {
                return -1;
            }
        };

        // 目前1inch的brokerage只支持主网和arbitrum的交易
        switch (chain_id) {
            case 1:
                _importantMessageKey = main_net_important_message_key;
                RegularMessageKey = main_net_regular_message_key;
                break;
            case 42161:
                _importantMessageKey = main_net_important_message_key;
                RegularMessageKey = main_net_regular_message_key;
                break;
            default:
                _importantMessageKey = goerli_importantMessageKey;
                RegularMessageKey = goerli_regular_message_key;
                break;
        }

        _isConnected = false;

        // 连接数据库
        try{
            switch(chain_id) {
                case 1:
                    local_db_main_net.Open();
                    history_db_main_net.Open();
                    http_port = "16688";
                    websocket_port = "16666";
                    break;
                case 42161:
                    local_db_arbitrum.Open();
                    history_db_arbitrum.Open();
                    http_port = "16886";
                    websocket_port = "18668";
                    break;
                default:
                    local_db_goerli.Open();
                    history_db_goerli.Open();
                    http_port="18888";
                    websocket_port = "16888";
                    break;
            }
            
            asset_allocation_db.Open();

            clientWebSocket = new WebSocket($"ws://192.168.10.243:{websocket_port}");
            clientWebSocket.Connect();
            Console.WriteLine("websocket status" + clientWebSocket.ReadyState);
            clientWebSocket.OnMessage += WsOnMessage;
            coin_meta_db.Open();
            Console.WriteLine("open mysql success");
        } catch (Exception ex)  {
            Console.WriteLine(ex.ToString());
        }

        // 加载仓位数据库中已有的币种
        string coin_meta_sql = $"select * from coin_meta_data where chain_id={chain_id}";
        MySqlCommand coin_meta_cmd = new MySqlCommand(coin_meta_sql, this.coin_meta_db);
        MySqlDataReader coin_meta_reader = coin_meta_cmd.ExecuteReader();
        while(coin_meta_reader.Read()) {
            
            string? asset_symbol_str = Convert.ToString(coin_meta_reader[1]);
            if(asset_symbol_str != null) {
                tickers.Add(asset_symbol_str);
            }
            
            
            string? coin_symbol_str = Convert.ToString(coin_meta_reader[1]);
            string? contract_addr = Convert.ToString(coin_meta_reader[3]);
            int decimals = Convert.ToInt32(coin_meta_reader[4]);
            string? coin_name = Convert.ToString(coin_meta_reader[2]);
            if(coin_symbol_str != null) {
                if(contract_addr != null) {
                    symbol2address[coin_symbol_str] = contract_addr;
                }
                if(coin_name != null) {
                    symbol2name[coin_symbol_str] = coin_name;
                }
                symbol2decimal[coin_symbol_str] = decimals;
            }
        }

        // 根据标的资产配置文件订阅
        string asset_allocation_file = Config.Get("asset-allocation-config-file");
        string[] asset_allocations = System.IO.File.ReadAllLines(asset_allocation_file);
        for(int i = 1; i < asset_allocations.Count(); i++) {
            string[] split_res = asset_allocations[i].Split(",");
            string ticker = split_res[0];
            string asset_symbol_str = ticker.Replace("USDC", "").Replace("ETH", "").Replace("USDT", "");
            // Console.WriteLine("in get cash balance:" + asset_symbol_str);
            Symbol ass_symbol;
            
            if (!SymbolCache.TryGetSymbol(ticker, out ass_symbol)) {
                var new_add_symbol = Symbol.Create(ticker, SecurityType.Crypto, Market.OneInch, ticker);
                new_add_symbol.SymbolProperties = new SymbolProperties(
                    ticker.Replace("USDC", "").Replace("USDT", "").Replace("ETH", ""),
                    base_currency,
                    1,
                    0.000001m,
                    0.000001m);
                SymbolCache.Set(ticker, new_add_symbol);
                SymbolPropertiesDatabase.FromDataFolder().Add(new_add_symbol);
                tickers.Add(ticker.Replace("USDC", "").Replace("USDT", "").Replace("ETH", ""));
            }
            SymbolCache.TryGetSymbol(ticker, out ass_symbol);
            _symbols.TryAdd(ass_symbol.Value, ass_symbol);
        }

        Console.WriteLine("in brokerage initialize after add coin symbol");

        coin_meta_reader.Close();
        coin_meta_db.Close();
    }


    private void WsOnMessage(object? sender, MessageEventArgs e) {
        if(e.IsPing) {
            // 证明链接还在，无需做任何事情
            Console.WriteLine("connection still online!");
            return;
        }
        if(e.IsText) {
            string message = e.Data;
            // Console.WriteLine("edata type:" + message.GetType());
            Console.WriteLine("received data from the server:" + message);
            
            var receivedMessage = JsonConvert.DeserializeObject<WebsocketReceipt>(message);
            
            if (receivedMessage is not null) {
                Console.WriteLine("received receipt from server:" + receivedMessage.token_symbol);
                Console.WriteLine("received order id from server:" + receivedMessage.order_id);

                if (receivedMessage.status == 1) {
                    List<Order> removedOrders = new List<Order>();
                    foreach (var order in open_orders) { 
                        if (order.id == Convert.ToInt64(receivedMessage.order_id)) {
                            removedOrders.Add(order);
                            order.status = OrderStatus.Filled;
                            SendOrderTrade(order, receivedMessage.price, receivedMessage.quote_amount, "ETH",
                            receivedMessage.commission, DateTime.UtcNow);
                            Console.WriteLine("transaction finished");
                        }
                    }
                } else {
                    foreach (var order in open_orders) {
                        if(order.id == Convert.ToInt64(receivedMessage.order_id)) {
                            // open_orders.Remove(order);
                            order.status = OrderStatus.Canceled;
                            SendOrderUpdate(order, OrderStatus.Canceled, DateTime.UtcNow);
                        }
                    }
                }
            } else {
                Console.WriteLine("convert data fail");
            }
        }
        return;
    }

    private static void WsOnError(object sender, WebSocketSharp.ErrorEventArgs e) {
        Console.WriteLine("received error from the server:");
        return;
    }

    public override bool IsConnected => _isConnected;

    /// <summary>
    /// 获取未完成的订单
    /// </summary>
    /// <returns></returns>
    public override List<Order> GetOpenOrders()
    {
        return open_orders;
    }

    /// <summary>
    /// 获取账户持仓信息
    /// </summary>
    /// <returns></returns>
    public override List<Holding> GetAccountHoldings() {
        return new List<Holding>();
    }

    public void SqlHealthCheck() {
        // Console.WriteLine("in sql health check");
        switch (chain_id) {
            case 1:
                if(this.local_db_main_net.Ping() == false) {
                    Console.WriteLine("sql health check main net local db connection closed, reconnecting");
                    this.local_db_main_net.Open();
                }
                break;
            case 42161:
                if(this.local_db_arbitrum.Ping() == false) {
                    Console.WriteLine("sql health check arbitrum local db connection closed, reconnecting");
                    this.local_db_arbitrum.Open();
                }
                break;
            default:
                if(this.local_db_goerli.Ping() == false) {
                    Console.WriteLine("sql health check goerli sql connection closed, reconnecting");
                    this.local_db_goerli.Open();
                }
                break;
        }
    }

    private void CloseHistoryDBConnection() {
        switch(chain_id) {
            case 1:
                if(history_db_main_net.Ping()) {
                    history_db_main_net.Clone();
                }
                break;
            case 42161:
                if(history_db_arbitrum.Ping()) {
                    history_db_arbitrum.Close();
                }
                break;
            default:
                // 默认是测试网的连接
                if(history_db_goerli.Ping()) {
                    history_db_goerli.Close();
                }
                break;
        }
    }

    private IList<BarData> GetHistoryBar(Symbol symbol, DateTime startUtc, DateTime endUtc, Resolution resolution) {   
        Console.WriteLine("in get history bar");
        Console.WriteLine("request resolution:" + resolution);
        
        if ((endUtc - startUtc).TotalSeconds <= 10) {
            // string conversion_rate_sql = $"select ts, price, amount from price_oracle where concat(quote_token_identifier, base_token_identifier)=\'{symbol.value}\' order by ts desc limit 1;";
            string conversion_rate_sql = $"select ts, quote_amount, base_amount from price_oracle_history where concat(quote_token_identifier, base_token_identifier) = \"{symbol.value}\" order by ts desc limit 1;";
            Console.WriteLine("conversion rate sql:" + conversion_rate_sql);
            Console.WriteLine("history total seconds:" + (endUtc - startUtc).TotalSeconds);
            MySqlCommand cmd;
            switch(chain_id) {
                case 1:
                    cmd = new MySqlCommand(conversion_rate_sql, this.history_db_main_net);
                    break;
                case 42161:
                    cmd = new MySqlCommand(conversion_rate_sql, this.history_db_arbitrum);
                    break;
                default:
                    cmd = new MySqlCommand(conversion_rate_sql, this.history_db_goerli);
                    break;
            }
            
            
            MySqlDataReader dataReader = cmd.ExecuteReader();
            var ret = new List<BarData>();
            while(dataReader.Read()) {
                var tick = new Tick();
                var bar_data = new BarData();
                decimal in_token_amount = 1m;
                if (!dataReader[2].Equals(System.DBNull.Value)) {
                    in_token_amount = Convert.ToDecimal(dataReader[2]);
                }
                bar_data.open = Decimal.Divide(in_token_amount, Convert.ToDecimal(dataReader[1]));
                bar_data.close = Decimal.Divide(in_token_amount, Convert.ToDecimal(dataReader[1]));
                bar_data.high = Decimal.Divide(in_token_amount, Convert.ToDecimal(dataReader[1]));
                bar_data.low = Decimal.Divide(in_token_amount, Convert.ToDecimal(dataReader[1]));
                bar_data.time = startUtc;
                ret.Add(bar_data);
                // Console.WriteLine("history bar time:" + bar_data.time);
                if(this.pair2timestamp.GetValueOrDefault(symbol.value, 0) == 0) {
                    // if (!this.pair2timestamp.ContainsKey(pair_name) || this.pair2timestamp.GetValueOrDefault(pair_name, 0) == 0 || this.pair2timestamp[pair_name] < ts_from_db) {
                    this.pair2timestamp[symbol.value] = Convert.ToInt32((startUtc - new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalSeconds);
                }
            }
            dataReader.Close();
            Console.WriteLine("history total ret size:" + ret.Count);
            return ret;
        } else {
            string quote_currency = symbol.value.Replace(base_currency, "");
            int start_timestamp = Convert.ToInt32((startUtc - new DateTime(1970, 1,1,0,0,0,0,DateTimeKind.Utc)).TotalSeconds);
            int end_timestamp = Convert.ToInt32((endUtc - new DateTime(1970,1,1,0,0,0,0, DateTimeKind.Utc)).TotalSeconds);
            // string history_sql = $"select avg(price) as avg_price, ts, amount, minutes, minutes * 60 as minute_timestamp, max(price) as high_price, min(price) as low_price, max(price) as open_price, min(price) as close_price, max(ts) as minute_max_ts from (select price, ts, amount, ts div 60 as minutes from price_oracle where quote_token_identifier=\'{quote_currency}\' and base_token_identifier=\'{base_currency}\' and ts >= {start_timestamp} and ts <= {end_timestamp})t group by minutes;";
            string history_sql = $"select avg(quote_amount) as avg_price, ts, base_amount, minutes, minutes * 60 as minute_timestamp, max(quote_amount) as high_price, min(quote_amount) as low_price, max(quote_amount) as open_price, min(quote_amount) as close_price, max(ts) as minute_max_ts from (select quote_amount, ts, base_amount, ts div 60 as minutes from price_oracle_history where quote_token_identifier =\"{quote_currency}\" and base_token_identifier=\"{base_currency}\" and ts >= {start_timestamp} and ts <= {end_timestamp})t group by minutes;";
            Console.WriteLine("history sql:" + history_sql);

            MySqlCommand cmd;
            switch(chain_id) {
                case 1:
                    cmd = new MySqlCommand(history_sql, this.history_db_main_net);
                    break;
                case 42161:
                    cmd = new MySqlCommand(history_sql, this.history_db_arbitrum);
                    break;
                default:
                    cmd = new MySqlCommand(history_sql, this.history_db_goerli);
                    break;
            }
            

            MySqlDataReader dataReader = cmd.ExecuteReader();
            var ret = new List<BarData>();
            while (dataReader.Read()) {
                var tick = new Tick();
                var bar_data = new BarData();
                
                string pair_name = dataReader[0] + "" + dataReader[1];

                decimal in_token_amount = 1m;

                if (!dataReader[2].Equals(System.DBNull.Value)) {
                    in_token_amount = Convert.ToDecimal(dataReader[2]);
                }
                
                bar_data.open = Decimal.Divide(in_token_amount, Convert.ToDecimal(dataReader[5]));
                bar_data.close = Decimal.Divide(in_token_amount, Convert.ToDecimal(dataReader[6]));
                bar_data.high = Decimal.Divide(in_token_amount, Convert.ToDecimal(dataReader[7]));
                bar_data.low = Decimal.Divide(in_token_amount, Convert.ToDecimal(dataReader[8]));
                // Console.WriteLine("data reader 4:" + dataReader[4]);
                
                bar_data.time = new DateTime(1970, 1, 1, 0, 0, 0,0, DateTimeKind.Utc).AddSeconds(Convert.ToDouble(dataReader[4])).ToLocalTime();
                // Console.WriteLine("bar data time:" + bar_data.time);
                ret.Add(bar_data);

                if(this.pair2timestamp.GetValueOrDefault(symbol.value, 0) == 0 || this.pair2timestamp.GetValueOrDefault(symbol.value) < Convert.ToInt32(dataReader[9])) {
                    // if (!this.pair2timestamp.ContainsKey(pair_name) || this.pair2timestamp.GetValueOrDefault(pair_name, 0) == 0 || this.pair2timestamp[pair_name] < ts_from_db) {
                    this.pair2timestamp[symbol.value] = Convert.ToInt32(dataReader[9]);
                }
            }

            dataReader.Close();
            Thread.Sleep(888);
            return ret;
        }
    }

    public override IEnumerable<BaseData> GetHistory(HistoryRequest request) {   
        Console.WriteLine("in get history data:" + request.Symbol.value);
        var bars = GetHistoryBar(request.Symbol, request.StartTimeUtc, request.EndTimeUtc, Resolution.Minute);
        Console.WriteLine("history start:" + request.StartTimeUtc);
        Console.WriteLine("history end:" + request.EndTimeUtc);
        Console.WriteLine("history resolution:" + request.Resolution);
        foreach (var bar in bars) {
            var tick = new Tick();
            Console.WriteLine("security type:" + request.Symbol.SecurityType);
            tick.TickType = request.Symbol.SecurityType is SecurityType.Future or SecurityType.Option 
                ? TickType.Quote 
                : TickType.Trade;
            tick.Symbol = request.Symbol;
            tick.Value = bar.close;
            tick.Quantity = 0;
            tick.BidPrice = tick.Value;
            tick.BidSize = tick.Quantity;
            tick.AskPrice = tick.Value;
            tick.AskSize = tick.Quantity;
            tick.Time = bar.time;
            yield return tick;
        }
    }

    private Security? GetConversionRateSecurity(Symbol symbol) {
        return _algorithm!.AddSecurity( 
            symbol.SecurityType,
            symbol.Value,
            Resolution.Tick,
            symbol.ID.Market,
            true,
            0m,
            false,
            symbol.SymbolProperties);
    }

    /// <summary>
    /// 获取账户资金
    /// </summary>
    /// <returns></returns>
    public override List<CashAmount> GetCashBalance() {
        // 这里需要查询链上所有的持仓币种余额
        // 基础货币包括eth、usdc、usdt
        // 额外的erc20的币需要根据配置文件的情况查询加载

        var list = new List<CashAmount>();
        foreach(var symbol in _symbols.Values.ToArray()) {
            Console.WriteLine("symbol value" + symbol.value.Replace(base_currency, ""));
            Console.WriteLine("symbol2address value:" + symbol2address.GetValueOrDefault(symbol.value.Replace(base_currency, ""), ""));
            var ticker_addr = symbol2address.GetValueOrDefault(symbol.value.Replace(base_currency, ""), "");
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://192.168.10.243:{http_port}/?wallet_addr={wallet_addr}&erc20_contract={ticker_addr}");
            var response = httpClient.Send(request);
            response.EnsureSuccessStatusCode();
            var stream = response.Content.ReadAsStream();
            StreamReader reader = new StreamReader(stream, Encoding.UTF8);
            string read_res = reader.ReadToEnd();
            Console.WriteLine("query cash balance response body:" + read_res);
            // Console.WriteLine("response from http server:" + response);
            var cash = new CashAmount(Convert.ToDecimal(read_res), symbol.value.Replace(base_currency, ""), true, GetConversionRateSecurity(SymbolCache.GetSymbol($"{symbol.value}")));
            list.Add(cash);
            Console.WriteLine($"coin {symbol.value} balance:" + cash.Amount);
        }

        // 把钱包里的eth和稳定币加载到qc中
        foreach (var ticker in web3_currencies) {
            if (ticker.ToLower() == "usdc" || ticker.ToLower() == "usdt") {
                var ticker_addr = symbol2address.GetValueOrDefault(ticker, "");
                var response = clientHttp.DownloadString($"http://192.168.10.243:{http_port}/?wallet_addr={wallet_addr}&erc20_contract={ticker_addr}");
                var cash = new CashAmount(Convert.ToDecimal(response), ticker, true);
                Console.WriteLine("balance request url:" + $"http://192.168.10.243:{http_port}/?wallet_addr={wallet_addr}&erc20_contract={ticker_addr}");
                Console.WriteLine($"initial {ticker} balance:" + cash.Amount);
                list.Add(cash);
            } else {
                var ticker_addr = symbol2address.GetValueOrDefault(ticker, "");
                var response = clientHttp.DownloadString($"http://192.168.10.243:{http_port}/?wallet_addr={wallet_addr}&erc20_contract={ticker_addr}");
                Console.WriteLine("balance request url:" + $"http://192.168.10.243:{http_port}/?wallet_addr={wallet_addr}&erc20_contract={ticker_addr}");
                var cash = new CashAmount(Convert.ToDecimal(response), ticker, true, GetConversionRateSecurity(SymbolCache.GetSymbol($"{ticker}USDC")));
                Console.WriteLine($"initial {ticker} balance:" + cash.Amount);
                list.Add(cash);
            }
        }

        return list;
    }

    public override bool PlaceOrder(Order order) {

        // throw new NotImplementedException();

        Console.WriteLine("in brokerage order");

        Console.WriteLine("ordered quote currency" + order.Symbol.SymbolProperties.Description);
        
        decimal buy_quantity = order.quantity;
        string token = order.Symbol.SymbolProperties.Description;
        int decimals = symbol2decimal[token];
        string name = symbol2name[token];
        decimal estimate_usd = Convert.ToDecimal(order.tag);
        OneInchOrder oneInchOrder = new OneInchOrder{
            instruction_type = "order", 
            quote_token = token, 
            quote_amount = buy_quantity, 
            slip_rate = 0.5m,
            wallet_address = wallet_addr, 
            quote_token_address = symbol2address[token], 
            quote_token_symbol = token, 
            quote_token_decimal = decimals, 
            quote_token_name = name,
            base_currency = base_currency,
            chain_id = chain_id,
            order_id = Convert.ToString(order.id),
            estimate_usd = estimate_usd
        };

        var json_order = Newtonsoft.Json.JsonConvert.SerializeObject(oneInchOrder);
        Console.WriteLine("before json order:" + oneInchOrder);
        Console.WriteLine("json order:" + json_order);

        // 发送给js的websocket服务器，等待异步回调响应
        if(clientWebSocket != null) {
            clientWebSocket.Send(json_order);
            open_orders.Add(order);
            SendOrderUpdate(order, OrderStatus.Submitted, DateTime.UtcNow);
            return true;
        } else {
            _algorithm.Notify.MomDingDing("fail to send order to websocket server", _importantMessageKey, _importantMessageToken);
            return false;
        }
        
    }

    private void SendOrderUpdate(Order order, OrderStatus status, DateTime utcUpdateTime, string message = "")
    {
        var orderEvent = new OrderEvent(order, utcUpdateTime, OrderFee.Zero, message)
        {
            Status = status
        };
        OnOrderEvent(orderEvent);
    }

    private void SendOrderTrade(Order order, decimal price, decimal qty, string? commissionAsset, decimal commission, DateTime utcUpdateTime, string message = "") {
        var orderEvent = new OrderEvent(
            order,
            utcUpdateTime,
            commissionAsset != null ? new OrderFee(new CashAmount(commission, commissionAsset)) : OrderFee.Zero,
            message)
        {
            FillPrice = price,
            FillQuantity = qty
        };
        OnOrderEvent(orderEvent);
    }

    public override bool UpdateOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public override bool CancelOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public override void Connect() {
        Console.WriteLine("brokerage connect");
        AddSymbol();
        _isConnected = true;
    }

    private void AddSymbol() {
        // 需要订阅的是，ethusdc的交易对，用于cashbook的计算
        var eth_symbol = Symbol.Create("ETHUSDC", SecurityType.Crypto, Market.OneInch, "ETHUSDC");
        eth_symbol.SymbolProperties = new SymbolProperties("ETH", "USDC", 1, 0.000001m, 0.000001m);
        SymbolCache.Set(eth_symbol.Value, eth_symbol);
        SymbolPropertiesDatabase.FromDataFolder().Add(eth_symbol);
        Console.WriteLine("in brokerage add symbol");
        // 把要做的erc20的币的行情订阅加载进来
        foreach (var ticker in tickers) {
            var symbol = Symbol.Create(ticker + base_currency, SecurityType.Crypto, Market.OneInch, ticker + base_currency);
            symbol.SymbolProperties = new SymbolProperties(ticker, base_currency, 1, 0.000001m, 0.000001m);
            SymbolCache.Set(symbol.Value, symbol);
            SymbolPropertiesDatabase.FromDataFolder().Add(symbol);
        }
    }

    public override void Disconnect() {
        _isConnected = false;
        if(clientWebSocket != null) {
            clientWebSocket.Close();
        }
    }

    public IEnumerable<BaseData> GetNextTicks() {

        var rand = new Random();
        if(_isConnected == false) {
            Console.WriteLine("brokerage is false in get next ticks");
        }

        while (_isConnected) {
            // 先更新最新的timestamp
            // this.updateGlobalEarliestTimestamp();
            
            string sql = "";
            
            List<string> subscribe_pairs = new List<string>();

            foreach (var symbol in _symbols.Values.ToArray()) {
                subscribe_pairs.Add(symbol.value);
            }

            string subscribe_pairs_str = string.Join("\",\"", subscribe_pairs);
            
            int current_timestamp = Convert.ToInt32((DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds);
            // 不区分global last timestamp，但是如果某个行情不动就会不推行情了，这个可以通过一个单独的检查来实现
            sql = $"select quote_token_identifier, base_token_identifier, ts, quote_amount, base_amount from price_oracle_realtime where concat(quote_token_identifier, base_token_identifier) in (\"{subscribe_pairs_str}\")";

            global_last_timestamp = current_timestamp;
            if(global_last_timestamp % 100 == 1) {
                Console.WriteLine("global_last_timestamp:" + global_last_timestamp);
            }
            
            
            // Console.WriteLine("this sql:" + sql);

            // 防止sql连接断开，如果断开了则重新连接
            SqlHealthCheck();

            MySqlCommand cmd = new MySqlCommand();

            switch(chain_id) {
                case 1:
                    try{
                        cmd = new MySqlCommand(sql, this.local_db_main_net);
                    }catch(Exception e) {
                        Console.WriteLine(e.Message);
                        Console.WriteLine("create main net mysql command instance failed");
                    }
                    break;
                case 42161:
                    try{
                        cmd = new MySqlCommand(sql, this.local_db_arbitrum);
                    } catch(Exception e) {
                        Console.WriteLine(e.Message);
                        Console.WriteLine("create arbitrum mysql command instance failed");
                    }
                    break;
                default:
                    try {
                        cmd = new MySqlCommand(sql, this.local_db_goerli);
                    } catch (Exception e) {
                        Console.WriteLine(e.Message);
                        Console.WriteLine("create goerli mysql command instance failed");
                    }
                    break;
            }

            MySqlDataReader dataReader;

            try{
                dataReader = cmd.ExecuteReader();
            } catch (Exception e) {
                Console.WriteLine(e.Message);
                Thread.Sleep(1000);
                sql_read_fail_times += 1;
                if(sql_read_fail_times >= 60) {
                    _algorithm.Notify.MomDingDing("requiring price from database fail times more than 60", _importantMessageKey, _importantMessageToken);
                    sql_read_fail_times = 0;
                }
                continue;
            }

            while (dataReader.Read()) {
                // Console.WriteLine("sql returned:" + dataReader[0] + "," + dataReader[1] + "," + dataReader[2] + "," + dataReader[3] + "," + dataReader[4]);
                var tick = new Tick();
                try{
                    string pair_name = dataReader[0] + "" + dataReader[1];
                    int insert_timestamp = Convert.ToInt32(dataReader[2]);
                    
                    if(!symbol2timestamp.ContainsKey(pair_name)) {
                        symbol2timestamp[pair_name] = insert_timestamp;
                    } else {
                        int last_timestamp = symbol2timestamp[pair_name];
                        if(last_timestamp == insert_timestamp) {
                            continue;
                        } else {
                            Console.WriteLine("get new insert data from price oracle!");
                            symbol2timestamp[pair_name] = insert_timestamp;
                        }
                    }
                    decimal in_token_amount = 1m;
                    if (!dataReader[4].Equals(System.DBNull.Value)) {
                        in_token_amount = Convert.ToDecimal(dataReader[4]);
                    }
                    // SymbolCache.GetSymbol(dataReader[0] + "" + dataReader[1]);
                    Symbol tmp;
                    SymbolCache.TryGetSymbol(pair_name, out tmp);
                    // tick.Symbol = dataReader[0] + "" + dataReader[1];
                    tick.Symbol = tmp;
                    tick.TickType = TickType.Trade;
                    tick.Value = Decimal.Divide(in_token_amount, Convert.ToDecimal(dataReader[3]));
                    tick.Volume = (decimal) 1;
                    tick.Time = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(Convert.ToDouble(dataReader[2])).ToLocalTime();
                } catch(Exception e) {
                    Console.WriteLine(e.Message);
                }
                yield return tick;
            }

            dataReader.Close();
            Thread.Sleep(1000);
        }
    }
  
    public static bool SupportSymbol(Symbol symbol) { 
        if (symbol.Value.ToLower().IndexOf("universe", StringComparison.Ordinal) != -1)
            return false;

        var market = symbol.ID.Market;
        return market is Market.OneInch;
    }

    public void Subscribe(LiveNodePacket job, IEnumerable<Symbol> symbols) {
        Console.WriteLine("in subscribe");
        foreach (var symbol in symbols) {
            if (symbol.IsCanonical()) {
                return;
            }
            Console.WriteLine("in brokerage subscribe:" + symbol.value);
            if (!SupportSymbol(symbol)) {
                var new_add_symbol = Symbol.Create(symbol.value, SecurityType.Crypto, Market.OneInch, symbol.value);
                new_add_symbol.SymbolProperties = new SymbolProperties(
                    symbol.value.Replace("USDC", "").Replace("USDT", "").Replace("ETH", ""),
                    base_currency,
                    1,
                    0.000001m,
                    0.000001m);
                SymbolCache.Set(new_add_symbol.Value, new_add_symbol);
                SymbolPropertiesDatabase.FromDataFolder().Add(new_add_symbol);
                tickers.Add(symbol.value.Replace("USDC", "").Replace("USDT", "").Replace("ETH", ""));
            }
            
            _symbols.TryAdd(symbol.Value, symbol);
            Console.WriteLine("in broker subscribe:" + symbol.Value);
        }
    }

    private void updateGlobalEarliestTimestamp() {
        
        foreach(KeyValuePair<string, int> kvp in this.pair2timestamp) {
            if (kvp.Value == 0) {
                continue;
            } else {
                if (kvp.Value < this.global_earliest_timestamp) {
                    this.global_earliest_timestamp = kvp.Value;
                }
            }
        }
        // Console.WriteLine("in update global earliest timestamp:" + this.global_earliest_timestamp);
    }

    public void Unsubscribe(LiveNodePacket job, IEnumerable<Symbol> symbols)
    {
        foreach (var symbol in symbols)
        {
            _symbols.TryRemove(symbol.Value, out _);
            this.pair2timestamp.Remove(symbol.Value);
        }
    }
}