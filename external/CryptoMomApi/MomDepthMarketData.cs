using System.Runtime.Serialization;

namespace MomCrypto.Api
{
    [DataContract]
    public struct MomDepthMarketData
    {
        public int InstrumentIndex;

        [DataMember(Order = 1)]
        public string TradingDay;

        [DataMember(Order = 2)]
        public string Symbol;

        [DataMember(Order = 3)]
        public string ExchangeId;

        [DataMember(Order = 4)]
        public string ExchangeSymbol;

        [DataMember(Order = 5)]
        public decimal LastPrice;

        [DataMember(Order = 6)]
        public decimal PreSettlementPrice;

        [DataMember(Order = 7)]
        public decimal PreClosePrice;

        [DataMember(Order = 8)]
        public decimal PreOpenInterest;

        [DataMember(Order = 9)]
        public decimal OpenPrice;

        [DataMember(Order = 10)]
        public decimal HighestPrice;

        [DataMember(Order = 11)]
        public decimal LowestPrice;

        [DataMember(Order = 12)]
        public decimal Volume;

        [DataMember(Order = 13)]
        public decimal Turnover;

        [DataMember(Order = 14)]
        public decimal OpenInterest;

        [DataMember(Order = 15)]
        public decimal ClosePrice;

        [DataMember(Order = 16)]
        public decimal SettlementPrice;

        [DataMember(Order = 17)]
        public decimal UpperLimitPrice;

        [DataMember(Order = 18)]
        public decimal LowerLimitPrice;

        [DataMember(Order = 19)]
        public decimal TimeOffset;

        [DataMember(Order = 20)]
        public decimal CurrDelta;

        [DataMember(Order = 21)]
        public string UpdateTime;

        [DataMember(Order = 22)]
        public int UpdateMillisec;

        [DataMember(Order = 23)]
        public decimal BidPrice1;

        [DataMember(Order = 24)]
        public decimal BidVolume1;

        [DataMember(Order = 25)]
        public decimal AskPrice1;

        [DataMember(Order = 26)]
        public decimal AskVolume1;

        [DataMember(Order = 27)]
        public decimal BidPrice2;

        [DataMember(Order = 28)]
        public decimal BidVolume2;

        [DataMember(Order = 29)]
        public decimal AskPrice2;

        [DataMember(Order = 30)]
        public decimal AskVolume2;

        [DataMember(Order = 31)]
        public decimal BidPrice3;

        [DataMember(Order = 32)]
        public decimal BidVolume3;

        [DataMember(Order = 33)]
        public decimal AskPrice3;

        [DataMember(Order = 34)]
        public decimal AskVolume3;

        [DataMember(Order = 35)]
        public decimal BidPrice4;

        [DataMember(Order = 36)]
        public decimal BidVolume4;

        [DataMember(Order = 37)]
        public decimal AskPrice4;

        [DataMember(Order = 38)]
        public decimal AskVolume4;

        [DataMember(Order = 39)]
        public decimal BidPrice5;

        [DataMember(Order = 40)]
        public decimal BidVolume5;

        [DataMember(Order = 41)]
        public decimal AskPrice5;

        [DataMember(Order = 42)]
        public decimal AskVolume5;

        [DataMember(Order = 43)]
        public decimal AveragePrice;

        [DataMember(Order = 44)]
        public string ActionDay;

        [DataMember(Order = 45)]
        public decimal MarkPrice;

        [DataMember(Order = 46)]
        public decimal IndexPrice;

        [DataMember(Order = 47)]
        public decimal AskIV;

        [DataMember(Order = 48)]
        public decimal BidIV;

        [DataMember(Order = 49)]
        public decimal MarkIV;

        [DataMember(Order = 50)]
        public decimal Theta;

        [DataMember(Order = 51)]
        public decimal Vega;

        [DataMember(Order = 52)]
        public decimal Gamma;
        
        [DataMember(Order = 53)]
        public decimal Rho;
    }
}
