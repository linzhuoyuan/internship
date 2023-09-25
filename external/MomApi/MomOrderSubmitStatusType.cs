using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomOrderSubmitStatusType
    {
        private MomOrderSubmitStatusType() { }

        [Description(nameof(InsertSubmitted))]
        public const byte InsertSubmitted = (byte)'0';

        [Description(nameof(CancelSubmitted))]
        public const byte CancelSubmitted = (byte)'1';

        [Description(nameof(ModifySubmitted))]
        public const byte ModifySubmitted = (byte)'2';

        [Description(nameof(Accepted))]
        public const byte Accepted = (byte)'3';

        [Description(nameof(InsertRejected))]
        public const byte InsertRejected = (byte)'4';

        [Description(nameof(CancelRejected))]
        public const byte CancelRejected = (byte)'5';

        [Description(nameof(ModifyRejected))]
        public const byte ModifyRejected = (byte)'6';
    }

}
