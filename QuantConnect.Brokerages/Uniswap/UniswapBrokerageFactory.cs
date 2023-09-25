using System;
using System.Collections.Generic;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Util;

namespace QuantConnect.Brokerages.Uniswap;

public class UniswapBrokerageFactory : BrokerageFactory
{
    public UniswapBrokerageFactory()
        : base(typeof(UniswapBrokerage))
    {
    }

    public override void Dispose()
    {
    }

    public override Dictionary<string, string> BrokerageData => new();

    public override IBrokerageModel BrokerageModel => new UniswapBrokerageModel();

    public override IBrokerage CreateBrokerage(LiveNodePacket job, IAlgorithm algorithm)
    {
        var brokerage = new UniswapBrokerage(algorithm);
        Composer.Instance.AddPart<IDataQueueHandler>(brokerage);
        return brokerage;
    }
}