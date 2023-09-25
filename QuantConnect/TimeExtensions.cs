using System;
using NodaTime;
using QuantConnect.Securities;

namespace QuantConnect
{
    public static class SecurityExtensions
    {
        public static DateTimeOffset GetLastDataTime(this Security security)
        {
            var exchangeTime = security.cache.LastDataTime;
            if (exchangeTime == DateTime.MinValue || exchangeTime == DateTime.MaxValue)
            {
                return DateTimeOffset.MinValue;
            }
            var offset = security.ExchangeTimeZone.GetUtcOffset(SystemClock.Instance.GetCurrentInstant());
            return new DateTimeOffset(exchangeTime, offset.ToTimeSpan());
        }
    }
}