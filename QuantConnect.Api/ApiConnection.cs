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
using System.Net;
using Newtonsoft.Json;
using QuantConnect.API;
using QuantConnect.Configuration;
using QuantConnect.Logging;
using QuantConnect.Orders;
using RestSharp;
using RestSharp.Authenticators;

namespace QuantConnect.Api
{
    /// <summary>
    /// API Connection and Hash Manager
    /// </summary>
    public class ApiConnection
    {
        /// <summary>
        /// Authorized client to use for requests.
        /// </summary>
        public RestClient Client;
        // Authorization Credentials
        private readonly string _userId;
        private readonly string _token;

        /// <summary>
        /// Create a new Api Connection Class.
        /// </summary>
        /// <param name="userId">User Id number from QuantConnect.com account. Found at www.quantconnect.com/account </param>
        /// <param name="token">Access token for the QuantConnect account. Found at www.quantconnect.com/account </param>
        public ApiConnection(int userId, string token)
        {
            _token = token;
            _userId = userId.ToString();
            var apiUrl = Config.Get("cloud-api-url", "https://www.quantconnect.com/api/v2/");
            Client = new RestClient(apiUrl);
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
                //Generate the hash each request
                // Add the UTC timestamp to the request header.
                // Timestamps older than 1800 seconds will not work.
                var timestamp = (int)Time.TimeStamp();
                var hash = Api.CreateSecureHash(timestamp, _token);
                request.AddHeader("Timestamp", timestamp.ToString());

                Client.Authenticator = new HttpBasicAuthenticator(_userId, hash);

                // Execute the authenticated REST API Call
                var response = Client.ExecuteAsync(request).Result;
                //if (task.Wait(1000))
                //{
                //    Log.Error($"ApiConnection.TryRequest({request.Resource}): Error: request timeout");
                //    result = null;
                //    return false;
                //}

                //var response = task.Result;
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

                responseContent = response.Content;
                if (responseContent.Contains("errors"))
                {
                    var start = responseContent.IndexOf(":", StringComparison.Ordinal);
                    var end = responseContent.IndexOf(",", StringComparison.Ordinal);
                    var errorContent = responseContent.Substring(start + 1, end - start - 1);
                    var text = errorContent;
                    if (!errorContent.StartsWith("["))
                    {
                        errorContent = "[" + errorContent;
                    }

                    if (!errorContent.EndsWith("]"))
                    {
                        errorContent += "]";
                    }
                    responseContent = responseContent.Replace(text, errorContent);
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
    }
}
