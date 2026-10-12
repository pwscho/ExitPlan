using System;
using System.Collections.Generic;
using System.Linq;
using ExitPlan.Core.Strategies;

namespace ExitPlan.Core.Engine
{
    public class IrmaaEngine
    {
        public decimal PayIrmaaForYear(
            int yearIndex,
            SimulationResult result,
            SocialSecuritySettings primary,
            SocialSecuritySettings? spouse,
            Scenario scenario,
            List<Account> workingAccounts,
            TaxYearAccumulator tax,
            Dictionary<Guid, decimal> deferredWithdrawnThisYear)
        {
            // No IRMAA due before year 2
            if (yearIndex < 2)
                return 0m;

            decimal perPersonIrmaaFromN = result.IrmaaByYear[yearIndex - 2];
            if (perPersonIrmaaFromN <= 0m)
                return 0m;

            // Determine enrollee count in year N+2
            int enrolleeCount = 0;

            int primaryAgeNow = primary.CurrentAge + yearIndex;
            bool primaryAliveNow = primaryAgeNow <= primary.DeathAge;
            bool primaryOnMedicareNow = primaryAliveNow && primaryAgeNow >= 65;

            if (primaryOnMedicareNow)
                enrolleeCount++;

            if (spouse != null)
            {
                int spouseAgeNow = spouse.CurrentAge + yearIndex;
                bool spouseAliveNow = spouseAgeNow <= spouse.DeathAge;
                bool spouseOnMedicareNow = spouseAliveNow && spouseAgeNow >= 65;

                if (spouseOnMedicareNow)
                    enrolleeCount++;
            }

            decimal irmaaDueNow = perPersonIrmaaFromN * enrolleeCount;
            if (irmaaDueNow <= 0m)
                return 0m;

            // Withdraw IRMAA
            var withdrawals = WithdrawalEngine.Withdraw(
                irmaaDueNow,
                new DefaultWithdrawalStrategy(),
                scenario,
                workingAccounts,
                tax,
                WithdrawalPurpose.TaxOrIrmaa);

            // Track deferred withdrawals
            foreach (var w in withdrawals)
            {
                if (w.Bucket != TaxBucket.TaxDeferred)
                    continue;

                var ownerId = w.Account.OwnerId;

                if (!deferredWithdrawnThisYear.TryGetValue(ownerId, out var current))
                    current = 0m;

                deferredWithdrawnThisYear[ownerId] = current + w.Amount;
            }

            return irmaaDueNow;
        }
    }
}
