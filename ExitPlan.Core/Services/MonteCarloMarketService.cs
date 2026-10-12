using ExitPlan.Core.MarketData;

namespace ExitPlan.Core.Services
{
    public class MonteCarloMarketService : IMarketService
    {
        private readonly List<MarketYear> _years;
        private readonly Random _rng;
        private readonly List<(decimal Equity, decimal Bond, decimal Inflation)> _path = new();

        public MonteCarloMarketService(MarketHistory history, int? seed = null)
        {
            _years = history.Years;
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        public decimal GetEquityReturnForYear(int yearIndex) => EnsureYear(yearIndex).Equity;

        public decimal GetBondReturnForYear(int yearIndex) => EnsureYear(yearIndex).Bond;

        public decimal GetInflationForYear(int yearIndex) => EnsureYear(yearIndex).Inflation;

        /// <summary>
        /// Draws equity, bond, and inflation for years 0..yearIndex in a fixed order.
        /// Extra calls (more accounts, spending lookups) reuse the cached year and do
        /// not consume additional RNG, so two scenarios with the same seed see the same path.
        /// </summary>
        private (decimal Equity, decimal Bond, decimal Inflation) EnsureYear(int yearIndex)
        {
            while (_path.Count <= yearIndex)
            {
                _path.Add((
                    _years[_rng.Next(_years.Count)].EquityReturn,
                    _years[_rng.Next(_years.Count)].BondReturn,
                    _years[_rng.Next(_years.Count)].InflationRate));
            }

            return _path[yearIndex];
        }
    }
}
