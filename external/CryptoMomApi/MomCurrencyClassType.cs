using System.ComponentModel;

namespace MomCrypto.Api
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

        [Description(nameof(Usdc))]
        public const string Usdc = "USDC";

        [Description(nameof(Busd))]
        public const string Busd = "BUSD";

        public static int GetIndex(string type)
        {
            return type switch
            {
                Usd => 0,
                Btc => 1,
                Eth => 2,
                Usdt => 3,
                Usdc => 4,
                Busd => 5,
                _ => -1
            };
        }
    }
}
