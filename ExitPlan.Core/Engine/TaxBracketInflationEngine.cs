using ExitPlan.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExitPlan.Core.Engine
{

    public class TaxBracketInflationEngine
    {
        private readonly IMarketService _market;

        public TaxBracketInflationEngine(IMarketService market)
        {
            _market = market;
        }

        public decimal GetBracketInflation(int yearIndex, Scenario scenario)
        {
            switch (scenario.TaxBracketInflationStrategy)
            {
                case "Fixed":
                    return scenario.TaxBracketInflationRate;

                case "Historical":
                    return _market.GetInflationForYear(yearIndex);

                case "MonteCarlo":
                    // Monte Carlo market service already returns random inflation
                    return _market.GetInflationForYear(yearIndex);

                default:
                    return scenario.TaxBracketInflationRate;
            }
        }
    }


}
