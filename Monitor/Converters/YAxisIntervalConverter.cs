using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Monitor.Converters
{
    [ValueConversion(typeof(double[]), typeof(double))]
    public class YAxisIntervalConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            double value0 = (double)values[0];
            double value1 = (double)values[1];

            double baseValue = Math.Abs(value1 - value0);
            double intervalValue = 0.05d;

            if (baseValue > 1.5 && baseValue <= 2.5)
            {
                intervalValue = 0.1d;
            }
            else if (baseValue > 2.5 && baseValue < 4)
            {
                intervalValue = 0.2d;
            }
            return intervalValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
