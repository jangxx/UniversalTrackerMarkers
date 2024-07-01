using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace UniversalTrackerMarkers
{
    [ValueConversion(typeof(double), typeof(string))]
    internal class DoubleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double val = (double)value;
            return val.ToString("N2");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? stringVal = value as string;

            if (stringVal == null)
            {
                return DependencyProperty.UnsetValue;
            }

            double resultValue;
            if (double.TryParse(stringVal, out resultValue))
            {
                return resultValue;
            }

            return DependencyProperty.UnsetValue;
        }
    }
}
