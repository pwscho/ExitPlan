using System;
using System.Globalization;
using System.Windows.Data;

namespace ExitPlan.UI.Converters
{
    public class LessThanOrEqualConverter : IValueConverter
    {
        // value = the number being tested
        // parameter = the threshold (e.g., "0")
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            if (!decimal.TryParse(value.ToString(), out decimal number))
                return false;

            if (!decimal.TryParse(parameter.ToString(), out decimal threshold))
                return false;

            return number <= threshold;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
