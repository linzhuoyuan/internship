using System;
using System.Linq;
using QuantConnect.Data;

namespace QuantConnect.Algorithm.CSharp;

public class UniswapLivingTest : QCAlgorithm
{
    private volatile bool _trading = false;
    private Symbol _symbol;

    public override void Initialize()
    {
        SetTimeZone(TimeZones.Shanghai);
        // _symbol = AddCrypto("BTC-USDC", Resolution.Tick, Market.Uniswap).Symbol;

        AddCrypto("ETHUSDC", Resolution.Tick, Market.Uniswap);
        this._symbol = AddCrypto("UNIUSDC", Resolution.Tick, Market.Uniswap).Symbol;

        Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromMinutes(1)), DoTrading);
    }

    private void DoTrading(string arg1, DateTime arg2)
    {
        _trading = true;
        Console.WriteLine("set trading flag to true");
    }

    public override void OnData(Slice slice)
    {
        foreach (var pair in slice.Ticks)
        {
            var tick = pair.Value.FirstOrDefault();
            Debug($"{tick.Symbol.Value} {slice.Time:yyyy-MM-dd HH:mm:ss} {tick.Time:yyyy-MM-dd HH:mm:ss} {tick.LastPrice}");
        }
        
        if (_trading)
        {
            if (Portfolio.CashBook["UNI"].Amount > 0)
            {
                MarketOrder(_symbol, -100);
            
            }
            else
            {
                Console.WriteLine("before buy uni");
                MarketOrder(_symbol, 0.001);
                Console.WriteLine("after buy uni");
            }

            _trading = false;
        }
    }
}