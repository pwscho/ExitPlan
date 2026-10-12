using System;
using System.Globalization;

namespace ExitPlan.Core.Reporting
{
    public static class RetirementSummaryFormat
    {
        public static string CompactCurrency(decimal value)
        {
            var abs = Math.Abs(value);
            var sign = value < 0 ? "-" : string.Empty;

            if (abs >= 1_000_000m)
                return $"{sign}${abs / 1_000_000m:0.00}M";
            if (abs >= 10_000m)
                return $"{sign}${abs / 1_000m:0}k";

            return value.ToString("C0", CultureInfo.CurrentCulture);
        }

        public static string Percent(decimal rate, int decimals = 0)
        {
            return (rate * 100m).ToString($"N{decimals}", CultureInfo.CurrentCulture) + "%";
        }

        /// <summary>
        /// Percent for values the Guardrails UI shows as 90, 2, 1.5 (fraction × 100, no forced decimals).
        /// </summary>
        public static string PercentTrim(decimal rate)
        {
            return (Math.Abs(rate) * 100m).ToString("0.##", CultureInfo.CurrentCulture) + "%";
        }

        public static string SuccessOrNa(StressTestMetric? metric)
        {
            if (metric == null)
                return "n/a";
            return Percent(metric.SuccessRate, 0);
        }

        public static string PathCountLabel(StressTestMetric? metric)
        {
            if (metric == null)
                return "not run";
            var unit = metric.PathCount == 1 ? "path" : "paths";
            return $"{metric.PathCount} {unit}";
        }

        /// <summary>
        /// Guardrail spending adjustments over the fixed path, for Spending Power details.
        /// </summary>
        public static string SpendingPowerAdjustments(int raises, int cuts)
        {
            return $"Raises: {raises} · Cuts: {cuts}";
        }

        public static string AppendSpendingPowerAdjustments(string? existingDetail, int raises, int cuts)
        {
            string adjustments = SpendingPowerAdjustments(raises, cuts);
            if (string.IsNullOrWhiteSpace(existingDetail))
                return adjustments;
            return $"{existingDetail}  ·  {adjustments}";
        }
    }
}
