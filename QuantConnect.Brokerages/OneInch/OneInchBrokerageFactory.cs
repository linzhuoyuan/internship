using System;
using System.Collections.Generic;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Util;

namespace QuantConnect.Brokerages.OneInch;

public class OneInchBrokerageFactory : BrokerageFactory
{
    public OneInchBrokerageFactory()
        : base(typeof(OneInchBrokerage))
    {
    }

    public override void Dispose()
    {
    }

    public override Dictionary<string, string> BrokerageData => new();

    public override IBrokerageModel BrokerageModel => new OneInchBrokerageModel();

    public override IBrokerage CreateBrokerage(LiveNodePacket job, IAlgorithm algorithm)
    {
        var brokerage = new OneInchBrokerage(algorithm);
        Composer.Instance.AddPart<IDataQueueHandler>(brokerage);
        return brokerage;
    }
}