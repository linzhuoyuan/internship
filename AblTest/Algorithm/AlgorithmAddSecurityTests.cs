using AblTest.Engine.DataFeeds;

namespace AblTest.Algorithm;

[TestClass]
public class AlgorithmAddSecurityTests
{
    private QCAlgorithm _algo;

    /// <summary>
    /// Instantiate a new algorithm before each test.
    /// Clear the <see cref="SymbolCache"/> so that no symbols and associated brokerage models are cached between test
    /// </summary>
    [TestInitialize]
    public void Setup()
    {
        _algo = new QCAlgorithm();
        _algo.SubscriptionManager.SetDataManager(new DataManagerStub(_algo));
    }

    [TestMethod]
    [DynamicData(nameof(TestAddSecurityWithSymbol))]
    public void AddSecurityWithSymbol(Symbol symbol)
    {
        var security = _algo.AddSecurity(symbol);
        Assert.AreEqual(security.Symbol, symbol);
        Assert.IsTrue(_algo.Securities.ContainsKey(symbol));

        AssertEx.NoExceptionThrown<Exception>(() =>
        {
            switch (symbol.SecurityType)
            {
                case SecurityType.Equity:
                    var equity = (Equity)security;
                    break;
                case SecurityType.Option:
                    var option = (Option)security;
                    break;
                case SecurityType.Forex:
                    var forex = (Forex)security;
                    break;
                case SecurityType.Future:
                    var future = (Future)security;
                    break;
                case SecurityType.Cfd:
                    var cfd = (Cfd)security;
                    break;
                case SecurityType.Crypto:
                    var crypto = (Crypto)security;
                    break;
                case SecurityType.Base:
                    break;
                default:
                    throw new Exception($"Invalid Security Type: {symbol.SecurityType}");
            }
        });

        if (symbol.IsCanonical())
        {
            // Throws NotImplementedException because we are using NullDataFeed
            // We need to call this to add the pending universe additions
            Assert.ThrowsException<NotImplementedException>(() => _algo.OnEndOfTimeStep());

            Assert.IsTrue(_algo.UniverseManager.ContainsKey(symbol));
        }
    }

    private static IEnumerable<object[]> TestAddSecurityWithSymbol
    {
        get
        {
            return new[]
            {
                new object[] { Symbols.SPY },
                new object[] { Symbols.EURUSD },
                new object[] { Symbols.DE30EUR },
                new object[] { Symbols.BTCUSD },
                //new object[] { Symbols.ES_Future_Chain },
                //new object[] { Symbols.Future_ESZ18_Dec2018 },
                //new object[] { Symbols.SPY_Option_Chain },
                //new object[] { Symbols.SPY_C_192_Feb19_2016 },
                //new object[] { Symbols.SPY_P_192_Feb19_2016 },
                //new object[] { Symbol.Create("CustomData", SecurityType.Base, Market.Binance) }
            };
        }
    }
}