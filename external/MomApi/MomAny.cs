namespace Quantmom.Api
{
    public class MomAny
    {
        public static readonly MomAny Empty;

        static MomAny()
        {
            Empty = new MomAny();
        }

        protected readonly object? refValue;
        protected readonly MomDepthMarketData marketData;

        public MomAny(int value)
        {
            IntValue = value;
        }
        public MomAny(ref MomDepthMarketData value)
        {
            marketData = value;
        }
        public MomAny(string[] value)
        {
            StringArray = value;
        }
        public MomAny(object value)
        {
            refValue = value;
        }
        private MomAny()
        {
        }

        public readonly int IntValue;
        public readonly string[]? StringArray;

        public void GetMarketData(out MomDepthMarketData value)
        {
            value = marketData;
        }
        public MomSpecificInstrument? AsSpecificInstrument => refValue as MomSpecificInstrument;
        public MomInputOrder? AsInputOrder => refValue as MomInputOrder;
        public MomInputOrderAction? AsInputOrderAction => refValue as MomInputOrderAction;
        public MomQryInstrument? AsQryInstrument => refValue as MomQryInstrument;
        public MomInstrument? AsInstrument => refValue as MomInstrument;
        public MomOrder? AsOrder => refValue as MomOrder;
        public MomTrade? AsTrade => refValue as MomTrade;
        public MomAccount? AsAccount => refValue as MomAccount;
        public MomPosition? AsPosition => refValue as MomPosition;
        public MomPositionDetail? AsPositionDetail => refValue as MomPositionDetail;
        public MomRspUserLogin? AsRspUserLogin => refValue as MomRspUserLogin;
        public MomReqUserLogin? AsReqUserLogin => refValue as MomReqUserLogin;
        public MomQryOrder? AsQryOrder => refValue as MomQryOrder;
        public MomQryTrade? AsQryTrade => refValue as MomQryTrade;
        public MomQryAccount? AsQryAccount => refValue as MomQryAccount;
        public MomQryPosition? AsQryPosition => refValue as MomQryPosition;
        public MomQryPositionDetail? AsQryPositionDetail => refValue as MomQryPositionDetail;
        public MomQryCashJournal? AsQryCashJournal => refValue as MomQryCashJournal;
        public MomFund? AsFund => refValue as MomFund;
        public MomFundAccount? AsFundAccount => refValue as MomFundAccount;
        public MomFundPosition? AsFundPosition => refValue as MomFundPosition;
        public MomFundOrder? AsFundOrder => refValue as MomFundOrder;
        public MomUser? AsUser => refValue as MomUser;
        public MomCashJournal? AsCashJournal => refValue as MomCashJournal;
        public MomDataAction? AsDataAction => refValue as MomDataAction;
        public MomSubscribeResponse? AsSubscribeResponse => refValue as MomSubscribeResponse;
        public MomQryField? AsQryField => refValue as MomQryField;
        public MomTradingChannel? AsTradingChannel => refValue as MomTradingChannel;
        public MomTradingRoute? AsTradingRoute => refValue as MomTradingRoute;
        public MomRiskExpression? AsRiskExpression => refValue as MomRiskExpression;
        public MomPerformance? AsPerformance => refValue as MomPerformance;
        public MomFundPerformance? AsFundPerformance => refValue as MomFundPerformance;
        public MomQryPerformance? AsQryPerformance => refValue as MomQryPerformance;
        public MomNotice? AsNotice => refValue as MomNotice;
        public MomQryUncheckInput? AsQryUncheckInput => refValue as MomQryUncheckInput;
    }
}