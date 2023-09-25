using NLog;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using MomCrypto.Api;
using Newtonsoft.Json;

namespace MomCrypto.DataApi
{
    public class MomHistoryDataApi
    {
        private readonly ILogger _logger;
        private readonly HttpClient _client;

        public MomHistoryDataApi(string address, ILogger logger)
        {
            _client = CreateClient(address);
            _logger = logger;
        }

        private static HttpClient CreateClient(string host)
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            client.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));
            client.BaseAddress = new Uri(host, UriKind.Absolute);
            return client;
        }

        public MomAccount[] GetAccounts()
        {
            var url = $"datafeed/mom/accounts";
            var task = _client.GetStringAsync(url);
            task.Wait();
            return JsonConvert.DeserializeObject<MomAccount[]>(task.Result);
        }

        public MomFundAccount[] GetFundAccounts()
        {
            var url = $"datafeed/mom/fundaccounts";
            var task = _client.GetStringAsync(url);
            task.Wait();
            return JsonConvert.DeserializeObject<MomFundAccount[]>(task.Result);
        }

        public MomTrade[] GetAccountTrades(string account, long localId = 0, int count = 1000)
        {
            var url = $"datafeed/mom/trade/account/{account}/{localId}/{count}";
            var task = _client.GetStringAsync(url);
            task.Wait();
            return JsonConvert.DeserializeObject<MomTrade[]>(task.Result);
        }

        public MomTrade[] GetUserTrades(string user, long localId = 0, int count = 1000)
        {
            var url = $"datafeed/mom/trade/user/{user}/{localId}/{count}";
            var task = _client.GetStringAsync(url);
            task.Wait();
            return JsonConvert.DeserializeObject<MomTrade[]>(task.Result);
        }

        public MomFundTrade[] GetFundTrades(string account, long localId = 0, int count = 1000)
        {
            var url = $"datafeed/mom/fundtrade/{account}/{localId}/{count}";
            var task = _client.GetStringAsync(url);
            task.Wait();
            return JsonConvert.DeserializeObject<MomFundTrade[]>(task.Result);
        }

        public MomPosition[] GetUserPositions(string user)
        {
            var url = $"datafeed/mom/position/user/{user}";
            var task = _client.GetStringAsync(url);
            task.Wait();
            return JsonConvert.DeserializeObject<MomPosition[]>(task.Result);
        }

        public MomFundPosition[] GetFundPositions(string account)
        {
            var url = $"datafeed/mom/fundposition/{account}";
            var task = _client.GetStringAsync(url);
            task.Wait();
            return JsonConvert.DeserializeObject<MomFundPosition[]>(task.Result);
        }

        public string QryHistoryData(MomQryHistoryData qryData, string market)
        {
            switch (market)
            {
                case "binance":
                    return QryBinanceHistoryData(qryData);
                case "deribit":
                    return QryDeribitHistoryData(qryData);
                case "ftx":
                    return QryFtxHistoryData(qryData);
                case "mexc":
                    return QryMexcHistoryData(qryData);
                case "dydx":
                    return QryDydxHistoryData(qryData);
                case "dex":
                    return QryDexHistoryData(qryData);
                default:
                    throw new ArgumentException($"No history data service for {market}!");
            }
        }
        
        private string QryDexHistoryData(MomQryHistoryData qryData)
        {
            try
            {
                _logger.Info("查询Dex历史数据");
                var url = $"datafeed/dex/history/{qryData.InstrumentId}/{qryData.DataType}/{qryData.Market}/{qryData.TimeStart}/{qryData.TimeEnd}";
                var task = _client.GetStringAsync(url);
                task.Wait();
                return task.Result;
            }
            catch (Exception e)
            {
                _logger.Error(e.Message);
            }
            return string.Empty;
        }

        private string QryDydxHistoryData(MomQryHistoryData qryData)
        {
            try
            {
                _logger.Info("查询Dydx历史数据");
                var url = $"datafeed/dydx/history/{qryData.InstrumentId}/{qryData.DataType}/{qryData.Market}/{qryData.TimeStart}/{qryData.TimeEnd}";
                var task = _client.GetStringAsync(url);
                task.Wait();
                return task.Result;
            }
            catch (Exception e)
            {
                _logger.Error(e.Message);
            }
            return string.Empty;
        }

        private string QryMexcHistoryData(MomQryHistoryData qryData)
        {
            try
            {
                _logger.Info("查询Mexc历史数据");
                var url = $"datafeed/mexc/history/{qryData.InstrumentId}/{qryData.DataType}/{qryData.Market}/{qryData.TimeStart}/{qryData.TimeEnd}";
                var task = _client.GetStringAsync(url);
                task.Wait();
                return task.Result;
            }
            catch (Exception e)
            {
                _logger.Error(e.Message);
            }
            return string.Empty;
        }

        public string QryBinanceHistoryData(MomQryHistoryData qryData)
        {
            try
            {
                _logger.Info("查询Binance历史数据");
                var url = $"datafeed/binance/history/{qryData.InstrumentId}/{qryData.DataType}/{qryData.Market}/{qryData.TimeStart}/{qryData.TimeEnd}";
                var task = _client.GetStringAsync(url);
                task.Wait();
                return task.Result;
            }
            catch (Exception e)
            {
                _logger.Error(e.Message);
            }
            return string.Empty;
        }

        public string QryDeribitHistoryData(MomQryHistoryData qryData)
        {
            try
            {
                _logger.Info("查询Deribit历史数据");
                var url = $"datafeed/deribit/history/{qryData.InstrumentId}/{qryData.DataType}/{qryData.TimeStart}/{qryData.TimeEnd}";
                var task = _client.GetStringAsync(url);
                task.Wait();
                return task.Result;
            }
            catch (Exception e)
            {
                _logger.Error(e.Message);
            }

            return string.Empty;
        }

        public string QryFtxHistoryData(MomQryHistoryData qryData)
        {
            try
            {
                _logger.Info("查询Ftx历史数据");
                var url = $"datafeed/ftx/history/{qryData.InstrumentId.Replace('/','_')}/{qryData.DataType}/{qryData.TimeStart}/{qryData.TimeEnd}";
                var task = _client.GetStringAsync(url);
                task.Wait();
                return task.Result;
            }
            catch (Exception e)
            {
                _logger.Error(e.Message);
            }

            return string.Empty;
        }

        public void SendDingMsg(string text, string token = null, string key = null)
        {
            try
            {
                _logger.Info($"发送钉钉消息:{text}");
                var url = $"ding/send/{text}";
                if (token != null && key != null)
                {
                    url += "$/{token}/{key}";
                }
                var task = _client.GetAsync(url);
                task.Wait();
            }
            catch (Exception e)
            {
                _logger.Error(e.Message);
            }
        }
    }
}
