using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public sealed class MomTradingRoute
    {
        internal long routeId;
        internal bool enabled;
        internal string userId = string.Empty;
        internal string channelName = string.Empty;
        internal bool isTrading;
        internal string? exchange;
        internal string? market;

        [DataMember(Order = 1)]
        public long RouteId
        {
            get => routeId;
            set => routeId = value;
        }

        [DataMember(Order = 2)]
        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        [DataMember(Order = 3)]
        public string UserId
        {
            get => userId;
            set => userId = value;
        }

        [DataMember(Order = 4)]
        public string ChannelName
        {
            get => channelName;
            set => channelName = value;
        }

        [DataMember(Order = 5)]
        public bool IsTrading
        {
            get => isTrading;
            set => isTrading = value;
        }

        [DataMember(Order = 6)]
        public string? Exchange
        {
            get => exchange;
            set => exchange = value;
        }

        [DataMember(Order = 7)]
        public string? Market
        {
            get => market;
            set => market = value;
        }

        public override string ToString()
        {
            return $"{UserId},{ChannelName},{Exchange ?? Market}";
        }
    }
}
