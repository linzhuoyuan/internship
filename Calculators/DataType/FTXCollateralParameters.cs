using CsvHelper.Configuration.Attributes;

namespace Calculators.DataType
{
    public class FTXCollateralParameters
    {
        [Name("coin")]
        public string Coin { get; set; }
        [Name("totalWeight")]
        public decimal TotalWeight { get; set; }
        [Name("initialWeight")]
        public decimal InitialWeight { get; set; }
        [Name("imfFactor")]
        public decimal IMFFactor { get; set; }

        public FTXCollateralParameters(string coin, decimal totalWeight, decimal initialWeight, decimal imfFactor)
        {
            Coin = coin;
            TotalWeight = totalWeight;
            InitialWeight = initialWeight;
            IMFFactor = imfFactor;
        }

        public FTXCollateralParameters Copy(decimal totalWeight)
        {
            return new FTXCollateralParameters(Coin, totalWeight, InitialWeight, IMFFactor);
        }
    }
}
