using System;
using System.Threading;
using Skyline;

namespace Quantmom.Api
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

    public class IdGenerator : IIdGenerator
    {
        private long _value;

        public IdGenerator(int baseValue, int seriesValue)
        {
            _value = new MakeLong(baseValue, seriesValue).Value;
        }

        public long Next() => Interlocked.Increment(ref _value);

        public long Current()
        {
            return _value;
        }
    }
}
