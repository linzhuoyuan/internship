using System;
using System.Collections.Generic;
using System.Globalization;
using MomCrypto.Api;
using QuantConnect.Securities;

namespace QuantConnect.Brokerages.MomCrypto
{
    /// <summary>
    /// 
    /// </summary>
    public class MomCryptoSymbolMapper : ISymbolMapper
    {
        private static readonly Dictionary<string, Symbol> IndexSymbol = new Dictionary<string, Symbol>();

        /// <summary>
        /// 
        /// </summary>
        public MomCryptoSymbolMapper()
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deribitSymbol"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string ConvertMomSymbolToLeanSymbol(string deribitSymbol)
        {
            if (string.IsNullOrWhiteSpace(deribitSymbol))
                throw new ArgumentException($"Invalid Deribit symbol: {deribitSymbol}");

            // return as it is due to Deribit has similar Symbol format
            return deribitSymbol.Replace("_", "").ToUpper();
        }

        /// <summary>
        /// 不支持外盘
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public string GetBrokerageSymbol(Symbol symbol)
        {
            if (symbol == null || string.IsNullOrWhiteSpace(symbol.Value) || symbol.Value == "?")
                throw new ArgumentException("Invalid symbol: " + (symbol == null ? "null" : symbol.ToString()));

            var brokerageSymbol = ConvertLeanSymbolToMomSymbol(symbol.Value);

            return brokerageSymbol;
        }

        /// <summary>
        /// ConvertLeanSymbolToMomSymbol
        /// </summary>
        /// <param name="leanSymbol"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string ConvertLeanSymbolToMomSymbol(string leanSymbol)
        {
            if (string.IsNullOrWhiteSpace(leanSymbol))
                throw new ArgumentException($"Invalid Lean symbol: {leanSymbol}");

            // return as it is due to Deribit has similar Symbol format
            return leanSymbol.ToUpper();
        }

        /// <summary>
        /// GetSymbolKey
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public static string GetSymbolKey(Symbol symbol)
        {
            if (symbol.ID.Market == Market.Deribit && symbol.SecurityType == SecurityType.Option)
            {
                return $"{symbol.Value}.{symbol.SecurityType}.{symbol.ID.Market}".ToLower();
            }
            return $"{symbol.ID.Symbol}.{symbol.SecurityType}.{symbol.ID.Market}".ToLower();
        }

        /// <summary>
        /// 获取Mom合约的Symbol
        /// </summary>
        /// <param name="instrument"></param>
        /// <returns></returns>
        public static Symbol CreateSymbol(MomInstrument instrument)
        {
            if (instrument == null)
                throw new ArgumentException("Invalid Instrument");

            var securityType = MomCryptoBrokerage.GetSecurityType(instrument.ProductClass);
            var market = instrument.Market;
            market = MomCryptoBrokerage.ConvertMarket(market);
            var lotSize = instrument.MinLimitOrderVolume > instrument.MinMarketOrderVolume
                ? instrument.MinLimitOrderVolume
                : instrument.MinMarketOrderVolume;
            //最小下单市值
            var minNotional = instrument.VolumeMultiple;

            var ticker = instrument.ExchangeSymbol.Replace("-", "_").Replace("/", "_");

            switch (securityType)
            {
                case SecurityType.Future:
                    {
                        var expirationData = DateTime.SpecifyKind(instrument.GetExpiredDateTime(), DateTimeKind.Unspecified);
                        var symbol = Symbol.CreateFuture(
                            instrument.ExchangeSymbol,
                            market,
                            expirationData,
                            instrument.ExchangeSymbol);
                        var quoteCurrency = instrument.QuoteCurrency;
                        //if (quoteCurrency == "USDT")
                        //{
                        //    quoteCurrency = "USDT_F";
                        //}

                        if (instrument.Market == Market.FTX && instrument.ExchangeSymbol == "ETH-PERP")
                        {
                        }

                        symbol.SymbolProperties = new SymbolProperties(string.Empty, quoteCurrency, 1, instrument.PriceTick, lotSize, minNotional);
                        return symbol;
                    }
                case SecurityType.Option:
                    {
                        var underlying = MomCryptoBrokerage.ConvertToUnderlyingSymbol(market, instrument.UnderlyingSymbol!);
                        var expirationData = DateTime.SpecifyKind(instrument.GetExpiredDateTime(), DateTimeKind.Utc);
                        var symbol = Symbol.CreateOption(
                            underlying,
                            market,
                            OptionStyle.European,
                            MomCryptoBrokerage.GetOptionRight(instrument.OptionsType),
                            instrument.StrikePrice,
                            expirationData,
                            instrument.Symbol);
                        var quoteCurrency = instrument.QuoteCurrency;
                        if (market == Market.Deribit)
                        {
                            quoteCurrency = instrument.BaseCurrency;
                        }
                        symbol.SymbolProperties = new SymbolProperties(string.Empty, quoteCurrency, instrument.VolumeMultiple, instrument.PriceTick, lotSize, minNotional);
                        return symbol;
                    }
                case SecurityType.Crypto:
                    {
                        if (market == "deribit")
                        {
                            if (lotSize <= 0)
                            {
                                lotSize = 1;
                            }
                            var symbolName = MomCryptoBrokerage.ConvertToUnderlyingSymbol(market, instrument.Symbol);
                            var symbol = Symbol.Create(
                                symbolName, 
                                securityType, 
                                market,
                                symbolName);
                            symbol.SymbolProperties = new SymbolProperties(
                                string.Empty, instrument.QuoteCurrency, 1, instrument.PriceTick, lotSize, minNotional);
                            return symbol;
                        }
                        else
                        {
                            var symbol = Symbol.Create(ticker, securityType, market, ticker);
                            symbol.SymbolProperties = new SymbolProperties(string.Empty, instrument.QuoteCurrency, 1, instrument.PriceTick, lotSize, minNotional);
                            return symbol;
                        }
                    }
            }

            return Symbol.Create(ticker, securityType, market, instrument.Symbol);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="brokerageSymbol"></param>
        /// <param name="securityType"></param>
        /// <param name="market"></param>
        /// <param name="expirationDate"></param>
        /// <param name="strike"></param>
        /// <param name="optionRight"></param>
        /// <returns></returns>
        public Symbol GetLeanSymbol(
            string brokerageSymbol,
            SecurityType securityType,
            string market,
            DateTime expirationDate = default(DateTime),
            decimal strike = 0,
            OptionRight optionRight = 0)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
                throw new ArgumentException($"Invalid symbol: {brokerageSymbol}");

            if (market != Market.Deribit && market != Market.Binance)
                throw new ArgumentException($"Invalid market: {market}");

            if (securityType != SecurityType.Crypto)
                throw new ArgumentException($"Invalid securityType: {securityType}");

            var symbolName = ConvertMomSymbolToLeanSymbol(brokerageSymbol);
            if (IndexSymbol.ContainsKey(symbolName))
            {
                return IndexSymbol[symbolName];
            }
            var symbol = Symbol.Create(ConvertMomSymbolToLeanSymbol(brokerageSymbol), securityType, Market.Deribit);
            IndexSymbol[symbolName] = symbol;
            return symbol;
        }

    }
}
