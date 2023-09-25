using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomHisPosition : PositionField
    {
        [DataMember(Order = 1)]
        public string TradingDay
        {
            get => tradingDay;
            set => tradingDay = value;
        }
        internal string tradingDay = string.Empty;

        public MomHisPosition()
        {
        }

        public MomHisPosition(MomPosition position) : base(position)
        {
        }
    }
}
