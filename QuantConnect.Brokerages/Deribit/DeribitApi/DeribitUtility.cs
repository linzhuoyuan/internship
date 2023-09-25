using System;
using System.Collections.Generic;
using System.Text;

namespace TheOne.Deribit
{
    public class DeribitUtility
    {
        private static readonly DateTime EpochTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            DateTime time;
            try
            {
                var ticks = unixTimeStamp * TimeSpan.TicksPerSecond;
                time = EpochTime.AddTicks((long)ticks);
            }
            catch (Exception err)
            {
                time = DateTime.Now;
            }
            return time;
        }

        public static DateTime UnixMillisecondTimeStampToDateTime(double unixTimeStamp)
        {
            DateTime time;
            try
            {
                var ticks = unixTimeStamp * TimeSpan.TicksPerMillisecond;
                time = EpochTime.AddTicks((long)ticks);
            }
            catch (Exception err)
            {
                time = DateTime.Now;
            }
            return time;
        }
    }
}
