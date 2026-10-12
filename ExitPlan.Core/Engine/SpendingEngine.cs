using System;
using System.Collections.Generic;
using ExitPlan.Core.Services;

namespace ExitPlan.Core.Engine
{
    public class SpendingEngine
    {
        private readonly IMarketService _market;

        public SpendingEngine(IMarketService market)
        {
            _market = market;
        }

        public decimal GetInflation (int yearIndex)
        {
            decimal _inflation = 0;

            _inflation = _market.GetInflationForYear (yearIndex);
            
            return _inflation;
            
        }
    }
}

