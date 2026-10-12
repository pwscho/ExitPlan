using System;
using System.Collections.Generic;
using System.Linq;

namespace ExitPlan.Core.Engine
{
    public class RmdEngine
    {
        public static int ResolveStartAge(RmdPolicy policy, int baseYear, int currentAge)
        {
            if (policy == null)
                return 0;

            int birthYear = RmdPolicy.ImpliedBirthYear(baseYear, currentAge);
            return policy.ResolveStartAge(birthYear);
        }

        public void ApplyRmdsForYear(
            int yearIndex,
            List<Account> workingAccounts,
            List<AccountProjection> projections,
            SimulationResult result,
            Dictionary<Guid, decimal> deferredWithdrawnThisYear,
            RmdPolicy rmdPolicy,
            SocialSecuritySettings primary,
            SocialSecuritySettings? spouse,
            int baseYear)
        {
            if (rmdPolicy == null)
                return;

            // Primary
            int primaryAge = primary.CurrentAge + yearIndex;
            ApplyRmdForOwner(
                ownerId: primary.UserId,
                ownerAgeThisYear: primaryAge,
                yearIndex: yearIndex,
                workingAccounts: workingAccounts,
                projections: projections,
                result: result,
                deferredWithdrawnThisYear: deferredWithdrawnThisYear,
                policy: rmdPolicy,
                deathAge: primary.DeathAge,
                startAge: ResolveStartAge(rmdPolicy, baseYear, primary.CurrentAge));

            // Spouse
            if (spouse != null)
            {
                int spouseAge = spouse.CurrentAge + yearIndex;
                ApplyRmdForOwner(
                    ownerId: spouse.UserId,
                    ownerAgeThisYear: spouseAge,
                    yearIndex: yearIndex,
                    workingAccounts: workingAccounts,
                    projections: projections,
                    result: result,
                    deferredWithdrawnThisYear: deferredWithdrawnThisYear,
                    policy: rmdPolicy,
                    deathAge: spouse.DeathAge,
                    startAge: ResolveStartAge(rmdPolicy, baseYear, spouse.CurrentAge));
            }
        }


        private void ApplyRmdForOwner(
            Guid ownerId,
            int ownerAgeThisYear,
            int yearIndex,
            List<Account> workingAccounts,
            List<AccountProjection> projections,
            SimulationResult result,
            Dictionary<Guid, decimal> deferredWithdrawnThisYear,
            RmdPolicy policy,
            int deathAge,
            int startAge)
        {
            if (policy == null)
                return;

            if (ownerAgeThisYear < startAge || ownerAgeThisYear > deathAge)
                return;

            if (!policy.LifeExpectancyDivisors.TryGetValue(ownerAgeThisYear, out var divisor) ||
                divisor <= 0m)
                return;

            // RMD base is this owner's Tax Deferred total at start of year
            // (year 0 uses opening balances; later years use prior year-end).
            int balanceYear = yearIndex - 1;

            var deferredAccounts = workingAccounts
                .Select((acct, index) => new { acct, index })
                .Where(x => x.acct.OwnerId == ownerId && x.acct.TaxBucket.Trim() == "Tax Deferred")
                .ToList();

            if (deferredAccounts.Count == 0)
                return;

            decimal totalStartOfYearBalance = 0m;

            foreach (var x in deferredAccounts)
            {
                if (balanceYear < 0)
                {
                    totalStartOfYearBalance += x.acct.Value;
                    continue;
                }

                if (x.index < projections.Count && projections[x.index].ValuesByYear.Count > balanceYear)
                    totalStartOfYearBalance += projections[x.index].ValuesByYear[balanceYear];
            }

            if (totalStartOfYearBalance <= 0m)
                return;

            decimal rmdAmount = totalStartOfYearBalance / divisor;

            if (rmdAmount <= 0m)
                return;

            deferredWithdrawnThisYear.TryGetValue(ownerId, out decimal alreadyWithdrawn);

            decimal remainingRmd = Math.Max(0m, rmdAmount - alreadyWithdrawn);

            if (remainingRmd <= 0m)
                return;

            var destination = workingAccounts.FirstOrDefault(a =>
                a.OwnerId == ownerId &&
                a.TaxBucket.Trim() == "Taxable" &&
                a.AssetClass.Trim() == "Cash")
                ?? workingAccounts.FirstOrDefault(a =>
                    a.OwnerId == ownerId &&
                    a.TaxBucket.Trim() == "Tax Free" &&
                    a.AssetClass.Trim() == "Cash");

            if (destination == null)
                return;

            bool destinationIsTaxable = destination.TaxBucket.Trim() == "Taxable";
            decimal remaining = remainingRmd;

            foreach (var x in deferredAccounts)
            {
                if (remaining <= 0m)
                    break;

                var acct = x.acct;
                decimal startBalance = balanceYear < 0
                    ? acct.Value
                    : (x.index < projections.Count && projections[x.index].ValuesByYear.Count > balanceYear
                        ? projections[x.index].ValuesByYear[balanceYear]
                        : 0m);

                if (startBalance <= 0m)
                    continue;

                decimal share = remainingRmd * (startBalance / totalStartOfYearBalance);
                decimal toWithdraw = Math.Min(share, Math.Min(acct.Value, remaining));
                if (toWithdraw <= 0m)
                    continue;

                acct.Value -= toWithdraw;
                destination.Value += toWithdraw;
                if (destinationIsTaxable)
                    destination.CostBasis += toWithdraw;

                remaining -= toWithdraw;
            }

            decimal executedRmd = remainingRmd - remaining;

            if (executedRmd <= 0m)
                return;

            // Add to income
            result.OrdinaryIncomeByYear[yearIndex] += executedRmd;
            result.RmdsByYear[yearIndex] += executedRmd;
        }
    }
}
