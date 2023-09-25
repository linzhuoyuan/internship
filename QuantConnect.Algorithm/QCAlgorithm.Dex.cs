using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using OneInch.Net;
using QLNet;
using QuantConnect.Configuration;
using QuantConnect.Logging;
using QuantConnect.Securities.Crypto;

namespace QuantConnect.Algorithm
{
    public class DexSwapInfo
    {
        public decimal Amount;
        public decimal Qty;
        public decimal Price;
        public decimal Fee;
    }

    public class DexSymbol
    {
        public string symbol { get; set; }
        public string base_coin { get; set; }
        public string quote_coin { get; set; }
        public string address { get; set; }
        public int decimals { get; set; }
        public BigInteger unit => new(Math.Pow(10, decimals));
    }

    public partial class QCAlgorithm
    {
        protected OneInchCore OneInchCore;
        protected Dictionary<string, DexSymbol> DexSymbols = new();

        public void DexInit()
        {
            OneInchCore = new OneInchCore(
                Config.Get("dex_wallet_address"),
                Config.Get("dex_net_id"));
            DexLoadSymbol();
        }

        public void DexLoadSymbol()
        {
            var jsonFile = PathHelper.CompletionPath(Config.Get("dex_symbol_file"));
            if (File.Exists(jsonFile))
            {
                throw new FileNotFoundException(jsonFile);
            }
            var symbols = JsonConvert.DeserializeObject<DexSymbol[]>(File.ReadAllText(jsonFile));
            if (symbols == null)
                return;
            DexSymbols = symbols.ToDictionary(n => n.base_coin);
        }

        public DexSwapInfo DexBuy(Crypto crypto, decimal amount, decimal slippage)
        {
            if (!DexSymbols.TryGetValue(crypto.BaseCurrencySymbol, out var symbol))
            {
                throw new ArgumentException($"不支持的合约,{crypto.BaseCurrencySymbol}");
            }
            var inputToken = OneInchCore.UsdcAddress!;
            var outputToken = symbol.address;
            var qty = new BigInteger(amount) * OneInchCore.UsdcUnit;
            var result = OneInchCore.GetSwapInfoAsync(inputToken, outputToken, qty, slippage).Result;
            if (!result.Success)
            {
                throw new InvalidOperationException(result.Error!.error);
            }

            var info = new DexSwapInfo();
            info.Amount = (decimal)(result.Data!.fromTokenAmount / OneInchCore.UsdcUnit);
            info.Qty = (decimal)(result.Data!.toTokenAmount / symbol.unit);
            info.Price = info.Amount / info.Qty;
            var tx = result.Data.tx;
            info.Fee = (decimal)(new BigInteger(tx.gas * tx.gasPrice) / OneInchCore.EthUnit); 
            return info;
        }

        public DexSwapInfo DexSell(Crypto crypto, decimal qty, decimal slippage)
        {
            if (!DexSymbols.TryGetValue(crypto.BaseCurrencySymbol, out var symbol))
            {
                throw new ArgumentException($"不支持的合约,{crypto.BaseCurrencySymbol}");
            }
            var inputToken = symbol.address;
            var outputToken = OneInchCore.UsdcAddress!;
            var qty2 = new BigInteger(qty) * symbol.unit;
            var result = OneInchCore.GetSwapInfoAsync(inputToken, outputToken, qty2, slippage).Result;
            if (!result.Success)
            {
                throw new InvalidOperationException(result.Error!.error);
            }

            var info = new DexSwapInfo();
            info.Amount = (decimal)(result.Data!.toTokenAmount / OneInchCore.UsdcUnit);
            info.Qty = (decimal)(result.Data!.fromTokenAmount / symbol.unit);
            info.Price = info.Amount / info.Qty;
            var tx = result.Data.tx;
            info.Fee = (decimal)(new BigInteger(tx.gas * tx.gasPrice) / OneInchCore.EthUnit);
            return info;
        }
    }
}