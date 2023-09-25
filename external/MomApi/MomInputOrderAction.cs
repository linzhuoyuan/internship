using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomInputOrderAction
    {
        internal int channelIndex = -1;

        [DataMember(Order = 1)]
        public string UserId
        {
            get => userId;
            set => userId = value;
        }
        internal string userId = string.Empty;

        [DataMember(Order = 2)]
        public long OrderRef
        {
            get => orderRef;
            set => orderRef = value;
        }
        internal long orderRef;

        [DataMember(Order = 3)]
        public long InputLocalId
        {
            get => inputLocalId;
            set => inputLocalId = value;
        }
        internal long inputLocalId;

        [DataMember(Order = 4)]
        public long StrategyInputId
        {
            get => strategyInputId;
            set => strategyInputId = value;
        }
        internal long strategyInputId;

        [DataMember(Order = 5)]
        public string FundAccountId
        {
            get => fundAccountId;
            set => fundAccountId = value;
        }
        internal string fundAccountId = string.Empty;

        [DataMember(Order = 6)]
        public string FundChannelType
        {
            get => fundChannelType;
            set => fundChannelType = value;
        }
        internal string fundChannelType = string.Empty;

        [DataMember(Order = 7)]
        public string OrderSysId
        {
            get => orderSysId;
            set => orderSysId = value;
        }
        internal string orderSysId = string.Empty;

        [DataMember(Order = 8)]
        public byte ActionFlag
        {
            get => actionFlag;
            set => actionFlag = value;
        }
        internal byte actionFlag = MomActionFlag.Delete;

        [DataMember(Order = 9)]
        public string ExchangeId
        {
            get => exchangeId;
            set => exchangeId = value;
        }
        internal string exchangeId = string.Empty;
        
        [DataMember(Order = 10)]
        public string ShortKey
        {
            get => shortKey;
            set => shortKey = value;
        }
        internal string shortKey = string.Empty;

        public override string ToString()
        {
            return $"{UserId},{FundAccountId},{OrderRef},{OrderSysId}";
        }

        public MomInputOrderAction Clone()
        {
            return (MomInputOrderAction) MemberwiseClone();
        }
    }
}
