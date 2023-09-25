using System.Runtime.Serialization;

namespace Quantmom.Api
{
    public enum MomUserType
    {
        Strategy, Manager, Admin, RiskManager, Trader, Maintain
    }

    [DataContract]
    public class MomUser
    {
        public static readonly MomUser Empty = new();
        internal int index;

        [DataMember(Order = 1)]
        public string UserId
        {
            get => userId;
            set => userId = value;
        }
        internal string userId = string.Empty;

        [DataMember(Order = 2)]
        public string Password
        {
            get => password;
            set => password = value;
        }
        internal string password = string.Empty;

        [DataMember(Order = 3)]
        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }
        internal bool enabled;

        [DataMember(Order = 4)]
        public string Description
        {
            get => description;
            set => description = value;
        }
        internal string description = string.Empty;

        [DataMember(Order = 5)]
        public MomUserType UserType
        {
            get => userType;
            set => userType = value;
        }
        internal MomUserType userType;

        [DataMember(Order = 6)]
        public string StrategyManager
        {
            get => strategyManager;
            set => strategyManager = value;
        }
        internal string strategyManager = string.Empty;

        [DataMember(Order = 7)]
        public string StrategyName
        {
            get => strategyName;
            set => strategyName = value;
        }
        internal string strategyName = string.Empty;

        [DataMember(Order = 8)]
        public string Trader
        {
            get => trader;
            set => trader = value;
        }
        internal string trader = string.Empty;

        [DataMember(Order = 9)]
        public bool AutoOrderCheck
        {
            get => autoOrderCheck;
            set => autoOrderCheck = value;
        }
        internal bool autoOrderCheck;

        [DataMember(Order = 10)]
        public string? UpdateTime 
        {
            get => updateTime;
            set => updateTime = value;
        }
        internal string? updateTime;

        public override string ToString()
        {
            return $"{userId},{userType},{enabled}";
        }

        public MomUser Clone()
        {
            return (MomUser)MemberwiseClone();
        }
    }
}
