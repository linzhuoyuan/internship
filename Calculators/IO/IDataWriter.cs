using System.Collections.Generic;

namespace Calculators.IO
{
    public interface IDataWriter
    {
        public void ToCsv<T>(IEnumerable<T> data, string path, bool append);
    }
}
