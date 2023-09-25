using System.Collections.Generic;

namespace Calculators.IO
{
    public interface IDataLoader
    {
        public IEnumerable<T> FetchData<T>(string location);
    }
}
