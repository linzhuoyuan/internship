using System;
using System.Globalization;
using Newtonsoft.Json;

namespace FtxApi.Rest.Models
{
    public class FtxOption
    {
        [JsonProperty("id")]
        public long Id;
        [JsonProperty("underlying")]
        public string Underlying = null!;
        [JsonProperty("type")]
        public string Type = null!;
        [JsonProperty("strike")]
        public decimal Strike;
        [JsonProperty("expiry")]
        public DateTime Expiry;

        public override string ToString()
        {
            return $"{Underlying}-{char.ToUpper(Type[0])}-{Expiry.ToString("ddMMMyy", CultureInfo.CreateSpecificCulture("en-GB")).ToUpper()}-{Strike}";
        }

        public void Parse(string symbol)
        {
            var items = symbol.Split('-');
            if (items.Length < 4)
            {
                return;
            }

            Underlying = items[0];
            Type = items[1] == "C" ? "call" : "put";
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