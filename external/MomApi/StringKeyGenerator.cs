using System;
using System.Threading;

namespace Quantmom.Api
{
    public class StringKeyGenerator
    {
        private long _lastKey;

        public void SetLastKey(long lastKey)
        {
            _lastKey = Math.Max(_lastKey, lastKey);
        }

        public void SetLastKey(string lastKey)
        {
            SetLastKey(lastKey.i64ToInt());
        }

        public string GetNextKey()
        {
            return Interlocked.Increment(ref _lastKey).IntToi64();
        }
    }
}