using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace QuantConnect
{
    public interface IIdGenerator
    {
        long Next();
        long Current();
    }

    public class TickIdGenLocked : IIdGenerator
    {
        private const int Used = 1;
        private const int Unused = 0;

        private int _flag;
        private long _last;

        public TickIdGenLocked()
        {
            _last = DateTime.UtcNow.Ticks;
        }

        public long Next()
        {
            while (Interlocked.CompareExchange(ref _flag, Used, Unused) != Used)
            {
                Thread.Sleep(0);
            }
            var ticks = DateTime.UtcNow.Ticks;
            while (_last == ticks)
            {
                ticks = DateTime.UtcNow.Ticks;
            }
            _last = ticks;
            Interlocked.Exchange(ref _flag, Unused);
            return ticks;
        }

        public long Current()
        {
            return _last;
        }
    }

    /// <summary>
    /// 主用
    /// </summary>
    public class TickBaseIdGen : IIdGenerator
    {
        private long _id;

        public TickBaseIdGen()
        {
            _id = DateTime.UtcNow.Ticks;
        }

        public long Next()
        {
            return Interlocked.Increment(ref _id);
        }

        public long Current()
        {
            return _id;
        }
    }

    public class TickIdGen : IIdGenerator
    {
        private long _last;

        public TickIdGen()
        {
            _last = DateTime.UtcNow.Ticks;
        }
        public long Next()
        {
            var ticks = DateTime.UtcNow.Ticks;
            while (_last == ticks)
            {
                ticks = DateTime.UtcNow.Ticks;
            }
            _last = ticks;
            return ticks;
        }

        public long Current()
        {
            return _last;
        }
    }

}
