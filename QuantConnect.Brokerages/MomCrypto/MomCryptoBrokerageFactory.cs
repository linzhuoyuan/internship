using System;
using System.Collections.Generic;
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Util;

namespace QuantConnect.Brokerages.MomCrypto
{
    internal class MomCryptoBrokerageFactory : BrokerageFactory
    {
        private bool _enableSpotMarginForFTX;

        public MomCryptoBrokerageFactory()
            : base(typeof(MomCryptoBrokerage))
        {
            _enableSpotMarginForFTX = Config.GetBool("ftx-enable-spot-margin");
        }

        public override Dictionary<string, string> BrokerageData => new()
        {
            {"momcrypto-trade-server", Config.Get("momcrypto-trade-server")},
            {"momcrypto-marketdata-server", Config.Get( "momcrypto-marketdata-server")},
            {"momcrypto-userId", Config.Get( "momcrypto-userId")},
            {"momcrypto-passwd", Config.Get("momcrypto-passwd")},
            {"momcrypto-historydata-server", Config.Get("momcrypto-historydata-server")},
            {"ftx_key", Config.Get("ftx_key")},
            {"ftx_secret", Config.Get("ftx_secret")},
            {"ftx_subaccount", Config.Get("ftx_subaccount")},
        };

        public override void Dispose()
        {

        }

        public override IBrokerageModel BrokerageModel => new MomCryptoBrokerageModel(_enableSpotMarginForFTX);

        public override IBrokerage CreateBrokerage(LiveNodePacket job, IAlgorithm algorithm)
        {
            var errors = new List<string>();
            var tradeServer = Read<string>(job.BrokerageData, "momcrypto-trade-server", errors);
            var mdServer = Read<string>(job.BrokerageData, "momcrypto-marketdata-server", errors);
            var userId = Read<string>(job.BrokerageData, "momcrypto-userId", errors);
            var password = Read<string>(job.BrokerageData, "momcrypto-passwd", errors);
            var historyServer = Read<string>(job.BrokerageData, "momcrypto-historydata-server", errors);
            var ftxKey = Read<string>(job.BrokerageData, "ftx_key", errors);
            var ftxSecret = Read<string>(job.BrokerageData, "ftx_secret", errors);
            var ftxSubAccount = Read<string>(job.BrokerageData, "ftx_subaccount", errors);

            if (errors.Count != 0)
            {
                throw new Exception(string.Join(Environment.NewLine, errors));
            }
            var brokerage = new MomCryptoBrokerage(
                algorithm, tradeServer, userId, password, mdServer, userId, password, historyServer);
            brokerage.FtxKey = ftxKey;
            brokerage.FtxSecret = ftxSecret;
            brokerage.FtxSubAccount = ftxSubAccount;

            Composer.Instance.AddPart<IDataQueueHandler>(brokerage);
            return brokerage;
        }
    }
}
