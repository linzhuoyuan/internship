using CsvHelper;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Calculators.IO
{
    public class CsvDataLoader: IDataLoader
    {
        public IEnumerable<T> FetchData<T>(string filePath)
        {
            var reader = new StreamReader(filePath);
            var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<T>();
        }
    }
}
