namespace AblTest.Common;

[TestClass]
public class AblSymbolDatabaseTest
{
    [TestInitialize]
    public void Init()
    {

    }

    [TestMethod]
    public void TestHKAHolidays()
    {
        var s700 = Symbol.Create("700", SecurityType.Equity, Market.HKG, "700");
        var marketHourG = MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.HKG, s700, SecurityType.Equity);
        Assert.AreEqual(new DateTime(2023, 1, 26), marketHourG.GetNextTradingDay(new DateTime(2023, 1, 20)));

        var s600519 = Symbol.Create("600519", SecurityType.Equity, Market.HKA, "600519");
        var marketHourA = MarketHoursDatabase.FromDataFolder().GetExchangeHours(Market.HKA, s600519, SecurityType.Equity);
        Assert.AreEqual(new DateTime(2023, 1, 30), marketHourA.GetNextTradingDay(new DateTime(2023, 1, 20)));
    }
}