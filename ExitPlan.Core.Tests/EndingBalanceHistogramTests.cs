using ExitPlan.Core.Reporting;

namespace ExitPlan.Core.Tests
{
    public class EndingBalanceHistogramTests
    {
        [Fact]
        public void ClipToInclusivePercentileRange_DropsTailsOutsideP10P90()
        {
            var values = Enumerable.Range(1, 100).Select(i => (decimal)i).ToList();
            var clipped = EndingBalanceHistogramBuilder.ClipToInclusivePercentileRange(values, 0.10m, 0.90m);

            Assert.Equal(10m, clipped.Min());
            Assert.Equal(90m, clipped.Max());
            Assert.DoesNotContain(1m, clipped);
            Assert.DoesNotContain(100m, clipped);
            Assert.Equal(81, clipped.Count);
        }

        [Fact]
        public void ClipToInclusivePercentileRange_OmitsExtremeOutliers()
        {
            var values = Enumerable.Range(1, 100).Select(i => (decimal)i).ToList();
            values.Add(-1_000_000m);
            values.Add(50_000_000m);

            var clipped = EndingBalanceHistogramBuilder.ClipToInclusivePercentileRange(values, 0.10m, 0.90m);

            Assert.DoesNotContain(-1_000_000m, clipped);
            Assert.DoesNotContain(50_000_000m, clipped);
            Assert.True(clipped.Min() >= 1m);
            Assert.True(clipped.Max() <= 100m);
        }

        [Fact]
        public void BuildBetweenPercentiles_BinsStayInsideP10P90()
        {
            var values = Enumerable.Range(1, 100).Select(i => (decimal)i).ToList();
            values.Add(0m);
            values.Add(1_000_000m);

            var bins = new EndingBalanceHistogramBuilder().BuildBetweenPercentiles(values, binCount: 10, 0.10m, 0.90m);

            Assert.NotEmpty(bins);
            decimal lo = EndingBalanceHistogramBuilder.Percentile(values.OrderBy(v => v).ToList(), 0.10m);
            decimal hi = EndingBalanceHistogramBuilder.Percentile(values.OrderBy(v => v).ToList(), 0.90m);
            Assert.InRange(bins.First().BinStart, lo, hi);
            Assert.InRange(bins.Last().BinEnd, lo, hi);
            Assert.True(bins.Sum(b => b.Count) < values.Count,
                "Tails outside P10–P90 should not be counted in the histogram.");
            Assert.Equal(0, bins.Count(b => b.BinEnd < lo || b.BinStart > hi));
        }

        [Fact]
        public void ClipToInclusivePercentileRange_EmptyOrEqualValues()
        {
            Assert.Empty(EndingBalanceHistogramBuilder.ClipToInclusivePercentileRange(Array.Empty<decimal>()));

            var same = new List<decimal> { 42m, 42m, 42m };
            var clipped = EndingBalanceHistogramBuilder.ClipToInclusivePercentileRange(same, 0.10m, 0.90m);
            Assert.Equal(3, clipped.Count);
            Assert.All(clipped, v => Assert.Equal(42m, v));

            var bins = new EndingBalanceHistogramBuilder().BuildBetweenPercentiles(same, 8);
            var bin = Assert.Single(bins);
            Assert.Equal(42m, bin.BinStart);
            Assert.Equal(3, bin.Count);
        }
    }
}
