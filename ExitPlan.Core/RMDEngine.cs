using System;
using System.Collections.Generic;
using System.Linq;

namespace ExitPlan.Core
{
    public class RmdEngine
    {
        private readonly TransferEngine _transferEngine;
        private readonly RmdPolicy _policy;

        public RmdEngine(TransferEngine transferEngine, RmdPolicy policy)
        {
            _transferEngine = transferEngine;
            _policy = policy;
        }

        public decimal ApplyRmdForOwner(
            Guid ownerId,
            int ownerAgeThisYear,
            int yearIndex,
            List<AccountProjection> allAccounts,
            AccountProjection taxableAccount,
            TaxYearAccumulator taxYear,
            int? birthYear = null)
        {
            // 1. Does RMD apply this year?
            int startAge = birthYear.HasValue
                ? _policy.ResolveStartAge(birthYear.Value)
                : _policy.RmdStartAge;
            if (ownerAgeThisYear < startAge)
                return 0m;

            if (!_policy.LifeExpectancyDivisors.TryGetValue(ownerAgeThisYear, out var divisor) ||
                divisor <= 0m)
                return 0m;

            int priorYear = yearIndex - 1;
            if (priorYear < 0)
                return 0m;

            // 2. Owner's tax-deferred accounts
            var deferredAccounts = allAccounts
                .Where(a => a.OwnerId == ownerId && a.TaxBucket == "TaxDeferred")
                .ToList();

            if (deferredAccounts.Count == 0)
                return 0m;

            // 3. Prior-year total balance
            decimal totalPriorYearBalance = 0m;

            foreach (var acct in deferredAccounts)
            {
                if (acct.ValuesByYear.Count > priorYear)
                    totalPriorYearBalance += acct.ValuesByYear[priorYear];
            }

            if (totalPriorYearBalance <= 0m)
                return 0m;

            // 4. Required RMD
            decimal rmdAmount = totalPriorYearBalance / divisor;

            // 5. Already withdrawn this year from this owner's tax-deferred accounts
            decimal alreadyWithdrawn = deferredAccounts.Sum(a =>
                a.WithdrawalsByYear.Count > yearIndex
                    ? a.WithdrawalsByYear[yearIndex]
                    : 0m);

            // 6. Remaining RMD requirement
            decimal remainingRmd = Math.Max(0m, rmdAmount - alreadyWithdrawn);
            if (remainingRmd <= 0m)
                return 0m;

            // 7. Withdraw remaining RMD proportionally from tax-deferred accounts
            decimal remaining = remainingRmd;

            foreach (var acct in deferredAccounts)
            {
                if (remaining <= 0m)
                    break;

                if (acct.ValuesByYear.Count <= priorYear)
                    continue;

                decimal priorBalance = acct.ValuesByYear[priorYear];
                if (priorBalance <= 0m)
                    continue;

                decimal share = remainingRmd * (priorBalance / totalPriorYearBalance);
                decimal toWithdraw = Math.Min(share, remaining);

                _transferEngine.Transfer(
                    acct,
                    taxableAccount,
                    toWithdraw,
                    yearIndex,
                    TransferType.Rmd);

                remaining -= toWithdraw;
            }

            // 8. Full remaining RMD is ordinary income; taxes computed later
            taxYear.OrdinaryIncome += remainingRmd;

            return remainingRmd;
        }
    }
}


