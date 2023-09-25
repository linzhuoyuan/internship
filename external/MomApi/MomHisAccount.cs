using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomHisAccount : AccountField
    {
        [DataMember(Order = 1)]
        public string TradingDay
        {
            get => tradingDay;
            set => tradingDay = value;
        }
        internal string tradingDay = string.Empty;

        public MomHisAccount()
        {
        }

        public MomHisAccount(MomAccount account) : base(account)
        {
        }
    }
}
