namespace QuantConnect.Algorithm.CSharp.LiveStrategy.DataType
{
    public class OptionInPortfolio
    {
        //期权类型call or put
        public string OptionType { get;  set;}

        //期权行权价，如果是call，则为上线
        public decimal Strike { get; set; }

        //数量，单位为1
        public decimal Volume { get; set; }

        //如果是call，是否突破上线
        public bool IsCrossUpline { get; set; }

        //如果突破上线后回落，是否建新的期权组合
        public bool IsAddOption { get; set; }

        public OptionInPortfolio(string optionType, decimal strike, decimal volume, bool isCrossUpline,
            bool isAddOption)
        {
            OptionType = optionType;
            Strike = strike;
            Volume = volume;
            IsCrossUpline = isCrossUpline;
            IsAddOption = isAddOption;
        }
    }
}
