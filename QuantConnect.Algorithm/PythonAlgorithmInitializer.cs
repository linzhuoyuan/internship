using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QuantConnect.Algorithm
{
    public static class PythonAlgorithmInitializer
    {
        public static void Initialize()
        {
            var currentPath = Path.GetDirectoryName(typeof(QCAlgorithm).Assembly.Location);
            var modules = new[] {
                string.Empty,
                "Alphas",
                "Execution",
                "Portfolio",
                "Risk",
                "Selection"
            };
            var sep = OS.IsLinux ? ":" : ";";
            var pythonPath = new List<string>(
                (Environment.GetEnvironmentVariable("PYTHONPATH") ?? string.Empty).Split(
                    new[] { sep }, StringSplitOptions.RemoveEmptyEntries));
            foreach (var module in modules)
            {
                var path = Path.Combine(currentPath, module);
                if (!pythonPath.Contains(path))
                {
                    pythonPath.Add(path);
                }
            }
            Environment.SetEnvironmentVariable("PYTHONPATH", string.Join(sep, pythonPath));
        }
    }
}
