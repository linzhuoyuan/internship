using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantConnect.Brokerages.Mom
{
    class MomBrokerageFactory : BrokerageFactory
    {
        public MomBrokerageFactory()
            : base(typeof(MomBrokerage))
        {

        }

        public override Dictionary<string, string> BrokerageData => new()
        {
            { "mom-trade-server", Config.Get("mom-trade-server") },
            { "mom-marketdata-server", Config.Get( "mom-marketdata-server") },
            { "mom-userId", Config.Get( "mom-userId") },
            { "mom-passwd", Config.Get("mom-passwd") },
        };

        public override void Dispose()
        {

        }

        public override IBrokerageModel BrokerageModel => new MomBrokerageModel();

        public override IBrokerage CreateBrokerage(LiveNodePacket job, IAlgorithm algorithm)
        {
            var errors = new List<string>();
            var tradeServer = Read<string>(job.BrokerageData, "mom-trade-server", errors);
            var mdServer = Read<string>(job.BrokerageData, "mom-marketdata-server", errors);
            var userId = Read<string>(job.BrokerageData, "mom-userId", errors);
            var passWd = Read<string>(job.BrokerageData, "mom-passwd", errors);

            if (errors.Count != 0)
            {
                throw new Exception(string.Join(Environment.NewLine, errors));
            }


            var brokerage = new  MomBrokerage(algorithm, tradeServer, userId, passWd, mdServer, userId, passWd);
            Composer.Instance.AddPart<IDataQueueHandler>(brokerage);
            return brokerage;
        }
    }
}
