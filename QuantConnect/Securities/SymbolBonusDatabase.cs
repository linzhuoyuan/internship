using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace QuantConnect.Securities
{
    // 分红调整改动：更新行权价和合约乘数； writter:lh
    internal static class SymbolBonusDatabase
    {
        private static readonly object DataFolderSymbolBonusDatabaseLock = new object();
        private static IReadOnlyDictionary<string, List<SymbolBonus>> _entries;

        public static bool ContainsKey(string contractID)
        {
            return _entries.ContainsKey(contractID);
        }


        public static bool TryGetSymbolBonusEarliestDate(DateTime date, out string firstDate)
        {
            if (_entries == null)
            {
                FromDataFolder();
            }
            var allDate = new HashSet<string>();
            foreach (var symbolBonusList in _entries.Values)
            {
                foreach (var symbolBonus in symbolBonusList)
                {
                    allDate.Add(symbolBonus.TradeDate);
                }
            }
            var result = allDate.Where(x => DateTime.ParseExact(x, DateFormat.EightCharacter, System.Globalization.CultureInfo.CurrentCulture).CompareTo(date) < 0).OrderByDescending(x => x).FirstOrDefault();
            if (result == null || allDate.Contains(date.ToString(DateFormat.EightCharacter)))
            {
                firstDate = null;
                return false;
            }
            firstDate = result;
            return true;
        }

        public static List<SymbolBonus> GetSymbolBonus(string contractID)
        {
            if (!_entries.TryGetValue(contractID, out var symbolBonus))
            {
                return null;
            }
            return symbolBonus;
        }

        public static SymbolBonus GetSymbolBonus(string contractID, string tradeDate)
        {
            if (contractID == null) { return null; }
            if (!_entries.TryGetValue(contractID, out var symbolBonus))
            {
                return null;
            }
            return symbolBonus.Where(x => x.TradeDate.Equals(tradeDate)).ToList().FirstOrDefault();
        }

        public static void FromDataFolder()
        {
            lock (DataFolderSymbolBonusDatabaseLock)
            {
                // 避免多次读取文件
                if (_entries == null)
                {
                    var directory = Path.Combine(Globals.DataFolder, "symbol-properties");
                    FromCsvFile(Path.Combine(directory, "XDEventMap.csv"));
                }
            }
        }

        private static void FromCsvFile(string file)
        {
            var entries = new Dictionary<string, List<SymbolBonus>>();
            if (!File.Exists(file))
            {
                throw new FileNotFoundException("Unable to locate symbol file: " + file);
            }

            foreach (var line in File.ReadLines(file).Where(x => !x.Contains("_")))
            {
                var entry = FromCsvLine(line, out var key);
                List<SymbolBonus> list = null;
                if (entries.ContainsKey(key))
                {
                    if (entries.TryGetValue(key, out list))
                    {
                        list.Add(entry);
                    }
                }
                else
                {
                    list = new List<SymbolBonus>();
                    list.Add(entry);
                }
                entries[key] = list;
            }
            _entries = entries;
        }

        private static SymbolBonus FromCsvLine(string line, out string key)
        {
            var csv = line.Split(',');
            key = csv[1];
            return new SymbolBonus(
                tradeDate: csv[0],
                contractId: csv[1],
                underlyingTicker: csv[2],
                contractType: csv[3],
                strikePrice: csv[4].ToDecimal(),
                expiryDate: csv[5],
                contractMultiplier: csv[6].ToDecimal());
        }

    }
}
