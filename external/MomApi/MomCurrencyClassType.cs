using System.ComponentModel;

namespace Quantmom.Api
{
    public sealed class MomCurrencyClassType
    {
        [Description(nameof(Usd))]
        public const string Usd = "USD";
  
        [Description(nameof(Btc))]
        public const string Btc = "BTC";

        [Description(nameof(Eth))]
        public const string Eth = "ETH";

        [Description(nameof(Usdt))]
        public const string Usdt = "USDT";

        public static int GetIndex(string type)
        {
            return type switch
            {
                Usd => 0,
                Btc => 1,
                Eth => 2,
                Usdt => 3,
                _ => -1
            };
        }

        public static int GetCount() => 4;
    }
}
