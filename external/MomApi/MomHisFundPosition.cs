using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomHisFundPosition : PositionField
    {
        [DataMember(Order = 1)]
        public string TradingDay
        {
            get => tradingDay;
            set => tradingDay = value;
        }
        internal string tradingDay = string.Empty;

        public MomHisFundPosition()
        {
        }

        public MomHisFundPosition(MomFundPosition position) : base(position)
        {
        }
    }
}
