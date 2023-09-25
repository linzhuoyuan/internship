using System;
using Accord.Math.Optimization;

namespace QuantConnect.Algorithm.Framework
{
    public static class NelderMeadHelper
    {
        public static NelderMead Create(Func<double[], double> func, int numberOfVariables = 1)
        {
            var solver = new NelderMead(numberOfVariables)
            {
                Function = func
            };
            solver.Minimize();
            return solver;
        }

        public static NelderMead Create(Func<double[], double[], double> func, double[] arg1, int numberOfVariables = 1)
        {
            double FuncInternal(double[] x) => func(x, arg1);
            var solver = new NelderMead(numberOfVariables)
            {
                Function = FuncInternal
            };
            solver.Minimize();
            return solver;
        }

        public static NelderMead Create(
            Func<double[], double[][], double> func,
            double[][] arg1,
            int numberOfVariables = 1)
        {
            double FuncInternal(double[] x) => func(x, arg1);
            var solver = new NelderMead(numberOfVariables)
            {
                Function = FuncInternal
            };
            return solver;
        }
    }
}