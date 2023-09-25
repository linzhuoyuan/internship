using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public class MomTradingChannel
    {
        internal sbyte index;
        internal string name = string.Empty;
        internal string apiName = string.Empty;
        internal bool enabled;
        internal bool isTrading;
        internal bool isData;
        internal bool isDefault;
        internal string? userName;
        internal string? password;
        internal string? tradingServerAddress;
        internal string? tradingServerPort;
        internal string? marketDataServerAddress;
        internal string? marketDataServerPort;
        internal bool syncEnabled = true;
        internal int syncInterval = 60;
        internal bool encrypted;
        internal bool rsaEnable;
        internal string? keyFile;
        internal string? items;

        [DataMember(Order = 1)]
        public string Name
        {
            get => name;
            set => name = value;
        }

        [DataMember(Order = 2)]
        public string ApiName
        {
            get => apiName;
            set => apiName = value;
        }

        [DataMember(Order = 3)]
        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        [DataMember(Order = 4)]
        public bool IsTrading
        {
            get => isTrading;
            set => isTrading = value;
        }

        [DataMember(Order = 5)]
        public bool IsData
        {
            get => isData;
            set => isData = value;
        }

        [DataMember(Order = 6)]
        public bool IsDefault
        {
            get => isDefault;
            set => isDefault = value;
        }

        [DataMember(Order = 7)]
        public string? UserName
        {
            get => userName;
            set => userName = value;
        }

        [DataMember(Order = 8)]
        public string? Password
        {
            get => password;
            set => password = value;
        }

        [DataMember(Order = 9)]
        public string? TradingServerAddress
        {
            get => tradingServerAddress;
            set => tradingServerAddress = value;
        }

        [DataMember(Order = 10)]
        public string? TradingServerPort
        {
            get => tradingServerPort;
            set => tradingServerPort = value;
        }

        [DataMember(Order = 11)]
        public string? MarketDataServerAddress
        {
            get => marketDataServerAddress;
            set => marketDataServerAddress = value;
        }

        [DataMember(Order = 12)]
        public string? MarketDataServerPort
        {
            get => marketDataServerPort;
            set => marketDataServerPort = value;
        }

        [DataMember(Order = 13)]
        public bool SyncEnabled
        {
            get => syncEnabled;
            set => syncEnabled = value;
        }

        [DataMember(Order = 14)]
        public int SyncInterval
        {
            get => syncInterval;
            set => syncInterval = value;
        }

        [DataMember(Order = 15)]
        public bool Encrypted
        {
            get => encrypted;
            set => encrypted = value;
        }

        [DataMember(Order = 16)]
        public bool RsaEnable
        {
            get => rsaEnable;
            set => rsaEnable = value;
        }

        [DataMember(Order = 17)]
        public string? KeyFile
        {
            get => keyFile;
            set => keyFile = value;
        }

        [DataMember(Order = 18)]
        public string? Items
        {
            get => items;
            set => items = value;
        }

        public override string ToString()
        {
            return $"{Name},{ApiName},{Enabled}";
        }
    }
}
