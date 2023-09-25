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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QLNet;

namespace QuantConnect.Securities.Option
{
    using PricingEngineFuncEx = Func<Symbol, GeneralizedBlackScholesProcess, IPricingEngine>;

    /// <summary>
    /// Static class contains definitions of major option pricing models that can be used in LEAN
    /// </summary>
    /// <remarks>
    /// To introduce particular model into algorithm add the following line to the algorithm's Initialize() method: 
    ///     
    ///     option.PriceModel = OptionPriceModels.BjerksundStensland(); // Option pricing model of choice
    /// 
    /// </remarks>
    public static class SyntheticOptionPriceModels
    {
        private static IQLUnderlyingVolatilityEstimator _underlyingVolEstimator = new ConstantQLUnderlyingVolatilityEstimator();
        private static IQLRiskFreeRateEstimator _riskFreeRateEstimator = new ConstantQLRiskFreeRateEstimator();
        private static IQLDividendYieldEstimator _dividendYieldEstimator = new ConstantQLDividendYieldEstimator();

        private const int _timeStepsBinomial = 100;
        private const int _timeStepsFD = 100;

        /// <summary>
        /// Pricing engine for European vanilla options using analytical formulae. 
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_analytic_european_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static ISyntheticOptionPriceModel BlackScholes()
        {
            return new QLSyntheticOptionPriceModel(process => new AnalyticEuropeanEngine(process));
        }

        /// <summary>
        /// Pricing engine for European vanilla options using MonteCarlo simulation. 
        /// QuantLib reference: http://quantlib.org/reference/class_quant_lib_1_1_analytic_european_engine.html
        /// </summary>
        /// <returns>New option price model instance</returns>
        public static ISyntheticOptionPriceModel MonteCarlo()
        {
            return new QLSyntheticOptionPriceModel(process => new MCEuropeanEngine<PseudoRandom, GeneralStatistics>(process,
                10, null, false, false, 100, null, 100, 42));
        }

    }
}
