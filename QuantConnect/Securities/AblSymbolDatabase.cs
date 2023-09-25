using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using Ionic.Zip;
using CsvHelper;
using Newtonsoft.Json;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace QuantConnect.Securities
{
    public class StocksSplitInfo
    {
        public decimal SplitFactor;
        public DateTime SplitDate;
        public string Symbol;
    }

    public class SplitManager
    {
        private readonly Dictionary<string, StocksSplitInfo> _splits = new();

        public SplitManager(string path)
        {
            LoadSplitData(Path.Combine(path, "china-split.csv"));
        }

        private static string GetSplitKey(string symbol, DateTime date)
        {
            return $"{symbol}_{date:yyyyMMdd}";
        }

        private void LoadSplitData(string file)
        {
            if (!File.Exists(file))
            {
                return;
            }
            using var csv = new CsvReader(new StringReader(File.ReadAllText(file)), CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var symbol = csv["symbol"];
                var date = DateTime.ParseExact(csv["split_date"]!, "yyyyMMdd", null, DateTimeStyles.None);
                decimal factor;
                var factorExpression = csv["split_factor"];
                var items = factorExpression?.Split(':', '/');
                if (items is { Length: > 1 })
                {
                    factor = items[0].ToDecimal() / items[1].ToDecimal();
                }
                else
                {
                    factor = factorExpression.ToDecimal();
                }
                _splits.Add(GetSplitKey(symbol, date), new StocksSplitInfo
                {
                    Symbol = symbol,
                    SplitDate = date,
                    SplitFactor = factor
                });
            }
        }

        public StocksSplitInfo GetSplit(string symbol, DateTime date)
        {
            var key = GetSplitKey(symbol, date);
            _splits.TryGetValue(key, out var split);
            return split;
        }
    }

    public class StocksDividendInfo
    {
        public DateTime PayDate;
        public DateTime ExDividendDate;
        public decimal DividendCash;
    }

    public class StocksDividend
    {
        private readonly Dictionary<DateTime, StocksDividendInfo> _dividends = new();

        public StocksDividend(string content)
        {
            using var csv = new CsvReader(new StringReader(content), CultureInfo.InvariantCulture);
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var exDate = DateTime.ParseExact(csv["ex_date"]!, "yyyyMMdd", null, DateTimeStyles.None);
                var payDate = DateTime.ParseExact(csv["pay_date"]!, "yyyyMMdd", null, DateTimeStyles.None);
                var divCash = csv["div_cash"].ToDecimal();
                var info = new StocksDividendInfo
                {
                    DividendCash = divCash,
                    PayDate = payDate,
                    ExDividendDate = exDate
                };

                if (!_dividends.ContainsKey(payDate))
                {
                    _dividends.Add(payDate, info);
                }

                if (!_dividends.ContainsKey(exDate))
                {
                    _dividends.Add(exDate, info);
                }
            }
        }

        public StocksDividendInfo GetDividend(DateTime date)
        {
            _dividends.TryGetValue(date, out var dividend);
            return dividend;
        }
    }

    public class DividendManager
    {
        private readonly Dictionary<string, StocksDividend> _dividends = new();
        private readonly Dictionary<string, ZipEntry> _entryCache = new(StringComparer.OrdinalIgnoreCase);
        private ZipFile _zipFile;


        public DividendManager(string path)
        {
            LoadDividendData(Path.Combine(path, "china-dividend.zip"));
        }

        private void LoadDividendData(string file)
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

        public StocksDividendInfo GetDividend(string symbol, DateTime date)
        {
            var dividend = GetStocksDividend(symbol);
            return dividend?.GetDividend(date);
        }

        private StocksDividend GetStocksDividend(string symbol)
        {
            if (!_dividends.TryGetValue(symbol, out var dividend))
            {
                var file = $"{symbol}.csv";
                if (_entryCache.TryGetValue(file, out var entry))
                {
                    dividend = new StocksDividend(entry.ReadZipEntry());
                    _dividends.Add(symbol, dividend);
                }
            }

            return dividend;
        }
    }

    public class AblSymbolDatabase
    {
        public static long ExchangeUtcTimeTicks = 0;

        private static readonly Dictionary<string, Func<string, bool>> Markets = new()
        {
            {"usa", n => n == Market.USA},
            {"china", Market.IsChinaMarket},
            {"hongkong", n => n == Market.HKG},
            {"china,hongkong", n => n == Market.HKA},
        };

        private static PriceFactorManager factors;
        private static DividendManager dividends;
        private static SplitManager splits;

        private static readonly ISet<string> indexSet = new HashSet<string>();
        private static readonly ISet<string> stocksSet = new HashSet<string>();
        private static readonly ISet<string> etfSet = new HashSet<string>();

        public static void FromDataFolder()
        {
            LoadIndex();
            LoadEtf();
            LoadStocks();
            LoadFactors();
            LoadDividends();
            LoadSplits();
            LoadMarketHoursDatabase();
            LoadSymbolPropertiesDatabase();
        }

        private static void LoadSplits()
        {
            var splitPath = Path.Combine(Globals.DataFolder, "abl-database");
            splits = new SplitManager(splitPath);
        }

        private static void LoadSymbol(string symbolFile, ISet<string> set)
        {
            if (File.Exists(symbolFile))
            {
                foreach (var line in File.ReadAllLines(symbolFile))
                {
                    if (line.StartsWith("ts_code"))
                    {
                        continue;
                    }

                    var items = line.Split(',');
                    if (items.Length == 0)
                    {
                        continue;
                    }

                    var symbol = items[0];
                    symbol = $"{symbol[^2..]}{symbol[..6]}".ToLower();
                    set.Add(symbol);
                }
            }
        }

        private static void LoadStocks()
        {
            var stocksFile = Path.Combine(Globals.DataFolder, "abl-database", "china-stocks.csv");
            LoadSymbol(stocksFile, stocksSet);
        }

        private static void LoadIndex()
        {
            var indexFile = Path.Combine(Globals.DataFolder, "abl-database", "china-index.csv");
            LoadSymbol(indexFile, indexSet);
        }

        private static void LoadEtf()
        {
            var indexFile = Path.Combine(Globals.DataFolder, "abl-database", "china-etf.csv");
            LoadSymbol(indexFile, etfSet);
        }

        private static void LoadFactors()
        {
            var factorPath = Path.Combine(Globals.DataFolder, "abl-database");
            factors = new PriceFactorManager(factorPath);
        }

        private static void LoadDividends()
        {
            var dividendPath = Path.Combine(Globals.DataFolder, "abl-database");
            dividends = new DividendManager(dividendPath);
        }

        private static void LoadSymbolPropertiesDatabase()
        {
            var file = Path.Combine(Globals.DataFolder, "abl-database", "symbol-properties-database-abl.csv");
            if (!File.Exists(file))
            {
                return;
            }
            var database = SymbolPropertiesDatabase.FromCsvFile(file);
            SymbolPropertiesDatabase.FromDataFolder().Merge(database);
        }

        private static IEnumerable<DateTime> LoadHolidays(string market)
        {
            var set = new HashSet<DateTime>();
            foreach (var m in market.Split(',', StringSplitOptions.TrimEntries))
            {
                var holidaysFile = Path.Combine(Globals.DataFolder, "abl-database", $"{m}-holidays.csv");
                set.UnionWith(LoadHolidaysFromFile(holidaysFile));
            }
            var list = set.ToList();
            list.Sort();
            return list;
        }

        private static IEnumerable<DateTime> LoadHolidaysFromFile(string filename)
        {
            if (!File.Exists(filename))
            {
                return Array.Empty<DateTime>();
            }
            return File.ReadAllLines(filename)
                .Select(n => DateTime.ParseExact(n, "yyyyMMdd", null, DateTimeStyles.None))
                .ToList();
        }

        private static void LoadMarketHoursHolidays(string market, Func<string, bool> check)
        {
            var holidays = LoadHolidays(market);
            if (!holidays.Any())
            {
                return;
            }
            var database = MarketHoursDatabase.FromDataFolder();
            foreach (var (key, value) in database.entries)
            {
                if (check(key.Market))
                {
                    value.ExchangeHours.SetHolidays(holidays);
                }
            }
        }

        private static IDictionary<DateTime, TimeSpan> LoadEarlyClosesFromFile(string earlyClosesFile)
        {
            if (!File.Exists(earlyClosesFile))
            {
                return new Dictionary<DateTime, TimeSpan>();
            }

            var json = File.ReadAllText(earlyClosesFile);
            json = $"{{{json}}}";
            return JsonConvert
                .DeserializeObject<Dictionary<string, TimeSpan>>(json)?
                .ToDictionary(n => DateTime.ParseExact(n.Key, "M/d/yyyy", CultureInfo.InvariantCulture), n => n.Value);
        }

        private static IDictionary<DateTime, TimeSpan> LoadEarlyCloses(string market)
        {
            var dict = new Dictionary<DateTime, TimeSpan>();
            foreach (var m in market.Split(',', StringSplitOptions.TrimEntries))
            {
                var holidaysFile = Path.Combine(Globals.DataFolder, "abl-database", $"{m}-early_closes.csv");
                var data = LoadEarlyClosesFromFile(holidaysFile);
                foreach (var (key, value) in data)
                {
                    if (!dict.ContainsKey(key))
                    {
                        dict.Add(key, value);
                    }
                }
            }

            return dict.ToImmutableSortedDictionary();
        }

        private static void LoadMarketEarlyCloses(string market, Func<string, bool> check)
        {
            var data = LoadEarlyCloses(market);
            if (!data.Any())
            {
                return;
            }

            var database = MarketHoursDatabase.FromDataFolder();
            foreach (var (key, value) in database.entries)
            {
                if (check(key.Market))
                {
                    value.ExchangeHours.SetEarlyCloses(data);
                }
            }
        }

        private static void LoadMarketHoursDatabase()
        {
            var file = Path.Combine(Globals.DataFolder, "abl-database", $"market-hours-database-abl.json");
            if (!File.Exists(file))
            {
                return;
            }
            var database = MarketHoursDatabase.FromFile(file);
            MarketHoursDatabase.FromDataFolder().Merge(database);
            foreach (var (key, func) in Markets)
            {
                LoadMarketHoursHolidays(key, func);
                LoadMarketEarlyCloses(key, func);
            }
        }

        public static bool IsChinaIndex(Symbol symbol)
        {
            return indexSet.Contains(symbol.Value.ToLower());
        }

        public static bool IsChinaEtf(Symbol symbol)
        {
            return !IsChinaIndex(symbol) && !IsChinaStocks(symbol);
        }

        public static bool IsChinaStocks(Symbol symbol)
        {
            return stocksSet.Contains(symbol.Value.ToLower());
        }

        public static decimal GetForwardAdjustFactor(Symbol symbol, DateTime date)
        {
            return factors.GetForwardAdjustFactor(symbol.Value.ToLower(), date);
        }

        public static decimal GetAdjustFactor(Symbol symbol, DateTime date)
        {
            return factors.GetAdjustFactor(symbol.Value.ToLower(), date);
        }

        public static StocksDividendInfo GetDividend(Symbol symbol, DateTime date)
        {
            return dividends.GetDividend(symbol.Value.ToLower(), date);
        }

        public static StocksSplitInfo GetSplit(Symbol symbol, DateTime date)
        {
            return splits.GetSplit(symbol.Value.ToLower(), date);
        }

        public static TickType GetSecurityDataFeed(Symbol symbol)
        {
            return symbol.SecurityType switch
            {
                SecurityType.Crypto when symbol.ID.Market != Market.Binance => TickType.Trade,
                SecurityType.Equity => TickType.Trade,
                _ => TickType.Quote
            };
        }

        public static string GetFeeCurrency(OrderFeeParameters parameters, string defaultCurrency = "USD")
        {
            var security = parameters.Security;
            if (security.Type != SecurityType.Crypto
                || security.SymbolProperties?.QuoteCurrency == null)
            {
                return defaultCurrency;
            }

            if (parameters.Order.Direction == OrderDirection.Buy)
            {
                var pos = security.symbol.Value.Length - security.SymbolProperties.QuoteCurrency.Length;
                return security.symbol.Value[..pos];
            }

            return security.SymbolProperties.QuoteCurrency;
        }
    }
}