using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FtxApi
{
    internal static class FtxHelper
    {
        private static readonly DateTime EpochTime = new(1970, 1, 1, 0, 0, 0);

        public static long GetMillisecondsFromEpochStart()
        {
            return new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
        }

        public static long GetSecondsFromEpochStart(DateTime time)
        {
            return new DateTimeOffset(time).ToUnixTimeSeconds();
        }

        public static DateTime GetExpiryTime(DateTime? time = null)
        {
            var next = DateTime.UtcNow.Date.AddHours(3);
            if (next < DateTime.UtcNow)
            {
                next = next.AddDays(1);
            }

            time ??= DateTime.UtcNow.Date;
            if (time < next)
            {
                return next;
            }

            return time.Value.Date.AddHours(3);
        }

        /// <summary>
        /// Add a parameter
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddParameter(this Dictionary<string, object> parameters, string key, string value)
        {
            parameters.Add(key, value);
        }

        /// <summary>
        /// Add a parameter
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="converter"></param>
        public static void AddParameter(this Dictionary<string, object> parameters, string key, string value, JsonConverter converter)
        {
            parameters.Add(key, JsonConvert.SerializeObject(value, converter));
        }

        /// <summary>
        /// Add a parameter
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddParameter(this Dictionary<string, object> parameters, string key, object value)
        {
            parameters.Add(key, value);
        }

        /// <summary>
        /// Add a parameter
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="converter"></param>
        public static void AddParameter(this Dictionary<string, object> parameters, string key, object value, JsonConverter converter)
        {
            parameters.Add(key, JsonConvert.SerializeObject(value, converter));
        }

        /// <summary>
        /// Add an optional parameter. Not added if value is null
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddOptionalParameter(this Dictionary<string, object> parameters, string key, object? value)
        {
            if (value != null)
                parameters.Add(key, value);
        }

        /// <summary>
        /// Add an optional parameter. Not added if value is null
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="converter"></param>
        public static void AddOptionalParameter(this Dictionary<string, object> parameters, string key, object? value, JsonConverter converter)
        {
            if (value != null)
                parameters.Add(key, JsonConvert.SerializeObject(value, converter));
        }

        /// <summary>
        /// Add an optional parameter. Not added if value is null
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddOptionalParameter(this Dictionary<string, object> parameters, string key, string? value)
        {
            if (value != null)
                parameters.Add(key, value);
        }

        /// <summary>
        /// Add an optional parameter. Not added if value is null
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="converter"></param>
        public static void AddOptionalParameter(this Dictionary<string, object> parameters, string key, string? value, JsonConverter converter)
        {
            if (value != null)
                parameters.Add(key, JsonConvert.SerializeObject(value, converter));
        }

        public static string ToJson(this Dictionary<string, object> parameters, NullValueHandling nullValueHandling = NullValueHandling.Ignore)
        {
            return JsonConvert.SerializeObject(
                parameters,
                Formatting.None,
                new JsonSerializerSettings { NullValueHandling = nullValueHandling });
        }
    }
}
