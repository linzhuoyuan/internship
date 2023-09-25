using System;
using System.Runtime.CompilerServices;

namespace FtxApi.WebSocket.Models
{
    public class FtxTicker
    {
        public decimal? bid { get; set; }
        public decimal? ask { get; set; }

        public decimal? bidSize { get; set; }
        public decimal? askSize { get; set; }

        public decimal? last { get; set; }

        public double time { get; set; }

        public string id { get; set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateTimeOffset GetTime()
        {
            return DateTimeOffset.FromUnixTimeSeconds((long)Math.Truncate(time));
        }
    }
}

//{"bid": 59839.0, "ask": 59840.0, "bidSize": 2.0003, "askSize": 0.1161, "last": 59840.0, "time": 1618247243.5760598}