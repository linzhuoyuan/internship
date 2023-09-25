using System.Runtime.Serialization;

namespace MomCrypto.Api
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
        internal string tradingDay;

        public MomHisPosition()
        {
        }

        public MomHisPosition(MomPosition position)
        {
            UserId = position.userId;
            AccountId = position.accountId;
            FundAccountId = position.fundAccountId;
            PositionId = position.positionId;
            InstrumentId = position.instrumentId;
            ExchangeSymbol = position.exchangeSymbol;
            ProductClass = position.productClass;
            ExchangeId = position.exchangeId;
            Position = position.position;
            SellFrozen = position.SellFrozen;
            BuyFrozen = position.BuyFrozen;
            SellUnfrozen = position.SellUnfrozen;
            BuyUnfrozen = position.BuyUnfrozen;
            BuyVolume = position.BuyVolume;
            SellVolume = position.SellVolume;
            BuyAmount = position.BuyAmount;
            SellAmount = position.SellAmount;
            PositionCost = position.positionCost;
            UseMargin = position.useMargin;
            Commission = position.commission;
            CloseProfit = position.closeProfit;
            PositionProfit = position.positionProfit;
            OpenCost = position.openCost;
            CashPosition = position.CashPosition;
        }
    }
}
