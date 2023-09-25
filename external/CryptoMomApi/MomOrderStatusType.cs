using System.ComponentModel;

namespace MomCrypto.Api
{
    public sealed class MomOrderStatusType
    {
        private MomOrderStatusType() {}

        [Description(nameof(PartTradedQueueing))]
        public const byte PartTradedQueueing = (byte)'0';

        [Description(nameof(PartTradedNotQueueing))]
        public const byte PartTradedNotQueueing = (byte)'1';

        [Description(nameof(NoTradeQueueing))]
        public const byte NoTradeQueueing = (byte)'2';

        [Description(nameof(NoTradeNotQueueing))]
        public const byte NoTradeNotQueueing = (byte)'3';

        [Description(nameof(Untriggered))]
        public const byte Untriggered = (byte)'4';

        [Description(nameof(Triggered))]
        public const byte Triggered = (byte)'5';


        [Description(nameof(Canceled))]
        public const byte Canceled = (byte)'a';

        [Description(nameof(Rejected))]
        public const byte Rejected = (byte)'b';

        [Description(nameof(PartCanceled))]
        public const byte PartCanceled = (byte)'c';

        [Description(nameof(Closed))]
        public const byte Closed = (byte)'d';

        [Description(nameof(AllTraded))]
        public const byte AllTraded = (byte)'e';

        [Description(nameof(Expired))]
        public const byte Expired = (byte)'f';
    }
}
