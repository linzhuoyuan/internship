using System;
using System.Collections.Generic;

namespace Calculators.IO
{
    public class AggregatedDataWriter: IDataWriter
    {
        private static readonly CsvDataWriter _csvDataWriter = new CsvDataWriter();

        public void ToCsv<T>(IEnumerable<T> data, string path, bool append)
        {
            var dataWritter = ResolveDataWriter(path);
            dataWritter.ToCsv(data, path, append);
        }

        private IDataWriter ResolveDataWriter(string path)
        {
            if (path.EndsWith(".csv"))
            {
                return _csvDataWriter;
            }

            throw new ArgumentException($"Data Writter for {path} not implemented!");
        }
    }
}
