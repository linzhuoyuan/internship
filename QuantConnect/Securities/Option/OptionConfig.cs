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

using QuantConnect.Orders.OptionExercise;
using QuantConnect.Securities.Option;
using System;

namespace QuantConnect.Securities
{
    /// <summary>
    /// 期权配置实体类
    /// </summary>
    public class OptionConfig
    {
        /// <summary>
        /// Specifies if option contract has physical or cash settlement on exercise
        /// </summary>
        public SettlementType ExerciseSettlement
        {
            get; set;
        }

        /// <summary>
        /// Gets or sets the price model for this option security
        /// </summary>
        public IOptionPriceModel PriceModel
        {
            get;
            private set;
        }

        /// <summary>
        /// Fill model used to produce fill events for this security
        /// </summary>
        public IOptionExerciseModel OptionExerciseModel
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates an instance of the <see cref="OptionConfig"/> class
        /// </summary>
        public OptionConfig(SettlementType settlementType, string exerciseModel, string priceModel)
        {
            ExerciseSettlement = settlementType;
            Type exerciseModelType = Type.GetType("QuantConnect.Orders.OptionExercise." + exerciseModel);
            IOptionExerciseModel iOptionExerciseModel = Activator.CreateInstance(exerciseModelType) as IOptionExerciseModel;
            OptionExerciseModel = iOptionExerciseModel;
           Type priceModelType = Type.GetType("QuantConnect.Securities.Option.OptionPriceModels");
            var method = priceModelType.GetMethod(priceModel);
            PriceModel = (IOptionPriceModel)method.Invoke(priceModel, null);
        }

        /// <summary>
        /// Gets a default instance of the <see cref="OptionConfig"/> class
        /// </summary>
        /// <returns>A default instance of the<see cref="OptionConfig"/> class</returns>
        public static OptionConfig GetDefault()
        {
            return new OptionConfig(SettlementType.PhysicalDelivery, "DefaultExerciseModel", "BlackScholes");
        }
    }
}
