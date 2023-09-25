using System;
using QuantConnect.Securities;
using Quantmom.Api;
using Convert = System.Convert;

namespace QuantConnect.Brokerages.Mom
{
    /// <summary>
    /// 
    /// </summary>
    public class MomSymbolMapper : ISymbolMapper
    {
        /// <summary>
        /// 
        /// </summary>
        public MomSymbolMapper()
        {

        }

        /// <summary>
        /// 不支持外盘
        /// </summary>
        /// <param name="symbol"></param>
        /// <returns></returns>
        public string GetBrokerageSymbol(Symbol symbol)
        {
            if (symbol == null || string.IsNullOrWhiteSpace(symbol.Value))
                throw new ArgumentException("Invalid symbol: " + (symbol == null ? "null" : symbol.ToString()));


            if (symbol.ID.SecurityType != SecurityType.Equity &&
                symbol.ID.SecurityType != SecurityType.Option &&
                symbol.ID.SecurityType != SecurityType.Future)
                throw new ArgumentException("Invalid security type: " + symbol.ID.SecurityType);

            switch (symbol.ID.SecurityType)
            {
                case SecurityType.Option:
                    return symbol.Underlying.Value;
            }

            return symbol.Value;
        }

        public static string GetQCUnderlying(string underlyingSymbol)
        {
            var items = underlyingSymbol.Split('.');
            return items.Length > 1
                ? $"{items[1]}{items[0]}".ToUpper()
                : underlyingSymbol;
        }

        private static string GetStockTicker(string market, string symbol)
        {
            return market switch
            {
                Market.SSE => $"sh{symbol}",
                Market.SZSE => $"sz{symbol}",
                Market.BSE => $"bj{symbol}",
                _ => symbol
            };
        }

        /// <summary>
        /// 获取Mom合约的Symbol
        /// </summary>
        /// <param name="instrument"></param>
        /// <returns></returns>
        public Symbol GetSymbol(MomInstrument instrument)
        {
            if (instrument == null)
                throw new ArgumentException("Invalid Instrument");

            var secType = MomBrokerage.GetQCSecurityType(instrument.ProductClass);
            var market = MomBrokerage.ConvertQcExchangeId(instrument.Exchange);

            var expirationData = instrument.GetExpiredDateTime();
            if (Market.IsChinaMarket(market) && expirationData.TimeOfDay == TimeSpan.Zero)
                expirationData = expirationData.AddHours(15);

            var optionRight = MomBrokerage.GetQCOptionRight(instrument.OptionsType);
            var strikePrice = Convert.ToDecimal(instrument.StrikePrice);

            switch (secType)
            {
                case SecurityType.Future:
                    {
                        var symbol = Symbol.CreateFuture(instrument.ExchangeSymbol, market, expirationData);
                        symbol.SymbolProperties = new SymbolProperties(string.Empty, "USD", instrument.VolumeMultiple, instrument.PriceTick, 1);
                        return symbol;
                    }

                case SecurityType.Option:
                    {
                        var symbol = Symbol.CreateOption(
                            GetQCUnderlying(instrument.UnderlyingSymbol),
                            market,
                            instrument.AmericanOption() ? OptionStyle.American : OptionStyle.European,
                            optionRight,
                            strikePrice,
                            expirationData,
                            instrument.ExchangeSymbol);
                        symbol.SymbolProperties = new SymbolProperties(string.Empty, "USD", instrument.VolumeMultiple, instrument.PriceTick, 1);
                        return symbol;
                    }
                default:
                    {
                        var ticker = Market.IsChinaMarket(market)
                            ? GetStockTicker(market, instrument.ExchangeSymbol)
                            : instrument.ExchangeSymbol;
                        var symbol = Symbol.Create(ticker, secType, market);
                        var lotSize = Math.Max(instrument.MinLimitOrderVolume,instrument.MinMarketOrderVolume);
                        if (lotSize < 100)
                        {

                        }

                        symbol.SymbolProperties = new SymbolProperties(string.Empty, "USD", instrument.VolumeMultiple, instrument.PriceTick, lotSize);
                        return symbol;
                    }
            }

            //return GetLeanSymbol(momInstrument.ExchangeSymbol, secType, market, expirationData, strikePrice, optionRight);
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
        public Symbol GetLeanSymbol(string brokerageSymbol, SecurityType securityType, string market, DateTime expirationDate = default, decimal strike = 0, OptionRight optionRight = 0)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
                throw new ArgumentException("Invalid symbol: " + brokerageSymbol);

            if (securityType != SecurityType.Equity &&
                securityType != SecurityType.Option &&
                securityType != SecurityType.Future)
                throw new ArgumentException("Invalid security type: " + securityType);

            try
            {
                switch (securityType)
                {
                    case SecurityType.Future:
                        return Symbol.CreateFuture(brokerageSymbol, market, expirationDate);

                    case SecurityType.Option:
                        return Symbol.CreateOption(brokerageSymbol, market, OptionStyle.European, optionRight, strike, expirationDate);

                }

                return Symbol.Create(brokerageSymbol, securityType, market);
            }
            catch (Exception)
            {
                throw new ArgumentException($"Invalid symbol: {brokerageSymbol}, security type: {securityType}, market: {market}.");
            }

        }
    }
}
