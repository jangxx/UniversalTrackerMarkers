﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace UniversalTrackerMarkers
{
    [ValueConversion(typeof(int), typeof(string))]
    internal class IntConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int val = (int)value;
            return val.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? stringVal = value as string;

            if (stringVal == null)
            {
                return DependencyProperty.UnsetValue;
            }

            int resultValue;
            if (int.TryParse(stringVal, out resultValue))
            {
                return resultValue;
            }

            return DependencyProperty.UnsetValue;
        }
    }
}
