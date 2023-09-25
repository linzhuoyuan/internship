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
using System.Collections.Concurrent;
using System.Collections.Generic;
using QuantConnect.Configuration;
using QuantConnect.Logging;

namespace QuantConnect.Notifications
{
    /// <summary>
    /// Local/desktop implementation of messaging system for Lean Engine.
    /// </summary>
    public class NotificationManager
    {
        private int _count;
        private DateTime _resetTime;
        private const int _rateLimit = 30;
        private readonly bool _liveMode;

        /// <summary>
        /// Public access to the messages
        /// </summary>
        public ConcurrentQueue<Notification> Messages
        {
            get; set;
        }

        /// <summary>
        /// Initialize the messaging system
        /// </summary>
        public NotificationManager(bool liveMode)
        {
            _count = 0;
            _liveMode = liveMode;
            _resetTime = DateTime.Now;
            Messages = new ConcurrentQueue<Notification>();
        }

        /// <summary>
        /// Maintain a rate limit of the notification messages per hour send of roughly 20 messages per hour.
        /// </summary>
        /// <returns>True on under rate limit and acceptable to send message</returns>
        private bool Allow()
        {
            if (DateTime.Now > _resetTime)
            {
                _count = 0;
                _resetTime = DateTime.Now.RoundUp(TimeSpan.FromHours(1));
            }

            if (_count < _rateLimit)
            {
                _count++;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Send an email to the address specified for live trading notifications.
        /// </summary>
        /// <param name="subject">Subject of the email</param>
        /// <param name="message">Message body, up to 10kb</param>
        /// <param name="data">Data attachment (optional)</param>
        /// <param name="address">Email address to send to</param>
        public bool Email(string address, string subject, string message, string data = "")
        {
            if (!_liveMode) return false;
            var allow = Allow();

            if (allow)
            {
                try
                {
                    Messages.Enqueue(new NotificationEmail(address, subject, message, data));
                    return true;
                }
                catch (ArgumentException e)
                {
                    Log.Error($"Failed in creating a request of email for {address} with arguement exception: {e}");
                    return false;
                }
                catch (Exception e)
                {
                    Log.Error($"Failed in creating a request of email for {address}: {e}");
                    return false;
                }
            }

            return allow;
        }

        /// <summary>
        /// Send an SMS to the phone number specified
        /// </summary>
        /// <param name="phoneNumber">Phone number to send to</param>
        /// <param name="message">Message to send</param>
        public bool Sms(string phoneNumber, string message)
        {
            if (!_liveMode) return false;
            var allow = Allow();
            if (allow)
            {
                try
                {
                    Messages.Enqueue(new NotificationSms(phoneNumber, message));
                    return true;
                }
                catch (ArgumentException e)
                {
                    Log.Error($"Failed in creating a request of sms message for {phoneNumber} with arguement exception: {e}");
                    return false;
                }
                catch (Exception e)
                {
                    Log.Error($"Failed in creating a request of sms message for {phoneNumber}: {e}");
                    Log.Error($"\n Source ---\n {e.Source}");
                    Log.Error($"\n Stack Trace ---\n {e.StackTrace}");
                    return false;
                }
            }
            return allow;
        }

        /// <summary>
        /// Place REST POST call to the specified address with the specified DATA.
        /// </summary>
        /// <param name="address">Endpoint address</param>
        /// <param name="data">Data to send in body JSON encoded (optional)</param>
        public bool Web(string address, object data = null)
        {
            if (!_liveMode) return false;
            var allow = Allow();
            if (allow)
            {
                try
                {
                    Messages.Enqueue(new NotificationWeb(address, data));
                    return true;
                }
                catch (ArgumentException e)
                {
                    Log.Error($"Failed in creating a request of REST POST call for {address} with arguement exception: {e}");
                    return false;
                }
                catch (Exception e)
                {
                    Log.Error($"Failed in creating a request of REST POST call for {address}: {e}");
                    Log.Error($"\n Source ---\n {e.Source}");
                    Log.Error($"\n Stack Trace ---\n {e.StackTrace}");
                    return false;
                }
            }
            return allow;
        }


        /// <summary>
        /// Send a slack message to the user specified
        /// </summary>
        /// <param name="user">telegram user to send to</param>
        /// <param name="message">Message to send</param>
        public bool Slack(string token, string message)
        {
            if (!_liveMode) return false;

            try
            {
                Messages.Enqueue(new NotificationSlack(token, message));
                return true;
            }
            catch (ArgumentException e)
            {
                Log.Error($"Failed in creating slack message request for {token} with arguement exception: {e}");
                return false;

            }
            catch (Exception e)
            {
                Log.Error($"Failed in creating slack message request for {token}: {e}");
                Log.Error($"\n Source ---\n {e.Source}");
                Log.Error($"\n Stack Trace ---\n {e.StackTrace}");
                return false;
            }

        }

        public bool Slack(string message)
        {
            string defaultToken = Config.Get("slack-token");
            return Slack(defaultToken, message);
        }


        #region slack pushcall implementation
        /// <summary>
        /// Send a phone call via slack to the endpoint token specified
        /// </summary>
        /// <param name="user">token endpoint to send to</param>
        public bool SlackCall(string token)
        {
            return SlackCall(new List<string> { token });
        }

        public bool SlackCall(IList<string> tokens)
        {

            if (!_liveMode) return false;

            try
            {
                Messages.Enqueue(new NotificationSlackCall(tokens));
                return true;
            }
            catch (ArgumentException e)
            {
                Log.Error($"Failed in creating slack call request for {tokens} with arguement exception: {e}");
                return false;

            }
            catch (Exception e)
            {
                Log.Error($"Failed in creating slack call request for {tokens}: {e}");
                Log.Error($"\n Source ---\n {e.Source}");
                Log.Error($"\n Stack Trace ---\n {e.StackTrace}");
                return false;
            }
        }

        public bool SlackCall()
        {
            string token = Config.Get("slack-call-tokens");
            return SlackCall(token);
        }

        // Call the saved endpoint tokens that in the global region shared with 
        // different algorithm.
        // TODO: Currently the notify manager is still used by algorithm-single-accompany-thread.
        // The multi-algorithm-enabled notification is not implemented, which is this function
        // aiming at. Now it's functionality is the same as using a config-enabled slack call token.
        public bool SlackCallSaved()
        {
            if (!_liveMode) return false;

            try
            {
                Messages.Enqueue(new NotificationSlackCall());
                return true;
            }
            catch (ArgumentException e)
            {
                Log.Error($"Failed in creating slack call request for reserved slack tokens with arguement exception: {e}");
                return false;

            }
            catch (Exception e)
            {
                Log.Error($"Failed in creating slack call request for reserved slack tokens: {e}");
                Log.Error($"\n Source ---\n {e.Source}");
                Log.Error($"\n Stack Trace ---\n {e.StackTrace}");
                return false;
            }
        }

        // Notice: This will update the perserved call endpoint tokesn group.
        public bool SlackCallSaved(IList<string> tokens)
        {
            NotificationSlackCall.ResetPerservedTokens(tokens);
            return SlackCallSaved();
        }
        #endregion

        #region voice notify implementation

        private static readonly Dictionary<TemplateType, string> defaultVoiceTemplateMap = new()
        {
                {TemplateType.PriceDropAlert,   "453116"},
                {TemplateType.SystemAlert,      "453115"},
                {TemplateType.MarginCall,       "453114"}
            };
        protected static string GetTemplateId(TemplateType type)
        {
            return defaultVoiceTemplateMap[type];
        }
        /// <summary>
        /// Send a voice notification over phone to a restful endpoint. 
        /// </summary>
        /// <remark> This sip gateway service is currently provided by danmi </remark>
        public bool VoiceCall(string phoneNumber, TemplateType type, params string[] templateParams)
        {
            return VoiceCall(new[] { phoneNumber }, type, templateParams);
        }

        public bool VoiceCall(string[] phoneNumbers, TemplateType type, params string[] templateParams)
        {
            if (!_liveMode) return false;

            try
            {
                Messages.Enqueue(new NotificationVoiceCall(phoneNumbers, GetTemplateId(type), new List<string>(templateParams)));
                return true;
            }
            catch (ArgumentException e)
            {
                Log.Error($"Failed in creating phone call request for {phoneNumbers} with arguement exception: {e}");
                return false;

            }
            catch (Exception e)
            {
                Log.Error($"Failed in creating phone call request for {phoneNumbers}: {e}");
                Log.Error($"\n Source ---\n {e.Source}");
                Log.Error($"\n Stack Trace ---\n {e.StackTrace}");
                return false;
            }
        }

        public bool VoiceCall(TemplateType type, params string[] templateParams)
        {
            string phoneNumber = Config.Get("voice-notify-number");
            return VoiceCall(phoneNumber, type, templateParams);
        }

        // Call the saved endpoint tokens that in the global region shared with 
        // different algorithm.
        // TODO: Currently the notify manager is still used by algorithm-single-accompany-thread.
        // The multi-algorithm-enabled notification is not implemented, which is this function
        // aiming at. Now it's functionality is the same as using a config-enabled slack call token.
        public bool VoiceCallSaved(TemplateType type, params string[] templateParams)
        {
            if (!_liveMode) return false;

            try
            {
                Messages.Enqueue(new NotificationVoiceCall(GetTemplateId(type), new List<string>(templateParams)));
                return true;
            }
            catch (ArgumentException e)
            {
                Log.Error($"Failed in creating phone call request for reserved phone numbers with arguement exception: {e}");
                return false;

            }
            catch (Exception e)
            {
                Log.Error($"Failed in creating phone call request for reserved phone numbers: {e}");
                Log.Error($"\n Source ---\n {e.Source}");
                Log.Error($"\n Stack Trace ---\n {e.StackTrace}");
                return false;
            }
        }

        // Notice: This will update the perserved call endpoint tokesn group.
        public bool VoiceCallSaved(string[] phoneNumbers, TemplateType type, params string[] templateParams)
        {
            NotificationVoiceCall.ResetPerservedNumbers(phoneNumbers);
            return VoiceCallSaved(type, templateParams);
        }
        #endregion

        #region dingding message implementation
        /// <summary>
        /// Send a dingding message to the webhook endpoint specified
        /// </summary>
        /// <param name="token">ding ding token</param>
        /// <param name="keyword"> An optional keyword according to the security policy of dingding</param>
        /// <param name="message">Message to send</param>
        public bool DingDing(string token, string keyword = "", string message = "")
        {
            if (!_liveMode) return false;

            try
            {
                //if (!_liveMode) 
                //    new NotificationDingDing(token, keyword, message).Send();
                
                Messages.Enqueue(new NotificationDingDing(token, keyword, message));
                return true;
            }
            catch (ArgumentException e)
            {
                Log.Error($"Failed in creating dingding message request for {token} with arguement exception: {e}");
                return false;
            }
            catch (Exception e)
            {
                Log.Error($"Failed in creating dingding message request for {token}: {e}");
                Log.Error($"\n Source ---\n {e.Source}");
                Log.Error($"\n Stack Trace ---\n {e.StackTrace}");
                return false;
            }
        }

        public bool MomDingDing(string message, string token, string keyword = null)
        {
            try
            {
                Messages.Enqueue(new NotificationMomDingDing(token, message, keyword));
                return true;
            }
            catch (ArgumentException e)
            {
                Log.Error($"Failed in creating dingding message request for {token} with argument exception: {e}");
                return false;
            }
            catch (Exception e)
            {
                Log.Error($"Failed in creating dingding message request for {token}: {e}");
                Log.Error($"\n Source ---\n {e.Source}");
                Log.Error($"\n Stack Trace ---\n {e.StackTrace}");
                return false;
            }
        }

        #endregion
    }

    public enum TemplateType
    {
        /// <summary> 
        /// 警告，{1}账户{2}的{3}的价格为{4}，几乎快要回落到0.98倍的上线{5}
        /// </summary>
        PriceDropAlert,

        /// <summary> 
        /// 服务器{1}紧急状态，{2}
        /// </summary>
        SystemAlert,

        /// <summary> 
        /// {1}账户保证金不足
        /// </summary>
        MarginCall
    }
}
