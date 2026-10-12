using ExitPlan.Core.MarketData;

namespace ExitPlan.Core.Tests
{
    public class MarketDataTests
    {
        [Fact]
        public void Year1940_InflationRate_IsZeroPointSevenPercent()
        {
            var history = MarketHistoryLoader.Load();
            var year1940 = history.Years.Single(y => y.Year == 1940);

            Assert.Equal(0.007m, year1940.InflationRate);
        }

        [Fact]
        public void NeighboringYears_AreUnchanged()
        {
            var history = MarketHistoryLoader.Load();
            var year1939 = history.Years.Single(y => y.Year == 1939);
            var year1941 = history.Years.Single(y => y.Year == 1941);

            Assert.Equal(-0.014m, year1939.InflationRate);
            Assert.Equal(0.05m, year1941.InflationRate);
        }
    }
}
