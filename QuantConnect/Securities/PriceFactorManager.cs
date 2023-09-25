using System;
using System.Collections.Generic;
using System.IO;
using Ionic.Zip;

namespace QuantConnect.Securities
{
    public class PriceFactorManager
    {
        private readonly Dictionary<string, StocksPriceFactor> _factors = new();
        private readonly Dictionary<string, ZipEntry> _entryCache = new(StringComparer.OrdinalIgnoreCase);
        private ZipFile _zipFile;
        private readonly object _locker = new();

        public PriceFactorManager(string path)
        {
            LoadFactorData(Path.Combine(path, "china-factor.zip"));
        }

        private void LoadFactorData(string file)
        {
            if (!File.Exists(file))
            {
                return;
            }
            _zipFile = ZipFile.Read(File.OpenRead(file));
            foreach (var entry in _zipFile.Entries)
            {
                _entryCache[entry.FileName] = entry;
            }
        }

        public decimal GetForwardAdjustFactor(string symbol, DateTime date)
        {
            var factor = GetPriceFactor(symbol);
            return factor?.GetForwardAdjustFactor(date) ?? 1m;
        }

        public decimal GetAdjustFactor(string symbol, DateTime date)
        {
            var factor = GetPriceFactor(symbol);
            return factor?.GetAdjustFactor(date) ?? 1m;
        }

        private StocksPriceFactor GetPriceFactor(string symbol)
        {
            lock (_locker)
            {
                if (!_factors.TryGetValue(symbol, out var factor))
                {
                    var file = $"{symbol}.csv";
                    if (_entryCache.TryGetValue(file, out var entry))
                    {
                        factor = new StocksPriceFactor(entry.ReadZipEntry());
                        _factors.Add(symbol, factor);
                    }
                }

                return factor;
            }
        }
    }
}