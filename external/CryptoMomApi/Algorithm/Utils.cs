using System;
using System.Runtime.CompilerServices;

namespace MomCrypto.Api.Algorithm
{
    public class Utils
    {
        // Both trade volume and initial volume are signed, positive means buy/long
        //TODO: run time optimization can be done by adding extra indicator for direction
        public static decimal CalculateTradePnL(
            decimal tradeVolume, decimal tradePrice,
            out decimal updatedVolume, out decimal updatedPrice,
            decimal initialVolume = 0, decimal initialPrice = 0)
        {
            updatedVolume = tradeVolume + initialVolume;
            if (tradeVolume * initialVolume >= 0)
            {
                updatedPrice = (tradeVolume * tradePrice + initialVolume * initialPrice) / (tradeVolume + initialVolume);
                return 0;
            }
            if (tradeVolume * updatedVolume >= 0)
            {
                updatedPrice = updatedVolume == 0 ? 0 : tradePrice;
                return initialVolume * (tradePrice - initialPrice);
            }

            updatedPrice = initialPrice;
            return tradeVolume * (initialPrice - tradePrice);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static decimal GetCoinCloseProfit(decimal volume, decimal openPrice, decimal closePrice, decimal volumeMultiple)
        {
            return openPrice > 0 && closePrice > 0
                ? volume * (volumeMultiple / openPrice - volumeMultiple / closePrice)
                : 0;
        }

        // Both trade volume and initial volume are signed, positive means buy/long
        //TODO: run time optimization can be done by adding extra indicator for direction
        public static decimal CalculateCoinTradePnL(
            decimal tradeAmount, decimal tradePrice,
            out decimal updatedAmount,
            decimal initialAmount = 0, decimal initialPrice = 0)
        {
            updatedAmount = tradeAmount + initialAmount;
            if (tradeAmount * initialAmount >= 0)
            {
                return 0;
            }

            if (tradeAmount * updatedAmount >= 0)
            {
                //全部平仓
                return GetCoinCloseProfit(initialAmount, initialPrice, tradePrice, 1);
            }
            return GetCoinCloseProfit(tradeAmount, tradePrice, initialPrice, 1);
        }
    }
}
