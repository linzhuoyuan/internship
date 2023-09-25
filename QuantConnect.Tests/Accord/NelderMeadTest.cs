using System;
using NUnit.Framework;
using QuantConnect.Algorithm.Framework;

namespace QuantConnect.Tests.Accord
{
    [TestFixture]
    public class NelderMeadTest
    {
        [Test]
        public void DemoTest()
        {
            // Let's say we would like to find the minimum 
            // of the function "f(x) = 10 * (x+1)^2 + y^2".

            // In code, this means we would like to minimize:
            static double Func(double[] x) =>
                10.0 * Math.Pow(x[0] + 1.0, 2.0) + Math.Pow(x[1], 2.0);

            // We can do so using the NelderMead class:
            var solver = NelderMeadHelper.Create(Func, numberOfVariables: 2);

            // Now, we can minimize it with:
            var success = solver.Minimize();

            // And get the solution vector using
            var solution = solver.Solution; // should be (-1, 1)

            // The minimum at this location would be:
            var minimum = solver.Value; // should be 0
            // Which can be double-checked against Wolfram Alpha if there is need:
            Assert.AreEqual(minimum, 0);
            // https://www.wolframalpha.com/input/?i=min+10+*+(x%2B1)%5E2+%2B+y%5E2
        }

        [Test]
        public void Demo2Test()
        {
            // Let's say we would like to find the minimum 
            // of the function "Y = b*X^3 + b*X^5".

            // In code, this means we would like to minimize:
            static double Func(double[] b, double[][] data)
            {
                double value = 0;
                for (var i = 0; i < data[0].Length; i++)
                {
                    value += Math.Pow(data[1][i] - b[0] * Math.Pow(data[0][i], 3) - b[0] * Math.Pow(data[0][i], 5), 2);
                }
                return value;
            }

            // We can do so using the NelderMead class:
            var data = new[]
            {
                new[] { 0d, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                new[] { 0d, 5, 100, 675, 2720, 8125, 19980, 42875, 83200, 149445, 252500 }
            };
            var solver = NelderMeadHelper.Create(Func, data);

            // Now, we can minimize it with:
            var success = solver.Minimize();

            // And get the solution vector using
            var solution = solver.Solution; // should be (-1, 1)
            Assert.AreEqual(Math.Round(solution[0], 1), 2.5);

            // The minimum at this location would be:
            var minimum = solver.Value; // should be 0
            Assert.AreEqual(Math.Round(minimum, 2), 0);

            // Which can be double-checked against Wolfram Alpha if there is need:
            // https://www.wolframalpha.com/input/?i=min+10+*+(x%2B1)%5E2+%2B+y%5E2
        }

        [Test]
        public void Demo3Test()
        {
            // Let's say we would like to find the minimum 
            // of the function "Y = a*X^3 + b*X^5".

            // In code, this means we would like to minimize:
            static double Func(double[] ab, double[][] data)
            {
                double value = 0;
                for (var i = 0; i < data[0].Length; i++)
                {
                    value += Math.Pow(data[1][i] - ab[0] * Math.Pow(data[0][i], 3) - ab[1] * Math.Pow(data[0][i], 5), 2);
                }
                return value;
            }

            // We can do so using the NelderMead class:
            var data = new[]
            {
                new[] { 0d, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                new[] { 0d, 6, 132, 918, 3744, 11250, 27756, 59682, 115968, 208494, 352500 }
            };
            var solver = NelderMeadHelper.Create(Func, data, 2);

            // Now, we can minimize it with:
            var success = solver.Minimize();

            // And get the solution vector using
            var solution = solver.Solution; // should be (-1, 1)
            Assert.AreEqual(Math.Round(solution[0], 1), 2.5);
            Assert.AreEqual(Math.Round(solution[1], 1), 3.5);

            // The minimum at this location would be:
            var minimum = solver.Value; // should be 0
            Assert.AreEqual(Math.Round(minimum, 2), 0);

            // Which can be double-checked against Wolfram Alpha if there is need:
            // https://www.wolframalpha.com/input/?i=min+10+*+(x%2B1)%5E2+%2B+y%5E2
        }
    }
}