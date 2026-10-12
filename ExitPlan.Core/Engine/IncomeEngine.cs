using System;
using System.Collections.Generic;

namespace ExitPlan.Core.Engine
{
    public class IncomeEngine
    {
        public decimal CalculateSpendingNeed(
            Scenario scenario,
            int year,
            Func<Guid, int, int> getAgeForOwner)
        {
            decimal spendingNeed = scenario.AnnualSpending;

            foreach (var w in scenario.WithdrawalStreams)
            {
                int ownerAge = getAgeForOwner(w.OwnerId, year);

                if (ownerAge >= w.StartAge && ownerAge <= w.EndAge)
                    spendingNeed += w.AnnualAmount;
            }

            return spendingNeed;
        }
    }
}

