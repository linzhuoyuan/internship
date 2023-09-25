using System.Collections.Concurrent;
using System.Timers;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Packets;
using Timer = System.Timers.Timer;

namespace AblTest.Brokerages;

internal class SimulationBrokerage : Brokerage, IDataQueueHandler
{
    private readonly IAlgorithm _algorithm;
    private volatile bool _connected;
    private readonly ConcurrentQueue<Tick> _ticks = new();
    private readonly Timer _tickTimer;
    private List<Symbol> _symbols = new();
    private volatile int _inTimer;

    public SimulationBrokerage(IAlgorithm algorithm) :
        base(nameof(SimulationBrokerage))
    {
        _algorithm = algorithm;
        _tickTimer = new Timer(500);
        _tickTimer.AutoReset = true;
        _tickTimer.Enabled = false;
        _tickTimer.Elapsed += OnTimer;
    }

    private void OnTimer(object? sender, ElapsedEventArgs e)
    {
        try
        {
            if (Interlocked.CompareExchange(ref _inTimer, 1, 0) == 1)
            {
                return;
            }
            var rand = new Random();
            foreach (var symbol in _symbols)
            {
                var tick = new Tick();
                tick.Symbol = symbol;
                tick.Time = DateTime.UtcNow;
                tick.Quantity = rand.Next(10);
                tick.Value = (decimal)Math.Round(rand.NextDouble() * 30, 2);
                tick.TickType = AblSymbolDatabase.GetSecurityDataFeed(symbol);
                tick.BidPrice = (decimal)Math.Round(rand.NextDouble() * 20, 2);
                tick.BidSize = rand.Next(10);
                tick.AskPrice = (decimal)Math.Round(rand.NextDouble() * 40, 2);
                tick.AskSize = rand.Next(10);
                _ticks.Enqueue(tick);
            }

        }
        finally
        {
            _inTimer = 0;
        }
    }

    public override bool IsConnected => _connected;

    public override List<Order> GetOpenOrders()
    {
        return new List<Order>();
    }

    public override List<Holding> GetAccountHoldings()
    {
        return new List<Holding>();
    }

    public override List<CashAmount> GetCashBalance()
    {
        return new List<CashAmount>
        {
            new(1000, Currencies.USD)
        };
    }

    public override bool PlaceOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public override bool UpdateOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public override bool CancelOrder(Order order)
    {
        throw new NotImplementedException();
    }

    public override void Connect()
    {
        _tickTimer.Start();
        _connected = true;
    }

    public override void Disconnect()
    {
        _tickTimer.Stop();
        _connected = false;
    }

    public IEnumerable<BaseData> GetNextTicks()
    {
        while (_ticks.TryDequeue(out var tick))
        {
            yield return tick;
        }
    }

    private static bool SupportSymbol(Symbol symbol)
    {
        return symbol.Value.ToLower().IndexOf("universe", StringComparison.Ordinal) == -1;
    }

    public void Subscribe(LiveNodePacket job, IEnumerable<Symbol> symbols)
    {
        var subscribes = new List<Symbol>();

        foreach (var symbol in symbols)
        {
            if (!SupportSymbol(symbol))
                continue;

            if (symbol.IsCanonical())
            {
                return;
            }
            if (_symbols.Contains(symbol))
            {
                Log.Trace($"{nameof(SimulationBrokerage)} 重复订阅: {symbol}");
                continue;
            }
            subscribes.Add(symbol);
            Log.Trace($"{nameof(SimulationBrokerage)} 订阅合约: {symbol}");
        }

        if (subscribes.Count == 0)
        {
            return;
        }

        var list = new List<Symbol>(_symbols);
        list.AddRange(subscribes);
        _symbols = list;
    }

    public void Unsubscribe(LiveNodePacket job, IEnumerable<Symbol> symbols)
    {
        var removed = new List<Symbol>(symbols);
        var list = new List<Symbol>();
        foreach (var symbol in _symbols)
        {
            if (removed.Contains(symbol))
            {
                Log.Trace($"{nameof(SimulationBrokerage)} 取消订阅: {symbol}");
                continue;
            }
            list.Add(symbol);
        }
        _symbols = list;
    }
}