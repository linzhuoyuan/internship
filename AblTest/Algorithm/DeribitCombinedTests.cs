using QuantConnect.Algorithm.CSharp;
using QuantConnect.Interfaces;
using QuantConnect.Packets;

namespace AblTest.Algorithm;

[TestClass]
public class DeribitCombinedTests
{
    private DeribitCombinedBacktestNew _algorithm;
    [Ignore]
    [DataTestMethod]
    [DataRow("BTC", 0.1, "893.339%", DisplayName = "BTC")]
    [DataRow("ETH", 1.0, "75.355%", DisplayName = "ETH")]
    public void DeribitCombined(string coin, double lotSize, string pnl)
    {
        _algorithm = TestSetupHandler.TestAlgorithm = new DeribitCombinedBacktestNew();
        _algorithm.StartDate_ = new DateTime(2022, 1, 1);
        _algorithm.EndDate_ = new DateTime(2022, 1, 5);
        _algorithm.Coin = coin;
        _algorithm.OptionLotSize = (decimal)lotSize;

        var result = AlgorithmRunner.RunLocalBacktest(nameof(DeribitCombinedBacktestNew),
            new Dictionary<string, string> {{ "Compounding Annual Return", pnl}},
            null!,
            Language.CSharp,
            AlgorithmStatus.Completed,
            setupHandler: typeof(TestSetupHandler).FullName!,
            initialCash: 10000);
    }

    public class TestSetupHandler : AlgorithmRunner.RegressionSetupHandlerWrapper
    {
        public static DeribitCombinedBacktestNew? TestAlgorithm { get; set; }

        public override IAlgorithm CreateAlgorithmInstance(AlgorithmNodePacket algorithmNodePacket, string assemblyPath, int loadTimeLimit = 60)
        {
            Algorithm = TestAlgorithm!;
            return Algorithm;
        }
    }
}