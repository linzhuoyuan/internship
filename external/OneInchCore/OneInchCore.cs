using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NLog;

namespace OneInch.Net
{
    public class OneInchCore
    {
        public static readonly Dictionary<string, string> UsdcAddresses = new()
        {
            { "mainnet", "0xa0b86991c6218b36c1d19d4a2e9eb0ce3606eb48" },
            { "arbitrum", "0xff970a61a04b1ca14834a43f5de4533ebddb5cc8" },
        };

        public static BigInteger UsdcUnit = new (Math.Pow(10, 6));
        public static BigInteger EthUnit = new (Math.Pow(10, 18));

        private const string SwapApiUrl = "https://api.1inch.io/v4.0";

        private static readonly IDictionary<string, string> NetNames = new Dictionary<string, string>
        {
            {"1", "mainnet"},
            {"3", "ropsten"},
            {"4", "rinkeby"},
            {"10", "optimism"},
            {"42", "kovan"},
            {"56", "binance"},
            {"97", "binance_testnet"},
            {"137", "polygon"},
            {"100", "xdai"},
            {"250", "fantom"},
            {"42161", "arbitrum"},
            {"421611", "arbitrum_testnet"},
        };

        protected readonly string walletAddress;
        protected readonly ILogger logger;
        protected string? netId;
        private readonly int _maxRetry;

        private static string QueryString(IDictionary<string, object?> dict)
        {
            var list = new List<string>();
            foreach (var (key, value) in dict)
            {
                if (value == null)
                    continue;
                list.Add($"{key}={value}");
            }
            return string.Join("&", list);
        }

        private async Task<CallResult<JToken?>> ApiRequest(string method, IDictionary<string, object?>? queryItems = null)
        {
            var client = new HttpClient();
            var tryCount = 0;
            var builder = new UriBuilder($"{SwapApiUrl}/{netId}/{method}");
            if (queryItems != null)
            {
                builder.Query = QueryString(queryItems);
            }

            CallError? error = null;
            try
            {
                while (tryCount < _maxRetry)
                {
                    var response = await client.GetAsync(builder.Uri);
                    if (response.IsSuccessStatusCode)
                    {
                        var data = await response.Content.ReadAsStringAsync();
                        var token = JToken.Parse(data);
                        return new CallResult<JToken?>(token);
                    }
                    logger.Warn($"请求失败,{method},{response.StatusCode}");
                    tryCount++;
                    error = new CallError { statusCode = (int)response.StatusCode };
                }
            }
            catch (Exception e)
            {
                logger.Error(e);
                error = new CallError { statusCode = -1, error = e.Message };
            }
            return new CallResult<JToken?>(error!);
        }

        public OneInchCore(string walletAddress, string? netId = "1", ILogger? logger = null, int maxRetry = 3)
        {
            this.walletAddress = walletAddress;
            this.netId = netId;
            this.logger = logger ?? LogManager.CreateNullLogger();
            _maxRetry = maxRetry;
        }

        public string? NetId => netId;
        public string? NetName => netId == null ? null : NetNames[netId];
        public string? UsdcAddress => NetName == null ? null : UsdcAddresses[NetName];

        public async Task<string?> GetSpenderAsync()
        {
            var result = await ApiRequest("approve/spender");
            if (result.Success)
            {
                return (string?)result.Data?["address"];
            }

            return null;
        }

        public async Task<CallResult<QuoteInfo>> GetQuoteInfoAsync(
            string inputToken,
            string outputToken,
            BigInteger qty,
            IEnumerable<string>? protocols = null,
            decimal? fee = null,
            int? gasPrice = null,
            int? complexityLevel = null,
            IEnumerable<string>? connectorTokens = null,
            int? gasLimit = null,
            int? mainRouteParts = null,
            int? parts = null)
        {
            var data = new Dictionary<string, object?>();
            data.Add("fromTokenAddress", inputToken);
            data.Add("toTokenAddress", outputToken);
            data.Add("amount", qty);
            data.Add("protocols", protocols == null ? null : string.Join(",", protocols));
            data.Add("fee", fee);
            data.Add("gasPrice", gasPrice);
            data.Add("complexityLevel", complexityLevel);
            data.Add("connectorTokens", connectorTokens == null ? null : string.Join(",", connectorTokens));
            data.Add("gasLimit", gasLimit);
            data.Add("mainRouteParts", mainRouteParts);
            data.Add("parts", parts);

            var result = await ApiRequest("quote", data!);
            if (result.Success)
            {
                var token = result.Data!;
                if ((string?)token["error"] == null)
                {
                    return new CallResult<QuoteInfo>(token.ToObject<QuoteInfo>()!);
                }
                return new CallResult<QuoteInfo>(token.ToObject<CallError>()!);
            }

            return new CallResult<QuoteInfo>(result.Error!);
        }

        public async Task<CallResult<SwapInfo>> GetSwapInfoAsync(
            string inputToken,
            string outputToken,
            BigInteger qty,
            decimal slippage,
            IEnumerable<string>? protocols = null,
            string? recipient = null,
            decimal? fee = null,
            int? gasPrice = null,
            bool burnChi = false,
            int? complexityLevel = null,
            IEnumerable<string>? connectorTokens = null,
            bool? allowPartialFill = null,
            int? gasLimit = null,
            int? mainRouteParts = null,
            int? parts = null)
        {
            var data = new Dictionary<string, object?>();
            data.Add("fromTokenAddress", inputToken);
            data.Add("toTokenAddress", outputToken);
            data.Add("amount", qty);
            data.Add("fromAddress", walletAddress);
            data.Add("slippage", slippage);
            data.Add("protocols", protocols == null ? null : string.Join(",", protocols));
            data.Add("recipient", recipient);
            data.Add("fee", fee);
            data.Add("gasPrice", gasPrice);
            data.Add("burnChi", burnChi);
            data.Add("complexityLevel", complexityLevel);
            data.Add("connectorTokens", connectorTokens == null ? null : string.Join(",", connectorTokens));
            data.Add("allowPartialFill", allowPartialFill);
            data.Add("gasLimit", gasLimit);
            data.Add("mainRouteParts", mainRouteParts);
            data.Add("parts", parts);

            var result = await ApiRequest("swap", data!);
            if (result.Success)
            {
                var token = result.Data!;
                if ((string?)token["error"] == null)
                {
                    return new CallResult<SwapInfo>(token.ToObject<SwapInfo>()!);
                }
                return new CallResult<SwapInfo>(token.ToObject<CallError>()!);
            }

            return new CallResult<SwapInfo>(result.Error!);
        }
    }
}
