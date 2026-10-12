using System;
using System.Collections.Generic;
using System.Linq;

namespace ExitPlan.Core.Engine
{
    public class InheritanceEngine
    {
        public void ApplyInheritanceForYear(
            int yearIndex,
            SocialSecuritySettings primary,
            SocialSecuritySettings? spouse,
            List<Account> workingAccounts,
            List<AccountProjection> projections,
            int totalYears)
        {
            if (spouse == null)
                return;

            int primaryAge = primary.CurrentAge + yearIndex;
            int spouseAge = spouse.CurrentAge + yearIndex;

            bool primaryDiesThisYear = primaryAge == primary.DeathAge;
            bool spouseDiesThisYear = spouseAge == spouse.DeathAge;

            // Last-to-die / both-dead: do not transfer balances.
            if (primaryDiesThisYear && spouseDiesThisYear)
                return;

            bool spouseStillAlive = spouseAge < spouse.DeathAge;
            bool primaryStillAlive = primaryAge < primary.DeathAge;

            if (primaryDiesThisYear && spouseStillAlive)
                TransferAccountsOnDeath(primary.UserId, spouse.UserId, workingAccounts, projections, primary, spouse, totalYears);

            if (spouseDiesThisYear && primaryStillAlive)
                TransferAccountsOnDeath(spouse.UserId, primary.UserId, workingAccounts, projections, primary, spouse, totalYears);
        }


        public void TransferAccountsOnDeath(
            Guid deceasedId,
            Guid survivorId,
            List<Account> workingAccounts,
            List<AccountProjection> projections,
            SocialSecuritySettings primary,
            SocialSecuritySettings? spouse,
            int totalYears)
        {
            foreach (var acct in workingAccounts.Where(a => a.OwnerId == deceasedId).ToList())
            {
                if (acct.Value <= 0m)
                    continue;

                // Find matching survivor account
                var survivorAcct = workingAccounts.FirstOrDefault(a =>
                    a.OwnerId == survivorId &&
                    a.TaxBucket.Trim() == acct.TaxBucket.Trim() &&
                    a.AssetClass.Trim() == acct.AssetClass.Trim());

                // Create survivor account if needed
                if (survivorAcct == null)
                {
                    survivorAcct = new Account
                    {
                        OwnerId = survivorId,
                        Owner = (survivorId == primary.UserId) ? primary.Owner : spouse!.Owner,
                        TaxBucket = acct.TaxBucket,
                        AssetClass = acct.AssetClass,
                        Priority = acct.Priority,
                        Bucket = acct.Bucket,
                        RateOfReturn = acct.RateOfReturn,
                        Value = 0m,
                        CostBasis = 0m
                    };

                    workingAccounts.Add(survivorAcct);

                    projections.Add(new AccountProjection
                    {
                        OwnerId = survivorId,
                        Name = survivorAcct.Owner,
                        TaxBucket = survivorAcct.TaxBucket.Trim(),
                        AssetClass = survivorAcct.AssetClass.Trim(),
                        StartingValue = 0m,
                        RateOfReturn = survivorAcct.RateOfReturn,
                        ValuesByYear = new List<decimal>(new decimal[totalYears]),
                        InvestmentReturnByYear = new List<decimal>(new decimal[totalYears])
                    });
                }

                // Transfer logic by tax bucket
                if (acct.TaxBucket.Trim() == "Taxable")
                {
                    // Step-up in basis
                    survivorAcct.Value += acct.Value;
                    survivorAcct.CostBasis += acct.Value;
                }
                else
                {
                    // Tax Deferred or Tax Free
                    survivorAcct.Value += acct.Value;
                }

                // Zero out deceased account
                acct.Value = 0m;
                acct.CostBasis = 0m;
            }
        }
    }
}
