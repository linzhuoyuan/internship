using System.ComponentModel;

namespace MomCrypto.Api
{
    public sealed class MomCashJournalTypeType
    {
        [Description(nameof(FundTransfer))]
        public const byte FundTransfer = (byte)'0';

        [Description(nameof(SubAccountTransfer))]
        public const byte SubAccountTransfer = (byte)'1';

        [Description(nameof(MainToUsdtFuture))]
        public const byte MainToUsdtFuture = (byte)'a';

        [Description(nameof(MainToCoinFuture))]
        public const byte MainToCoinFuture = (byte)'b';

        [Description(nameof(UsdtFutureToMain))]
        public const byte UsdtFutureToMain = (byte)'c';

        [Description(nameof(CoinFutureToMain))]
        public const byte CoinFutureToMain = (byte)'d';
    }
}