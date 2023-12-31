﻿using System.Collections.Immutable;
using System.Reflection;

namespace AblTest;

/// <summary>
/// Provides symbol instances for unit tests
/// </summary>
public static class Symbols
{
    public static readonly Symbol ETF50 = CreateEquitySymbol("sh510050", Market.SSE);
    public static readonly Symbol H600519 = CreateEquitySymbol("600519", Market.HKA);

    public static readonly Symbol SPY = CreateEquitySymbol("SPY");
    public static readonly Symbol AAPL = CreateEquitySymbol("AAPL");
    public static readonly Symbol MSFT = CreateEquitySymbol("MSFT");
    public static readonly Symbol ZNGA = CreateEquitySymbol("ZNGA");
    public static readonly Symbol FXE = CreateEquitySymbol("FXE");
    public static readonly Symbol LODE = CreateEquitySymbol("LODE");
    public static readonly Symbol IBM = CreateEquitySymbol("IBM");
    public static readonly Symbol GOOG = CreateEquitySymbol("GOOG");
    public static readonly Symbol SBIN = CreateEquitySymbol("SBIN", Market.NSE);
    public static readonly Symbol IDEA = CreateEquitySymbol("IDEA", Market.NSE);

    public static readonly Symbol LOW = CreateEquitySymbol("LOW");

    public static readonly Symbol USDJPY = CreateForexSymbol("USDJPY");
    public static readonly Symbol EURUSD = CreateForexSymbol("EURUSD");
    public static readonly Symbol EURGBP = CreateForexSymbol("EURGBP");
    public static readonly Symbol GBPUSD = CreateForexSymbol("GBPUSD");
    public static readonly Symbol GBPJPY = CreateForexSymbol("GBPJPY");

    public static readonly Symbol BTCUSD = CreateCryptoSymbol("BTCUSD");
    public static readonly Symbol LTCUSD = CreateCryptoSymbol("LTCUSD");
    public static readonly Symbol ETHUSD = CreateCryptoSymbol("ETHUSD");
    public static readonly Symbol BTCEUR = CreateCryptoSymbol("BTCEUR");
    public static readonly Symbol ETHBTC = CreateCryptoSymbol("ETHBTC");

    public static readonly Symbol DE10YBEUR = CreateCfdSymbol("DE10YBEUR", Market.FXCM);
    public static readonly Symbol DE30EUR = CreateCfdSymbol("DE30EUR", Market.FXCM);
    public static readonly Symbol XAGUSD = CreateCfdSymbol("XAGUSD", Market.FXCM);
    public static readonly Symbol XAUUSD = CreateCfdSymbol("XAUUSD", Market.FXCM);

    public static readonly Symbol SPY_Option_Chain = CreateOptionsCanonicalSymbol("SPY");
    public static readonly Symbol SPY_C_192_Feb19_2016 = CreateOptionSymbol("SPY", OptionRight.Call, 192m, new DateTime(2016, 02, 19));
    public static readonly Symbol SPY_P_192_Feb19_2016 = CreateOptionSymbol("SPY", OptionRight.Put, 192m, new DateTime(2016, 02, 19));

    public static readonly Symbol Fut_SPY_Feb19_2016 = CreateFutureSymbol(Futures.Indices.SP500EMini, new DateTime(2016, 02, 19));
    public static readonly Symbol Fut_SPY_Mar19_2016 = CreateFutureSymbol(Futures.Indices.SP500EMini, new DateTime(2016, 03, 19));

    public static readonly Symbol ES_Future_Chain = CreateFuturesCanonicalSymbol(Futures.Indices.SP500EMini);
    public static readonly Symbol Future_ESZ18_Dec2018 = CreateFutureSymbol(Futures.Indices.SP500EMini, new DateTime(2018, 12, 21));
    public static readonly Symbol Future_CLF19_Jan2019 = CreateFutureSymbol("CL", new DateTime(2018, 12, 19));

    public static readonly ImmutableArray<Symbol> All =
        typeof(Symbols).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(Symbol))
            .Select(field => (Symbol)field.GetValue(null)!)
            .ToImmutableArray();

    /// <summary>
    /// Can be supplied in TestCase attribute
    /// </summary>
    public enum SymbolsKey
    {
        SPY,
        AAPL,
        MSFT,
        SBIN,
        IDEA,
        ZNGA,
        FXE,
        USDJPY,
        EURUSD,
        BTCUSD,
        EURGBP,
        GBPUSD,
        DE10YBEUR,
        SPY_C_192_Feb19_2016,
        SPY_P_192_Feb19_2016,
        Fut_SPY_Feb19_2016,
        Fut_SPY_Mar19_2016
    }

    /// <summary>
    /// Convert key into symbol instance
    /// </summary>
    /// <param name="key">the symbol key</param>
    /// <returns>The matching symbol instance</returns>
    /// <remarks>Using reflection minimizes maintenance but is slower at runtime.</remarks>
    public static Symbol Lookup(SymbolsKey key)
    {
        return (Symbol)typeof(Symbols).GetField(key.ToString(), BindingFlags.Public | BindingFlags.Static).GetValue(null);
    }

    /// <summary>
    /// Gets a symbol matching the specified <paramref name="type"/>
    /// </summary>
    public static Symbol GetBySecurityType(SecurityType type)
    {
        switch (type)
        {
            case SecurityType.Equity: return SPY;
            case SecurityType.Option: return SPY_C_192_Feb19_2016;
            case SecurityType.Forex: return EURUSD;
            case SecurityType.Future: return Future_CLF19_Jan2019;
            case SecurityType.Cfd: return XAGUSD;
            case SecurityType.Crypto: return BTCUSD;
            default:
                throw new NotImplementedException($"Symbols.GetBySecurityType({type}) is not implemented.");
        }
    }

    private static Symbol CreateForexSymbol(string symbol)
    {
        return Symbol.Create(symbol, SecurityType.Forex, Market.Oanda);
    }

    private static Symbol CreateEquitySymbol(string symbol, string market = Market.USA)
    {
        return Symbol.Create(symbol, SecurityType.Equity, market);
    }

    private static Symbol CreateFutureSymbol(string symbol, DateTime expiry)
    {
        if (!SymbolPropertiesDatabase.FromDataFolder().TryGetMarket(symbol, SecurityType.Future, out var market))
        {
            market = DefaultBrokerageModel.DefaultMarketMap[SecurityType.Future];
        }
        return Symbol.CreateFuture(symbol, market, expiry);
    }

    private static Symbol CreateCfdSymbol(string symbol, string market)
    {
        return Symbol.Create(symbol, SecurityType.Cfd, market);
    }

    private static Symbol CreateOptionSymbol(string symbol, OptionRight right, decimal strike, DateTime expiry, string market = Market.USA)
    {
        return Symbol.CreateOption(symbol, market, OptionStyle.American, right, strike, expiry);
    }

    private static Symbol CreateCryptoSymbol(string symbol)
    {
        return Symbol.Create(symbol, SecurityType.Crypto, Market.GDAX);
    }

    private static Symbol CreateOptionsCanonicalSymbol(string underlying)
    {
        return Symbol.Create(underlying, SecurityType.Option, Market.USA, "?" + underlying);
    }

    private static Symbol CreateFuturesCanonicalSymbol(string ticker)
    {
        if (!SymbolPropertiesDatabase.FromDataFolder().TryGetMarket(ticker, SecurityType.Future, out var market))
        {
            market = DefaultBrokerageModel.DefaultMarketMap[SecurityType.Future];
        }
        return Symbol.Create(ticker, SecurityType.Future, market, "/" + ticker);
    }
}