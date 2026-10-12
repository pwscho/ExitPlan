using System;
using System.Globalization;
using System.Windows.Data;

namespace ExitPlan.UI.Converters
{
    /// <summary>
    /// Displays report-card weights as whole/fractional points (25 = 25%)
    /// and accepts typed values with or without a trailing %.
    /// </summary>
    public class ReportCardWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal d)
                return d.ToString("0.##", culture);
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value?.ToString()?.Trim();
            if (string.IsNullOrEmpty(text))
                return 0m;

            if (text.EndsWith("%", StringComparison.Ordinal))
                text = text[..^1].Trim();

            if (decimal.TryParse(text, NumberStyles.Number, culture, out var result))
                return result < 0m ? 0m : result;

            return Binding.DoNothing;
        }
    }
}
