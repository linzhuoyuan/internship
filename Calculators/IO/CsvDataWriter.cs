using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Calculators.IO
{
    public class CsvDataWriter: IDataWriter
    {
        public void ToCsv<T>(IEnumerable<T> datas, string path, bool append)
        {
            if (!datas.Any())
            {
                return;
            }

            var csvData = new StringBuilder();
            var headers = typeof(T).GetProperties();
            if (!append || !File.Exists(path))
            {
                csvData.AppendLine(string.Join(",", headers.Select(p => p.Name)));
            }

            foreach (var data in datas)
            {
                csvData.AppendLine(string.Join(",", headers.Select(h => (h.GetValue(data, null) ?? "").ToString())));
            }

            if (append)
            {
                File.AppendAllText(path, csvData.ToString());
            }
            else
            {
                File.WriteAllText(path, csvData.ToString());
            }
        }
    }
}
