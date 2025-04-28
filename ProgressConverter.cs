using System;
using System.Globalization;
using System.Windows.Data;

namespace AI_Context_Generator
{
    public class ProgressConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = Value
            // values[1] = Minimum
            // values[2] = Maximum
            // values[3] = ActualWidth of track

            if (values.Length < 4 ||
                values[0] is not double value ||
                values[1] is not double minimum ||
                values[2] is not double maximum ||
                values[3] is not double width)
            {
                return 0.0;
            }

            double range = maximum - minimum;
            if (range <= 0)
                return 0.0;

            value = Math.Max(minimum, Math.Min(maximum, value));
            double percentage = (value - minimum) / range;
            return percentage * width;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}