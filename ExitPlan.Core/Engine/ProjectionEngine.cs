using System;
using System.Collections.Generic;

namespace ExitPlan.Core.Engine
{
    public class ProjectionEngine
    {
        public List<AccountProjection> CreateProjectionsSkeleton(
            List<Account> accounts,
            int years)
        {
            var projections = new List<AccountProjection>();

            foreach (var acct in accounts)
            {
                var proj = new AccountProjection
                {
                    OwnerId = acct.OwnerId,
                    Name = acct.Owner,
                    TaxBucket = acct.TaxBucket.Trim(),
                    AssetClass = acct.AssetClass.Trim(),
                    StartingValue = acct.Value,
                    RateOfReturn = acct.RateOfReturn,
                    ValuesByYear = new List<decimal>(new decimal[years]),
                    InvestmentReturnByYear = new List<decimal>(new decimal[years])
                };

                projections.Add(proj);
            }

            return projections;
        }


        public void SnapshotEndOfYear(
            List<Account> workingAccounts,
            List<AccountProjection> projections,
            int yearIndex,
            List<Account> originalAccounts)
        {
            for (int i = 0; i < workingAccounts.Count; i++)
            {
                projections[i].ValuesByYear[yearIndex] = workingAccounts[i].Value;

                if (yearIndex == 0)
                    projections[i].StartingValue = originalAccounts[i].Value;
            }
        }
    }
}


