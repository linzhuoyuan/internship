using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using QuantConnect.Configuration;
using QuantConnect.Util;
using RestSharp;

namespace QuantConnect.Securities
{
    // 分红调整改动：更新行权价和合约乘数； writter:lh
    public class SymbolPropertyDatabase
    {
        private static readonly object DataFolderSymbolPropertyDatabaseLock = new object();
        /// <summary>
        /// 
        /// </summary>
        private static IReadOnlyDictionary<string, List<SymbolProperty>> _entries;
        private static IReadOnlyDictionary<string, List<SymbolProperty>> _entriesOkex;
        private static IReadOnlyDictionary<string, List<SymbolProperty>> _entriesDeribit;
        private static IReadOnlyDictionary<string, List<SymbolProperty>> _entriesCffex;
        private static IReadOnlyDictionary<string, List<SymbolProperty>> _entriesBinance;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contractID"></param>
        /// <param name="market"></param>
        /// <returns></returns>
        public static bool ContainsKey(string contractID, string market)
        {
            var entries = SelectEntries(market);
            return entries.ContainsKey(contractID);
        }

        /// <summary>
        /// 
        /// </summary>
        public static List<SymbolProperty> GetSymbolProperty(string contractId, string market)
        {
            var entries = SelectEntries(market);
            if (!entries.TryGetValue(contractId, out var symbolProperty))
            {
                return null;
            }
            return symbolProperty;
        }

        /// <summary>
        /// 
        /// </summary>
        public static bool GetSymbolProperty(string contractId, string tradeDate, string market, out SymbolProperty property)
        {
            property = null;
            var entries = SelectEntries(market);
            if (!entries.TryGetValue(contractId, out var properties))
            {
                return false;
            }

            foreach (var p in properties)
            {
                if (p.TradeDate == tradeDate)
                {
                    property = p;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        public static void FromDataFolder()
        {
            return;
            lock (DataFolderSymbolPropertyDatabaseLock)
            {
                if (!Config.GetBool("live-mode") && Config.GetBool("download-symbol-property", false))
                {
                    DownloadSymbolProperties();
                }
                // 避免多次读取文件
                if (_entries == null)
                {
                    var directory = Path.Combine(Globals.DataFolder, "symbol-properties");
                    _entries = FromCsvFile(Path.Combine(directory, "SymbolPropertie.csv"));
                }

                if (_entriesOkex == null)
                {
                    var directory = Path.Combine(Globals.DataFolder, "symbol-properties");
                    _entriesOkex = FromCsvFile(Path.Combine(directory, "SymbolPropertieOkex.csv"));
                }

                if (_entriesDeribit == null)
                {
                    var directory = Path.Combine(Globals.DataFolder, "symbol-properties");
                    _entriesDeribit = FromCsvFile(Path.Combine(directory, "SymbolPropertieDeribit.csv"));
                }

                if (_entriesCffex == null)
                {
                    var directory = Path.Combine(Globals.DataFolder, "symbol-properties");
                    _entriesCffex = FromCsvFile(Path.Combine(directory, "SymbolPropertieCffex.csv"));
                }

                if (_entriesBinance == null)
                {
                    var directory = Path.Combine(Globals.DataFolder, "symbol-properties");
                    _entriesBinance = FromCsvFile(Path.Combine(directory, "SymbolPropertieBinance.csv"));
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tradeDate"></param>
        /// <param name="market"></param>
        /// <param name="contractType"></param>
        /// <param name="underlying"></param>
        /// <returns></returns>
        public static HashSet<string> GetSymbolByDate(string tradeDate, string market, string contractType, string underlying = "")
        {
            var entries = SelectEntries(market);

            var symbols = new HashSet<string>();
            foreach (var item in entries)
            {
                foreach (var i in item.Value)
                {
                    if (i.TradeDate.Equals(tradeDate) && i.ContractType == contractType)
                    {
                        if (string.IsNullOrEmpty(underlying))
                        {
                            symbols.Add(i.ContractId);
                        }
                        else
                        {
                            if (i.UnderlyingTicker == underlying)
                            {
                                symbols.Add(i.ContractId);
                            }
                        }
                    }
                }
            }
            return symbols;
        }

        public static HashSet<SymbolProperty> GetSymbolPropertyByDate(
            string tradeDate, string market, string contractType, string underlying = "")
        {
            var entries = SelectEntries(market);

            var properties = new HashSet<SymbolProperty>();
            foreach (var item in entries)
            {
                foreach (var i in item.Value)
                {
                    if (i.TradeDate.Equals(tradeDate) && i.ContractType == contractType)
                    {
                        if (string.IsNullOrEmpty(underlying))
                        {
                            properties.Add(i);
                        }
                        else
                        {
                            if (i.UnderlyingTicker == underlying)
                            {
                                properties.Add(i);
                            }
                        }
                    }
                }
            }
            return properties;
        }

        private static IReadOnlyDictionary<string, List<SymbolProperty>> SelectEntries(string market)
        {
            if (market.IsNullOrEmpty())
            {
                throw new Exception("SelectEntries market IsNullOrEmpty");
            }
            switch (market)
            {
                case Market.Okex:
                    return _entriesOkex;
                case Market.Deribit:
                    return _entriesDeribit;
                case Market.CFFEX:
                    return _entriesCffex;
                case Market.Binance:
                    return _entriesBinance;
                default:
                    return _entries;
            }
        }

        private static IReadOnlyDictionary<string, List<SymbolProperty>> FromCsvFile(string file)
        {
            var entries = new Dictionary<string, List<SymbolProperty>>();
            if (!File.Exists(file))
            {
                throw new FileNotFoundException("Unable to locate symbol file: " + file);
            }

            foreach (var line in File.ReadLines(file).Where(x => !x.Contains("_")))
            {
                var entry = FromCsvLine(line, out var key);
                List<SymbolProperty> list;
                if (entries.ContainsKey(key))
                {
                    if (entries.TryGetValue(key, out list))
                    {
                        list.Add(entry);
                    }
                }
                else
                {
                    list = new List<SymbolProperty>();
                    list.Add(entry);
                }
                entries[key] = list;
            }
            return entries;
        }

        private static SymbolProperty FromCsvLine(string line, out string key)
        {
            var csv = line.Split(',');
            key = csv[1];
            var contractMultiplier = 1m;
            if (csv.Length > 7)
            {
                contractMultiplier = csv[7].ToDecimal();
            }
            return new SymbolProperty(
                contractId: csv[1],
                underlyingTicker: csv[2],
                contractType: csv[3],
                optionRight: csv[4],
                strikePrice: csv[5].ToDecimal(),
                expiryDate: csv[6],
                tradeDate: csv[0],
                contractMultiplier: contractMultiplier);
        }

        private static void DownloadFile(RestClient client, RestRequest request, string localFile)
        {
            const int maxTry = 3;
            var tryCount = 0;
            while (tryCount < maxTry)
            {
                ++tryCount;
                try
                {
                    var data = client.DownloadDataAsync(request).Result;
                    if (data == null || data.Length == 0)
                        continue;
                    File.WriteAllBytes(localFile, data);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        private static void DownloadSymbolProperties()
        {
            var dataFolder = Config.Get("data-folder");
            var link = Config.Get("cloud-api-url");
            if (link.IsNullOrEmpty()) return;
            string[] sps = { "SymbolPropertie", "SymbolPropertieDeribit", "SymbolPropertieBinance", "SymbolPropertieCffex" };
            link = link + "obs/download/object?file=/quantconnect/symbol-properties";

            foreach (var sp in sps)
            {
                var path = Path.Combine(dataFolder, "symbol-properties");

                var filename = path + "/" + sp + ".csv";
                var filename1 = path + "/" + sp + "-1.csv";

                var thislink = link + "/" + sp + ".csv";
                var uri = new Uri(thislink);
                var client = uri.Port > 0
                    ? new RestClient(uri.Scheme + "://" + uri.Host + ":" + uri.Port)
                    : new RestClient(uri.Scheme + "://" + uri.Host);

                var request = new RestRequest(uri.PathAndQuery);
                DownloadFile(client, request, filename1);
                if (!File.Exists(filename1))
                    return;

                if (File.Exists(filename))
                {
                    var f1 = new FileInfo(filename1);
                    if (f1.Length > 0)
                    {
                        File.Delete(filename);
                        f1.MoveTo(filename);
                    }
                    else
                    {
                        File.Delete(filename1);
                    }
                }
                else
                {
                    var fileInfo = new FileInfo(filename1);
                    fileInfo.MoveTo(filename);
                }
            }
        }
    }
}
