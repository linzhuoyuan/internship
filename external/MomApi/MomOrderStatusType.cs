using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomOrderStatusType
    {
        private MomOrderStatusType() {}

        [Description(nameof(PartTradedQueueing))] //48
        public const byte PartTradedQueueing = (byte)'0';

        [Description(nameof(PartTradedNotQueueing))] //49
        public const byte PartTradedNotQueueing = (byte)'1';

        [Description(nameof(NoTradeQueueing))] //50
        public const byte NoTradeQueueing = (byte)'2';

        [Description(nameof(NoTradeNotQueueing))] //51
        public const byte NoTradeNotQueueing = (byte)'3';
        
        [Description(nameof(NoCheck))] //52
        public const byte NoCheck = (byte)'4';

        [Description(nameof(Checked))] //53
        public const byte Checked = (byte)'5';

        [Description(nameof(Canceled))] //97
        public const byte Canceled = (byte)'a';

        [Description(nameof(Rejected))] //98
        public const byte Rejected = (byte)'b';

        [Description(nameof(PartCanceled))] //99
        public const byte PartCanceled = (byte)'c';

        [Description(nameof(AllTraded))] //100
        public const byte AllTraded = (byte)'d';

        [Description(nameof(CancelRejected))] //101
        public const byte CancelRejected = (byte)'e';
    }
}
