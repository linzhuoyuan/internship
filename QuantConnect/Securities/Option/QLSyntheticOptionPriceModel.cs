using System;
using QLNet;
using QuantConnect.Securities.Option;
using System.Collections.Generic;
using QuantConnect.Interfaces;


namespace QuantConnect.Securities.Option
{
    using Logging;
    using QuantConnect.Data;
    using QuantConnect.Data.Market;
    using QuantConnect.Securities;
    using PricingEngineFunc = Func<GeneralizedBlackScholesProcess, IPricingEngine>;
    using PricingEngineFuncEx = Func<Symbol, GeneralizedBlackScholesProcess, IPricingEngine>;

    /// <summary>
    /// Provides QuantLib(QL) implementation of <see cref="ISyntheticOptionPriceModel"/> to support major option pricing models, available in QL. 
    /// </summary>
    class QLSyntheticOptionPriceModel : ISyntheticOptionPriceModel
    {
        private readonly PricingEngineFunc _pricingEngineFunc;


        //S：标的资产现价
        //X：执行价
        //r：无风险利率
        //q：连续分红率，Cost of Carry = r-q
        //sigma：波动率
        //t：距离到期时间
        //PutCall：Call/Put
        //public enum EPutCall
        //{
        //    Call,
        //    Put,
        //}

        public EPutCall PutCall
        {
            get;
            set;
        }

        /// <summary>
        /// When enabled, approximates Greeks if corresponding pricing model didn't calculate exact numbers.
        /// The default value is true.
        /// </summary>
        public bool EnableGreekApproximation { get; set; } = true;

        /// <summary>
        /// Method constructs QuantLib option price model with necessary estimators of underlying volatility, risk free rate, and underlying dividend yield
        /// </summary>
        /// <param name="pricingEngineFunc">Function modeled stochastic process, and returns new pricing engine to run calculations for that option</param>
        /// <param name="underlyingVolEstimator">The underlying volatility estimator</param>
        /// <param name="riskFreeRateEstimator">The risk free rate estimator</param>
        /// <param name="dividendYieldEstimator">The underlying dividend yield estimator</param>
        public QLSyntheticOptionPriceModel(PricingEngineFunc pricingEngineFunc)
        {
            _pricingEngineFunc = pricingEngineFunc;
        }

        /// <summary>
        /// Evaluates the specified option contract to compute a theoretical price, IV and greeks
        /// </summary>
        /// <param name="security">The option security object</param>
        /// <param name="slice">The current data slice. This can be used to access other information
        /// available to the algorithm</param>
        /// <param name="contract">The option contract to evaluate</param>
        /// <returns>An instance of <see cref="OptionPriceModelResult"/> containing the theoretical
        /// price of the specified option contract</returns>
        public SyntheticOptionPriceModelResult Evaluate(double S, double X, double q, double r,
                                     double sigma, DateTime expiryDate, EPutCall PutCall)
        {
            try
            {
                // setting up option pricing parameters
                var calendar = new UnitedStates();
                var dayCounter = new Actual365Fixed();

                var maturityDate = expiryDate;
                var underlyingQuoteValue = new SimpleQuote(S);

                var dividendYieldValue = new SimpleQuote(q);
                var dividendYield = new Handle<YieldTermStructure>(new FlatForward(0, calendar, dividendYieldValue, dayCounter));

                var riskFreeRateValue = new SimpleQuote(r);
                var riskFreeRate = new Handle<YieldTermStructure>(new FlatForward(0, calendar, riskFreeRateValue, dayCounter));

                var underlyingVolValue = new SimpleQuote(sigma);
                var underlyingVol = new Handle<BlackVolTermStructure>(new BlackConstantVol(0, calendar, new Handle<Quote>(underlyingVolValue), dayCounter));

                // preparing stochastic process and payoff functions
                var stochasticProcess = new BlackScholesMertonProcess(new Handle<Quote>(underlyingQuoteValue), dividendYield, riskFreeRate, underlyingVol);

                var payoff = new PlainVanillaPayoff(PutCall == EPutCall.Call ? QLNet.Option.Type.Call : QLNet.Option.Type.Put, (double)X);
                // convert underlying price for crypto option


                // creating option QL object
                var option = new VanillaOption(payoff, new EuropeanExercise(maturityDate));


                Settings.setEvaluationDate(maturityDate.AddDays(-30));
                // set up pricing volatility
                var pricingVolValue = new SimpleQuote(sigma);
                var pricingVol = new Handle<BlackVolTermStructure>(new BlackConstantVol(0, calendar, new Handle<Quote>(pricingVolValue), dayCounter));

                // preparing pricing engine QL object
                var stochasticProcessUsePricingVol = new BlackScholesMertonProcess(new Handle<Quote>(underlyingQuoteValue), dividendYield, riskFreeRate, pricingVol);
                option.setPricingEngine(_pricingEngineFunc(stochasticProcessUsePricingVol));
                // running calculations
                var npv = EvaluateOption(option);


                // 希腊数计算 改动
                // 改用隐含波动率计算
                //var underlyingImpliedVolValue = new SimpleQuote(impliedVolatility);
                //var underlyingImpliedVol = new Handle<BlackVolTermStructure>(new BlackConstantVol(0, calendar, new Handle<Quote>(underlyingImpliedVolValue), dayCounter));

                //var stochasticProcessUseImpliedVol = new BlackScholesMertonProcess(new Handle<Quote>(underlyingQuoteValue), dividendYield, riskFreeRate, underlyingImpliedVol);

                //option.setPricingEngine(_pricingEngineFunc(contract.Symbol, stochasticProcessUsePricingVol));



                // function extracts QL greeks catching exception if greek is not generated by the pricing engine and reevaluates option to get numerical estimate of the seisitivity
                Func<Func<double>, Func<double>, decimal> tryGetGreekOrReevaluate = (greek, reevalFunc) => {
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


                Func<Tuple<decimal, decimal>> evalDeltaGamma = () => {
                    try
                    {
                        return Tuple.Create((decimal)option.delta(), (decimal)option.gamma());
                    }
                    catch (Exception)
                    {
                        if (EnableGreekApproximation)
                        {
                            var step = 0.01;
                            var initial = underlyingQuoteValue.value();
                            underlyingQuoteValue.setValue(initial - step);
                            var npvMinus = EvaluateOption(option);
                            underlyingQuoteValue.setValue(initial + step);
                            var npvPlus = EvaluateOption(option);
                            underlyingQuoteValue.setValue(initial);

                            return Tuple.Create((decimal)((npvPlus - npvMinus) / (2 * step)),
                                                (decimal)((npvPlus - 2 * npv + npvMinus) / (step * step)));
                        }
                        else
                            return Tuple.Create(0.0m, 0.0m);
                    }
                };

                Func<double> reevalVega = () => {
                    var step = 0.001;
                    // 希腊数计算 改动
                    // 使用隐含波动率计算Vega
                    var initial = pricingVolValue.value();
                    pricingVolValue.setValue(initial + step);
                    var npvPlus = EvaluateOption(option);
                    pricingVolValue.setValue(initial);

                    return (npvPlus - npv) / step;
                };

                Func<double> reevalTheta = () => {
                    var step = 1.0 / 365.0;

                    Settings.setEvaluationDate(maturityDate.AddDays(-1));
                    var npvMinus = EvaluateOption(option);
                    Settings.setEvaluationDate(maturityDate);

                    return (npv - npvMinus) / step;
                };

                Func<double> reevalRho = () => {
                    var step = 0.001;
                    var initial = riskFreeRateValue.value();
                    riskFreeRateValue.setValue(initial + step);
                    var npvPlus = EvaluateOption(option);
                    riskFreeRateValue.setValue(initial);

                    return (npvPlus - npv) / step;
                };

                // producing output with lazy calculations of IV and greeks

                return new SyntheticOptionPriceModelResult((decimal)npv,
                            new Greeks(evalDeltaGamma,
                                            () => tryGetGreekOrReevaluate(() => option.vega(), reevalVega),
                                            () => tryGetGreekOrReevaluate(() => option.theta(), reevalTheta),
                                            () => tryGetGreekOrReevaluate(() => option.rho(), reevalRho),
                                            () => tryGetGreek(() => option.elasticity())));
            }
            catch (Exception err)
            {
                Log.Debug("QLOptionPriceModel.Evaluate() error: " + err.Message);
                return new SyntheticOptionPriceModelResult(0m, new Greeks());
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
