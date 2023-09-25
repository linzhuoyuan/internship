using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using QuantConnect.Algorithm.CSharp.LiveStrategy.DataType;

namespace QuantConnect.Algorithm.CSharp.qlnet.tools
{
    internal class BinanceAccountHelper
    {
        private readonly Dictionary<string, string> _data;

        public BinanceAccountHelper(Dictionary<string, string> data)
        {
            _data = data;
        }

        public decimal? Leverage(Symbol symbol)
        {
            if (_data.TryGetValue($"Leverage:{symbol.Value}", out var value))
            {
                return value.ToDecimal();
            }

            return null;
        }

        public decimal? AvailableBalance()
        {
            if (_data.TryGetValue($"UsdFutures:AvailableBalance", out var value))
            {
                return value.ToDecimal();
            }

            return null;
        }

        public decimal? TotalMaintMargin()
        {
            if (_data.TryGetValue($"UsdFutures:TotalMaintMargin", out var value))
            {
                return value.ToDecimal();
            }

            return null;
        }

        public decimal? TotalMarginBalance()
        {
            if (_data.TryGetValue($"UsdFutures:TotalMarginBalance", out var value))
            {
                return value.ToDecimal();
            }

            return null;
        }

        public decimal? TotalWalletBalance()
        {
            if (_data.TryGetValue($"UsdFutures:TotalWalletBalance", out var value))
            {
                return value.ToDecimal();
            }

            return null;
        }

        public decimal? TotalCrossWalletBalance()
        {
            if (_data.TryGetValue($"UsdFutures:TotalCrossWalletBalance", out var value))
            {
                return value.ToDecimal();
            }

            return null;
        }

        public IEnumerable<BinanceFuturesAccountAsset> FuturesAssets()
        {
            if (_data.TryGetValue($"UsdFutures:Assets", out var s))
            {
                return JsonConvert.DeserializeObject<BinanceFuturesAccountAsset[]>(s);
            }
            return Array.Empty<BinanceFuturesAccountAsset>();
        }

        public IEnumerable<BinanceUsdFuturesPosition> FuturesPositions()
        {
            return _data.TryGetValue($"UsdFutures:Positions", out var s)
                ? JsonConvert.DeserializeObject<BinanceUsdFuturesPosition[]>(s)
                : Array.Empty<BinanceUsdFuturesPosition>();
        }

        public IEnumerable<BinanceBalance> GetSpotBalances()
        {
            return _data.TryGetValue("Spot:Balances", out var s)
                ? JsonConvert.DeserializeObject<BinanceBalance[]>(s)
                : Array.Empty<BinanceBalance>();
        }
    }
}
