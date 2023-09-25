using System.Diagnostics;
using AblTest.Engine.DataFeeds;

namespace AblTest.Algorithm;

[TestClass]
public class AlgorithmLiveStartTests
{
    private const string NewYorkName = "America/New_York";

    private QCAlgorithm _algorithm;
    private Isolator _isolator;

    [TestInitialize]
    public void Setup()
    {
        _algorithm = new QCAlgorithm();
        _algorithm.SubscriptionManager.SetDataManager(new DataManagerStub(_algorithm));
        _isolator = new Isolator();
    }

    [TestMethod]
    public void LiveStartNoSet()
    {
        _algorithm.SetLiveMode(false);
        Assert.AreEqual(DateTimeOffset.MinValue, _algorithm.LiveStartTime, "live-start-time, 默认值错误");
        Assert.AreEqual(DateTimeOffset.MaxValue, _algorithm.LiveStopTime, "live-stop-time, 默认值错误");
    }

    [TestMethod]
    public void LiveStartAtNextDay()
    {
        Config.Set("live-start-time", $"1.04:00:00 {NewYorkName}");
        Config.Set("live-stop-time", $"1.20:00:00 {NewYorkName}");

        var zone = TimeZones.GetDateTimeZone(NewYorkName);
        var today = TimeZones.GetZoneToday(zone);

        var start = today.Add(new TimeSpan(1, 4, 0, 0));
        var stop = today.Add(new TimeSpan(1, 20, 0, 0));

        _algorithm.SetLiveMode(true);
        Assert.AreEqual(start, _algorithm.LiveStartTime, "live-start-time, 解析错误");
        Assert.AreEqual(stop, _algorithm.LiveStopTime, "live-stop-time, 解析错误");
    }

    [TestMethod]
    public void LiveStopAtNextDay()
    {
        Config.Set("live-start-time", $"04:00:00 {NewYorkName}");
        Config.Set("live-stop-time", $"1.20:00:00 {NewYorkName}");

        var zone = TimeZones.GetDateTimeZone(NewYorkName);
        var today = TimeZones.GetZoneToday(zone);

        var start = today.Add(new TimeSpan(4, 0, 0));
        var stop = today.Add(new TimeSpan(1, 20, 0, 0));

        _algorithm.SetLiveMode(true);
        Assert.AreEqual(start, _algorithm.LiveStartTime, "live-start-time, 解析错误");
        Assert.AreEqual(stop, _algorithm.LiveStopTime, "live-stop-time, 解析错误");
    }

    [TestMethod]
    public void LiveStartOnBacktest()
    {
        Config.Set("live-start-time", $"1.04:00:00 {NewYorkName}");
        Config.Set("live-stop-time", $"1.20:00:00 {NewYorkName}");
        _algorithm.SetLiveMode(false);
        var sw = Stopwatch.StartNew();
        _algorithm.WaitForLiveStart(_isolator);
        sw.Stop();
        Assert.IsTrue(sw.Elapsed < TimeSpan.FromMilliseconds(1));
    }

    [TestMethod]
    public void LiveStopAtDaylightSavings()
    {
        var zone = TimeZones.GetDateTimeZone(NewYorkName);
        var date = new DateTime(2023, 3, 12, 7, 0, 0, DateTimeKind.Utc);
        var offset = TimeZones.GetOffset(zone, date).ToTimeSpan();
        Assert.AreEqual(new TimeSpan(-4, 0, 0), offset, "夏令时时区错误");
        
        _algorithm.LiveStartTime = new DateTimeOffset(new DateTime(2023, 3, 12, 4, 0, 0), offset);
        _algorithm.LiveStopTime = new DateTimeOffset(new DateTime(2023, 3, 12, 20, 0, 0), offset);

        var sw = Stopwatch.StartNew();
        _algorithm.WaitForLiveStart(_isolator, _algorithm.LiveStartTime.AddSeconds(-1));
        sw.Stop();
        Assert.IsTrue(sw.Elapsed < TimeSpan.FromSeconds(2));
    }
}