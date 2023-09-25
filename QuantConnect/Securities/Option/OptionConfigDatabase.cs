/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); 
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuantConnect.Util;

namespace QuantConnect.Securities
{
    /// <summary>
    ///期权模型配置文件 writtter ：lh
    /// </summary>
    public class OptionConfigDatabase
    {
        private static readonly object DataFolderOptionConfigLock = new object();

        private static IReadOnlyDictionary<SecurityDatabaseKey, OptionConfig> _entries;

        /// <summary>
        /// Check whether option config exists for the specified market/symbol
        /// </summary>
        /// <param name="market">The market the exchange resides in, i.e, 'usa', 'fxcm', ect...</param>
        /// <param name="symbol">The particular symbol being traded</param>
        public bool ContainsKey(string market, string symbol)
        {
            var key = new SecurityDatabaseKey(market, symbol, SecurityType.Option);
            return _entries.ContainsKey(key);
        }

        /// <summary>
        /// Check whether option config exists for the specified market/symbo
        /// </summary>
        /// <param name="market">The market the exchange resides in, i.e, 'usa', 'fxcm', ect...</param>
        /// <param name="symbol">The particular symbol being traded (Symbol class)</param>
        public bool ContainsKey(string market, Symbol symbol)
        {
            return ContainsKey(
                market,
                MarketHoursDatabase.GetDatabaseSymbolKey(symbol));
        }

        /// <summary>
        /// Gets the option config  for the specified market/symbol
        /// </summary>
        /// <param name="market">The market the exchange resides in, i.e, 'usa', 'fxcm', ect...</param>
        /// <param name="symbol">The particular symbol being traded</param>
        /// <returns>The option config matching the specified market/symbol or null if not found</returns>
        private static OptionConfig GetOptionConfig(string market, string symbol)
        {
            var key = new SecurityDatabaseKey(market, symbol, SecurityType.Option);
            if (!_entries.TryGetValue(key, out var optionConfig))
            {
                // now check with null symbol key
                if (!_entries.TryGetValue(new SecurityDatabaseKey(market, null, SecurityType.Option), out optionConfig))
                {
                    // no option config found, return object with default property values
                    return OptionConfig.GetDefault();
                }
            }
            return optionConfig;
        }

        /// <summary>
        /// Gets the option config for the specified market/symbol
        /// </summary>
        /// <param name="market">The market the exchange resides in, i.e, 'usa', 'fxcm', ect...</param>
        /// <param name="symbol">The particular symbol being traded (Symbol class)</param>
        /// <returns>The symbol properties matching the specified market/symbol or null if not found</returns>
        public static OptionConfig GetOptionConfig(string market, Symbol symbol)
        {
            return GetOptionConfig(
                market,
                MarketHoursDatabase.GetDatabaseSymbolKey(symbol));
        }

        /// <summary>
        /// Gets the instance of the <see cref="OptionConfigDatabase"/> class produced by reading in the symbol properties
        /// data found in /Data/option/
        /// </summary>
        /// <returns>A <see cref="OptionConfigDatabase"/> class that represents the data in the option folder</returns>
        public static void FromDataFolder()
        {
            lock (DataFolderOptionConfigLock)
            {
                if (_entries == null)
                {
                    var directory = Path.Combine(Globals.DataFolder, "option");
                    _entries = FromCsvFile(Path.Combine(directory, "option-config.csv"));
                }
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="OptionConfigDatabase"/> class by reading the specified csv file
        /// </summary>
        /// <param name="file">The csv file to be read</param>
        /// <returns>A new instance of the <see cref="OptionConfigDatabase"/> class representing the data in the specified file</returns>
        private static IReadOnlyDictionary<SecurityDatabaseKey, OptionConfig> FromCsvFile(string file)
        {
            var entries = new Dictionary<SecurityDatabaseKey, OptionConfig>();

            if (!File.Exists(file))
            {
                throw new FileNotFoundException("Unable to locate option config file: " + file);
            }

            // skip the first header line, also skip #'s as these are comment lines
            foreach (var line in File.ReadLines(file).Where(x => !x.StartsWith("#") && !string.IsNullOrWhiteSpace(x)).Skip(1))
            {
                if (line.StartsWith("#") || line.StartsWith(","))
                {
                    continue;
                }
                SecurityDatabaseKey key;
                var entry = FromCsvLine(line, out key);
                if (entries.ContainsKey(key))
                {
                    throw new Exception("Encountered duplicate key while processing file: " + file + ". Key: " + key);
                }

                entries[key] = entry;
            }
            return entries;
        }

        /// <summary>
        /// Creates a new instance of <see cref="OptionConfig"/> from the specified csv line
        /// </summary>
        /// <param name="line">The csv line to be parsed</param>
        /// <param name="key">The key used to uniquely identify this security</param>
        /// <returns>A new <see cref="OptionConfig"/> for the specified csv line</returns>
        private static OptionConfig FromCsvLine(string line, out SecurityDatabaseKey key)
        {
            var csv = line.Split(',');

            key = new SecurityDatabaseKey(
                market: csv[0],
                symbol: csv[1],
                securityType: SecurityType.Option);

            return new OptionConfig(
                settlementType: (SettlementType)Enum.Parse(typeof(SettlementType), csv[2], true),
                exerciseModel: csv[3],
                priceModel: csv[4]);
        }
    }
}
