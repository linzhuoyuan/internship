using System.Runtime.Serialization;
using System.Threading;
using ProtoBuf;

namespace Quantmom.Api
{
    [DataContract]
    [ProtoInclude(40, typeof(MomAccount))]
    [ProtoInclude(80, typeof(MomFundAccount))]
    public abstract class AccountField
    {
        internal string accountId = string.Empty;
        internal string accountType = string.Empty;
        internal string accountName = string.Empty;
        internal string currencyType = string.Empty;
        internal long fundId;
        internal string? fundAccountId;
        internal string? channelType;
        internal string? userId;
        internal string? exchange;
        internal string? market;
        internal string? fundChannelType;
        internal decimal preBalance;
        internal decimal guaranteeRate = -1;
        internal decimal buyingPower;
        internal decimal deposit;
        internal decimal withdraw;
        internal decimal maintMargin;
        internal decimal frozenMargin;
        internal decimal commission;
        internal decimal frozenCommission;
        internal decimal premium;
        internal decimal frozenPremium;
        internal decimal financingUsed;
        internal decimal frozenFinancing;
        internal decimal realizedPnL;
        internal decimal unrealizedPnL;
        internal decimal cashBalance;
        internal decimal available;
        internal decimal financingCommission;
        internal decimal financingRate;
        internal string? customData;
        internal int version;

        [DataMember(Order = 1)]
        public long FundId
        {
            get => fundId;
            set => fundId = value;
        }

        [DataMember(Order = 2)]
        public string? ChannelType
        {
            get => channelType;
            set => channelType = value;
        }

        [DataMember(Order = 3)]
        public string? UserId
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
        public string? Exchange
        {
            get => exchange;
            set => exchange = value;
        }

        [DataMember(Order = 6)]
        public string? Market
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
        public decimal GuaranteeRate
        {
            get => guaranteeRate;
            set => guaranteeRate = value;
        }

        [DataMember(Order = 13)]
        public decimal BuyingPower
        {
            get => buyingPower;
            set => buyingPower = value;
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
        public decimal MaintMargin
        {
            get => maintMargin;
            set => maintMargin = value;
        }

        [DataMember(Order = 17)]
        public decimal FrozenMargin
        {
            get => frozenMargin;
            set => frozenMargin = value;
        }

        [DataMember(Order = 18)]
        public decimal Commission
        {
            get => commission;
            set => commission = value;
        }

        [DataMember(Order = 19)]
        public decimal FrozenCommission
        {
            get => frozenCommission;
            set => frozenCommission = value;
        }

        [DataMember(Order = 20)]
        public decimal Premium
        {
            get => premium;
            set => premium = value;
        }

        [DataMember(Order = 21)]
        public decimal FrozenPremium
        {
            get => frozenPremium;
            set => frozenPremium = value;
        }

        [DataMember(Order = 22)]
        public decimal FinancingUsed
        {
            get => financingUsed;
            set => financingUsed = value;
        }

        [DataMember(Order = 23)]
        public decimal FrozenFinancing
        {
            get => frozenFinancing;
            set => frozenFinancing = value;
        }

        [DataMember(Order = 24)]
        public decimal RealizedPnL
        {
            get => realizedPnL;
            set => realizedPnL = value;
        }

        [DataMember(Order = 25)]
        public decimal UnrealizedPnL
        {
            get => unrealizedPnL;
            set => unrealizedPnL = value;
        }

        [DataMember(Order = 26)]
        public decimal CashBalance
        {
            get => cashBalance;
            set => cashBalance = value;
        }

        [DataMember(Order = 27)]
        public decimal Available
        {
            get => available;
            set => available = value;
        }

        [DataMember(Order = 28)]
        public decimal FinancingCommission
        {
            get => financingCommission;
            set => financingCommission = value;
        }

        [DataMember(Order = 29)]
        public decimal FinancingRate
        {
            get => financingRate;
            set => financingRate = value;
        }

        [DataMember(Order = 30)]
        public string CurrencyType
        {
            get => currencyType;
            set => currencyType = value;
        }

        [DataMember(Order = 31)]
        public string? CustomData
        {
            get => customData;
            set => customData = value;
        }

        [DataMember(Order = 32)]
        public int Version
        {
            get => version;
            set => version = value;
        }

        public void UpdateVersion()
        {
            Interlocked.Increment(ref version);
        }
        
        public override string ToString()
        {
            return $"{AccountId},{AccountName},{AccountType},{PreBalance},{Available}";
        }

        protected AccountField()
        {
        }

        protected AccountField(AccountField account)
        {
            accountId = account.accountId;
            accountType = account.accountType;
            accountName = account.accountName;
            currencyType = account.currencyType;
            fundId = account.fundId;
            fundAccountId = account.fundAccountId;
            channelType = account.channelType;
            userId = account.userId;
            exchange = account.exchange;
            market = account.market;
            fundChannelType = account.fundChannelType;
            preBalance = account.preBalance;
            guaranteeRate = account.guaranteeRate;
            buyingPower = account.buyingPower;
            deposit = account.deposit;
            withdraw = account.withdraw;
            maintMargin = account.maintMargin;
            frozenMargin = account.frozenMargin;
            commission = account.commission;
            frozenCommission = account.frozenCommission;
            premium = account.premium;
            frozenPremium = account.frozenPremium;
            realizedPnL = account.realizedPnL;
            unrealizedPnL = account.unrealizedPnL;
            cashBalance = account.cashBalance;
            available = account.available;
            financingUsed = account.financingUsed;
            version = account.version;
        }

        public void Reset()
        {
            commission = 0;
            maintMargin = 0;
            premium = 0;
            financingUsed = 0;
            frozenCommission = 0;
            frozenPremium = 0;
            frozenMargin = 0;
            frozenFinancing = 0;
            realizedPnL = 0;
            unrealizedPnL = 0;
        }
    }
}
