using System;
using System.Collections.Generic;

namespace ExitPlan.Core
{
    public class AccountProjection
    {
        public Guid OwnerId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string TaxBucket { get; set; } = string.Empty;
        public string AssetClass { get; set; } = string.Empty;

        public decimal StartingValue { get; set; }
        public decimal RateOfReturn { get; set; }

        // Year-by-year values AFTER deposits/withdrawals/growth
        public List<decimal> ValuesByYear { get; set; } = new();

        // Year-by-year investment return amounts
        public List<decimal> InvestmentReturnByYear { get; set; } = new();

        // Year-by-year withdrawals (gross) from this account
        public List<decimal> WithdrawalsByYear { get; set; } = new();

        public decimal Withdraw(decimal amount, int year)
        {
            if (amount <= 0m)
                return 0m;

            while (ValuesByYear.Count <= year)
                ValuesByYear.Add(0m);

            while (WithdrawalsByYear.Count <= year)
                WithdrawalsByYear.Add(0m);

            decimal current = ValuesByYear[year];
            decimal withdrawn = Math.Min(current, amount);

            ValuesByYear[year] = current - withdrawn;
            WithdrawalsByYear[year] += withdrawn;

            return withdrawn;
        }

        public void Deposit(decimal amount, int year)
        {
            if (amount <= 0m)
                return;

            while (ValuesByYear.Count <= year)
                ValuesByYear.Add(0m);

            ValuesByYear[year] += amount;
        }
    }
}


