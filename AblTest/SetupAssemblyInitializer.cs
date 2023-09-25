using System.Runtime.CompilerServices;

namespace AblTest;

public sealed class AssertEx
{
    public static void NoExceptionThrown<T>(Action a) where T:Exception
    {
        try
        {
            a();
        }
        catch (T)
        {
            Assert.Fail("Expected no {0} to be thrown", typeof(T).Name);
        }
    }
}

[TestClass]
public class SetupAssemblyInitializer
{
    [ModuleInitializer]
    internal static void ModuleInit()
    {
        var dataPath = Environment.GetEnvironmentVariable("QC_DATA_FOLDER") ?? "c:\\work\\data\\quantconnect";
        Config.Set("data-folder", dataPath);
        Config.Set("cloud-api-url", "http://121.36.100.76:2020/");
        Config.Set("data-provider", "QuantConnect.Lean.Engine.DataFeeds.ApiDataProvider");
        OptionConfigDatabase.FromDataFolder();
        MarketHoursDatabase.UseAlwaysOpen();
        AblSymbolDatabase.FromDataFolder();
    }

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
    }

    [AssemblyCleanup]
    public static void TearDown()
    {
    }
}