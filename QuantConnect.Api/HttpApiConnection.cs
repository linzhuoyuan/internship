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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using QuantConnect.API;
using QuantConnect.Logging;
using QuantConnect.Orders;
using RestSharp;
using Config = QuantConnect.Configuration.Config;

namespace QuantConnect.Api
{
    public class HttpRestResponse
    {
        public Exception? ErrorException { get; set; }
        public string? Content { get; set; }
    }

    /// <summary>
    /// API Connection and Hash Manager
    /// </summary>
    public class HttpApiConnection
    {
        private readonly string _apiUrl;

        public HttpApiConnection(int userId, string token)
        {
            _apiUrl = Config.Get("cloud-api-url", "https://www.quantconnect.com/api/v2/");            
        }

        /// <summary>
        /// Return true if connected successfully.
        /// </summary>
        public bool Connected
        {
            get
            {
                var request = new RestRequest("authenticate");
                if (TryRequest(request, out AuthenticationResponse? response))
                {
                    return response != null && response.Success;
                }
                return false;
            }
        }



        /// <summary>
        /// Place a secure request and get back an object of type T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="request"></param>
        /// <param name="result">Result object from the </param>
        /// <returns>T typed object response</returns>
        public bool TryRequest<T>(RestRequest request, out T? result)
            where T : RestResponse
        {
            var responseContent = string.Empty;

            try
            {
                var response = ExecuteAsync(request).Result;
                // Use custom converter for deserializing live results data
                JsonConvert.DefaultSettings = () => new JsonSerializerSettings
                {
                    Converters = { new LiveAlgorithmResultsJsonConverter(), new OrderJsonConverter() }
                };

                //Verify success
                if (response.ErrorException != null)
                {
                    Log.Error($"ApiConnection.TryRequest({request.Resource}): Error: {response.ErrorException.Message}");
                    result = null;
                    return false;
                }

                responseContent = response.Content ?? string.Empty;
                if (responseContent.Contains("errors"))
                {
                    var start = responseContent.IndexOf(":", StringComparison.Ordinal);
                    var end = responseContent.IndexOf(",", StringComparison.Ordinal);
                    var errorContent = responseContent.Substring(start + 1, end - start - 1);
                    var content = errorContent;
                    if (!errorContent.StartsWith("["))
                    {
                        errorContent = "[" + errorContent;
                    }

                    if (!errorContent.EndsWith("]"))
                    {
                        errorContent += "]";
                    }
                    responseContent = responseContent.Replace(content, errorContent);
                }

                result = JsonConvert.DeserializeObject<T>(responseContent);
                if (!result.Success)
                {
                    //result;
                    return false;
                }
            }
            catch (Exception err)
            {
                Log.Error($"ApiConnection.TryRequest({request.Resource}): Error: {err.Message}, Response content: {responseContent}");
                result = null;
                return false;
            }
            return true;
        }

        private async Task<HttpRestResponse> ExecuteAsync(RestRequest request)
        {
            switch (request.Method)
            {
                case Method.Get:
                    break;
                default:
                    return new HttpRestResponse
                    {
                        ErrorException = new NotImplementedException("Not Implemented")
                    };
            }
            var client = new HttpClient { BaseAddress = new Uri(_apiUrl) };
            var query = new StringBuilder();
            foreach (var parameter in request.Parameters)
            {
                if (parameter.Type == ParameterType.HttpHeader)
                {
                    client.DefaultRequestHeaders.Add(parameter.Name, parameter.Value?.ToString());
                }
                else if (parameter.Type == ParameterType.GetOrPost)
                {
                    if (query.Length > 0)
                    {
                        query.Append("&");
                    }

                    query.Append($"{parameter.Name}={parameter.Value}");
                }
            }

            var url = request.Resource;
            if (query.Length > 0)
            {
                url += "?" + query;
            }

            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            return new HttpRestResponse { Content = content };
        }
    }
}
