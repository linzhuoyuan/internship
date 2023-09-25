using AblTest.Engine.DataFeeds;
using NodaTime;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Data.Market;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Lean.Engine.HistoricalData;
using QuantConnect.Util;


namespace AblTest.Algorithm;

[TestClass]
public class AlgorithmHistoryTests
{
    private class TestHistoryProvider : HistoryProviderBase
    {
        public override int DataPointCount { get; }
        public List<HistoryRequest> HistryRequests { get; } = new();

        public List<Slice> Slices { get; set; } = new();

        public override void Initialize(HistoryProviderInitializeParameters parameters)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<Slice> GetHistory(IEnumerable<HistoryRequest> requests, DateTimeZone sliceTimeZone)
        {
            foreach (var request in requests)
            {
                HistryRequests.Add(request);
            }

            var startTime = requests.Min(x => x.StartTimeUtc.ConvertFromUtc(x.DataTimeZone));
            var endTime = requests.Max(x => x.EndTimeUtc.ConvertFromUtc(x.DataTimeZone));

            return Slices.Where(x => x.Time >= startTime && x.Time <= endTime).ToList();
        }
    }

    private QCAlgorithm _algorithm;
    private TestHistoryProvider _testHistoryProvider;

    [TestInitialize]
    public void Setup()
    {
        _algorithm = new QCAlgorithm();
        _algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(_algorithm));
        _algorithm.HistoryProvider = _testHistoryProvider = new TestHistoryProvider();
    }

    [TestMethod]
    public void TradeBarResolutionHistoryRequest()
    {
        _algorithm = new QCAlgorithm();
        _algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(_algorithm));
        _algorithm.HistoryProvider = new SubscriptionDataReaderHistoryProvider();
        var dataProvider = new ApiDataProvider();
        var zipCacheProvider = new ZipDataCacheProvider(dataProvider);
        _algorithm.HistoryProvider.Initialize(new HistoryProviderInitializeParameters(
            null,
            null,
            dataProvider,
            zipCacheProvider,
            new LocalDiskMapFileProvider(),
            new LocalDiskFactorFileProvider(),
            null));

        var start = new DateTime(2023, 2, 3);
        _algorithm.SetTimeZone(TimeZones.Shanghai);
        _algorithm.SetStartDate(start);
        _algorithm.SetEndDate(2023, 2, 6);

        var historyDate = new DateTime(2023, 2, 1);
        var result = _algorithm.History(new[] { Symbols.ETF50 }, historyDate.AddHours(9.5), historyDate.AddHours(15), Resolution.Minute).ToList();
        var result2 = _algorithm.History<TradeBar>(Symbols.ETF50, historyDate.AddHours(9.5), historyDate.AddHours(14), Resolution.Minute).ToList();

        zipCacheProvider.DisposeSafely();
        Assert.IsTrue(result.Count > 0, message: "获取A股历史数据(Slice)失败");
        Assert.IsTrue(result2.Count > 0, message: "获取A股历史数据(TradeBar)失败");
    }

    /// <summary>
    /// 香港市场中的A股历史数据获取
    /// </summary>
    [TestMethod]
    public void HongkongAStocksResolutionHistoryRequest()
    {
        _algorithm = new QCAlgorithm();
        _algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(_algorithm));
        _algorithm.HistoryProvider = new SubscriptionDataReaderHistoryProvider();
        var dataProvider = new ApiDataProvider();
        var zipCacheProvider = new ZipDataCacheProvider(dataProvider);
        _algorithm.HistoryProvider.Initialize(new HistoryProviderInitializeParameters(
            null,
            null,
            dataProvider,
            zipCacheProvider,
            new LocalDiskMapFileProvider(),
            new LocalDiskFactorFileProvider(),
            null));

        var start = new DateTime(2023, 2, 3);
        _algorithm.SetTimeZone(TimeZones.Shanghai);
        _algorithm.SetStartDate(start);
        _algorithm.SetEndDate(2023, 2, 6);

        var historyDate = new DateTime(2023, 2, 1);
        var result = _algorithm.History(new[] { Symbols.H600519 }, historyDate.AddHours(9.5), historyDate.AddHours(15), Resolution.Minute).ToList();

        zipCacheProvider.DisposeSafely();
        Assert.IsTrue(result.Count > 0, message: "获取香港市场中的A股历史数据失败");
    }
}