using System;
using System.Globalization;
using System.Windows.Data;

namespace ExitPlan.UI.Converters
{
    public class DecimalToPercentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
                return (d * 100).ToString("0.##");
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (decimal.TryParse(value?.ToString(), out decimal result))
                return result / 100m;
            return 0m;
        }
    }
}


