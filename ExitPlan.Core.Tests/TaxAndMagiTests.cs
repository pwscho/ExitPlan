using ExitPlan.Core.Taxes;

namespace ExitPlan.Core.Tests
{
    public class TaxAndMagiTests
    {
        [Fact]
        public void Magi_UsesTaxableSocialSecurity_NotGross()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            var strategy = new ConfigIncomeTaxStrategy();

            var input = new TaxComputationInput
            {
                Year = 2026,
                FilingStatus = FilingStatus.Single,
                OrdinaryIncome = 20_000m,
                CapitalGains = 0m,
                SocialSecurityBenefits = 40_000m
            };

            var result = strategy.Compute(input, policy);
            var taxableSs = SocialSecurityTaxHelper.ComputeTaxableSocialSecurity(
                input.SocialSecurityBenefits,
                input.OrdinaryIncome,
                input.FilingStatus,
                policy,
                input.CapitalGains);

            Assert.True(taxableSs < input.SocialSecurityBenefits);
            Assert.True(taxableSs > 0m);
            Assert.Equal(input.OrdinaryIncome + input.CapitalGains + taxableSs, result.Magi);
            Assert.NotEqual(input.OrdinaryIncome + input.SocialSecurityBenefits, result.Magi);
        }

        [Fact]
        public void LeftoverStandardDeduction_SheltersCapitalGains()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            var strategy = new ConfigIncomeTaxStrategy();

            var input = new TaxComputationInput
            {
                Year = 2026,
                FilingStatus = FilingStatus.Single,
                OrdinaryIncome = 5_000m,
                CapitalGains = 8_000m,
                SocialSecurityBenefits = 0m
            };

            var result = strategy.Compute(input, policy);

            // Std deduction 16,100 − ordinary 5,000 = 11,100 leftover, which fully shelters 8,000 CG.
            Assert.Equal(0m, result.TaxableOrdinaryIncome);
            Assert.Equal(0m, result.TaxableCapitalGains);
            Assert.Equal(0m, result.FederalTax);
        }

        [Fact]
        public void Tax2026_IsUsedForYear2026_AndTax2025ForEarlierYears()
        {
            var tax2026 = TaxPolicyLoader.LoadForYear(2026);
            var tax2025 = TaxPolicyLoader.LoadForYear(2025);
            var tax2024 = TaxPolicyLoader.LoadForYear(2024);

            Assert.Equal(2026, tax2026.Year);
            Assert.Equal(16_100m, tax2026.Single.StandardDeduction);
            Assert.Equal(2025, tax2025.Year);
            Assert.Equal(tax2025.Year, tax2024.Year);
            Assert.Equal(95.70m, tax2026.Single.IrmaaBrackets.OrderBy(b => b.Threshold).Skip(1).First().MonthlySurcharge);
        }

        [Fact]
        public void IrmaaCliffsAndSurcharges_InflateWithTaxBracketInflation()
        {
            var basePolicy = TaxPolicyLoader.LoadForYear(2026);
            var inflated = TaxPolicyLoader.LoadForYear(2026, 1.10m);

            var baseBracket = basePolicy.Single.IrmaaBrackets.OrderBy(b => b.Threshold).Skip(1).First();
            var inflatedBracket = inflated.Single.IrmaaBrackets.OrderBy(b => b.Threshold).Skip(1).First();

            Assert.Equal(baseBracket.Threshold * 1.10m, inflatedBracket.Threshold);
            Assert.Equal(baseBracket.MonthlySurcharge * 1.10m, inflatedBracket.MonthlySurcharge);
        }
    }
}
