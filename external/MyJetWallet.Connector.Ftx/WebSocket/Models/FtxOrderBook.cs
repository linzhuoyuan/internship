// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace FtxApi.WebSocket.Models
{
    public class FtxOrderBook
    {
        public double time { get; set; }

        public long checksum { get; set; }

        public string action { get; set; }

        public List<decimal?[]> bids { get; set; }

        public List<decimal?[]> asks { get; set; }

        public string id { get; set; }

        public FtxOrderBook Copy()
        {
            var result = new FtxOrderBook()
            {
                id = id,
                action = action,
                checksum = checksum,
                time = time,
                asks = asks.OrderBy(e => e.GetFtxOrderBookPrice()).ToList(),
                bids = bids.OrderByDescending(e => e.GetFtxOrderBookPrice()).ToList()
            };

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateTimeOffset GetTime()
        {
            return DateTimeOffset.FromUnixTimeSeconds((long)Math.Truncate(time));
        }
    }
}