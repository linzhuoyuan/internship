using System;
using System.Collections.Generic;

namespace Calculators.IO
{
    public class AggregatedDataLoader: IDataLoader
    {
        private static readonly CsvDataLoader _csvDataLoader = new CsvDataLoader();

        public IEnumerable<T> FetchData<T>(string location)
        {
            var dataLoader = ResolveDataLoader(location);
            return dataLoader.FetchData<T>(location);
        }

        private IDataLoader ResolveDataLoader(string location)
        {
            if (location.EndsWith(".csv"))
            {
                return _csvDataLoader;
            }

            throw new ArgumentException($"Data loader for {location} not implemented!");
        }
    }
}
