using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ExitPlan.Core.Taxes;

namespace ExitPlan.Core.Reporting
{
    public static class RetirementSummaryCharts
    {
        public static string DonutSvg(IReadOnlyList<PortfolioSlice> slices)
        {
            var sb = new StringBuilder();
            sb.Append("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 220 210">""");
            if (slices == null || slices.Count == 0)
            {
                sb.Append("</svg>");
                return sb.ToString();
            }

            const double cx = 110;
            const double cy = 105;
            const double radius = 72;
            const double stroke = 34;
            double start = -90;
            foreach (var slice in slices)
            {
                double sweep = 360.0 * (double)slice.Share;
                if (sweep <= 0.05)
                    continue;
                if (sweep >= 359.9)
                {
                    sb.Append(CultureInfo.InvariantCulture,
                        $"<circle cx='{cx}' cy='{cy}' r='{radius}' fill='none' stroke='{slice.ColorHex}' stroke-width='{stroke}' />");
                    break;
                }

                double drawStart = start - 0.25;
                double drawSweep = Math.Min(359.9, sweep + 0.5);
                sb.Append(Arc(cx, cy, radius, drawStart, drawSweep, slice.ColorHex, stroke));
                start += sweep;
            }

            sb.Append("</svg>");
            return sb.ToString();
        }

        public static string SuccessBarsSvg(
            StressTestMetric? historical,
            StressTestMetric? monteCarlo,
            StressTestMetric? fixedMarket)
        {
            var bars = new[]
            {
                (Label: "Historical", Metric: historical, Color: "#2E5A88"),
                (Label: "Monte Carlo", Metric: monteCarlo, Color: "#4A9B9B"),
                (Label: "Fixed market", Metric: fixedMarket, Color: "#7CB342")
            };

            const double width = 520;
            const double height = 210;
            const double left = 36;
            const double right = 508;
            const double top = 18;
            const double bottom = 162;
            double chartHeight = bottom - top;
            double chartWidth = right - left;

            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture,
                $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}">""");

            for (int pct = 20; pct <= 100; pct += 20)
            {
                double y = bottom - chartHeight * (pct / 100.0);
                sb.Append(CultureInfo.InvariantCulture,
                    $"<line x1='{left}' y1='{y:0.##}' x2='{right}' y2='{y:0.##}' stroke='#E4EAF0' />");
                sb.Append(CultureInfo.InvariantCulture,
                    $"<text x='8' y='{y + 3:0.##}' font-size='9' fill='#5F6B73'>{pct}</text>");
            }

            double eightyY = bottom - chartHeight * 0.80;
            sb.Append(CultureInfo.InvariantCulture,
                $"<line x1='{left}' y1='{eightyY:0.##}' x2='{right}' y2='{eightyY:0.##}' stroke='#9AA8B3' stroke-dasharray='6 4' />");
            sb.Append(CultureInfo.InvariantCulture,
                $"<line x1='{left}' y1='{bottom}' x2='{right}' y2='{bottom}' stroke='#C5D0DA' />");

            double slot = chartWidth / bars.Length;
            double barWidth = Math.Min(72, slot * 0.42);

            for (int i = 0; i < bars.Length; i++)
            {
                double cx = left + slot * (i + 0.5);
                double x = cx - barWidth / 2;
                var metric = bars[i].Metric;
                if (metric == null)
                {
                    sb.Append(CultureInfo.InvariantCulture,
                        $"<text x='{cx:0.##}' y='{bottom - 14:0.##}' text-anchor='middle' font-size='12' font-weight='700' fill='#1F3344'>n/a</text>");
                }
                else
                {
                    double barHeight = chartHeight * (double)metric.SuccessRate;
                    double y = bottom - barHeight;
                    sb.Append(CultureInfo.InvariantCulture,
                        $"<rect x='{x:0.##}' y='{y:0.##}' width='{barWidth:0.##}' height='{Math.Max(0, barHeight):0.##}' rx='3' fill='{bars[i].Color}' />");
                    string label = Escape(RetirementSummaryFormat.Percent(metric.SuccessRate, 0));
                    double labelY = Math.Max(top + 10, y - 6);
                    sb.Append(CultureInfo.InvariantCulture,
                        $"<text x='{cx:0.##}' y='{labelY:0.##}' text-anchor='middle' font-size='12' font-weight='700' fill='#1F3344'>{label}</text>");
                }

                sb.Append(CultureInfo.InvariantCulture,
                    $"<text x='{cx:0.##}' y='{bottom + 16:0.##}' text-anchor='middle' font-size='10' fill='#5F6B73'>{Escape(bars[i].Label)}</text>");
                sb.Append(CultureInfo.InvariantCulture,
                    $"<text x='{cx:0.##}' y='{bottom + 30:0.##}' text-anchor='middle' font-size='9' fill='#5F6B73'>{Escape(RetirementSummaryFormat.PathCountLabel(metric))}</text>");
            }

            sb.Append("</svg>");
            return sb.ToString();
        }

        public static string SpendingTimelineSvg(IReadOnlyList<SpendingYearPoint> points)
        {
            const double width = 540;
            const double height = 210;
            const double left = 58;
            const double right = 528;
            const double top = 12;
            const double bottom = 178;
            double chartWidth = right - left;
            double chartHeight = bottom - top;

            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture,
                $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}">""");

            if (points == null || points.Count == 0)
            {
                sb.Append("</svg>");
                return sb.ToString();
            }

            decimal max = points.Max(p => p.TotalSpending);
            if (max <= 0m)
                max = 1m;

            sb.Append(CultureInfo.InvariantCulture,
                $"<line x1='{left}' y1='{bottom}' x2='{right}' y2='{bottom}' stroke='#C5D0DA' />");
            sb.Append(CultureInfo.InvariantCulture,
                $"<line x1='{left}' y1='{top}' x2='{left}' y2='{bottom}' stroke='#C5D0DA' />");

            for (int i = 1; i <= 4; i++)
            {
                double y = bottom - chartHeight * (i / 4.0);
                sb.Append(CultureInfo.InvariantCulture,
                    $"<line x1='{left}' y1='{y:0.##}' x2='{right}' y2='{y:0.##}' stroke='#E4EAF0' />");
                string axis = Escape(RetirementSummaryFormat.CompactCurrency(max * i / 4m));
                sb.Append(CultureInfo.InvariantCulture,
                    $"<text x='4' y='{y + 3:0.##}' font-size='9' fill='#5F6B73'>{axis}</text>");
            }

            var xs = new double[points.Count];
            var baseYs = new double[points.Count];
            var totalYs = new double[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                double t = points.Count == 1 ? 0 : i / (double)(points.Count - 1);
                xs[i] = left + chartWidth * t;
                baseYs[i] = bottom - chartHeight * (double)(points[i].BaseSpending / max);
                totalYs[i] = bottom - chartHeight * (double)(points[i].TotalSpending / max);
            }

            sb.Append("<polygon fill='#E0A83A' points='");
            for (int i = 0; i < points.Count; i++)
                sb.Append(CultureInfo.InvariantCulture, $"{xs[i]:0.##},{totalYs[i]:0.##} ");
            for (int i = points.Count - 1; i >= 0; i--)
                sb.Append(CultureInfo.InvariantCulture, $"{xs[i]:0.##},{baseYs[i]:0.##} ");
            sb.Append("' />");

            sb.Append("<polygon fill='#2E5A88' points='");
            sb.Append(CultureInfo.InvariantCulture, $"{xs[0]:0.##},{bottom} ");
            for (int i = 0; i < points.Count; i++)
                sb.Append(CultureInfo.InvariantCulture, $"{xs[i]:0.##},{baseYs[i]:0.##} ");
            sb.Append(CultureInfo.InvariantCulture, $"{xs[^1]:0.##},{bottom}' />");

            if (points.Count <= 8)
            {
                for (int i = 0; i < points.Count; i++)
                {
                    string anchor = i == 0 ? "start" : i == points.Count - 1 ? "end" : "middle";
                    AppendYear(sb, points[i].Year.ToString(), xs[i], bottom + 16, anchor);
                }
            }
            else
            {
                AppendYear(sb, points[0].Year.ToString(), xs[0], bottom + 16, "start");
                AppendYear(sb, points[^1].Year.ToString(), xs[^1], bottom + 16, "end");
                int mid = points.Count / 2;
                AppendYear(sb, points[mid].Year.ToString(), xs[mid], bottom + 16, "middle");
            }

            sb.Append("</svg>");
            return sb.ToString();
        }

        public static string PortfolioPathSvg(IReadOnlyList<PortfolioYearPoint> points)
        {
            var values = points?.Select(p => p.EndingBalance).ToList() ?? new List<decimal>();
            var years = points?.Select(p => p.Year).ToList() ?? new List<int>();
            return LineChartSvg(
                years,
                new[] { (Name: "Portfolio", Color: "#2E5A88", Values: (IReadOnlyList<decimal>)values) },
                yLabeler: RetirementSummaryFormat.CompactCurrency,
                yMin: 0m);
        }

        public static string GuardrailPortfolioSvg(
            IReadOnlyList<PortfolioYearPoint> points,
            GuardrailSummary guardrails)
        {
            var percents = points?.Select(p => p.GuardrailPortfolioPercent).ToList() ?? new List<decimal>();
            var years = points?.Select(p => p.Year).ToList() ?? new List<int>();
            int n = percents.Count;
            var lower = Enumerable.Repeat(guardrails?.LowerTrigger ?? 0m, n).ToList();
            var upper = Enumerable.Repeat(guardrails?.UpperTrigger ?? 0m, n).ToList();

            decimal yMax = percents.Count == 0 ? 1m : percents.Max();
            if (guardrails != null)
            {
                yMax = Math.Max(yMax, guardrails.UpperTrigger);
                yMax = Math.Max(yMax, guardrails.LowerTrigger);
            }

            if (yMax <= 0m)
                yMax = 1m;

            return LineChartSvg(
                years,
                new[]
                {
                    (Name: "Portfolio / baseline", Color: "#1F4E79", Values: (IReadOnlyList<decimal>)percents),
                    (Name: "Lower trigger", Color: "#C45C5C", Values: (IReadOnlyList<decimal>)lower),
                    (Name: "Upper trigger", Color: "#2E8A8A", Values: (IReadOnlyList<decimal>)upper)
                },
                yLabeler: v => RetirementSummaryFormat.PercentTrim(v),
                yMin: 0m,
                yMax: yMax);
        }

        public static string OrdinaryBracketFillSvg(IReadOnlyList<OrdinaryBracketYearPoint> years)
        {
            const double width = 540;
            const double height = 210;
            const double left = 58;
            const double right = 528;
            const double top = 12;
            const double bottom = 178;
            double chartWidth = right - left;
            double chartHeight = bottom - top;

            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture,
                $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}">""");

            if (years == null || years.Count == 0)
            {
                sb.Append("</svg>");
                return sb.ToString();
            }

            var rates = OrdinaryRatesIn(years);

            decimal max = 0m;
            foreach (var year in years)
            {
                decimal total = (year.Fills ?? Array.Empty<OrdinaryBracketFill>()).Sum(f => f.Filled) + year.Headroom;
                if (total > max)
                    max = total;
            }

            if (max <= 0m)
                max = 1m;

            sb.Append(CultureInfo.InvariantCulture,
                $"<line x1='{left}' y1='{bottom}' x2='{right}' y2='{bottom}' stroke='#C5D0DA' />");
            sb.Append(CultureInfo.InvariantCulture,
                $"<line x1='{left}' y1='{top}' x2='{left}' y2='{bottom}' stroke='#C5D0DA' />");

            for (int i = 1; i <= 4; i++)
            {
                double y = bottom - chartHeight * (i / 4.0);
                sb.Append(CultureInfo.InvariantCulture,
                    $"<line x1='{left}' y1='{y:0.##}' x2='{right}' y2='{y:0.##}' stroke='#E4EAF0' />");
                string axis = Escape(RetirementSummaryFormat.CompactCurrency(max * i / 4m));
                sb.Append(CultureInfo.InvariantCulture,
                    $"<text x='4' y='{y + 3:0.##}' font-size='9' fill='#5F6B73'>{axis}</text>");
            }

            double slot = chartWidth / years.Count;
            double barWidth = Math.Max(2, Math.Min(18, slot * 0.7));

            for (int i = 0; i < years.Count; i++)
            {
                var year = years[i];
                double x = left + slot * (i + 0.5) - barWidth / 2;
                double y = bottom;

                foreach (var rate in rates)
                {
                    decimal filled = 0m;
                    if (year.Fills != null)
                    {
                        foreach (var fill in year.Fills)
                        {
                            if (fill.Rate == rate)
                            {
                                filled = fill.Filled;
                                break;
                            }
                        }
                    }

                    if (filled <= 0m)
                        continue;

                    double h = chartHeight * (double)(filled / max);
                    y -= h;
                    sb.Append(CultureInfo.InvariantCulture,
                        $"<rect x='{x:0.##}' y='{y:0.##}' width='{barWidth:0.##}' height='{Math.Max(0, h):0.##}' fill='{ColorForOrdinaryRate(rate)}' />");
                }

                if (year.Headroom > 0m)
                {
                    double h = chartHeight * (double)(year.Headroom / max);
                    y -= h;
                    sb.Append(CultureInfo.InvariantCulture,
                        $"<rect x='{x:0.##}' y='{y:0.##}' width='{barWidth:0.##}' height='{Math.Max(0, h):0.##}' fill='#C5D0DA' />");
                }
            }

            if (years.Count <= 8)
            {
                for (int i = 0; i < years.Count; i++)
                {
                    double cx = left + slot * (i + 0.5);
                    string anchor = i == 0 ? "start" : i == years.Count - 1 ? "end" : "middle";
                    AppendYear(sb, years[i].Year.ToString(), cx, bottom + 16, anchor);
                }
            }
            else
            {
                double firstX = left + slot * 0.5;
                double lastX = left + slot * (years.Count - 0.5);
                double midX = left + slot * (years.Count / 2 + 0.5);
                AppendYear(sb, years[0].Year.ToString(), firstX, bottom + 16, "start");
                AppendYear(sb, years[^1].Year.ToString(), lastX, bottom + 16, "end");
                int mid = years.Count / 2;
                AppendYear(sb, years[mid].Year.ToString(), midX, bottom + 16, "middle");
            }

            sb.Append("</svg>");
            return sb.ToString();
        }

        public static string ColorForOrdinaryRate(decimal rate)
        {
            if (rate == 0.10m) return "#2E8B57";
            if (rate == 0.12m) return "#3CB371";
            if (rate == 0.22m) return "#DAA520";
            if (rate == 0.24m) return "#C9962A";
            if (rate == 0.32m) return "#C47A3A";
            if (rate == 0.35m) return "#C45C5C";
            if (rate == 0.37m) return "#8E3A3A";
            return "#2E5A88";
        }

        public static IReadOnlyList<decimal> OrdinaryRatesIn(IReadOnlyList<OrdinaryBracketYearPoint> years)
        {
            return (years ?? Array.Empty<OrdinaryBracketYearPoint>())
                .SelectMany(y => y.Fills ?? Array.Empty<OrdinaryBracketFill>())
                .Select(f => f.Rate)
                .Distinct()
                .OrderBy(r => r)
                .ToList();
        }

        private static string LineChartSvg(
            IReadOnlyList<int> years,
            IReadOnlyList<(string Name, string Color, IReadOnlyList<decimal> Values)> series,
            Func<decimal, string> yLabeler,
            decimal yMin,
            decimal? yMax = null)
        {
            const double width = 540;
            const double height = 210;
            const double left = 58;
            const double right = 528;
            const double top = 12;
            const double bottom = 178;
            double chartWidth = right - left;
            double chartHeight = bottom - top;

            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture,
                $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}">""");

            var yearList = years ?? Array.Empty<int>();
            int n = yearList.Count;
            if (n == 0 || series == null || series.Count == 0)
            {
                sb.Append("</svg>");
                return sb.ToString();
            }

            decimal max = yMax ?? 0m;
            if (yMax == null)
            {
                foreach (var s in series)
                {
                    if (s.Values == null || s.Values.Count == 0)
                        continue;
                    decimal seriesMax = s.Values.Max();
                    if (seriesMax > max)
                        max = seriesMax;
                }
            }

            if (max <= yMin)
                max = yMin + 1m;

            decimal span = max - yMin;
            if (span <= 0m)
                span = 1m;

            sb.Append(CultureInfo.InvariantCulture,
                $"<line x1='{left}' y1='{bottom}' x2='{right}' y2='{bottom}' stroke='#C5D0DA' />");
            sb.Append(CultureInfo.InvariantCulture,
                $"<line x1='{left}' y1='{top}' x2='{left}' y2='{bottom}' stroke='#C5D0DA' />");

            for (int i = 1; i <= 4; i++)
            {
                double y = bottom - chartHeight * (i / 4.0);
                sb.Append(CultureInfo.InvariantCulture,
                    $"<line x1='{left}' y1='{y:0.##}' x2='{right}' y2='{y:0.##}' stroke='#E4EAF0' />");
                decimal tick = yMin + span * i / 4m;
                sb.Append(CultureInfo.InvariantCulture,
                    $"<text x='4' y='{y + 3:0.##}' font-size='9' fill='#5F6B73'>{Escape(yLabeler(tick))}</text>");
            }

            foreach (var s in series)
            {
                if (s.Values == null || s.Values.Count == 0)
                    continue;

                var sbLine = new StringBuilder();
                int count = Math.Min(n, s.Values.Count);
                for (int i = 0; i < count; i++)
                {
                    double t = count == 1 ? 0 : i / (double)(count - 1);
                    double x = left + chartWidth * t;
                    double y = bottom - chartHeight * (double)((s.Values[i] - yMin) / span);
                    if (i == 0)
                        sbLine.Append(CultureInfo.InvariantCulture, $"M {x:0.##} {y:0.##}");
                    else
                        sbLine.Append(CultureInfo.InvariantCulture, $" L {x:0.##} {y:0.##}");
                }

                sb.Append(CultureInfo.InvariantCulture,
                    $"<path d='{sbLine}' fill='none' stroke='{s.Color}' stroke-width='2.2' stroke-linejoin='round' stroke-linecap='round' />");
            }

            if (n <= 8)
            {
                for (int i = 0; i < n; i++)
                {
                    double t = n == 1 ? 0 : i / (double)(n - 1);
                    double x = left + chartWidth * t;
                    string anchor = i == 0 ? "start" : i == n - 1 ? "end" : "middle";
                    AppendYear(sb, yearList[i].ToString(), x, bottom + 16, anchor);
                }
            }
            else
            {
                AppendYear(sb, yearList[0].ToString(), left, bottom + 16, "start");
                AppendYear(sb, yearList[^1].ToString(), right, bottom + 16, "end");
                int mid = n / 2;
                double midT = mid / (double)(n - 1);
                AppendYear(sb, yearList[mid].ToString(), left + chartWidth * midT, bottom + 16, "middle");
            }

            sb.Append("</svg>");
            return sb.ToString();
        }

        private static void AppendYear(StringBuilder sb, string text, double x, double y, string anchor)
        {
            sb.Append(CultureInfo.InvariantCulture,
                $"<text x='{x:0.##}' y='{y:0.##}' text-anchor='{anchor}' font-size='10' fill='#5F6B73'>{Escape(text)}</text>");
        }

        private static string Arc(double cx, double cy, double radius, double startDeg, double sweepDeg, string color, double stroke)
        {
            double startRad = startDeg * Math.PI / 180.0;
            double endRad = (startDeg + sweepDeg) * Math.PI / 180.0;
            double x1 = cx + radius * Math.Cos(startRad);
            double y1 = cy + radius * Math.Sin(startRad);
            double x2 = cx + radius * Math.Cos(endRad);
            double y2 = cy + radius * Math.Sin(endRad);
            int large = sweepDeg > 180 ? 1 : 0;
            return string.Format(CultureInfo.InvariantCulture,
                "<path d='M {0:0.##} {1:0.##} A {2:0.##} {2:0.##} 0 {3} 1 {4:0.##} {5:0.##}' fill='none' stroke='{6}' stroke-width='{7}' />",
                x1, y1, radius, large, x2, y2, color, stroke);
        }

        private static string Escape(string text)
        {
            return (text ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
