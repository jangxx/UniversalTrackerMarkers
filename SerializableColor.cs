using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace UniversalTrackerMarkers
{
    public class SerializableColor
    {
        public static readonly SerializableColor White = new SerializableColor(255, 255, 255);

        public SerializableColor(byte r, byte g, byte b)
        {
            R = r; G = g; B = b;
        }

        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
    }

    [ValueConversion(typeof(SerializableColor), typeof(Color))]
    internal class SerializableColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var val = (SerializableColor)value;

            return Color.FromRgb(val.R, val.G, val.B);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var colorVal = (Color)value;

            return new SerializableColor(colorVal.R, colorVal.G, colorVal.B);
        }
    }
}
