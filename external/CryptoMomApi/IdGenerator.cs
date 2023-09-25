using System;
using System.Threading;
using Skyline;

namespace MomCrypto.Api
{
    public interface IIdGenerator
    {
        long Next();
    }

    public class TickIdGenLocked : IIdGenerator
    {
        private const int Used = 1;
        private const int Unused = 0;

        private int _flag;
        private long _last;

        public TickIdGenLocked()
        {
            _last = DateTime.Now.Ticks;
        }
        public long Next()
        {
            while (Interlocked.CompareExchange(ref _flag, Used, Unused) != Used)
            {
                Thread.Sleep(0);
            }
            var ticks = DateTime.Now.Ticks;
            while (_last == ticks)
            {
                ticks = DateTime.Now.Ticks;
            }
            _last = ticks;
            Interlocked.Exchange(ref _flag, Unused);
            return ticks;
        }
    }

    public class TickBaseIdGen : IIdGenerator
    {
        private long _id;

        public TickBaseIdGen()
        {
            _id = DateTime.Now.Ticks;
        }

        public long Next()
        {
            return Interlocked.Increment(ref _id);
        }
    }

    public class TickIdGen : IIdGenerator
    {
        private long _last;

        public TickIdGen()
        {
            _last = DateTime.Now.Ticks;
        }
        public long Next()
        {
            var ticks = DateTime.Now.Ticks;
            while (_last == ticks)
            {
                ticks = DateTime.Now.Ticks;
            }
            _last = ticks;
            return ticks;
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
    }
}
