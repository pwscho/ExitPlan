using ExitPlan.Core.Strategies;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExitPlan.Core.Engine
{
    public class TaxPaymentEngine
    {
        public decimal PayLastYearsTax(
            decimal nextYearTaxBill,
            int yearIndex,
            IWithdrawalStrategy strategy,
            Scenario scenario,
            List<Account> workingAccounts,
            TaxYearAccumulator tax,
            Dictionary<Guid, decimal> deferredWithdrawnThisYear)
        {
            if (nextYearTaxBill <= 0m)
                return 0m;

            var withdrawals = WithdrawalEngine.Withdraw(
                nextYearTaxBill,
                strategy,
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

            return nextYearTaxBill;
        }
    }
}
