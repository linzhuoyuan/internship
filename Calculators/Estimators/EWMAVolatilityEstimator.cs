using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper.Configuration.Attributes;

namespace Calculators.Estimators
{
    /// <summary>
    /// use r0square as start point, allow empty price list
    /// </summary>
    public class EWMAVolatilityEstimator
    {
        public double CurrentVolatility { get; private set; }
        public IList<double> Volatilities { get; private set; } = new List<double>();
        public IList<double> _returns = new List<double>();
        private double _lambda;
        private int _warmUpPeriods;
        public double _lastPrice;
        private static int _maxPeriodsSaved = 100000;

        public EWMAVolatilityEstimator(IList<double> prices, double lambda)
        {
            _warmUpPeriods = prices.Count - 1;
            _lambda = lambda;
            _lastPrice = prices.LastOrDefault();
            for (var i = 1; i < prices.Count; i++)
            {
                _returns.Add(Math.Log(Convert.ToDouble(prices[i] / prices[i - 1]), Math.E));
            }

            if (!_returns.Any())
            {
                throw new ArgumentException($"Must have at least 2 prices to initialize EWMAVolatilityEstimator!!!");
            }
            else
            {
                CurrentVolatility = _returns.First() * _returns.First();
                Volatilities.Add(CurrentVolatility);
                for (var i = 1; i < _returns.Count; i++)
                {
                    CurrentVolatility = _lambda * CurrentVolatility + (1 - _lambda) * _returns[i] * _returns[i];
                    Volatilities.Add(CurrentVolatility);
                }
            }
        }

        public EWMAVolatilityEstimator(IList<double> returns, double lastPrice, double lambda, string name)
        {
            double[] copyArray = new double[returns.Count];
            returns.CopyTo(copyArray, 0);
            _returns = copyArray.ToList();
            _lastPrice = lastPrice;
            _lambda = lambda;

            if (!_returns.Any())
            {
                throw new ArgumentException($"Must have at least 1 return to initialize EWMAVolatilityEstimator!!!");
            }
            else
            {
                CurrentVolatility = _returns.First() * _returns.First();
                Volatilities.Add(CurrentVolatility);

                for (var i = 1; i < _returns.Count; i++)
                {
                    CurrentVolatility = _lambda * CurrentVolatility + (1 - _lambda) * _returns[i] * _returns[i];
                    Volatilities.Add(CurrentVolatility);
                }
                var lines = new StringBuilder();
                lines.AppendLine("return,volatility");
                for (var i = 0; i < _returns.Count; i++)
                {
                    lines.AppendLine($"{_returns[i]},{Volatilities[i]}");
                }

                var filename = $"{name}_{_lastPrice}_{lambda}_{DateTime.Now:O}.csv".Replace('+', '_').Replace(':', '_');
                // File.WriteAllText(filename, lines.ToString());
            }
        }

        public EWMAVolatilityEstimator(double price, double currentVolatility, double lambda)
        {
            _lambda = lambda;
            _lastPrice = price;
            CurrentVolatility = currentVolatility;
            Volatilities.Add(CurrentVolatility);
        }

        public void AddPrice(double price)
        {
            if (_lastPrice == 0)
            {
                _lastPrice = price;
                return;
            }
            var newReturn = Math.Log(Convert.ToDouble(price / _lastPrice), Math.E);
            _lastPrice = price;
            CurrentVolatility = _lambda * CurrentVolatility + (1 - _lambda) * newReturn * newReturn;
            if (Volatilities.Count > _maxPeriodsSaved)
            {
                Volatilities = new List<double> { CurrentVolatility };
                _returns = new List<double> { newReturn };
            }
            else
            {
                Volatilities.Add(CurrentVolatility);
                _returns.Add(newReturn);
            }
        }

        public void AddPrices(IList<double> prices)
        {
            foreach (var price in prices)
            {
                AddPrice(price);
            }
        }

        public double UpdateEWMAVolatility(double new_price, double last_price)
        {
            // double price = prices.Last();
            // _lastPrice = prices.ElementAt(prices.Count() - 2);
            var newReturn = Math.Log(Convert.ToDouble(new_price / last_price), Math.E);
            CurrentVolatility = Math.Sqrt(_lambda * CurrentVolatility * CurrentVolatility + (1 - _lambda) * newReturn * newReturn);

            return CurrentVolatility;
        }
    }
}
