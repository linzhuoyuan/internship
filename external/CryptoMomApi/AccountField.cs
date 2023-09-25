using System.Runtime.Serialization;
using System.Threading;
using ProtoBuf;

namespace MomCrypto.Api
{
    [DataContract]
    [ProtoInclude(40, typeof(MomAccount))]
    [ProtoInclude(80, typeof(MomFundAccount))]
    public abstract class AccountField
    {
        internal string userId = null!;
        internal string accountId = null!;
        internal string accountType = null!;
        internal string accountName = null!;
        internal string currencyType = null!;
        internal string? fundAccountId;
        internal string? channelType;
        internal string? fundChannelType;
        internal string exchange = null!;
        internal string market = null!;
        internal decimal preBalance;
        internal decimal preMargin;
        internal decimal preEquity;
        internal decimal deposit;
        internal decimal withdraw;
        internal decimal currMargin;
        internal decimal frozenMargin;
        internal decimal unfrozenMargin;
        internal decimal commission;
        internal decimal frozenCommission;
        internal decimal unfrozenCommission;
        internal decimal premiumIn;
        internal decimal premiumOut;
        internal decimal frozenPremium;
        internal decimal unfrozenPremium;
        internal decimal closeProfit;
        internal decimal positionProfit;
        internal decimal balance;
        internal decimal available;
        internal decimal riskDegree;
        internal string? customData;
        internal int version;

        [DataMember(Order = 1)]
        public long FundId
        {
            get => fundId;
            set => fundId = value;
        }
        internal long fundId;

        [DataMember(Order = 2)]
        public string? ChannelType
        {
            get => channelType;
            set => channelType = value;
        }

        [DataMember(Order = 3)]
        public string UserId
        {
            get => userId;
            set => userId = value;
        }

        [DataMember(Order = 4)]
        public string? FundAccountId
        {
            get => fundAccountId;
            set => fundAccountId = value;
        }

        [DataMember(Order = 5)]
        public string Exchange
        {
            get => exchange;
            set => exchange = value;
        }

        [DataMember(Order = 6)]
        public string Market
        {
            get => market;
            set => market = value;
        }

        [DataMember(Order = 7)]
        public string? FundChannelType
        {
            get => fundChannelType;
            set => fundChannelType = value;
        }

        [DataMember(Order = 8)]
        public string AccountId
        {
            get => accountId;
            set => accountId = value;
        }

        [DataMember(Order = 9)]
        public string AccountType
        {
            get => accountType;
            set => accountType = value;
        }

        [DataMember(Order = 10)]
        public string AccountName
        {
            get => accountName;
            set => accountName = value;
        }

        [DataMember(Order = 11)]
        public decimal PreBalance
        {
            get => preBalance;
            set => preBalance = value;
        }

        [DataMember(Order = 12)]
        public decimal PreMargin
        {
            get => preMargin;
            set => preMargin = value;
        }

        [DataMember(Order = 13)]
        public decimal PreEquity
        {
            get => preEquity;
            set => preEquity = value;
        }

        [DataMember(Order = 14)]
        public decimal Deposit
        {
            get => deposit;
            set => deposit = value;
        }

        [DataMember(Order = 15)]
        public decimal Withdraw
        {
            get => withdraw;
            set => withdraw = value;
        }

        [DataMember(Order = 16)]
        public decimal CurrMargin
        {
            get => currMargin;
            set => currMargin = value;
        }

        [DataMember(Order = 17)]
        public decimal FrozenMargin
        {
            get => frozenMargin;
            set => frozenMargin = value;
        }

        [DataMember(Order = 18)]
        public decimal UnfrozenMargin
        {
            get => unfrozenMargin;
            set => unfrozenMargin = value;
        }

        [DataMember(Order = 19)]
        public decimal Commission
        {
            get => commission;
            set => commission = value;
        }

        [DataMember(Order = 20)]
        public decimal FrozenCommission
        {
            get => frozenCommission;
            set => frozenCommission = value;
        }

        [DataMember(Order = 21)]
        public decimal UnfrozenCommission
        {
            get => unfrozenCommission;
            set => unfrozenCommission = value;
        }

        [DataMember(Order = 22)]
        public decimal PremiumIn
        {
            get => premiumIn;
            set => premiumIn = value;
        }

        [DataMember(Order = 23)]
        public decimal PremiumOut
        {
            get => premiumOut;
            set => premiumOut = value;
        }

        [DataMember(Order = 24)]
        public decimal FrozenPremium
        {
            get => frozenPremium;
            set => frozenPremium = value;
        }

        [DataMember(Order = 25)]
        public decimal UnfrozenPremium
        {
            get => unfrozenPremium;
            set => unfrozenPremium = value;
        }

        [DataMember(Order = 26)]
        public decimal CloseProfit
        {
            get => closeProfit;
            set => closeProfit = value;
        }

        [DataMember(Order = 27)]
        public decimal PositionProfit
        {
            get => positionProfit;
            set => positionProfit = value;
        }

        [DataMember(Order = 28)]
        public decimal Balance
        {
            get => balance;
            set => balance = value;
        }

        [DataMember(Order = 29)]
        public decimal Available
        {
            get => available;
            set => available = value;
        }

        [DataMember(Order = 30)]
        public string CurrencyType
        {
            get => currencyType;
            set => currencyType = value;
        }

        [DataMember(Order = 31)]
        public decimal RiskDegree
        {
            get => riskDegree;
            set => riskDegree = value;
        }

        [DataMember(Order = 32)]
        public string? CustomData
        {
            get => customData;
            set => customData = value;
        }

        [DataMember(Order = 33)]
        public int Version
        {
            get => version;
            set => version = value;
        }

        public void UpdateVersion()
        {
            Interlocked.Increment(ref version);
        }

        public void CalcRiskDegree()
        {
            var marginValid = available + positionProfit;
            riskDegree = marginValid > 0 ? currMargin / marginValid : 0;
        }

        public void Reset()
        {
            commission = 0;
            currMargin = 0;
            premiumIn = 0;
            premiumOut = 0;
            frozenCommission = 0;
            unfrozenCommission = 0;
            frozenPremium = 0;
            unfrozenPremium = 0;
            frozenMargin = 0;
            unfrozenMargin = 0;
            closeProfit = 0;
        }
    }
}
