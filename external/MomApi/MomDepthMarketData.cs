using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Quantmom.Api
{
    [DataContract]
    public struct MomDepthMarketData
    {
        public int InstrumentIndex;
        public DateTime ExchangeTime;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public decimal GetPrice() => (decimal)LastPrice;

        [DataMember(Order = 1)]
        public string TradingDay;

        [DataMember(Order = 2)]
        public string Symbol;

        [DataMember(Order = 3)]
        public string ExchangeId;

        [DataMember(Order = 4)]
        public string ExchangeSymbol;

        [DataMember(Order = 5)]
        public double LastPrice;

        [DataMember(Order = 6)]
        public double PreSettlementPrice;

        [DataMember(Order = 7)]
        public double PreClosePrice;

        [DataMember(Order = 8)]
        public double PreOpenInterest;

        [DataMember(Order = 9)]
        public double OpenPrice;

        [DataMember(Order = 10)]
        public double HighestPrice;

        [DataMember(Order = 11)]
        public double LowestPrice;

        [DataMember(Order = 12)]
        public long Volume;

        [DataMember(Order = 13)]
        public double Turnover;

        [DataMember(Order = 14)]
        public double OpenInterest;

        [DataMember(Order = 15)]
        public double ClosePrice;

        [DataMember(Order = 16)]
        public double SettlementPrice;

        [DataMember(Order = 17)]
        public double UpperLimitPrice;

        [DataMember(Order = 18)]
        public double LowerLimitPrice;

        [DataMember(Order = 19)]
        public double PreDelta;

        [DataMember(Order = 20)]
        public double CurrDelta;

        [DataMember(Order = 21)]
        public string UpdateTime;

        [DataMember(Order = 22)]
        public int UpdateMillisec;

        [DataMember(Order = 23)]
        public double BidPrice1;

        [DataMember(Order = 24)]
        public int BidVolume1;

        [DataMember(Order = 25)]
        public double AskPrice1;

        [DataMember(Order = 26)]
        public int AskVolume1;

        [DataMember(Order = 27)]
        public double BidPrice2;

        [DataMember(Order = 28)]
        public int BidVolume2;

        [DataMember(Order = 29)]
        public double AskPrice2;

        [DataMember(Order = 30)]
        public int AskVolume2;

        [DataMember(Order = 31)]
        public double BidPrice3;

        [DataMember(Order = 32)]
        public int BidVolume3;

        [DataMember(Order = 33)]
        public double AskPrice3;

        [DataMember(Order = 34)]
        public int AskVolume3;

        [DataMember(Order = 35)]
        public double BidPrice4;

        [DataMember(Order = 36)]
        public int BidVolume4;

        [DataMember(Order = 37)]
        public double AskPrice4;

        [DataMember(Order = 38)]
        public int AskVolume4;

        [DataMember(Order = 39)]
        public double BidPrice5;

        [DataMember(Order = 40)]
        public int BidVolume5;

        [DataMember(Order = 41)]
        public double AskPrice5;

        [DataMember(Order = 42)]
        public int AskVolume5;

        [DataMember(Order = 43)]
        public double AveragePrice;

        [DataMember(Order = 44)]
        public string ActionDay;

        [DataMember(Order = 45)]
        public decimal MarkPrice;

        [DataMember(Order = 46)]
        public decimal IndexPrice;

        public MomDepthMarketData()
        {
            InstrumentIndex = -1;
            ExchangeTime = default;
            TradingDay = string.Empty;
            Symbol = string.Empty;
            ExchangeId = string.Empty;
            ExchangeSymbol = string.Empty;
            LastPrice = 0;
            PreSettlementPrice = 0;
            PreClosePrice = 0;
            PreOpenInterest = 0;
            OpenPrice = 0;
            HighestPrice = 0;
            LowestPrice = 0;
            Volume = 0;
            Turnover = 0;
            OpenInterest = 0;
            ClosePrice = 0;
            SettlementPrice = 0;
            UpperLimitPrice = 0;
            LowerLimitPrice = 0;
            PreDelta = 0;
            CurrDelta = 0;
            UpdateTime = string.Empty;
            UpdateMillisec = 0;
            BidPrice1 = 0;
            BidVolume1 = 0;
            AskPrice1 = 0;
            AskVolume1 = 0;
            BidPrice2 = 0;
            BidVolume2 = 0;
            AskPrice2 = 0;
            AskVolume2 = 0;
            BidPrice3 = 0;
            BidVolume3 = 0;
            AskPrice3 = 0;
            AskVolume3 = 0;
            BidPrice4 = 0;
            BidVolume4 = 0;
            AskPrice4 = 0;
            AskVolume4 = 0;
            BidPrice5 = 0;
            BidVolume5 = 0;
            AskPrice5 = 0;
            AskVolume5 = 0;
            AveragePrice = 0;
            ActionDay = string.Empty;
            MarkPrice = 0;
            IndexPrice = 0;
        }
    }
}
