namespace ExitPlan.Core.Strategies
{
    public class RothConversionWithdrawalStrategy : IRothConversionWithdrawalStrategy
    {
        private readonly IWithdrawalStrategy _priorityStrategy;

        public RothConversionWithdrawalStrategy(IWithdrawalStrategy priorityStrategy)
        {
            _priorityStrategy = priorityStrategy;
        }

        public decimal Convert(
            Guid ownerId,
            decimal requestedAmount,
            List<Account> workingAccounts,
            TaxYearAccumulator tax,
            SimulationResult result,
            int yearIndex)
        {
            var eligibleAccounts = workingAccounts
                .Where(a => a.OwnerId == ownerId &&
                            a.TaxBucket.Trim() == "Tax Deferred")
                .ToList();

            if (!eligibleAccounts.Any())
                return 0m;

            var ordered = eligibleAccounts
                .OrderBy(a => a.Priority)
                .ThenBy(a => a.TaxBucket)
                .ThenBy(a => a.AssetClass)
                .ToList();

            decimal remaining = requestedAmount;
            decimal actualConverted = 0m;

            foreach (var acct in ordered)
            {
                if (remaining <= 0)
                    break;

                decimal available = acct.Value;
                if (available <= 0)
                    continue;

                decimal toWithdraw = Math.Min(available, remaining);

                acct.Value -= toWithdraw;

                var rothAcct = workingAccounts.FirstOrDefault(a =>
                    a.OwnerId == ownerId &&
                    a.TaxBucket.Trim() == "Tax Free" &&
                    a.AssetClass.Trim() == acct.AssetClass.Trim());

                if (rothAcct == null)
                {
                    rothAcct = new Account
                    {
                        OwnerId = ownerId,
                        Owner = acct.Owner,
                        TaxBucket = "Tax Free",
                        AssetClass = acct.AssetClass,
                        Priority = acct.Priority,
                        Bucket = acct.Bucket,
                        RateOfReturn = acct.RateOfReturn,
                        Value = 0m,
                        CostBasis = 0m,
                        Name = $"{acct.Owner} Tax Free {acct.AssetClass}"
                    };
                    workingAccounts.Add(rothAcct);
                }

                rothAcct.Value += toWithdraw;

                tax.OrdinaryIncome += toWithdraw;
                if (result?.OrdinaryIncomeByYear != null &&
                    yearIndex >= 0 &&
                    yearIndex < result.OrdinaryIncomeByYear.Length)
                {
                    result.OrdinaryIncomeByYear[yearIndex] += toWithdraw;
                }

                actualConverted += toWithdraw;
                remaining -= toWithdraw;
            }

            return actualConverted;
        }
    }
}
