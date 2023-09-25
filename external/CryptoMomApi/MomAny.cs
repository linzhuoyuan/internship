using System.Collections.Generic;

namespace MomCrypto.Api
{
    public class MomAny
    {
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
        public MomAny()
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
        public IList<MomInstrument>? AsInstrumentList => refValue as IList<MomInstrument>;
        public MomOrder? AsOrder => refValue as MomOrder;
        public MomTrade? AsTrade => refValue as MomTrade;
        public MomAccount? AsAccount => refValue as MomAccount;
        public MomPosition? AsPosition => refValue as MomPosition;
        public MomRspUserLogin? AsRspUserLogin => refValue as MomRspUserLogin;
        public MomReqUserLogin? AsReqUserLogin => refValue as MomReqUserLogin;
        public MomQryOrder? AsQryOrder => refValue as MomQryOrder;
        public MomQryTrade? AsQryTrade => refValue as MomQryTrade;
        public MomQryAccount? AsQryAccount => refValue as MomQryAccount;
        public MomQryPosition? AsQryPosition => refValue as MomQryPosition;
        public MomFund? AsFund => refValue as MomFund;
        public MomFundOrder? AsFundOrder => refValue as MomFundOrder;
        public MomFundAccount? AsFundAccount => refValue as MomFundAccount;
        public MomFundPosition? AsFundPosition => refValue as MomFundPosition;
        public MomUserAction? AsUserAction => refValue as MomUserAction;
        public MomChangeLeverage? AsChangeLeverage => refValue as MomChangeLeverage;
        public MomCashJournal? AsCashJournal => refValue as MomCashJournal;
    }
}