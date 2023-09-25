using System;
using System.Collections.Generic;
using System.Globalization;

namespace QuantConnect.Securities
{
    public class StocksPriceFactor
    {
        private readonly IDictionary<DateTime, decimal> _factors = new Dictionary<DateTime, decimal>();

        public StocksPriceFactor(string content)
        {
            foreach (var line in content.Split("\n"))
            {
                if (line.StartsWith("trade_date"))
                {
                    continue;
                }

                var items = line.Split(',', StringSplitOptions.RemoveEmptyEntries);
                if (items.Length <= 1)
                    continue;
                if (!DateTime.TryParseExact(items[0], "yyyyMMdd", null, DateTimeStyles.None, out var date))
                    continue;

                var factor = Convert.ToDecimal(items[1]);
                if (date > LastDate)
                {
                    LastDate = date;
                    LatestFactor = factor;
                }
                FirstDate = date < FirstDate ? date : FirstDate;
                _factors.Add(date, factor);
            }
        }

        public decimal LatestFactor { get; } = 1m;
        public DateTime LastDate { get; } = DateTime.MinValue;
        public DateTime FirstDate { get; } = DateTime.MaxValue;

        public decimal GetForwardAdjustFactor(DateTime date)
        {
            return GetAdjustFactor(date) / LatestFactor;
        }

        public decimal GetAdjustFactor(DateTime date)
        {
            var factor = 1m;
            if (date.Date > LastDate)
            {
                factor = _factors[LastDate];
            }
            else if (date.Date < FirstDate)
            {
                factor = _factors[FirstDate];
            }
            else
            {
                var tradingDay = date.Date;
                while (tradingDay < LastDate)
                {
                    if (_factors.TryGetValue(tradingDay, out factor))
                    {
                        break;
                    }

                    tradingDay = tradingDay.AddDays(1);
                }
                if (tradingDay == LastDate)
                {
                    factor = _factors[LastDate];
                }
            }
            return factor;
        }
    }
}