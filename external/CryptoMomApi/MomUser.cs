using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    public enum MomUserType
    {
        Strategy, Manager
    }

    [DataContract]
    public class MomUser
    {
        internal int index;

        [DataMember(Order = 1)]
        public string UserId
        {
            get => userId;
            set => userId = value;
        }
        internal string userId;

        [DataMember(Order = 2)]
        public string Password
        {
            get => password;
            set => password = value;
        }
        internal string password;

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
        internal string description;

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
        internal string strategyManager;

        [DataMember(Order = 7)]
        public string StrategyName
        {
            get => strategyName;
            set => strategyName = value;
        }
        internal string strategyName;

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
