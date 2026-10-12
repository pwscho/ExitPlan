using System.Diagnostics;
using ExitPlan.Core.Strategies;

namespace ExitPlan.Core.Engine
{
    public static class WithdrawalEngine
    {
        public static List<WithdrawalResult> Withdraw(
            decimal amount,
            IWithdrawalStrategy strategy,
            Scenario scenario,
            List<Account> accounts,
            TaxYearAccumulator tax,
            WithdrawalPurpose purpose = WithdrawalPurpose.Spending)
        {
            var results = new List<WithdrawalResult>();

            if (amount <= 0)
                return (results);

            decimal remaining = amount;

            foreach (var acct in strategy.GetWithdrawalOrder(scenario, accounts, purpose))
            {
                if (remaining <= 0)
                    break;

                decimal available = acct.Value;
                if (available <= 0)
                    continue;

                decimal take = Math.Min(available, remaining);

                acct.Value -= take;
                remaining -= take;


                // Normalize bucket name ("Tax Free" → "TaxFree")
                string normalizedBucket = acct.TaxBucket.Replace(" ", "");
                var bucket = Enum.Parse<TaxBucket>(normalizedBucket);

                var result = new WithdrawalResult
                {
                    Account = acct,
                    Amount = take,
                    Bucket = bucket,
                    TaxableAmount = 0m
                };

                if (bucket == TaxBucket.TaxDeferred)
                {
                    result.TaxableAmount = take;
                    tax.OrdinaryIncome += take;
                }
                else if (bucket == TaxBucket.Taxable)
                {
                    decimal basis = acct.CostBasis;
                    decimal gain = Math.Max(0, take - basis);

                    result.TaxableAmount = gain;
                    tax.CapitalGains += gain;

                    acct.CostBasis -= Math.Min(acct.CostBasis, take);
                }

                results.Add(result);
            }

            return (results);
        }
    }


}

