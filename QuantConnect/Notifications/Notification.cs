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
using Newtonsoft.Json;
using QuantConnect.Util;
using QuantConnect.Logging;
using QuantConnect.Configuration;
using System.Net.Http;
using System.Text;
using RestSharp;

namespace QuantConnect.Notifications
{

    /// <summary>
    /// Local/desktop implementation of messaging system for Lean Engine.
    /// </summary>
    [JsonConverter(typeof(NotificationJsonConverter))]
    public abstract class Notification
    {
        protected static int _debugLevel = 1;
        /// <summary>
        /// Method for sending implementations of notification object types.
        /// </summary>
        /// <remarks>SMS, Email and Web are all handled by the QC Messaging Handler. To implement your own notification type implement it here.</remarks>
        public virtual void Send()
        {
            //
        }
    }

    /// <summary>
    /// Web Notification Class
    /// </summary>
    public class NotificationWeb : Notification
    {
        /// <summary>
        /// Optional email headers
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<string, string> Headers;

        /// <summary>
        /// Send a notification message to this web address
        /// </summary>
        public string Address;

        /// <summary>
        /// Object data to send.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public object Data;

        /// <summary>
        /// Constructor for sending a notification SMS to a specified phone number
        /// </summary>
        /// <param name="address">Address to send to</param>
        /// <param name="data">Data to send</param>
        /// <param name="headers">Optional headers to use</param>
        public NotificationWeb(string address, object data = null, Dictionary<string, string> headers = null)
        {
            Address = address;
            Data = data;
            Headers = headers;
        }
    }

    /// <summary>
    /// Sms Notification Class
    /// </summary>
    public class NotificationSms : Notification
    {
        /// <summary>
        /// Send a notification message to this phone number
        /// </summary>
        public string PhoneNumber;

        /// <summary>
        /// Message to send. Limited to 160 characters
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Message;

        /// <summary>
        /// Constructor for sending a notification SMS to a specified phone number
        /// </summary>
        /// <param name="number"></param>
        /// <param name="message"></param>
        public NotificationSms(string number, string message)
        {
            PhoneNumber = number;
            Message = message;
        }
    }

    /// <summary>
    /// Email notification data.
    /// </summary>
    public class NotificationEmail : Notification
    {
        /// <summary>
        /// Optional email headers
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<string, string> Headers;

        /// <summary>
        /// Send to address:
        /// </summary>
        public string Address;

        /// <summary>
        /// Email subject
        /// </summary>
        public string Subject;

        /// <summary>
        /// Message to send.
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Message;

        /// <summary>
        /// Email Data
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Data;

        /// <summary>
        /// Default constructor for sending an email notification
        /// </summary>
        /// <param name="address">Address to send to. Will throw <see cref="ArgumentException"/> if invalid
        /// <see cref="Validate.EmailAddress"/></param>
        /// <param name="subject">Subject of the email. Will set to <see cref="string.Empty"/> if null</param>
        /// <param name="message">Message body of the email. Will set to <see cref="string.Empty"/> if null</param>
        /// <param name="data">Data to attach to the email. Will set to <see cref="string.Empty"/> if null</param>
        /// <param name="headers">Optional email headers to use</param>
        public NotificationEmail(string address, string subject = "", string message = "", string data = "", Dictionary<string, string> headers = null)
        {
            if (!Validate.EmailAddress(address))
            {
                throw new ArgumentException($"Invalid email address: {address}");
            }

            Address = address;
            Data = data ?? string.Empty;
            Message = message ?? string.Empty;
            Subject = subject ?? string.Empty;
            Headers = headers;
        }
    }

    /// <summary>
    /// Slack notification data.
    /// </summary>
    public class NotificationSlack : Notification
    {
        private static readonly HttpClient _client = new HttpClient();
        /// <summary>
        /// Send to address:
        /// </summary>
        public string Token;

        /// <summary>
        /// Message to send. Limited to 160 characters
        /// </summary>
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Message;


        /// <summary>
        /// Default constructor for sending an email notification
        /// </summary>
        /// <param name="address">Address to send to</param>
        /// <param name="subject">Subject of the email</param>
        /// <param name="message">Message body of the email</param>
        /// <param name="data">Data to attach to the email</param>
        public NotificationSlack(string token, string message)
        {
            Token = token;
            Message = message;
        }

        public override void Send()
        {
            if (!Token.IsNullOrEmpty())
            {
                if (!Message.IsNullOrEmpty())
                {
                    var content = new
                    {
                        text = Message,
                    };
                    using (var message = new StringContent(JsonConvert.SerializeObject(content), System.Text.Encoding.UTF8, "application/json"))
                    {
                        var result = _client.PostAsync(Token, message).Result;
                        if (result.IsSuccessStatusCode) return;
                        string returnValue = result.Content.ReadAsStringAsync().Result;
                        throw new Exception($"Failed to POST data to slack: ({result.StatusCode}): {returnValue}");
                    }
                }
            }
            else
            {
                Log.Debug($"Invoking a slack call towards an endpoint of empty token", _debugLevel);
            }

        }
    }

    /// <summary>
    /// Slack notification data.
    /// </summary>
    public class NotificationSlackCall : Notification
    {
        // TODO: Here used the native httpclient. It is preferable to wrap this 
        //  httpclient with raw http request and a specified timeout.
        private static readonly HttpClient _client = new HttpClient();
        // The tokens that perserved across slack calls, aiming at invoking a group notification.
        public static IList<string> PerservedTokens { get; private set; }
        // The tokens that active in one slack call, aiming at immediate notification.
        public IList<string> ImmediateTokens { get; private set; }
        /// <summary>
        /// Static constructor for registering the default call endpoints group.
        /// </summary>
        static NotificationSlackCall()
        {
            List<string> defaultTokens = new List<string> {"https://pushcall.boomware.com/api/make/T0203G8JHLG/boomware?number=8618612568458",
                                                           "https://pushcall.boomware.com/api/make/T0203G8JHLG/boomware?number=8615701582865",
                                                           "https://pushcall.boomware.com/api/make/T0203G8JHLG/boomware?number=8613436339690",
                                                           "https://pushcall.boomware.com/api/make/T0203G8JHLG/boomware?number=8613683386181",
                                                           "https://pushcall.boomware.com/api/make/T0203G8JHLG/boomware?number=8613133899462"};

            PerservedTokens = new List<string>(Config.GetValue<List<string>>("slack-call-saved-tokens", defaultTokens));
        }

        public static void ResetPerservedTokens(IList<string> inputTokens)
        {
            if (!PerservedTokens.IsNullOrEmpty()) PerservedTokens.Clear();
            if (PerservedTokens is List<string>)
            {
                ((List<string>)PerservedTokens).AddRange(inputTokens);
            }
            else
            {
                foreach (var t in inputTokens)
                {
                    PerservedTokens.Add(t);
                }
            }
        }
        /// <summary>
        /// Default constructor for sending an slack phone call notification
        /// </summary>
        /// <param name="token">Token endpoint for this slack call</param>
        public NotificationSlackCall() { }


        /// <summary>
        /// Default constructor for sending an slack phone call notification
        /// </summary>
        /// <param name="token">Token endpoint for this slack call</param>
        public NotificationSlackCall(string token)
        {
            ImmediateTokens = new List<string>();
            ImmediateTokens.Add(token);
        }

        /// <summary>
        /// Default constructor for sending an slack phone call notification
        /// </summary>
        /// <param name="token">Token endpoint for this slack call</param>
        public NotificationSlackCall(IList<string> tokens)
        {
            ImmediateTokens = new List<string>();
            ((List<string>)ImmediateTokens).AddRange(tokens);
        }

        public override void Send()
        {
            if (!ImmediateTokens.IsNullOrEmpty())
            {
                foreach (var tokenUri in ImmediateTokens)
                {
                    SendImpl(tokenUri);
                }

                // Implicitly don't want to call the perserved group call.
                return;
            }

            foreach (var tokenUri in PerservedTokens)
            {
                SendImpl(tokenUri);
            }
        }

        private void SendImpl(string tokenUri)
        {
            if (!tokenUri.IsNullOrEmpty())
            {
                var result = _client.PostAsync(tokenUri, null).Result;
                if (result.IsSuccessStatusCode) return;
                string returnValue = result.Content.ReadAsStringAsync().Result;
                throw new Exception($"Failed to POST slack call api: ({result.StatusCode}): {returnValue}");
            }
            else
            {
                Log.Debug($"Invoking a slack call towards an endpoint of empty token", _debugLevel);
            }
        }
    }


    /// <summary>
    /// Voice call notification data.
    /// </summary>
    public class NotificationVoiceCall : Notification
    {
        // TODO: Here used the native httpclient. It is preferable to wrap this 
        //  httpclient with raw http request and a specified timeout.
        private static readonly RestClient _client = new RestClient("https://openapi.danmi.com/voice");

        // The accound-related constants that defined on http://sms.danmi.com/voice_notice_overview.html?accountId=717273
        // , which provided by danmi technology.
        private static readonly string _accoundId = "2cbb052010de419f9b0c2ae0f233fb1f";
        private static readonly string _authToken = "62984fb730e2416da3895934dd553dd1";

        // The phone numbers that perserved across voice calls, aiming at invoking a group voice call.
        public static IList<string> PerservedNumbers { get; private set; }

        // The template id defined on http://sms.danmi.com/login.html
        public string TemplateId { get; private set; }
        // The play times of sended content.
        public string PlayTimes { get; private set; } = "2";

        // The parameters that is passing to the template script defined in http://sms.danmi.com/login.html
        public List<string> TemplateParams { get; private set; }

        // The phone numbers that active in one voice call, aiming at an immediate notification.
        public List<string> ImmediateNumbers { get; private set; }

        /// <summary>
        /// Static constructor for registering the default call phone numbers group.
        /// </summary>
        static NotificationVoiceCall()
        {
            var defaultNumbers = new List<string> {"18612568458",  //liyang 
                                                            "13436339690",  //hetao
                                                            "18711621032"}; //weixin

            PerservedNumbers = new List<string>(Config.GetValue<List<string>>("voice-call-saved-numbers", defaultNumbers));
            foreach (var phoneNumber in PerservedNumbers)
            {
                if (!Validate.ChineseMobilePhone(phoneNumber))
                {
                    throw new ArgumentException($"The reserved phone number is invalid: ${phoneNumber}");
                }
            }
        }

        public static void ResetPerservedNumbers(IList<string> inputNumbers)
        {
            if (!PerservedNumbers.IsNullOrEmpty()) PerservedNumbers.Clear();
            if (PerservedNumbers is List<string>)
            {
                ((List<string>)PerservedNumbers).AddRange(inputNumbers);
            }
            else
            {
                foreach (var t in inputNumbers)
                {
                    PerservedNumbers.Add(t);
                }
            }
        }
        /// <summary>
        /// Default constructor for sending an voice call notification
        /// </summary>
        /// <param name="token">Token endpoint for this slack call</param>
        public NotificationVoiceCall(string templateId, IList<string> templateParams)
        {
            TemplateId = templateId;
            TemplateParams = new List<string>();
            ((List<string>)TemplateParams).AddRange(templateParams);
        }
        
        /// <summary>
        /// Default constructor for sending an voice call notification
        /// </summary>
        /// <param name="token">Token endpoint for this slack call</param>
        public NotificationVoiceCall(string phoneNumber, string templateId, IEnumerable<string> templateParams)
        {
            if (!Validate.ChineseMobilePhone(phoneNumber))
            {
                throw new ArgumentException($"Invalid chinese mobile phone number: ${phoneNumber}");
            }

            ImmediateNumbers = new List<string>();
            ImmediateNumbers.Add(phoneNumber);
            TemplateId = templateId;
            TemplateParams = new List<string>();
            ((List<string>)TemplateParams).AddRange(templateParams);
        }

        /// <summary>
        /// Default constructor for sending an voice call notification
        /// </summary>
        public NotificationVoiceCall(IEnumerable<string> phoneNumbers, string templateId, IEnumerable<string> templateParams)
        {
            ImmediateNumbers = new List<string>();
            ImmediateNumbers.AddRange(phoneNumbers);
            TemplateId = templateId;
            TemplateParams = new List<string>();
            TemplateParams.AddRange(templateParams);
        }

        public override void Send()
        {
            if (!ImmediateNumbers.IsNullOrEmpty())
            {
                foreach (var phoneNumber in ImmediateNumbers)
                {
                    SendImpl(phoneNumber);
                }

                // Implicitly don't want to call the preserved group call when it is an immediate voice notify.
                return;
            }

            foreach (var savedPhoneNumber in PerservedNumbers)
            {
                SendImpl(savedPhoneNumber);
            }
        }

        private RestRequest AssembleRequest(string sourceUrl, string phoneNumber)
        {
            var request = new RestRequest(sourceUrl, Method.Post);
            request.Timeout = 3000;
            request.AddHeader("Content-type", "application/x-www-form-urlencoded");
            request.AddHeader("charset", "UTF-8");
            request.AddParameter("accountSid", _accoundId, ParameterType.GetOrPost);
            request.AddParameter("called", phoneNumber, ParameterType.GetOrPost);
            request.AddParameter("templateId", TemplateId, ParameterType.GetOrPost);
            request.AddParameter("param", string.Join(", ", TemplateParams.ToArray()), ParameterType.GetOrPost);
            request.AddParameter("playTimes", PlayTimes, ParameterType.GetOrPost);
            request.AddParameter("timestamp", $"{DateTimeOffset.Now.ToUnixTimeSeconds()}", ParameterType.GetOrPost);
            request.AddParameter("sig", $"{_accoundId}{_authToken}{DateTimeOffset.Now.ToUnixTimeSeconds()}".ToMD5(), ParameterType.GetOrPost);
            return request;
        }

        private void SendImpl(string phoneNumber)
        {
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                try
                {
                    var res = _client.ExecuteAsync<VoiceNotifyResponse>(AssembleRequest("voiceTemplate", phoneNumber)).Result.Data;
                    if (res != null && res.RespCode != 0)
                    {
                        const string urgentNotify = "f3d27243e1e869851b55e0a0e1420ac27e71a9543841d8320a04d8f8f6151104";
                        const string urgentKeyword = "urgent";
                        var FailedVoiceNotify = $"语音提醒功能呼叫失败，错误为：{res.RespCode} | {res.RespDesc}";
                        var ddNotify = new NotificationDingDing(urgentNotify, urgentKeyword, FailedVoiceNotify);
                        ddNotify.Send();

                        throw new Exception($"Error code: ({res.RespCode}) | Message: {res.RespDesc}");
                    }
                }
                catch (Exception e)
                {
                    Log.Error($"Failed in voice notify. {e}");
                    return;
                }

            }
            else
            {
                Log.Error($"Invoking a voice notify towards a NULL phone number");
                return;
            }
        }

        private class VoiceNotifyResponse
        {
            /// <summary>
            /// response status code
            /// </summary>
            /// <remarks>0 for success; otherwise failed</remarks>
            [JsonProperty(PropertyName = "respCode")]
            public int RespCode { get; set; }

            /// <summary>
            /// response error message
            /// </summary>
            [JsonProperty(PropertyName = "respDesc")]
            public string RespDesc { get; set; }
        }
    }

    /// <summary>
    /// Dingding message notification data.
    /// </summary>
    public class NotificationDingDing : Notification
    {
        private static readonly HttpClient _client = new();
        public string Token { get; private set; }
        public string Keyword { get; private set; }
        public string Content { get; set; }


        public NotificationDingDing(string token, string keyword = "", string content = "")
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new ArgumentException("The dingding endpoint cannot be empty");
            }

            Token = token;
            Keyword = keyword ?? string.Empty;
            Content = content ?? string.Empty;
        }

        public override void Send()
        {
            if (string.IsNullOrWhiteSpace(Content))
            {
                throw new Exception($"Sending an empty message to ${Token}");
            }

            if (string.IsNullOrWhiteSpace(Token))
            {
                throw new Exception($"Sending a message to a NULL Endpoint");
            }

            if (!Validate.DingDingEndpoint(Token))
            {
                Token = "https://oapi.dingtalk.com/robot/send?access_token=" + Token;
                // Last resort!
                if (!Validate.DingDingEndpoint(Token))
                {
                    throw new Exception($"The DingDing webhook endpoint is not a valid one: {Token}");
                }
            }

            var ContentToSend = Content;
            if (!string.IsNullOrWhiteSpace(Keyword))
            {
                ContentToSend += "[keyword]:" + Keyword;
            }

            var messageContent = new
            {
                msgtype = "text",
                text = new
                {
                    content = ContentToSend
                }
            };

            using var message = new StringContent(JsonConvert.SerializeObject(messageContent), Encoding.UTF8, "application/json");
            try
            {
                var result = _client.PostAsync(Token, message).Result;
                if (result.IsSuccessStatusCode) return;
                var returnValue = result.Content.ReadAsStringAsync().Result;
                throw new Exception($"Failed to POST data to slack: ({result.StatusCode}): {returnValue}");
            }
            catch (Exception e)
            {
                Log.Error($"Failed in dingding notify. {e}");
            }
        }

    }

    /// <summary>
    /// Mom Dingding message notification data.
    /// </summary>
    public class NotificationMomDingDing : Notification
    {
        private static readonly Dictionary<string, string> TokenMap = new()
        {
            {"MonteCarloDelta", "1004abb9b05cb2ca41464d534cfed869062434383c54f6b81b58f0ea98fe8e3a"},
            {"BinanceStressTest", "debb532842c9c5abe91724d6c656ab1a1f37db08847728cfb4d81db70d89b839"},
            {"ZYDelta2", "ce13e52789df3e21ccc0eb2fdaa7aaf5eab3a3cd5668a6157699e881c6a1edf7"},
            {"YX", "ee6685d8bfe2a5636d47457a2c0c192e8360e6f3aaadd424decc9b96a142ece9"},
            {"YXt", "debb532842c9c5abe91724d6c656ab1a1f37db08847728cfb4d81db70d89b839"},
            {"ActionRequired", "ffc3b3c5a1571e5d5137e5a2803f513c073c4eb0fd6be2525d625457f035a76b"},
            {"Delta4","eb2ea5a035f43e7f06a4e8c4b573933171ec98c5d12f7ac56bf0db9cb546d08e"},
            {"Long","12f0189112404ee0b9d5101395891e45394567ac0a85dbfecb7209f1cfa185ad"},
            {"FTX_test", "2aa1df8623c842b98dd8a6e7cebb045d6764574ad811151529f3c2a746e82df4"},
            {"FTX1","de86690a68ea537afc106aa964510148098b3ad6954121d79b6adffe4ae33f24"},
            {"FTX_zouyi_yixuan", "a0839346eab6d096aebdf459af2e0756dfa1b795144fde71624bf7e8ba6d1585"},
            {"FTX_Hedge", "e1a28086443eadb52c668a6bc0c13ae19d7a7398461c493d2fbb820931f3dbbe"},
            {"Aggressive", "cb94b4fbaeb3e15c552a3a9bae9b1bb5014cffd6ad10b9e110ccf951d63db67b"},
            {"SError", "53f4c00a7a83b806d3f62b8a36437f8f628f515962133e2cfd6e031678901231"},
            {"DeribitTest", "462c714431845eb3c3c6ed8e9a7359e1f31e53f532ebb9e3fa0a129565e1db59"},
            {"108ZG", "5257eecc313b680f939dd888e9614a74ca2570535c6f4e9b2313e11d5cf22108"},
            {"Arbi", "7d440c669d6c43b9e1f96e12738b152588545120197e30be5678ec673d0b46f4"},
            {"Backtest", "ed86a722e2dea59c35194192e163590249936cf3653957afced21b4410b6e69e"},
            {"ZY_test", "5d54291396f820b529d060e4fd77d410768e6f295bf4346148b0b871692cd7d2"},
            {"ZY_important", "810d6857225a5e2bc780194caabb86d058c97f9363772a24ddb1f8263212e5ce"},
            {"50ETF_paper_message", "5148a6e8a9ad43ac812a620e999b7d7ba7661e9d38eeafeaed9e9a2055932f5b"},
            {"50ETF_paper_error", "e0f0adbd03141c994ae3e812d163434e39c9ab898a75fc53f430960a5279bac9"},
            {"50etf_test_message", "83336a8d1f2325ed79b34cf2993e5ec31d4d9a81d71ec3f78919765c2102f639"},
            {"50etf_test_error", "b1c75be974c3aab2d3df1d9e562e353fbe015776b22fd863dde3249587d3dbb"},
            {"Leverage", "0b1092bec38b187f8836942680d4fdf449954166db92bccca4eaadf4fddf76c4"},
            {"report", "ce92aff86795e475e10acf5eac788101c972d4d8773589c1c6ab1263a2e20bfd"},
            {"straddle", "d5c780c88eeb2a460ca0e1e5820e30baac5d752343542e2985054ceefe3ae921"}
        };

        private static readonly Dictionary<string, string> KeywordMap = new()
        {
            { "Backtest", "Backtest" }
        };

        private readonly string _token;
        private readonly string _keyword;
        private readonly string _message;

        public NotificationMomDingDing(string token, string message, string keyword = null)
        {
            _token = token;
            _keyword = keyword;
            _message = message.Replace('/', '-');
        }

        public override void Send()
        {
            if (_token == "NoMessage")
            {
                return;
            }
            try
            {
                var token = TokenMap.TryGetValue(_token, out var t) ? t : _token;
                var keyword = KeywordMap.TryGetValue(_token, out var k) ? k : _keyword;

                var url = Config.Get("momcrypto-historydata-server");
                if (string.IsNullOrEmpty(url))
                {
                    url = Config.Get("mom_dingding_server", "http://94.74.88.76:7004/");
                }
                url = url.EndsWith("/") ? url[..^1] : url;
                url = $"{url}/ding/send/{_message}/{token}/{keyword}";
                var http = new HttpClient();
                var task = http.GetAsync(url);
                task.Wait();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
