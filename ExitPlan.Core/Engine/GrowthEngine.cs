using System;
using System.Collections.Generic;
using System.Diagnostics;
using ExitPlan.Core.Services;

namespace ExitPlan.Core.Engine
{
    public class GrowthEngine
    {
        private readonly IMarketService _market;

        public GrowthEngine(IMarketService market)
        {
            _market = market;
        }

        public void ApplyGrowth(List<Account> workingAccounts, int yearIndex)
        {
            foreach (var acct in workingAccounts)
            {
                decimal rate = 0m;

                // FIXED SIMULATION → use account's own RateOfReturn
                if (_market is FixedMarketService)
                {
                    rate = acct.RateOfReturn;
                }
                else
                {
                    // HISTORICAL or MONTE CARLO → use market returns
                    switch (acct.AssetClass.Trim().ToLower())
                    {
                        case "equities":
                            rate = _market.GetEquityReturnForYear(yearIndex);
                            break;

                        case "bonds":
                            rate = _market.GetBondReturnForYear(yearIndex);
                            break;

                        case "cash":
                            rate = 0m;
                            break;

                        default:
                            rate = 0m;
                            break;
                    }
                }

                acct.Value *= (1 + rate);
            }
        }

        public decimal GetBondReturn(int yearIndex)
        {
            decimal _inflation = 0;

            _inflation = _market.GetBondReturnForYear(yearIndex);

            return _inflation;

        }

        public decimal GetInflation(int yearIndex)
        {
            decimal _inflation = 0;

            _inflation = _market.GetInflationForYear(yearIndex);

            return _inflation;

        }
    }
}

