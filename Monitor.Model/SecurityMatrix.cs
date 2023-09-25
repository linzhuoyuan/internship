using System;
using System.Collections.Generic;
using System.Text;
using QLNet;
using System.Threading;
using System.Threading.Tasks;
using QuantConnect.Packets;
using QuantConnect.Securities.Option;
using QuantConnect.Logging;

namespace Monitor.Model
{
    using PricingEngineFunc = Func<GeneralizedBlackScholesProcess, IPricingEngine>;
    using PricingEngineFuncEx = Func<string, GeneralizedBlackScholesProcess, IPricingEngine>;
    
    class SecurityMatrix
    {
        private readonly PricingEngineFuncEx _pricingEngineFunc;

        public SecurityMatrix(PricingEngineFunc pricingEngineFunc)
        {
            _pricingEngineFunc = (symbol, process) => pricingEngineFunc(process);
        }

        /// <summary>
        /// When enabled, approximates Greeks if corresponding pricing model didn't calculate exact numbers.
        /// The default value is true.
        /// </summary>
        public bool EnableGreekApproximation { get; set; } = true;


        public ImmediateOptionPriceModelResult[,] EvaluateMatrix(OptionPriceMarketData data, int[] underlyingChangeSeq, int[] volChangeSeq)
        {
            try
            {
                ImmediateOptionPriceModelResult[,] results = new ImmediateOptionPriceModelResult[underlyingChangeSeq.Length, volChangeSeq.Length];

                // setting up option pricing parameters
                var calendar = new UnitedStates();
                var dayCounter = new Actual365Fixed();

                var DefaultSettlementDays = 1;

                var settlementDate = data.Time.Date.AddDays(DefaultSettlementDays);
                var maturityDate = data.Expiry.Date.AddDays(DefaultSettlementDays);
                //underlyingQuoteValue 根据涨跌幅设置矩阵
                var underlyingQuoteValue = new SimpleQuote((double)data.UnderlyingPrice);

                var dividendYieldValue = new SimpleQuote(0);
                var dividendYield = new Handle<YieldTermStructure>(new FlatForward(0, calendar, dividendYieldValue, dayCounter));

                var riskFreeRateValue = new SimpleQuote(0.03);
                var riskFreeRate = new Handle<YieldTermStructure>(new FlatForward(0, calendar, riskFreeRateValue, dayCounter));

                //定价波动率
                var underlyingVolValue = new SimpleQuote(0.3);
                var underlyingVol = new Handle<BlackVolTermStructure>(new BlackConstantVol(0, calendar, new Handle<Quote>(underlyingVolValue), dayCounter));

                VanillaOption option = null;
                //underlyingImpliedVolValue 根据涨跌幅设置矩阵
                //var underlyingImpliedVolValue = PrepareImpliedVolatility(contract, (double)optionSecurity.Price, underlyingQuoteValue, dividendYield, riskFreeRate, underlyingVol, out option);
                // preparing stochastic process functions
                var stochasticProcess = new BlackScholesMertonProcess(new Handle<Quote>(underlyingQuoteValue), dividendYield, riskFreeRate, underlyingVol);
                var payoff = new PlainVanillaPayoff(data.Right == "call" ? QLNet.Option.Type.Call : QLNet.Option.Type.Put, (double)data.Strike);

                //// creating option QL object
                option = data.OptionStyle == "american" ?
                            new VanillaOption(payoff, new AmericanExercise(settlementDate, maturityDate)) :
                            new VanillaOption(payoff, new EuropeanExercise(maturityDate));

                Settings.setEvaluationDate(settlementDate);

                // preparing pricing engine QL object
                option.setPricingEngine(_pricingEngineFunc(data.Symbol, stochasticProcess));

                var impliedVolatility = option.impliedVolatility((double)data.Price, stochasticProcess);
                //underlyingImpliedVolValue 根据涨跌幅设置矩阵
                var underlyingImpliedVolValue = new SimpleQuote(impliedVolatility);


                //先只标的价格变动并行。波动率暂时不用多线程
                Parallel.For(0, underlyingChangeSeq.Length, i =>
                {

                    //标的价目标参数
                    var uc = 1 + (double)underlyingChangeSeq[i] / 100;
                    var underlyingQuoteValueTarget = new SimpleQuote(underlyingQuoteValue.value() * uc);

                    for (int j = 0; j < volChangeSeq.Length; j++)
                    {
                        //波动率目标参数
                        var vc = (double)volChangeSeq[j] / 100;
                        var underlyingImpliedVolValueTarget = new SimpleQuote(underlyingImpliedVolValue.value() + vc);

                        // 此处更改 underlyingImpliedVolValue 涨跌幅度
                        var underlyingImpliedVol = new Handle<BlackVolTermStructure>(new BlackConstantVol(0, calendar, new Handle<Quote>(underlyingImpliedVolValueTarget), dayCounter));
                        // 此处更改 underlyingQuoteValue 涨跌幅度
                        var stochasticProcessUseImpliedVol = new BlackScholesMertonProcess(new Handle<Quote>(underlyingQuoteValueTarget), dividendYield, riskFreeRate, underlyingImpliedVol);
                        option.setPricingEngine(_pricingEngineFunc(data.Symbol, stochasticProcessUseImpliedVol));

                        // running calculations
                        var npv = EvaluateOption(option);

                        // function extracts QL greeks catching exception if greek is not generated by the pricing engine and reevaluates option to get numerical estimate of the seisitivity
                        Func<Func<double>, Func<double>, decimal> tryGetGreekOrReevaluate = (greek, reevalFunc) =>
                        {
                            try
                            {
                                return (decimal)greek();
                            }
                            catch (Exception)
                            {
                                return EnableGreekApproximation ? (decimal)reevalFunc() : 0.0m;
                            }
                        };

                        // function extracts QL greeks catching exception if greek is not generated by the pricing engine
                        Func<Func<double>, decimal> tryGetGreek = greek => tryGetGreekOrReevaluate(greek, () => 0.0);

                        // function extracts QL IV catching exception if IV is not generated by the pricing engine
                        Func<decimal> tryGetImpliedVol = () =>
                        {
                            try
                            {
                                return (decimal)underlyingImpliedVolValueTarget.value();
                            }
                            catch (Exception err)
                            {
                                Log.Debug("tryGetImpliedVol() error: " + err.Message);
                                return 0m;
                            }
                        };

                        Func<Tuple<decimal, decimal>> evalDeltaGamma = () =>
                        {
                            try
                            {
                                return Tuple.Create((decimal)option.delta(), (decimal)option.gamma());
                            }
                            catch (Exception)
                            {
                                if (EnableGreekApproximation)
                                {
                                    var step = 0.01;
                                    var initial = underlyingQuoteValueTarget.value();
                                    underlyingQuoteValueTarget.setValue(initial - step);
                                    var npvMinus = EvaluateOption(option);
                                    underlyingQuoteValueTarget.setValue(initial + step);
                                    var npvPlus = EvaluateOption(option);
                                    underlyingQuoteValueTarget.setValue(initial);

                                    return Tuple.Create((decimal)((npvPlus - npvMinus) / (2 * step)),
                                                        (decimal)((npvPlus - 2 * npv + npvMinus) / (step * step)));
                                }
                                else
                                    return Tuple.Create(0.0m, 0.0m);
                            }
                        };

                        Func<double> reevalVega = () =>
                        {
                            var step = 0.001;
                            // 希腊数计算 改动
                            // 使用隐含波动率计算Vega
                            var initial = underlyingImpliedVolValueTarget.value();
                            underlyingImpliedVolValueTarget.setValue(initial + step);
                            var npvPlus = EvaluateOption(option);
                            underlyingImpliedVolValueTarget.setValue(initial);

                            return (npvPlus - npv) / step;
                        };

                        Func<double> reevalTheta = () =>
                        {
                            var step = 1.0 / 365.0;

                            Settings.setEvaluationDate(settlementDate.AddDays(-1));
                            var npvMinus = EvaluateOption(option);
                            Settings.setEvaluationDate(settlementDate);

                            return (npv - npvMinus) / step;
                        };

                        Func<double> reevalRho = () =>
                        {
                            var step = 0.001;
                            var initial = riskFreeRateValue.value();
                            riskFreeRateValue.setValue(initial + step);
                            var npvPlus = EvaluateOption(option);
                            riskFreeRateValue.setValue(initial);

                            return (npvPlus - npv) / step;
                        };

                        // producing output with lazy calculations of IV and greeks
                        var result = new ImmediateOptionPriceModelResult((decimal)npv,
                                    tryGetImpliedVol(),
                                    new ImmediateGreeks(evalDeltaGamma(),
                                                    tryGetGreekOrReevaluate(() => option.vega(), reevalVega),
                                                    tryGetGreekOrReevaluate(() => option.theta(), reevalTheta),
                                                    tryGetGreekOrReevaluate(() => option.rho(), reevalRho),
                                                    tryGetGreek(() => option.elasticity())));
                        results[i, j] = result;
                    }
                });
                return results;
            }
            catch (Exception err)
            {
                Log.Debug("QLOptionPriceModel.Evaluate() error: " + err.Message);
                ImmediateOptionPriceModelResult[,] results = new ImmediateOptionPriceModelResult[underlyingChangeSeq.Length, volChangeSeq.Length];
                for (int i = 0; i < underlyingChangeSeq.Length; i++)
                {
                    for (int j = 0; j < volChangeSeq.Length; j++)
                    {
                        results[i, j] = new ImmediateOptionPriceModelResult(0m, new ImmediateGreeks());
                    }
                }
                return results;
            }
        }


        /// <summary>
        /// Runs option evaluation and logs exceptions
        /// </summary>
        /// <param name="option"></param>
        /// <returns></returns>
        private static double EvaluateOption(VanillaOption option)
        {
            try
            {
                var npv = option.NPV();

                if (double.IsNaN(npv) ||
                    double.IsInfinity(npv))
                    npv = 0.0;

                return npv;
            }
            catch (Exception err)
            {
                Log.Debug("QLOptionPriceModel.EvaluateOption() error: " + err.Message);
                return 0.0;
            }
        }
    }
}
