using System;

namespace Calculators.DataType
{
    public class StopLossInfo
    {
        public string Coin { get; set; }
        public DateTime StartTime { get; set; }
        public double MaxLossBase { get; set; }
        public double MaxLossRatio { get; set; }
        public double MinProfit { get; set; }
        public int MaxVentureDays { get; set; }
        public bool IsStopLoss { get; set; }

        // TODO: Create own CSVHelper to allow ctor
        /*public StopLossInfo(string coin, DateTime startDate, double maxLossBase, double maxLossRatio, double minProfit)
        {
            Coin = coin;
            StartDate = startDate;
            MaxLossBase = maxLossBase;
            MaxLossRatio = maxLossRatio;
            MinProfit = minProfit;
        }*/

        public bool StopLoss(DateTime currentTime, double pnl, double price)
        {
            if (IsStopLoss)
            {
                return IsStopLoss;
            }
            var holdingDays = (currentTime - StartTime).TotalDays;

            if (MinProfit == 0 && holdingDays >= MaxVentureDays)
            {
                if (pnl > 0)
                {
                    MinProfit = pnl / price;
                }
                else
                {
                    IsStopLoss = true;
                    return IsStopLoss;
                }
            }

            if (MinProfit > 0)
            {
                return false;
            }

            var maxLoss = MaxLossBase * MaxLossRatio / MaxVentureDays * (MaxVentureDays - holdingDays);
            IsStopLoss = pnl <= -maxLoss;
            return IsStopLoss;
        }
    }
}
