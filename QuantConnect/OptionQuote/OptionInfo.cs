using System;
using System.Globalization;

namespace QuantConnect.OptionQuote
{
    public class OptionInfo
    {
        public string Underlying;
        public OptionRight Type;
        public decimal Strike;
        public DateTime Expiry;

        public override string ToString()
        {
            return $"{Underlying}-{Type.ToString()[0]}-{Expiry.ToString("ddMMMyy", CultureInfo.CreateSpecificCulture("en-GB")).ToUpper()}-{Strike}";
        }

        public void Parse(string symbol)
        {
            var items = symbol.Split('-');
            if (items.Length < 4)
            {
                return;
            }

            Underlying = items[0];
            Type = items[1] == "C" ? OptionRight.Call : OptionRight.Put;
            Expiry = ParseExpiry(items[2]);
            Strike = decimal.Parse(items[3]);
        }

        public static DateTime ParseExpiry(string expiry)
        {
            DateTime.TryParseExact(expiry, "ddMMMyy", CultureInfo.CreateSpecificCulture("en-GB"), DateTimeStyles.None, out var date);
            return date;
        }
    }
}