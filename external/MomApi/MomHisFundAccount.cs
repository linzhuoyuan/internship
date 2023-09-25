using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomHisFundAccount : AccountField
    {
        [DataMember(Order = 1)]
        public string TradingDay
        {
            get => tradingDay;
            set => tradingDay = value;
        }
        internal string tradingDay = string.Empty;

        public MomHisFundAccount()
        {
        }

        public MomHisFundAccount(MomFundAccount account) : base(account)
        {
        }
    }
}
