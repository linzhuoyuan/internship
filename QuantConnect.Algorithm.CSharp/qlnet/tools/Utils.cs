using System;
using System.Collections.Generic;
using BSModel = QuantConnect.Algorithm.CSharp.LiveStrategy.PricingModels.BSModel;
using QuantConnect.Statistics;

namespace QuantConnect.Algorithm.CSharp.qlnet.tools
{
    public class Utils
    {
        public static decimal Var(List<decimal> v)
        {
            //    double tt = 2;
            //double mm = tt ^ 2;

            decimal sum1 = 0;
            for (int i = 0; i < v.Count; i++)
            {
                decimal temp = v[i] * v[i];
                sum1 = sum1 + temp;

            }

            decimal sum = 0;
            foreach (decimal d in v)
            {
                sum = sum + d;
            }

            decimal var = sum1 / v.Count - (sum / v.Count) * (sum / v.Count);
            return var;
        }

        //https://stackoverflow.com/questions/4124189/performing-math-operations-on-decimal-datatype-in-c
        public static decimal Sqrt(decimal x, decimal epsilon = 0.0M)
        {
            if (x < 0) throw new OverflowException("Cannot calculate square root from a negative number");

            decimal current = (decimal)Math.Sqrt((double)x), previous;
            do
            {
                previous = current;
                if (previous == 0.0M) return 0;
                current = (previous + x / previous) / 2;
            }
            while (Math.Abs(previous - current) > epsilon);
            return current;
        }

        public static decimal RoundTradeVolume(decimal tradeVolume, decimal lotSize)
        {
            return tradeVolume > 0
                    ? Math.Floor(tradeVolume / lotSize) * lotSize
                    : Math.Ceiling(tradeVolume / lotSize) * lotSize;
        }

        public static decimal CalculateMinOptionCost(MinOptionCostMode mode, decimal t2MDays, decimal costMultiplier,
            decimal underlyingPrice, decimal strike, decimal sigmaAddition, double sigma)
        {
            switch (mode)
            {
                case MinOptionCostMode.TimeDecay:
                {
                    var multiplier = t2MDays < 1 ? 0.0045m : 0.0025m;
                    var ratio = costMultiplier + (t2MDays - 1) * multiplier;
                    return ratio * underlyingPrice;
                }
                case MinOptionCostMode.BS:
                {
                    var basePrice = BSModel.GetOptionValue((double) underlyingPrice, (double) underlyingPrice * 1.02, 0,
                        0.07, sigma, 1.0 / 365.0, BSModel.EPutCall.Call);
                    var optionPrice = BSModel.GetOptionValue((double) underlyingPrice, (double) strike, 0, 0.07, sigma,
                        (double)t2MDays / 365.0, BSModel.EPutCall.Call);
                    return costMultiplier * underlyingPrice * (decimal)optionPrice / (decimal)basePrice;
                }
                default:
                    throw new ArgumentOutOfRangeException($"Cannot find min strike for mode {mode}");
            }
        }
        public static double GetTradingDaysToMaturity(DateTime utcExpiry, DateTime utcTime, List<DateTime> tradingDays)
        {
            var T2MDays = tradingDays.FindIndex(d => d == utcExpiry.Date) - tradingDays.FindIndex(d => d == utcTime.Date);
            var T2MExtraMinutes = (utcExpiry.TimeOfDay - utcTime.TimeOfDay).TotalMinutes - (utcTime.Hour < 4 ? 90.0 : 0);
            return T2MDays + T2MExtraMinutes / 240.0;
        }


        public enum MoveStrikeType
        {
            MoveDownCashGamma = -1,
            MoveBothCashGamma = 0,
            MoveUpCashGamma = 1
        }

        public enum OptionParityTradeMode
        {
            BuyBoth,
            OnlyBuyOTM,
            OnlyBuyITM
        }
    }

    
}
