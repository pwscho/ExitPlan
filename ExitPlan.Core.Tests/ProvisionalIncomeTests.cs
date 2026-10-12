using ExitPlan.Core.Taxes;

namespace ExitPlan.Core.Tests
{
    public class ProvisionalIncomeTests
    {
        [Fact]
        public void CapitalGains_RaiseProvisionalIncome_AndTaxableSocialSecurity()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            var strategy = new ConfigIncomeTaxStrategy();

            const decimal ordinary = 22_000m;
            const decimal socialSecurity = 20_000m;
            const decimal capitalGains = 5_000m;

            decimal withoutGains = SocialSecurityTaxHelper.ComputeProvisionalIncome(
                ordinary, capitalGains: 0m, taxExemptInterest: 0m, socialSecurity);
            decimal withGains = SocialSecurityTaxHelper.ComputeProvisionalIncome(
                ordinary, capitalGains, taxExemptInterest: 0m, socialSecurity);

            // AGI excluding SS + 50% of SS. Gains are realized taxable gains in AGI.
            Assert.Equal(ordinary + 0.5m * socialSecurity, withoutGains);
            Assert.Equal(ordinary + capitalGains + 0.5m * socialSecurity, withGains);
            Assert.True(withGains > withoutGains);
            Assert.Equal(32_000m, withoutGains);
            Assert.Equal(37_000m, withGains);

            var noGains = strategy.Compute(Input(ordinary, capitalGains: 0m, socialSecurity), policy);
            var gains = strategy.Compute(Input(ordinary, capitalGains, socialSecurity), policy);

            Assert.Equal(0m, noGains.TaxableSocialSecurity);
            // 50% of the $5,000 that lands between the $32,000 and $44,000 MFJ thresholds.
            Assert.Equal(2_500m, gains.TaxableSocialSecurity);
            Assert.True(gains.TaxableSocialSecurity > noGains.TaxableSocialSecurity);
            Assert.Equal(ordinary + capitalGains + gains.TaxableSocialSecurity, gains.Magi);
            Assert.True(gains.Magi > noGains.Magi + capitalGains);
        }

        [Fact]
        public void TaxExemptInterest_IsIncludedInProvisionalIncome()
        {
            decimal provisional = SocialSecurityTaxHelper.ComputeProvisionalIncome(
                ordinaryIncomeExcludingSs: 10_000m,
                capitalGains: 4_000m,
                taxExemptInterest: 1_500m,
                ssBenefits: 8_000m);

            Assert.Equal(10_000m + 4_000m + 1_500m + 0.5m * 8_000m, provisional);
        }

        private static TaxComputationInput Input(decimal ordinary, decimal capitalGains, decimal socialSecurity) =>
            new()
            {
                Year = 2026,
                FilingStatus = FilingStatus.MarriedFilingJointly,
                OrdinaryIncome = ordinary,
                CapitalGains = capitalGains,
                SocialSecurityBenefits = socialSecurity
            };
    }
}
