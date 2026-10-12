using ExitPlan.Core.Taxes;

namespace ExitPlan.Core.Tests
{
    public class SocialSecurityThresholdTests
    {
        [Fact]
        public void SocialSecurityThresholds_StayAtStatutoryAmounts_AcrossFortyYearsOfBracketInflation()
        {
            const decimal annualInflation = 0.02m;
            decimal multiplier = 1m;

            for (int yearOffset = 0; yearOffset < 40; yearOffset++)
            {
                int year = 2026 + yearOffset;
                var policy = TaxPolicyLoader.LoadForYear(year, multiplier);

                Assert.Equal(25_000m, policy.SingleSsBaseThreshold);
                Assert.Equal(34_000m, policy.SingleSsSecondThreshold);
                Assert.Equal(32_000m, policy.MarriedSsBaseThreshold);
                Assert.Equal(44_000m, policy.MarriedSsSecondThreshold);

                multiplier *= 1m + annualInflation;
            }

            var basePolicy = TaxPolicyLoader.LoadForYear(2026);
            var afterFortyYears = TaxPolicyLoader.LoadForYear(2066, multiplier);

            Assert.Equal(25_000m, basePolicy.SingleSsBaseThreshold);
            Assert.Equal(34_000m, basePolicy.SingleSsSecondThreshold);
            Assert.Equal(32_000m, basePolicy.MarriedSsBaseThreshold);
            Assert.Equal(44_000m, basePolicy.MarriedSsSecondThreshold);
            Assert.Equal(basePolicy.SingleSsBaseThreshold, afterFortyYears.SingleSsBaseThreshold);
            Assert.Equal(basePolicy.MarriedSsSecondThreshold, afterFortyYears.MarriedSsSecondThreshold);

            // Brackets, the standard deduction, and IRMAA still inflate.
            Assert.True(afterFortyYears.Single.StandardDeduction > basePolicy.Single.StandardDeduction);
            Assert.True(afterFortyYears.Single.OrdinaryBrackets[0].Threshold > basePolicy.Single.OrdinaryBrackets[0].Threshold);
            Assert.True(
                afterFortyYears.Single.IrmaaBrackets[1].MonthlySurcharge >
                basePolicy.Single.IrmaaBrackets[1].MonthlySurcharge);

            var tax2025 = TaxPolicyLoader.LoadForYear(2025);
            Assert.Equal(25_000m, tax2025.SingleSsBaseThreshold);
            Assert.Equal(34_000m, tax2025.SingleSsSecondThreshold);
            Assert.Equal(32_000m, tax2025.MarriedSsBaseThreshold);
            Assert.Equal(44_000m, tax2025.MarriedSsSecondThreshold);
        }
    }
}
