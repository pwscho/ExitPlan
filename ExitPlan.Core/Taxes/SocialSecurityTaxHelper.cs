namespace ExitPlan.Core.Taxes
{
    public static class SocialSecurityTaxHelper
    {
        /// <summary>
        /// IRS combined (provisional) income: AGI excluding Social Security
        /// (ordinary income plus realized taxable capital gains) + tax-exempt
        /// interest + 50% of Social Security.
        /// </summary>
        public static decimal ComputeProvisionalIncome(
            decimal ordinaryIncomeExcludingSs,
            decimal capitalGains,
            decimal taxExemptInterest,
            decimal ssBenefits)
        {
            decimal agiExcludingSs = ordinaryIncomeExcludingSs + capitalGains;
            return agiExcludingSs + taxExemptInterest + 0.5m * ssBenefits;
        }

        public static decimal ComputeTaxableSocialSecurity(
            decimal ssBenefits,
            decimal ordinaryIncomeExcludingSs,
            FilingStatus filingStatus,
            TaxPolicy policy,
            decimal capitalGains = 0m,
            decimal taxExemptInterest = 0m)
        {
            if (ssBenefits <= 0) return 0m;

            decimal baseThreshold;
            decimal secondThreshold;

            if (filingStatus == FilingStatus.Single)
            {
                baseThreshold = policy.SingleSsBaseThreshold;
                secondThreshold = policy.SingleSsSecondThreshold;
            }
            else
            {
                baseThreshold = policy.MarriedSsBaseThreshold;
                secondThreshold = policy.MarriedSsSecondThreshold;
            }

            decimal provisional = ComputeProvisionalIncome(
                ordinaryIncomeExcludingSs,
                capitalGains,
                taxExemptInterest,
                ssBenefits);
            decimal taxable;

            if (provisional <= baseThreshold)
            {
                taxable = 0m;
            }
            else if (provisional <= secondThreshold)
            {
                taxable = 0.5m * (provisional - baseThreshold);
            }
            else
            {
                taxable = 0.85m * (provisional - secondThreshold)
                          + 0.5m * (secondThreshold - baseThreshold);

                taxable = Math.Min(taxable, 0.85m * ssBenefits);
            }

            return taxable;
        }
    }
}
