using ExitPlan.Core;

namespace ExitPlan.Core.Strategies
{
    public class DefaultWithdrawalStrategy : IWithdrawalStrategy
    {
        public IEnumerable<Account> GetWithdrawalOrder(
            Scenario scenario,
            List<Account> accounts,
            WithdrawalPurpose purpose = WithdrawalPurpose.Spending)
        {
            IOrderedEnumerable<Account> ordered = purpose == WithdrawalPurpose.TaxOrIrmaa
                ? accounts
                    .OrderBy(a => IsTaxFree(a.TaxBucket) ? 1 : 0)
                    .ThenBy(a => a.Bucket)
                    .ThenBy(a => a.Priority)
                    .ThenBy(a => GetTaxRank(a.TaxBucket))
                : accounts
                    .OrderBy(a => a.Bucket)
                    .ThenBy(a => a.Priority)
                    .ThenBy(a => GetTaxRank(a.TaxBucket));

            return ordered.ToList();
        }

        private static bool IsTaxFree(string taxBucket) =>
            taxBucket.Trim() == "Tax Free";

        private static int GetTaxRank(string taxBucket)
        {
            switch (taxBucket.Trim())
            {
                case "Taxable": return 1;
                case "Tax Deferred": return 2;
                case "Tax Free": return 3;
                default: return 99;
            }
        }
    }
}
