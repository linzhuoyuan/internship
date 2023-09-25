using System;
using System.Collections.Generic;
using System.Text;

namespace Monitor.Model
{
    public class ImmediateGreeks
    {
        public decimal Delta { get; set; }
        public decimal Gamma { get; set; }
        public decimal Vega { get; set; }
        public decimal Theta { get; set; }
        public decimal Rho { get; set; }
        public decimal Lambda { get; set; }

        /// <summary>
        /// Initializes a new default instance of the <see cref="Greeks"/> class
        /// </summary>
        public ImmediateGreeks()
            : this(0m, 0m, 0m, 0m, 0m, 0m)
        {
        }
        public ImmediateGreeks(decimal delta, decimal gamma, decimal vega, decimal theta, decimal rho, decimal lambda)
        {
            Delta = delta;
            Gamma = gamma;
            Vega = vega;
            Theta = theta;
            Rho = rho;
            Lambda = lambda;
        }

        public ImmediateGreeks(Tuple<decimal, decimal> deltaGamma, decimal vega, decimal theta, decimal rho, decimal lambda)
        {
            Delta = deltaGamma.Item1;
            Gamma = deltaGamma.Item2;
            Vega = vega;
            Theta = theta;
            Rho = rho;
            Lambda = lambda;
        }
    }

    public class ImmediateOptionPriceModelResult
    {
        public ImmediateGreeks Greeks { get; set; }
        public decimal TheoreticalPrice { get; set; }
        public decimal ImpliedVolatility { get; set; }
               
        /// <summary>
        /// Initializes a new instance of the <see cref="OptionPriceModelResult"/> class
        /// </summary>
        /// <param name="theoreticalPrice">The theoretical price computed by the price model</param>
        /// <param name="greeks">The sensitivities (greeks) computed by the price model</param>
        public ImmediateOptionPriceModelResult(decimal theoreticalPrice, ImmediateGreeks greeks)
        {
            TheoreticalPrice = theoreticalPrice;
            ImpliedVolatility = 0m;
            Greeks = greeks;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OptionPriceModelResult"/> class with lazy calculations of implied volatility and greeks
        /// </summary>
        /// <param name="theoreticalPrice">The theoretical price computed by the price model</param>
        /// <param name="impliedVolatility">The calculated implied volatility</param>
        /// <param name="greeks">The sensitivities (greeks) computed by the price model</param>
        public ImmediateOptionPriceModelResult(decimal theoreticalPrice, decimal impliedVolatility, ImmediateGreeks greeks)
        {
            TheoreticalPrice = theoreticalPrice;
            ImpliedVolatility = impliedVolatility;
            Greeks = greeks;
        }
    }

    public class SecurityMatrixResult
    {
        public string Symbol { get; set; }
        public ImmediateOptionPriceModelResult[,] Matrix { get; set; }
    }

}
