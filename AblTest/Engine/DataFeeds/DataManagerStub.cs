using QuantConnect.Algorithm;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;

namespace AblTest.Engine.DataFeeds;

internal class DataManagerStub : DataManager
{
    public ISecurityService SecurityService { get; }
    public IAlgorithm Algorithm { get; }

    public DataManagerStub()
        : this(new QCAlgorithm())
    {

    }

    public DataManagerStub(ITimeKeeper timeKeeper)
        : this(new QCAlgorithm(), timeKeeper)
    {

    }

    public DataManagerStub(IAlgorithm algorithm, IDataFeed dataFeed, bool liveMode = false)
        : this(dataFeed, algorithm, new TimeKeeper(DateTime.UtcNow, TimeZones.NewYork), liveMode)
    {

    }

    public DataManagerStub(IAlgorithm algorithm)
        : this(new NullDataFeed(), algorithm, new TimeKeeper(DateTime.UtcNow, TimeZones.NewYork))
    {

    }

    public DataManagerStub(IDataFeed dataFeed, IAlgorithm algorithm)
        : this(dataFeed, algorithm, new TimeKeeper(DateTime.UtcNow, TimeZones.NewYork))
    {

    }

    public DataManagerStub(IAlgorithm algorithm, ITimeKeeper timeKeeper)
        : this(new NullDataFeed(), algorithm, timeKeeper)
    {

    }

    public DataManagerStub(IDataFeed dataFeed, IAlgorithm algorithm, ITimeKeeper timeKeeper, bool liveMode = false)
        : this(dataFeed, algorithm, timeKeeper, MarketHoursDatabase.FromDataFolder(), SymbolPropertiesDatabase.FromDataFolder(), liveMode)
    {

    }

    public DataManagerStub(IDataFeed dataFeed, IAlgorithm algorithm, ITimeKeeper timeKeeper, MarketHoursDatabase marketHoursDatabase, SymbolPropertiesDatabase symbolPropertiesDatabase, bool liveMode = false)
        : this(dataFeed, algorithm, timeKeeper, marketHoursDatabase,
            new SecurityService(
                algorithm.Portfolio.CashBook,
                marketHoursDatabase,
                symbolPropertiesDatabase,
                algorithm),
            liveMode)
    {
    }

    public DataManagerStub(IDataFeed dataFeed, IAlgorithm algorithm, ITimeKeeper timeKeeper, MarketHoursDatabase marketHoursDatabase, SecurityService securityService, bool liveMode = false)
        : base(dataFeed,
            new UniverseSelection(algorithm, securityService, new DefaultDataProvider()),
            algorithm,
            timeKeeper,
            marketHoursDatabase,
            liveMode)
    {
        SecurityService = securityService;
        algorithm.Securities.SetSecurityService(securityService);
        Algorithm = algorithm;
    }
}