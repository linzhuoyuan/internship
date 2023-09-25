using QuantConnect.Interfaces;
using QuantConnect.Packets;

namespace AblTest.Algorithm;

[TestClass]
public class AlgorithmWarmupTests
{
    private TestWarmupAlgorithm _algorithm;

    [DataTestMethod]
    [DataRow(Resolution.Second, SecurityType.Equity, DisplayName = "Equity-Second")]
    [DataRow(Resolution.Minute, SecurityType.Crypto, DisplayName = "Crypto-Minute")]
    public void WarmupDifferentResolutions(Resolution resolution, SecurityType securityType)
    {
        _algorithm = TestSetupHandler.TestAlgorithm = new TestWarmupAlgorithm(resolution);
        _algorithm.SecurityType = securityType;
        if (securityType == SecurityType.Crypto)
        {
            _algorithm.StartDateToUse = new DateTime(2023, 1, 1);
            _algorithm.EndDateToUse = new DateTime(2023, 2, 1);
        }
        else if (securityType == SecurityType.Equity)
        {
            _algorithm.StartDateToUse = new DateTime(2023, 1, 1);
            _algorithm.EndDateToUse = new DateTime(2023, 1, 7);
        }

        var result = AlgorithmRunner.RunLocalBacktest(nameof(TestWarmupAlgorithm),
            new Dictionary<string, string> { { "Total Trades", "1" } },
            null!,
            Language.CSharp,
            AlgorithmStatus.Completed,
            setupHandler: typeof(TestSetupHandler).FullName!,
            currency: securityType == SecurityType.Crypto ? "USDT" : Currencies.USD,
            initialCash: 10000);
    }

    public class TestSetupHandler : AlgorithmRunner.RegressionSetupHandlerWrapper
    {
        public static TestWarmupAlgorithm? TestAlgorithm { get; set; }

        public override IAlgorithm CreateAlgorithmInstance(AlgorithmNodePacket algorithmNodePacket, string assemblyPath, int loadTimeLimit = 60)
        {
            Algorithm = TestAlgorithm!;
            return Algorithm;
        }
    }

    public class TestWarmupAlgorithm : QCAlgorithm
    {
        private readonly Resolution _resolution;
        private Symbol? _symbol;
        public SecurityType SecurityType { get; set; }

        public DateTime StartDateToUse { get; set; }

        public DateTime EndDateToUse { get; set; }

        public int WarmUpDataCount { get; set; }

        public TestWarmupAlgorithm(Resolution resolution)
        {
            _resolution = resolution;
        }

        public override void Initialize()
        {
            SetStartDate(StartDateToUse);
            SetEndDate(EndDateToUse);

            if (SecurityType == SecurityType.Equity)
            {
                SetTimeZone(TimeZones.Shanghai);
                _symbol = AddEquity("sh510050", _resolution, Market.SSE).Symbol;
            }
            else if (SecurityType == SecurityType.Future)
            {
                SetTimeZone(TimeZones.Utc);
                _symbol = AddPerpetual("BTC-PERPETUAL", _resolution, Market.Deribit).Symbol;
            }
            else if (SecurityType == SecurityType.Crypto)
            {
                SetTimeZone(TimeZones.Utc);
                _symbol = AddCrypto("ETHUSDT", _resolution, Market.Binance).Symbol;
            }
            SetBenchmark(_symbol);
            SetWarmUp(TimeSpan.FromDays(2), _resolution);
        }

        public override void OnData(Slice data)
        {
            if (IsWarmingUp)
            {
                WarmUpDataCount += data.Count;
            }
            else
            {
                if (!Portfolio.Invested)
                {
                    SetHoldings(_symbol, 1);
                }
            }
        }
    }
}