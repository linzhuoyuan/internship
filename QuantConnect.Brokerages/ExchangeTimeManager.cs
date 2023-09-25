using System;
using System.Threading;
using NodaTime;
using QuantConnect.Securities;
using Quantmom.Api;
using Skyline;

namespace QuantConnect.Brokerages;

public class ExchangeTimeManager
{
    private readonly IdArray<DateTimeOffset> _exchangeTimes = new(100);
    private readonly DictView<string, TimeSpan> _zoneOffset = new();
    private DateTime _maxExchangeUtcTime = DateTime.MinValue;

    public void AddSymbol(Symbol symbol)
    {
        var offset = symbol.ExchangeTimeZone.GetUtcOffset(SystemClock.Instance.GetCurrentInstant());
        if (_zoneOffset.ContainsKey(symbol.ExchangeTimeZone.Id))
        {
            return;
        }
        _zoneOffset.Add(symbol.ExchangeTimeZone.Id, offset.ToTimeSpan());
    }

    public void SetExchangeTime(Symbol symbol, DateTime exchangeTime)
    {
        try
        {
            if (!_zoneOffset.TryGetValue(symbol.ExchangeTimeZone.Id, out var span))
            {
                return;
            }

            var time = new DateTimeOffset(exchangeTime, span);
            var utcTime = time.ToUniversalTime().DateTime;
            if (utcTime > _maxExchangeUtcTime)
            {
                _maxExchangeUtcTime = utcTime.AddSeconds(1);
                Interlocked.Exchange(ref AblSymbolDatabase.ExchangeUtcTimeTicks, _maxExchangeUtcTime.Ticks);
            }
            _exchangeTimes[span.Hours + 24] = time;
        }
        catch (Exception)
        {
            // ignored
        }
    }

    public DateTimeOffset GetExchangeTime(Symbol symbol)
    {
        if (!_zoneOffset.TryGetValue(symbol.ExchangeTimeZone.Id, out var span))
        {
            throw new InvalidOperationException($"{symbol.ExchangeTimeZone.Id} 未订阅");
        }
        return _exchangeTimes[span.Hours + 24];
    }
}