using AblTest.Brokerages;
using QuantConnect.Interfaces;
using QuantConnect.Packets;

namespace AblTest.Simulation;

[TestClass]
public class AlgorithmSimulationTests
{
    [TestMethod]
    public void AddEquityOnRunning()
    {
        var algorithm = TestSetupHandler.TestAlgorithm = new RunningAddSecurityAlgorithm();
        var result = AlgorithmRunner.RunLocalSimulation(
            nameof(RunningAddSecurityAlgorithm),
            Language.CSharp,
            AlgorithmStatus.Completed,
            nameof(SimulationBrokerage),
            typeof(TestSetupHandler).FullName!);
        Assert.IsTrue(algorithm.Success, "运行中订阅合约失败");
    }

    public class TestSetupHandler : AlgorithmRunner.SimulationSetupHandler
    {
        public static RunningAddSecurityAlgorithm? TestAlgorithm { get; set; }

        public override IAlgorithm CreateAlgorithmInstance(AlgorithmNodePacket algorithmNodePacket, string assemblyPath, int loadTimeLimit = 60)
        {
            Algorithm = TestAlgorithm!;
            return Algorithm;
        }
    }

    public class RunningAddSecurityAlgorithm : QCAlgorithm
    {
        private static readonly string[] Tickers = {
            "SH510050","SH510300","SH510500"
        };

        public bool Success = false;

        private DateTime _firstTime;

        private void AddEquity(string ticker)
        {
            AddEquity(ticker, Resolution.Tick, Market.SSE);
        }

        private void Callback(string name, DateTime date)
        {
            foreach (var ticker in Tickers)
            {
                if (SymbolCache.TryGetSymbol(ticker, out _))
                {
                    continue;
                }
                AddEquity(ticker);
            }
        }

        public override void Initialize()
        {
            AddEquity(Tickers[0]);
            Schedule.On(DateRules.EveryDay(), TimeRules.Every(TimeSpan.FromSeconds(5)), Callback);
        }

        public override void OnData(Slice slice)
        {
            if (IsWarmingUp)
            {
                return;
            }

            if (_firstTime == DateTime.MinValue)
            {
                _firstTime = DateTime.UtcNow;
                return;
            }

            if (slice.Ticks.Keys.Count == Tickers.Length)
            {
                Success = true;
                ExitTokenSource.Cancel();
            }

            if (DateTime.UtcNow - _firstTime > TimeSpan.FromSeconds(10))
            {
                //超时退出
                ExitTokenSource.Cancel();
            }
        }
    }
}