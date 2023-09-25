using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using RestSharp;
using System;
using System.Timers;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using NLog;
using QuantConnect.Logging;

namespace TheOne.Deribit
{
    public class DeribitRestApi
    {
        /// <summary>
        /// Select the Content-Type header's value from the given content-type array:
        /// if JSON type exists in the given array, use it;
        /// otherwise use the first one defined in 'consumes'
        /// </summary>
        /// <param name="contentTypes">The Content-Type array to select from.</param>
        /// <returns>The Content-Type header to use.</returns>
        ///
        public static String SelectHeaderContentType(String[] contentTypes)
        {
            if (contentTypes.Length == 0)
                return "application/json";

            foreach (var contentType in contentTypes)
            {
                if (IsJsonMime(contentType.ToLower()))
                    return contentType;
            }

            return contentTypes[0]; // use the first content type specified in 'consumes'
        }

        /// <summary>
        ///Check if the given MIME is a JSON MIME.
        ///JSON MIME examples:
        ///    application/json
        ///    application/json; charset=UTF8
        ///    APPLICATION/JSON
        ///    application/vnd.company+json
        /// </summary>
        /// <param name="mime">MIME</param>
        /// <returns>Returns True if MIME type is json.</returns>
        public static bool IsJsonMime(String mime)
        {
            var jsonRegex = new Regex("(?i)^(application/json|[^;/ \t]+/[^;/ \t]+[+]json)[ \t]*(;.*)?$");
            return mime != null && (jsonRegex.IsMatch(mime) || mime.Equals("application/json-patch+json"));
        }

        public static RestRequest PrepareRequest(
            String path, RestSharp.Method method, List<KeyValuePair<String, String>> queryParams, Object postBody,
            Dictionary<String, String> headerParams, Dictionary<String, String> formParams,
            Dictionary<String, FileParameter> fileParams, Dictionary<String, String> pathParams,
            String contentType)
        {
            var request = new RestRequest(path, method);

            // add path parameter, if any
            foreach (var param in pathParams)
                request.AddParameter(param.Key, param.Value, ParameterType.UrlSegment);

            // add header parameter, if any
            foreach (var param in headerParams)
                request.AddHeader(param.Key, param.Value);

            // add query parameter, if any
            foreach (var param in queryParams)
                request.AddQueryParameter(param.Key, param.Value);

            // add form parameter, if any
            foreach (var param in formParams)
                request.AddParameter(param.Key, param.Value);

            //// add file parameter, if any
            //foreach (var param in fileParams)
            //{
            //    request.AddFile(param.Value.Name, param.Value.Writer, param.Value.FileName, param.Value.ContentType);
            //}

            if (postBody != null) // http body (model or byte[]) parameter
            {
                request.AddParameter(contentType, postBody, ParameterType.RequestBody);
            }

            return request;
        }

        /// <summary>
        /// Select the Accept header's value from the given accepts array:
        /// if JSON exists in the given array, use it;
        /// otherwise use all of them (joining into a string)
        /// </summary>
        /// <param name="accepts">The accepts array to select from.</param>
        /// <returns>The Accept header to use.</returns>
        public static String SelectHeaderAccept(String[] accepts)
        {
            if (accepts.Length == 0)
                return null;

            if (accepts.Contains("application/json", StringComparer.OrdinalIgnoreCase))
                return "application/json";

            return String.Join(",", accepts);
        }

        /// <summary>
        /// If parameter is DateTime, output in a formatted string (default ISO 8601), customizable with Configuration.DateTime.
        /// If parameter is a list, join the list with ",".
        /// Otherwise just return the string.
        /// </summary>
        /// <param name="obj">The parameter (header, path, query, form).</param>
        /// <returns>Formatted string.</returns>
        public static string ParameterToString(object obj)
        {
            if (obj is DateTime)
                // Return a formatted date string - Can be customized with Configuration.DateTimeFormat
                // Defaults to an ISO 8601, using the known as a Round-trip date/time pattern ("o")
                // https://msdn.microsoft.com/en-us/library/az4se3k1(v=vs.110).aspx#Anchor_8
                // For example: 2009-06-15T13:45:30.0000000
                return ((DateTime)obj).ToString("o");
            else if (obj is DateTimeOffset)
                // Return a formatted date string - Can be customized with Configuration.DateTimeFormat
                // Defaults to an ISO 8601, using the known as a Round-trip date/time pattern ("o")
                // https://msdn.microsoft.com/en-us/library/az4se3k1(v=vs.110).aspx#Anchor_8
                // For example: 2009-06-15T13:45:30.0000000
                return ((DateTimeOffset)obj).ToString("o");
            else if (obj is IList)
            {
                var flattenedString = new StringBuilder();
                foreach (var param in (IList)obj)
                {
                    if (flattenedString.Length > 0)
                        flattenedString.Append(",");
                    flattenedString.Append(param);
                }
                return flattenedString.ToString();
            }
            else
                return Convert.ToString(obj);
        }

        /// <summary>
        /// Convert params to key/value pairs. 
        /// Use collectionFormat to properly format lists and collections.
        /// </summary>
        /// <param name="name">Key name.</param>
        /// <param name="value">Value object.</param>
        /// <returns>A list of KeyValuePairs</returns>
        public static IEnumerable<KeyValuePair<string, string>> ParameterToKeyValuePairs(string collectionFormat, string name, object value)
        {
            var parameters = new List<KeyValuePair<string, string>>();

            if (IsCollection(value) && collectionFormat == "multi")
            {
                var valueCollection = value as IEnumerable;
                parameters.AddRange(from object item in valueCollection select new KeyValuePair<string, string>(name, ParameterToString(item)));
            }
            else
            {
                parameters.Add(new KeyValuePair<string, string>(name, ParameterToString(value)));
            }

            return parameters;
        }

        public static string Base64Encode(string text)
        {
            return System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));
        }

        /// <summary>
        /// Check if generic object is a collection.
        /// </summary>
        /// <param name="value"></param>
        /// <returns>True if object is a collection type</returns>
        public static bool IsCollection(object value)
        {
            return value is IList || value is ICollection;
        }

        protected RestClient RestClient;
        private string _apiKey;
        private string _apiSecret;
        private Logger _logger;
        private string _restUrl;
        public RateGate _restRateLimiter = new RateGate(20, TimeSpan.FromSeconds(1));
        public DeribitRestApi(string restUrl, string apiKey, string apiSecret, Logger logger = null)
        {
            if (logger == null)
            {
                _logger = NLog.LogManager.GetLogger("DeribitRestApi");
            }
            else
            {
                _logger = logger;
            }

            _restUrl = restUrl;
            _apiKey = apiKey;
            _apiSecret = apiSecret;
            RestClient =new RestClient(restUrl);
        }

        /// <summary>
        /// If an IP address exceeds a certain number of requests per minute
        /// the 429 status code and JSON response {"error": "ERR_RATE_LIMIT"} will be returned
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        public RestResponse ExecuteRestRequest(RestRequest request)
        {
            const int maxAttempts = 10;
            var attempts = 0;
            RestResponse response;

            do
            {
                if (!_restRateLimiter.WaitToProceed(TimeSpan.Zero))
                {
                    var msg =
                        "RateLimit:The API request has been rate limited. To avoid this message, please reduce the frequency of API calls.";
                    _logger.Warn(msg);
                    Log.Trace(msg);
                    _restRateLimiter.WaitToProceed();
                }

                response = RestClient.ExecuteAsync(request).Result;
                // 429 status code: Too Many Requests
            } while (++attempts < maxAttempts && (int)response.StatusCode == 429);

            return response;
        }

        public List<DeribitMessages.Position> GetPositions(string currency)
        {
            var localVarPath = "/private/get_positions";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "currency", currency)); // query parameter
            //all position
            //localVarQueryParams.AddRange(this.ParameterToKeyValuePairs("", "kind", "option")); // query parameter

            // authentication (bearerAuth) required
            // http basic authentication required
            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey + ":" + _apiSecret);

            var request = PrepareRequest(localVarPath,
               Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
               localVarPathParams, localVarHttpContentType);

            var response = ExecuteRestRequest(request);
            //var msg = $"RestAPI GetPositions:{response.Content}";
            //_logger.Trace(msg);
            //Log.Trace(msg);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"RestAPI GetPositions:{(int)response.StatusCode} {response.StatusDescription}, Content: {response.Content}, ErrorMessage: {response.ErrorMessage}");
            }

            var positionResult = JsonConvert.DeserializeObject<DeribitMessages.PositionResult>(response.Content);
            return new List<DeribitMessages.Position>(positionResult.positions);
        }

        public DeribitMessages.Account GetAccountSummary(string currency)
        {
            var localVarPath = "/private/get_account_summary";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "currency", currency)); // query parameter

            // authentication (bearerAuth) required
            // http basic authentication required
            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey + ":" + _apiSecret);

            var request = PrepareRequest(localVarPath,
               Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
               localVarPathParams, localVarHttpContentType);

            var response = ExecuteRestRequest(request);
            //var msg =$"RestAPI GetAccountSummary:{response.Content}";
            //_logger.Trace(msg);
            //Log.Trace(msg);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"GetAccountSummary: {(int)response.StatusCode} {response.StatusDescription}, Content: {response.Content}, ErrorMessage: {response.ErrorMessage}");
            }

            var accountResult = JsonConvert.DeserializeObject<DeribitMessages.AccountResult>(response.Content);
           

            return accountResult.account;
        }

        public DeribitMessages.Candles GetTradingViewChartData(string instrumentName, long start, long end, string resolution)
        {
            var localVarPath = "public/get_tradingview_chart_data";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);


            //instrument_name	true	string		Instrument name
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "instrument_name", instrumentName));
            //start_timestamp	true	integer		The earliest timestamp to return result for
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "start_timestamp", start));
            //end_timestamp	true	integer		The most recent timestamp to return result for
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "end_timestamp", end));
            //resolution	true	string		Chart bars resolution given in full minutes or keyword 1D
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "resolution", resolution));

            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey + ":" + _apiSecret);

            var req = PrepareRequest(localVarPath,
               Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
               localVarPathParams, localVarHttpContentType);


            var response = ExecuteRestRequest(req);
            var msg = $"RestAPI GetTradingViewChartData:{response.Content}";
            _logger.Trace(msg);
            Log.Trace(msg);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"GetTradingViewChartData: {(int)response.StatusCode} {response.StatusDescription}, Content: {response.Content}, ErrorMessage: {response.ErrorMessage}");
            }

            var candleResult = JsonConvert.DeserializeObject<DeribitMessages.CandleResult>(response.Content);
            return candleResult.candles;
        }


        public DeribitMessages.Tick Ticker(string instrumentName)
        {
            var localVarPath = "public/ticker";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            //instrument_name	true	string		Instrument name
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "instrument_name", instrumentName));

            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey + ":" + _apiSecret);

            var req = PrepareRequest(localVarPath,
               Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
               localVarPathParams, localVarHttpContentType);


            var response = ExecuteRestRequest(req);
            //var msg = $"RestAPI GetTick:{response.Content}";
            //_logger.Trace(msg);
            //Log.Trace(msg);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new Exception($"GetTick:{(int)response.StatusCode} {response.StatusDescription}, Content: {response.Content}, ErrorMessage: {response.ErrorMessage}");
            }

            var result = JsonConvert.DeserializeObject<DeribitMessages.TickResult>(response.Content);
            return result.tick;
        }

        public bool GetOrderState(string orderId,out DeribitMessages.Order order)
        {
            order = null;
            var localVarPath = "/private/get_order_state";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "order_id", orderId)); // query parameter

            // authentication (bearerAuth) required
            // http basic authentication required
            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey+ ":" + _apiSecret);

            var request = PrepareRequest(localVarPath,
               Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
               localVarPathParams, localVarHttpContentType);


            var response = ExecuteRestRequest(request);
            //var msg = $"RestAPI GetOrderState:{response.Content}";
            //_logger.Trace(msg);
            //Log.Trace(msg);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                //throw new Exception($"DeribitBrokerage.GetOrderState: request failed: [{(int)response.StatusCode}] {response.StatusDescription}, Content: {response.Content}, ErrorMessage: {response.ErrorMessage}");
                var errStr =
                    $"GetOrderState: {(int) response.StatusCode} {response.StatusDescription},ErrorMessage: {response.ErrorMessage}";
                _logger.Error(errStr);
                Log.Error(errStr);
                return false;
            }

            var content = response.Content;
            if (response.Content.Contains("\"price\":\"market_price\""))
            {
                content = response.Content.Replace("\"price\":\"market_price\"", "\"price\":0");
            }
            var orderStateResult = JsonConvert.DeserializeObject<DeribitMessages.OrderStateResult>(content);

            order= orderStateResult.order;
            return true;
        }

        public List<DeribitMessages.Order> GetOrderHistoryByCurrency(string currency)
        {
            var list = new List<DeribitMessages.Order>();

            var localVarPath = "/private/get_order_history_by_currency";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "currency", currency));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "count", 1000));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "include_old", "true"));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "include_unfilled", "true"));

            // authentication (bearerAuth) required
            // http basic authentication required
            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey+ ":" + _apiSecret);

            var request = PrepareRequest(localVarPath,
               Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
               localVarPathParams, localVarHttpContentType);


            var response = ExecuteRestRequest(request);
            //var msg = $"RestAPI GetOrderHistoryByCurrency:{response.Content}";
            //_logger.Trace(msg);
            //Log.Trace(msg);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                //throw new Exception($"DeribitBrokerage.GetStopOrderHistory: request failed: [{(int)response.StatusCode}] {response.StatusDescription}, Content: {response.Content}, ErrorMessage: {response.ErrorMessage}");
                var errStr =
                    $"GetOrderHistoryByCurrency: {(int) response.StatusCode} {response.StatusDescription},ErrorMessage: {response.ErrorMessage}";
                _logger.Error(errStr);
                Log.Error(errStr);
                return list;
            }

            var content = response.Content;
            if (response.Content.Contains("\"price\":\"market_price\""))
            {
                content = response.Content.Replace("\"price\":\"market_price\"", "\"price\":0");
            }

            var orderResult = JsonConvert.DeserializeObject<DeribitMessages.OrderResult>(content);

            list.AddRange(orderResult.orders);
            return list;
        }

        public List<DeribitMessages.StopOrderEntity> GetStopOrderHistory(string currency, string instrumentName = "", int count = 1000)
        {
            var list = new List<DeribitMessages.StopOrderEntity>();

            var localVarPath = "/private/get_stop_order_history";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "currency", currency));
            if (!string.IsNullOrEmpty(instrumentName))
            {
                localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "instrument_name", instrumentName));
            }
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "count", count));


            // authentication (bearerAuth) required
            // http basic authentication required
            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey + ":" + _apiSecret);

            var request = PrepareRequest(localVarPath,
               Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
               localVarPathParams, localVarHttpContentType);


            var response = ExecuteRestRequest(request);
            //var msg =$"RestAPI GetStopOrderHistory:{response.Content}";
            //_logger.Trace(msg);
            //Log.Trace(msg);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                //throw new Exception($"DeribitBrokerage.GetStopOrderHistory: request failed: [{(int)response.StatusCode}] {response.StatusDescription}, Content: {response.Content}, ErrorMessage: {response.ErrorMessage}");
                var errStr =$"GetStopOrderHistory: request failed: [{(int)response.StatusCode}] {response.StatusDescription}, ErrorMessage: {response.ErrorMessage}";
                _logger.Error(errStr);
                Log.Error(errStr);
                return list;
            }

            //try
            //{
            var content = response.Content;
            if (response.Content.Contains("\"price\":\"market_price\""))
            {
                content = response.Content.Replace("\"price\":\"market_price\"", "\"price\":0");
            }

            var orderResult = JsonConvert.DeserializeObject<DeribitMessages.StopOrderHistoryResult>(content);

            list.AddRange(orderResult.result.entries);
            //}
            //catch (Exception e)
            //{
            //    Log.Error(e.Message);
            //}

            return list;
        }

        public List<DeribitMessages.Order> GetOpenOrdersByCurrency(string currency)
        {
            var list = new List<DeribitMessages.Order>();

            var localVarPath = "/private/get_open_orders_by_currency";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "currency", currency)); // query parameter
            //all kinds
            //localVarQueryParams.AddRange(this.ParameterToKeyValuePairs("", "kind", "option")); // query parameter

            // authentication (bearerAuth) required
            // http basic authentication required
            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey + ":" + _apiSecret);

            var request = PrepareRequest(localVarPath,
               Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
               localVarPathParams, localVarHttpContentType);


            var response = ExecuteRestRequest(request);
            //var msg =$"RestAPI GetOpenOrdersByCurrency:{response.Content}";
            //_logger.Trace(msg);
            //Log.Trace(msg);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errStr =
                    $"DeribitBrokerage.GetOpenOrders: request failed: [{(int) response.StatusCode}] {response.StatusDescription}, Content: {response.Content}, ErrorMessage: {response.ErrorMessage}";

                throw new Exception(errStr);
            }

            var content = response.Content;
            if (response.Content.Contains("\"price\":\"market_price\""))
            {
                content = response.Content.Replace("\"price\":\"market_price\"", "\"price\":0");
            }

            var orderResult = JsonConvert.DeserializeObject<DeribitMessages.OrderResult>(content);

            list.AddRange(orderResult.orders);


            return list;

        }

        public DeribitMessages.Trades GetUserTradesByCurrency(string currency, string startId, string endId, int count)
        {
            var localVarPath = "/private/get_user_trades_by_currency";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "currency", currency));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "start_id", startId));
            if (!string.IsNullOrEmpty(endId))
            {
                localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "end_id", endId));
            }
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "count", count));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "include_old", "true"));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "sorting", "asc"));

            // authentication (bearerAuth) required
            // http basic authentication required
            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey + ":" + _apiSecret);

            var request = PrepareRequest(localVarPath,
               Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
               localVarPathParams, localVarHttpContentType);


            var response = ExecuteRestRequest(request);
            //var msg =$"RestAPI GetUserTradesByCurrency:{response.Content}";
            //_logger.Trace(msg);
            //Log.Trace(msg);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errStr =
                    $"DeribitBrokerage.GetUserTrades: request failed: [{(int) response.StatusCode}] {response.StatusDescription}, Content: {response.Content}, ErrorMessage: {response.ErrorMessage}";
                throw new Exception(errStr);

            }

            var tradeResult = JsonConvert.DeserializeObject<DeribitMessages.TradeResult>(response.Content);
            return tradeResult.result;
        }

        public DeribitMessages.Trades GetUserTradesByCurrencyAndTime(string currency, int startTime, int endTime, int count)
        {
            var localVarPath = "/private/get_user_trades_by_currency_and_time";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "currency", currency));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "start_timestamp", startTime));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "end_timestamp", endTime));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "count", count));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "include_old", "true"));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "sorting", "asc"));

            // authentication (bearerAuth) required
            // http basic authentication required
            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey + ":" + _apiSecret);

            var request = PrepareRequest(localVarPath,
               Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
               localVarPathParams, localVarHttpContentType);


            var response = ExecuteRestRequest(request);
            //var msg =$"RestAPI GetUserTradesByCurrencyAndTime:{response.Content}";
            //_logger.Trace(msg);
            //Log.Trace(msg);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errStr = $"GetUserTrades: request failed: [{(int)response.StatusCode}] {response.StatusDescription}, ErrorMessage: {response.ErrorMessage}";
                throw new Exception(errStr);

            }

            var tradeResult = JsonConvert.DeserializeObject<DeribitMessages.TradeResult>(response.Content);
            return tradeResult.result;
        }

        public List<DeribitMessages.Trade> GetUserTradesByOrder(string orderId)
        {
            var trades = new List<DeribitMessages.Trade>();
            var localVarPath = "/private/get_user_trades_by_order";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "order_id", orderId));


            // authentication (bearerAuth) required
            // http basic authentication required
            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey + ":" + _apiSecret);

            var request = PrepareRequest(localVarPath,
               Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
               localVarPathParams, localVarHttpContentType);


            var response = ExecuteRestRequest(request);
            //var msg = $"GetAccountSummary:{response.Content}";
            //_logger.Trace(msg);
            //Log.Trace(msg);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                var errStr =$"DeribitBrokerage.GetUserTrades: request failed: [{(int)response.StatusCode}] {response.StatusDescription}, Content: {response.Content}, ErrorMessage: {response.ErrorMessage} param:{orderId}";
                //throw new Exception(errStr);
                _logger.Error(errStr);
                Log.Error(errStr);
                return trades;
            }

            var tradeResult = JsonConvert.DeserializeObject<DeribitMessages.TradeByOrderResult>(response.Content);
            trades.AddRange(tradeResult.trades);
            return trades;
        }

        public bool Cancel(string orderId, out DeribitMessages.CancelOrderResult result)
        {
            result = null;
            var localVarPath = "private/cancel";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);


            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "order_id",orderId));

            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey + ":" + _apiSecret);

            var request = PrepareRequest(localVarPath,
               Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
               localVarPathParams, localVarHttpContentType);
            
            var response = ExecuteRestRequest(request);
            var msg =$"RestAPI Cancel: orderId:{orderId} response:{response.Content}";
            _logger.Trace(msg);
            Log.Trace(msg);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = response.Content;
                var rsp = JObject.Parse(response.Content);
                if (rsp["result"]["price"].ToString() == "market_price")
                {
                    rsp["result"]["price"] = 0;
                    content = rsp.ToString();
                }
                result = JsonConvert.DeserializeObject<DeribitMessages.CancelOrderResult>(content);
                return true;
            }

            result = JsonConvert.DeserializeObject<DeribitMessages.CancelOrderResult>(response.Content);
            return false;
        }

        public bool Edit(EditOrderRequest param,out DeribitMessages.PlaceOrderResult result)
        {
            result = null;
            var localVarPath = "/private/edit";
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "order_id", param.order_id));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "amount", param.amount));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "price", param.price));


            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey + ":" + _apiSecret);

            var request = PrepareRequest(localVarPath,
                Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
                localVarPathParams, localVarHttpContentType);

            var response = ExecuteRestRequest(request);
            var msg = $"RestAPI Edit : {param.order_id} response:{response.Content}";
            _logger.Trace(msg);
            Log.Trace(msg);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = response.Content;
                var rsp = JObject.Parse(response.Content);
                if (rsp["result"]["order"]["price"].ToString() == "market_price")
                {
                    rsp["result"]["order"]["price"] = 0;
                    content = rsp.ToString();
                }
                
                result = JsonConvert.DeserializeObject<DeribitMessages.PlaceOrderResult>(content);
                return true;
            }

            result = JsonConvert.DeserializeObject<DeribitMessages.PlaceOrderResult>(response.Content);
            return false;
        }

        public bool Buy(DeribitBuySellRequest param, out DeribitMessages.PlaceOrderResult result)
        {
            return BuySell("buy", param,out result);
        }

        public bool Sell(DeribitBuySellRequest param, out DeribitMessages.PlaceOrderResult result)
        {
            return BuySell("sell", param,out result);
        }

        public bool BuySell(string buySell,DeribitBuySellRequest param,out DeribitMessages.PlaceOrderResult result)
        {
            result = null;
            var localVarPath = "/private/" + buySell;
            var localVarPathParams = new Dictionary<string, string>();
            var localVarQueryParams = new List<KeyValuePair<string, string>>();
            var localVarHeaderParams = new Dictionary<string, string>(new ConcurrentDictionary<string, string>());
            var localVarFormParams = new Dictionary<string, string>();
            var localVarFileParams = new Dictionary<string, FileParameter>();
            Object localVarPostBody = null;


            // to determine the Content-Type header
            string[] localVarHttpContentTypes = new string[] {
            };
            string localVarHttpContentType = SelectHeaderContentType(localVarHttpContentTypes);

            // to determine the Accept header
            string[] localVarHttpHeaderAccepts = new string[] {
                "application/json"
            };
            string localVarHttpHeaderAccept = SelectHeaderAccept(localVarHttpHeaderAccepts);
            if (localVarHttpHeaderAccept != null)
                localVarHeaderParams.Add("Accept", localVarHttpHeaderAccept);

            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "instrument_name", param.instrument_name));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "amount", param.amount));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "type", param.type));
            localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "label", param.label));
            if (param.type == DeribitEnums.OrderType.stop_limit || param.type == DeribitEnums.OrderType.stop_market)
            {
                localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "trigger", param.trigger));
                localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "stop_price", param.stop_price));
            }

            if (param.type == DeribitEnums.OrderType.limit || param.type == DeribitEnums.OrderType.stop_limit)
            {
                localVarQueryParams.AddRange(ParameterToKeyValuePairs("", "price", param.price));
            }

            localVarHeaderParams["Authorization"] = "Basic " + Base64Encode(_apiKey + ":" + _apiSecret);

            var request = PrepareRequest(localVarPath,
                Method.Get, localVarQueryParams, localVarPostBody, localVarHeaderParams, localVarFormParams, localVarFileParams,
                localVarPathParams, localVarHttpContentType);

            var response = ExecuteRestRequest(request);
            var msg =$"RestAPI BuySell: label:{param.label} response:{response.Content}";
            _logger.Trace(msg);
            Log.Trace(msg);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var content = response.Content;
                var rsp = JObject.Parse(response.Content);
                if (rsp["result"]["order"]["price"].ToString() == "market_price")
                {
                    rsp["result"]["order"]["price"] = 0;
                    content = rsp.ToString();
                }

                result = JsonConvert.DeserializeObject<DeribitMessages.PlaceOrderResult>(content);
                return true;
            }

            result = JsonConvert.DeserializeObject<DeribitMessages.PlaceOrderResult>(response.Content);
            return false;
        }

        public static DeribitMessages.Instrument[] GetInstruments(string env,string currency, bool expired=false)
        {
            try
            {
                var client = new WebClient();
                string url = "";
                if (env.Contains("test"))
                {
                    url = "https://test.deribit.com/api/v2/public/get_instruments?currency=" + currency + "&expired" +
                          expired.ToString();
                }
                else
                {
                    url = "https://www.deribit.com/api/v2/public/get_instruments?currency=" + currency + "&expired=" +
                          expired.ToString();
                }

                var content = client.DownloadString(url);
                var result = JsonConvert.DeserializeObject<DeribitMessages.InstrumentResult>(content);
                return result.instruments;
            }
            catch (Exception ex)
            {
                return Array.Empty<DeribitMessages.Instrument>();
            }
        }

    }
}
