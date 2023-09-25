using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Monitor.Converters
{
    [ValueConversion(typeof(string), typeof(SolidColorBrush))]
    public class DataColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double sourceValue;
            SolidColorBrush dataBrush = new SolidColorBrush(Colors.White);
            if (double.TryParse(value.ToString(), out sourceValue))
            {
                dataBrush = sourceValue > 0d ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Green);
            }
            return dataBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
