using System;
using System.Collections.Generic;
using System.Linq;

namespace ExitPlan.Core.Reporting
{
    public class EndingBalanceHistogramBuilder
    {
        public const decimal DefaultLowerPercentile = 0.10m;
        public const decimal DefaultUpperPercentile = 0.90m;

        // ------------------------------------------------------------
        // Build histogram from List<ResultRow> (original version)
        // ------------------------------------------------------------
        public List<HistogramBin> Build(List<ResultRow> monteCarloResults, int binCount)
        {
            if (monteCarloResults == null || monteCarloResults.Count == 0)
                return new List<HistogramBin>();

            // Extract ending balances per simulation
            var endings = monteCarloResults
                .GroupBy(r => r.SimNo)
                .Select(sim => sim.Last().EndingBalance)
                .ToList();

            return Build(endings, binCount);
        }

        /// <summary>
        /// Histogram of ending balances between the inclusive lower and upper
        /// percentiles (default P10–P90). Outcomes in the long tails are omitted
        /// so the x-axis is not stretched by extremes.
        /// </summary>
        public List<HistogramBin> BuildBetweenPercentiles(
            IReadOnlyList<decimal> values,
            int binCount,
            decimal lowerPercentile = DefaultLowerPercentile,
            decimal upperPercentile = DefaultUpperPercentile)
        {
            var clipped = ClipToInclusivePercentileRange(values, lowerPercentile, upperPercentile);
            return Build(clipped, binCount);
        }

        /// <summary>
        /// Keep values at or between the inclusive percentile bounds.
        /// Tails below the lower percentile or above the upper percentile are dropped.
        /// </summary>
        public static List<decimal> ClipToInclusivePercentileRange(
            IReadOnlyList<decimal> values,
            decimal lowerPercentile = DefaultLowerPercentile,
            decimal upperPercentile = DefaultUpperPercentile)
        {
            if (values == null || values.Count == 0)
                return new List<decimal>();

            var sorted = values.OrderBy(v => v).ToList();
            decimal lo = Percentile(sorted, lowerPercentile);
            decimal hi = Percentile(sorted, upperPercentile);
            if (hi < lo)
                (lo, hi) = (hi, lo);

            return values.Where(v => v >= lo && v <= hi).ToList();
        }

        public static decimal Percentile(IReadOnlyList<decimal> sortedAscending, decimal percentile)
        {
            if (sortedAscending == null || sortedAscending.Count == 0)
                return 0m;

            int index = (int)Math.Floor(percentile * (sortedAscending.Count - 1));
            index = Math.Clamp(index, 0, sortedAscending.Count - 1);
            return sortedAscending[index];
        }

        // ------------------------------------------------------------
        // Build histogram from List<decimal> (needed for winsorization)
        // ------------------------------------------------------------
        public List<HistogramBin> Build(List<decimal> values, int binCount)
        {
            if (values == null || values.Count == 0)
                return new List<HistogramBin>();

            decimal min = values.Min();
            decimal max = values.Max();

            if (min == max)
            {
                return new List<HistogramBin>
                {
                    new HistogramBin
                    {
                        BinStart = min,
                        BinEnd = min,
                        Count = values.Count
                    }
                };
            }

            decimal binSize = (max - min) / binCount;

            var bins = new List<HistogramBin>();

            for (int i = 0; i < binCount; i++)
            {
                bins.Add(new HistogramBin
                {
                    BinStart = min + i * binSize,
                    BinEnd = min + (i + 1) * binSize,
                    Count = 0
                });
            }

            foreach (var v in values)
            {
                int index = (int)((v - min) / binSize);
                if (index == binCount) index--; // edge case
                bins[index].Count++;
            }

            return bins;
        }
    }
}
