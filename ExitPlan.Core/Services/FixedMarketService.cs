using ExitPlan.Core.Services;

namespace ExitPlan.Core.Services
{
    public class FixedMarketService : IMarketService
    {
        public decimal GetEquityReturnForYear(int yearIndex) => 0m;
        public decimal GetBondReturnForYear(int yearIndex) => 0m;
        public decimal GetInflationForYear(int yearIndex) => 0m;
    }

}


