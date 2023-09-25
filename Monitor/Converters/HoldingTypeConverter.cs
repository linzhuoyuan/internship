using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Monitor.Converters
{
    [ValueConversion(typeof(string), typeof(string))]
    public class HoldingTypeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string holdingType = string.Empty;
            if (value != null)
            {               
                string _type = value.ToString();
                if (_type == "0")
                {
                    holdingType = Application.Current.TryFindResource("type_net") as string;
                }
                else if (_type == "1")
                {
                    holdingType = Application.Current.TryFindResource("type_long") as string;
                }
                else if (_type == "2")
                {
                    holdingType = Application.Current.TryFindResource("type_short") as string;
                }
            }
            return holdingType;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
