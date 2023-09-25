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

using Newtonsoft.Json;
using QuantConnect.Util;

namespace QuantConnect.Algorithm.Framework.Alphas.Serialization
{
    /// <summary>
    /// DTO used for serializing an insight that was just generated by an algorithm.
    /// This type does not contain any of the analysis dependent fields, such as scores
    /// and estimated value
    /// </summary>
    public class SerializedInsight
    {
        /// <summary>
        /// See <see cref="Insight.Id"/>
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// See <see cref="Insight.GroupId"/>
        /// </summary>
        [JsonProperty("group-id", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string GroupId { get; set; }

        /// <summary>
        /// See <see cref="Insight.SourceModel"/>
        /// </summary>
        [JsonProperty("source-model", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string SourceModel { get; set; }

        /// <summary>
        /// See <see cref="Insight.GeneratedTimeUtc"/>
        /// </summary>
        [JsonProperty("generated-time")]
        public double GeneratedTime { get; set; }

        /// <summary>
        /// See <see cref="Insight.CloseTimeUtc"/>
        /// </summary>
        [JsonProperty("close-time")]
        public double CloseTime { get; set; }

        /// <summary>
        /// See <see cref="Insight.Symbol"/>
        /// The symbol's security identifier string
        /// </summary>
        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        /// <summary>
        /// See <see cref="Insight.Symbol"/>
        /// The symbol's ticker at the generated time
        /// </summary>
        [JsonProperty("ticker")]
        public string Ticker { get; set; }

        /// <summary>
        /// See <see cref="Insight.Type"/>
        /// </summary>
        [JsonProperty("type")]
        public InsightType Type { get; set; }

        /// <summary>
        /// See <see cref="Insight.ReferenceValue"/>
        /// </summary>
        [JsonProperty("reference")]
        public decimal ReferenceValue { get; set; }

        /// <summary>
        /// See <see cref="Insight.Direction"/>
        /// </summary>
        [JsonProperty("direction")]
        public InsightDirection Direction { get; set; }

        /// <summary>
        /// See <see cref="Insight.Period"/>
        /// </summary>
        [JsonProperty("period")]
        public double Period { get; set; }

        /// <summary>
        /// See <see cref="Insight.Magnitude"/>
        /// </summary>
        [JsonProperty("magnitude", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonConverter(typeof(JsonRoundingConverter))]
        public double? Magnitude { get; set; }

        /// <summary>
        /// See <see cref="Insight.Confidence"/>
        /// </summary>
        [JsonProperty("confidence", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonConverter(typeof(JsonRoundingConverter))]
        public double? Confidence { get; set; }

        /// <summary>
        /// See <see cref="Insight.Weight"/>
        /// </summary>
        [JsonProperty("weight", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public double? Weight { get; set; }

        /// <summary>
        /// See <see cref="InsightScore.IsFinalScore"/>
        /// </summary>
        [JsonProperty("score-final", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool ScoreIsFinal { get; set; }

        /// <summary>
        /// See <see cref="InsightScore.Magnitude"/>
        /// </summary>
        [JsonProperty("score-magnitude", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonConverter(typeof(JsonRoundingConverter))]
        public double ScoreMagnitude { get; set; }

        /// <summary>
        /// See <see cref="InsightScore.Direction"/>
        /// </summary>
        [JsonProperty("score-direction", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonConverter(typeof(JsonRoundingConverter))]
        public double ScoreDirection { get; set; }

        /// <summary>
        /// See <see cref="Insight.EstimatedValue"/>
        /// </summary>
        [JsonProperty("estimated-value", DefaultValueHandling = DefaultValueHandling.Ignore)]
        [JsonConverter(typeof(JsonRoundingConverter))]
        public decimal EstimatedValue { get; set; }

        /// <summary>
        /// Initializes a new default instance of the <see cref="SerializedInsight"/> class
        /// </summary>
        public SerializedInsight()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SerializedInsight "/> class by copying the specified insight
        /// </summary>
        /// <param name="insight">The insight to copy</param>
        public SerializedInsight(Insight insight)
        {
            Id = insight.Id.ToString("N");
            SourceModel = insight.SourceModel;
            GroupId = insight.GroupId?.ToString("N");
            GeneratedTime = Time.DateTimeToUnixTimeStamp(insight.GeneratedTimeUtc);
            CloseTime = Time.DateTimeToUnixTimeStamp(insight.CloseTimeUtc);
            Symbol = insight.Symbol.id.ToString();
            Ticker = insight.Symbol.Value;
            Type = insight.Type;
            ReferenceValue = insight.ReferenceValue;
            Direction = insight.Direction;
            Period = insight.Period.TotalSeconds;
            Magnitude = insight.Magnitude;
            Confidence = insight.Confidence;
            ScoreIsFinal = insight.Score.IsFinalScore;
            ScoreMagnitude = insight.Score.Magnitude;
            ScoreDirection = insight.Score.Direction;
            EstimatedValue = insight.EstimatedValue;
            Weight = insight.Weight;
        }
    }
}