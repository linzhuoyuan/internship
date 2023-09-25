using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Util;

namespace AblTest.Brokerages;

internal class SimulationBrokerageFactory : BrokerageFactory
{
    public SimulationBrokerageFactory()
        : base(typeof(SimulationBrokerage))
    {
    }

    public override void Dispose()
    {
    }

    public override Dictionary<string, string> BrokerageData => new();
    public override IBrokerageModel BrokerageModel => new DefaultBrokerageModel();
    public override IBrokerage CreateBrokerage(LiveNodePacket job, IAlgorithm algorithm)
    {
        var brokerage = new SimulationBrokerage(algorithm);
        Composer.Instance.AddPart<IDataQueueHandler>(brokerage);
        return brokerage;
    }
}