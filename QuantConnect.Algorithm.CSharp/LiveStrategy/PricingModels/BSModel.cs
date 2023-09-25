using System;
using System.Collections.Generic;

namespace QuantConnect.Algorithm.CSharp.LiveStrategy.PricingModels
{
    /// <summary>
    /// This example demonstrates how to add options for a given underlying equity security.
    /// It also shows how you can prefilter contracts easily based on strikes and expirations, and how you
    /// can inspect the option chain to pick a specific option contract to trade.
    /// </summary>
    /// <meta name="tag" content="using data" />
    /// <meta name="tag" content="options" />
    /// <meta name="tag" content="filter selection" />
    public static class BSModel
    {

        //S：标的资产现价
        //X：执行价
        //r：无风险利率
        //q：连续分红率，Cost of Carry = r-q
        //sigma：波动率
        //t：距离到期时间
        //PutCall：Call/Put
        public enum EPutCall
        {
            Call,
            Put,
        }

        public static EPutCall PutCall
        {
            get;
            set;
        }

        public static double GetOptionValue(double S, double X, double q, double r,
                                     double sigma, double t, EPutCall PutCall)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);
            double d2 = d1 - t_sqrt * sigma;

            switch (PutCall)
            {
                case EPutCall.Call:
                    return S * Math.Exp(-q * t) * N(d1) - X * Math.Exp(-r * t) * N(d2);
                case EPutCall.Put:
                    return -S * Math.Exp(-q * t) * N(-d1) + X * Math.Exp(-r * t) * N(-d2);
                default:
                    return 0.0;
            }
        }

        // provide old delta if using delta decay
        public static double GetDelta(double S, double X, double q, double r,
            double sigma, double t, EPutCall PutCall)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);

            switch (PutCall)
            {
                case EPutCall.Call:
                    return Math.Exp(-q * t) * N(d1);
                case EPutCall.Put:
                    return -Math.Exp(-q * t) * N(-d1);
                default:
                    return 0.0;
            }
        }

        public static double GetGamma(double S, double X, double q, double r,
                                     double sigma, double t)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);

            return Math.Exp(-q * t) * n(d1) / S / t_sqrt / sigma;
        }

        public static Dictionary<string, decimal> ZakDelta(double S, double X, double r, double sigma, double t, double cost, double risk, EPutCall PutCall)
        {
            double K = -4.76 * Math.Pow(cost, 0.78) / Math.Pow(t, 0.02) * Math.Pow(Math.Exp(-r * t) / sigma, 0.25) *
                       Math.Pow(risk * S * S * Math.Abs(GetGamma(S, X, 0, r, sigma, t)), 0.15);
            double sigma_m = sigma * Math.Sqrt(1 + K);
            double H0 = cost / (risk * S * sigma * sigma * t);
            double H1 = 1.12 * Math.Pow(cost, 0.31) * Math.Pow(t, 0.05) * Math.Pow(Math.Exp(-r * t) / sigma, 0.25) *
                        Math.Pow(Math.Abs(GetGamma(S, X, 0, r, sigma, t)) / risk, 0.5);
            decimal delta_up = Convert.ToDecimal(GetDelta(S, X, 0, r,
                sigma_m, t, PutCall) + H1 + H0);
            decimal delta_down = Convert.ToDecimal(GetDelta(S, X, 0, r,
                sigma_m, t, PutCall) - H1 - H0);
            decimal delta = Convert.ToDecimal(GetDelta(S, X, 0, r,
                sigma_m, t, PutCall));

            Dictionary<string, decimal> ret = new Dictionary<string, decimal>();
            ret.Add("delta_up", delta_up);
            ret.Add("delta_down", delta_down);
            ret.Add("delta", delta);
            return ret;
        }
        public static Dictionary<string, decimal> ZakDeltaMulti(double S, double r, double sigma, double t, double cost, double risk, double delta_multi, double gamma_multi)
        {
            double K = -4.76 * Math.Pow(cost, 0.78) / Math.Pow(t, 0.02) * Math.Pow(Math.Exp(-r * t) / sigma, 0.25) *
                       Math.Pow(risk * S * S * Math.Abs(gamma_multi), 0.15);
            double sigma_m = sigma * Math.Sqrt(1 + K);
            double H0 = cost / (risk * S * sigma * sigma * t);
            double H1 = 1.12 * Math.Pow(cost, 0.31) * Math.Pow(t, 0.05) * Math.Pow(Math.Exp(-r * t) / sigma, 0.25) *
                        Math.Pow(Math.Abs(gamma_multi) / risk, 0.5);
            decimal delta_up = Convert.ToDecimal(delta_multi + H1 + H0);
            decimal delta_down = Convert.ToDecimal(delta_multi - H1 - H0);
            decimal delta = Convert.ToDecimal(delta_multi);

            Dictionary<string, decimal> ret = new Dictionary<string, decimal>();
            ret.Add("delta_up", delta_up);
            ret.Add("delta_down", delta_down);
            ret.Add("delta", delta);
            return ret;
        }


        public static double GetTheta(double S, double X, double q, double r,
                                     double sigma, double t, EPutCall PutCall)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);
            double d2 = d1 - t_sqrt * sigma;

            double part1 = S * sigma * Math.Exp(-q * t) * n(d1) / 2.0 / t_sqrt;
            double part2 = -q * S * Math.Exp(-q * t);
            double part3 = r * X * Math.Exp(-r * t);
            switch (PutCall)
            {
                case EPutCall.Call:
                    return -part1 - part2 * N(d1) - part3 * N(d2);
                case EPutCall.Put:
                    return -part1 + part2 * N(-d1) + part3 * N(-d2);
                default:
                    return 0.0;
            }
        }

        public static double GetVega(double S, double X, double q, double r,
                                     double sigma, double t)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);

            return S * Math.Exp(-q * t) * n(d1) * t_sqrt;
        }

        public static double GetRho(double S, double X, double q, double r,
                                     double sigma, double t, EPutCall PutCall)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);
            double d2 = d1 - t_sqrt * sigma;

            switch (PutCall)
            {
                case EPutCall.Call:
                    return t * X * Math.Exp(-r * t) * N(d2);
                case EPutCall.Put:
                    return -t * X * Math.Exp(-r * t) * N(-d2);
                default:
                    return 0.0;
            }
        }
        public static double GetVanna(double S, double X, double q, double r,
                                     double sigma, double t)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);
            double d2 = d1 - t_sqrt * sigma;

            double v = GetVega( S,  X,  q,  r, sigma,  t);
            return v/S * (1 - d1/sigma/t_sqrt);
            
        }
        public static double GetCharm(double S, double X, double q, double r,
                                     double sigma, double t, EPutCall PutCall)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);
            double d2 = d1 - t_sqrt * sigma;

            double part1 = -q * Math.Exp(-q * t) * N(d1);
            double part1_2 = q * Math.Exp(-q * t) * N(-d1);
            double part2 = Math.Exp(-q * t) * n(d1) ;
            double part3 = 2 * (r-q) * t - d2 * sigma * t_sqrt;
            double part4 = 2 * t * sigma * t_sqrt;
            switch (PutCall)
            {
                case EPutCall.Call:
                    return part1 + part2 * part3 / part4;
                case EPutCall.Put:
                    return part1_2 + part2 * part3 / part4;
                default:
                    return 0.0;
            }
        }
        public static double GetSpeed(double S, double X, double q, double r,
                                     double sigma, double t)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);
            double d2 = d1 - t_sqrt * sigma;

            double gamma = GetGamma( S,  X,  q,  r, sigma,  t);
            return -gamma/S * (d1/sigma/t_sqrt + 1);
            
        }
        public static double GetZomma(double S, double X, double q, double r,
                                     double sigma, double t)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);
            double d2 = d1 - t_sqrt * sigma;

            double gamma = GetGamma( S,  X,  q,  r, sigma,  t);
            return gamma * ((d1 * d2) - 1)/ sigma;
            
        }
        public static double GetColor(double S, double X, double q, double r,
                                     double sigma, double t)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);
            double d2 = d1 - t_sqrt * sigma;

            double part1 = -Math.Exp(-q * t) * n(d1);
            double part2 = 2 * S * t * sigma * t_sqrt ;
            double part3 = 2 * q * t + 1;
            double part4 = 2 * (r - q) * t - d2 * sigma * t_sqrt;
            double part5 = d1 / sigma / t_sqrt;
            
            
            return part1 / part2 * (part3 + part4 * part5);
        }
        public static double GetDvegaDtime(double S, double X, double q, double r,
                                     double sigma, double t)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);
            double d2 = d1 - t_sqrt * sigma;

            double part1 = S * Math.Exp(-q * t) * n(d1) * t_sqrt;
            double part2 = q;
            double part3 = (r - q) * d1 / sigma / t_sqrt;
            double part4 = - (1 + d1 * d2) / t_sqrt /2 ;
            return part1 * (part2 + part3 + part4);
        }
        public static double GetVomma(double S, double X, double q, double r,
                                     double sigma, double t)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);
            double d2 = d1 - t_sqrt * sigma;

            double v = GetVega( S,  X,  q,  r, sigma,  t);
            return v* d1 * d2 /sigma;
        }
        public static double GetDualDelta(double S, double X, double q, double r,
                                     double sigma, double t, EPutCall PutCall)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);
            double d2 = d1 - t_sqrt * sigma;

            switch (PutCall)
            {
                case EPutCall.Call:
                    return -Math.Exp(-r * t) * N(d2);
                case EPutCall.Put:
                    return Math.Exp(-r * t) * N(-d2);
                default:
                    return 0.0;
            }
        }
        public static double GetDualGamma(double S, double X, double q, double r,
                                     double sigma, double t)
        {
            double t_sqrt = Math.Sqrt(t);
            double sigma2 = sigma * sigma;
            double d1 = (Math.Log(S / X) + (r - q + sigma2 * 0.5) * t) / (t_sqrt * sigma);
            double d2 = d1 - t_sqrt * sigma;

            return -Math.Exp(-r * t) * n(d2) / X / sigma /t_sqrt;
        }
        public static double GetImpliedVol(double S, double X, double q, double r, double optionPrice,
            double t, EPutCall PutCall, double accuracy, int maxIterations)
        {
            if (optionPrice < 0.99 * (S - X * Math.Exp(-r * t)))
                return 0.0;
            double t_sqrt = Math.Sqrt(t);
            double sigma = optionPrice / S / 0.398 / t_sqrt;
            for (int i = 0; i < maxIterations; i++)
            {
                double price = GetOptionValue(S, X, q, r, sigma, t, PutCall);
                double diff = optionPrice - price;
                if (Math.Abs(diff) < accuracy)
                    return sigma;
                double vega = GetVega(S, X, q, r, sigma, t);
                if (vega != 0)
                {
                    sigma = sigma + diff / vega;
                }

            }

            if (sigma < 0 || sigma > 2 || double.IsNaN(sigma))
            {
                return 0.0;
            }

            return sigma;
        }

        // 二分法求隐波
        public static double GetImpliedVolBisection(double S, double X, double q, double r, double optionPrice,
            double t, EPutCall PutCall, double accuracy, int maxIterations)
        {

            double top = 20;
            double floor = 0;
            double sigma = 0.2;

            for (int i = 0; i < maxIterations; i++)
            {
                double price = GetOptionValue(S, X, q, r, sigma, t, PutCall);
                double diff = optionPrice - price;
                if (Math.Abs(diff) < accuracy)
                {
                    return sigma;
                }
                else
                {
                    if (diff > 0)
                    {
                        floor = sigma;
                        sigma = (sigma + top) / 2;
                    }
                    else
                    {
                        top = sigma;
                        sigma = (sigma + floor) / 2;
                    }
                }

            }

            return sigma;
        }

        public static double N(double d)
        {
            /*
			//Bagby, R. J. "Calculating Normal Probabilities." Amer. Math. Monthly 102, 46-49, 1995			
			double part1 = 7.0*Math.Exp(-d*d/2.0);
			double part2 = 16.0*Math.Exp(-d*d*(2.0-Math.Sqrt(2.0)));
			double part3 = (7.0+Math.PI*d*d/4.0)*Math.Exp(-d*d);
			double cumProb = Math.Sqrt(1.0-(part1+part2+part3)/30.0)/2.0;
			if(d>0)
				cumProb = 0.5+cumProb;
			else
				cumProb = 0.5-cumProb;
			return cumProb;
			*/

            //出自Financial Numerical Recipes in C++
            if (d > 6.0)
                return 1.0;
            else if (d < -6.0)
                return 0.0;
            double b1 = 0.31938153;
            double b2 = -0.356563782;
            double b3 = 1.781477937;
            double b4 = -1.821255978;
            double b5 = 1.330274429;
            double p = 0.2316419;
            double c2 = 0.3989423;

            double a = Math.Abs(d);
            double t = 1.0 / (1.0 + a * p);
            double b = c2 * Math.Exp((-d) * (d * 0.5));
            double n = ((((b5 * t + b4) * t + b3) * t + b2) * t + b1) * t;
            n = 1.0 - b * n;
            if (d < 0)
                n = 1.0 - n;
            return n;
        }

        public static double n(double d)
        {
            return 1.0 / Math.Sqrt(2.0 * Math.PI) * Math.Exp(-d * d * 0.5);
        }
    }
}
