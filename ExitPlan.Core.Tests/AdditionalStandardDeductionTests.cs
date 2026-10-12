using ExitPlan.Core;
using ExitPlan.Core.Engine;
using ExitPlan.Core.Services;
using ExitPlan.Core.Taxes;

namespace ExitPlan.Core.Tests
{
    public class AdditionalStandardDeductionTests
    {
        [Fact]
        public void Tax2026_SeedsIrc63fAmounts()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);

            Assert.Equal(65, policy.AdditionalStandardDeductionAge);
            Assert.Equal(2_050m, policy.Single.AdditionalStandardDeduction);
            Assert.Equal(1_650m, policy.MarriedFilingJointly.AdditionalStandardDeduction);
        }

        [Fact]
        public void Mfj_BothUnderAge_AddsNoExtra()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            var ages = new[] { 64, 63 };

            Assert.Equal(0m, policy.AdditionalStandardDeductionFor(FilingStatus.MarriedFilingJointly, ages));
            Assert.Equal(
                policy.MarriedFilingJointly.StandardDeduction,
                policy.EffectiveStandardDeduction(FilingStatus.MarriedFilingJointly, ages));

            var result = Compute(policy, FilingStatus.MarriedFilingJointly, ordinary: 40_000m, ages);
            Assert.Equal(40_000m - 32_200m, result.TaxableOrdinaryIncome);
        }

        [Fact]
        public void Mfj_OneSpouseQualifies_AddsOneMarriedAmount()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            var ages = new[] { 65, 64 };

            Assert.Equal(1_650m, policy.AdditionalStandardDeductionFor(FilingStatus.MarriedFilingJointly, ages));
            Assert.Equal(32_200m + 1_650m, policy.EffectiveStandardDeduction(FilingStatus.MarriedFilingJointly, ages));

            var result = Compute(policy, FilingStatus.MarriedFilingJointly, ordinary: 40_000m, ages);
            Assert.Equal(40_000m - 33_850m, result.TaxableOrdinaryIncome);
        }

        [Fact]
        public void Mfj_BothQualify_AddsTwoMarriedAmounts()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            var ages = new[] { 65, 70 };

            Assert.Equal(3_300m, policy.AdditionalStandardDeductionFor(FilingStatus.MarriedFilingJointly, ages));
            Assert.Equal(32_200m + 3_300m, policy.EffectiveStandardDeduction(FilingStatus.MarriedFilingJointly, ages));

            var result = Compute(policy, FilingStatus.MarriedFilingJointly, ordinary: 40_000m, ages);
            Assert.Equal(40_000m - 35_500m, result.TaxableOrdinaryIncome);
        }

        [Fact]
        public void YearAfterFirstDeath_Survivor65Plus_FilesSingleAndGetsSingleAmount()
        {
            var primary = new SocialSecuritySettings { CurrentAge = 70, DeathAge = 70 };
            var spouse = new SocialSecuritySettings { CurrentAge = 68, DeathAge = 90 };
            var policy = TaxPolicyLoader.LoadForYear(2026);

            // Death year: both still counted alive; MFJ; both 65+.
            var deathYearAges = FilingStatusHelper.GetLivingOwnerAges(0, primary, spouse);
            var deathYearStatus = FilingStatusHelper.GetFilingStatusForYear(0, primary, spouse);
            Assert.Equal(FilingStatus.MarriedFilingJointly, deathYearStatus);
            Assert.Equal(new[] { 70, 68 }, deathYearAges);
            Assert.Equal(3_300m, policy.AdditionalStandardDeductionFor(deathYearStatus, deathYearAges));

            // Year after first death: survivor files Single and gets the unmarried amount.
            var nextYearAges = FilingStatusHelper.GetLivingOwnerAges(1, primary, spouse);
            var nextYearStatus = FilingStatusHelper.GetFilingStatusForYear(1, primary, spouse);
            Assert.Equal(FilingStatus.Single, nextYearStatus);
            Assert.Equal(new[] { 69 }, nextYearAges);
            Assert.Equal(2_050m, policy.AdditionalStandardDeductionFor(nextYearStatus, nextYearAges));
            Assert.Equal(16_100m + 2_050m, policy.EffectiveStandardDeduction(nextYearStatus, nextYearAges));

            var result = Compute(policy, nextYearStatus, ordinary: 20_000m, nextYearAges);
            Assert.Equal(20_000m - 18_150m, result.TaxableOrdinaryIncome);
        }

        [Fact]
        public void ExtraDeduction_InflatesWithExistingBracketMultiplier()
        {
            var baseline = TaxPolicyLoader.LoadForYear(2026);
            var inflated = TaxPolicyLoader.LoadForYear(2026, 1.10m);

            Assert.Equal(2_050m * 1.10m, inflated.Single.AdditionalStandardDeduction);
            Assert.Equal(1_650m * 1.10m, inflated.MarriedFilingJointly.AdditionalStandardDeduction);
            Assert.Equal(16_100m * 1.10m, inflated.Single.StandardDeduction);
            Assert.Equal(32_200m * 1.10m, inflated.MarriedFilingJointly.StandardDeduction);
            Assert.Equal(65, inflated.AdditionalStandardDeductionAge);

            var ages = new[] { 70 };
            Assert.Equal(
                (16_100m + 2_050m) * 1.10m,
                inflated.EffectiveStandardDeduction(FilingStatus.Single, ages));
            Assert.Equal(
                baseline.EffectiveStandardDeduction(FilingStatus.Single, ages) * 1.10m,
                inflated.EffectiveStandardDeduction(FilingStatus.Single, ages));
        }

        [Fact]
        public void MissingExtraDeductionFields_KeepsBaseStandardDeductionOnly()
        {
            var policy = TaxPolicyLoader.LoadForYear(2025);

            Assert.Equal(65, policy.AdditionalStandardDeductionAge);
            Assert.Equal(0m, policy.Single.AdditionalStandardDeduction);
            Assert.Equal(0m, policy.MarriedFilingJointly.AdditionalStandardDeduction);

            var mfjAges = new[] { 80, 78 };
            Assert.Equal(0m, policy.AdditionalStandardDeductionFor(FilingStatus.MarriedFilingJointly, mfjAges));
            Assert.Equal(
                policy.MarriedFilingJointly.StandardDeduction,
                policy.EffectiveStandardDeduction(FilingStatus.MarriedFilingJointly, mfjAges));

            var result = Compute(policy, FilingStatus.MarriedFilingJointly, ordinary: 40_000m, mfjAges);
            Assert.Equal(40_000m - policy.MarriedFilingJointly.StandardDeduction, result.TaxableOrdinaryIncome);
        }

        [Fact]
        public void ExtraDeduction_IsPartOfLeftoverThatSheltersLtcg()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            var strategy = new ConfigIncomeTaxStrategy();
            var ages = new[] { 70 };

            // Ordinary 16,000 leaves 100 of the 16,100 base deduction.
            // Extra 2,050 raises leftover to 2,150, so only 850 of 3,000 CG is taxable.
            var result = strategy.Compute(
                new TaxComputationInput
                {
                    Year = 2026,
                    FilingStatus = FilingStatus.Single,
                    OrdinaryIncome = 16_000m,
                    CapitalGains = 3_000m,
                    SocialSecurityBenefits = 0m,
                    LivingOwnerAges = ages
                },
                policy);

            Assert.Equal(0m, result.TaxableOrdinaryIncome);
            Assert.Equal(850m, result.TaxableCapitalGains);
        }

        [Fact]
        public void ComputeEngine_SingleFilerTurns65_UsesExtraDeduction()
        {
            var under = RunSingleWithTaxableIncome(currentAge: 64, ordinaryIncome: 20_000m);
            var qualifies = RunSingleWithTaxableIncome(currentAge: 65, ordinaryIncome: 20_000m);

            Assert.Equal(20_000m - 16_100m, under.OrdinaryIncome);
            Assert.Equal(20_000m - 18_150m, qualifies.OrdinaryIncome);
        }

        [Fact]
        public void ComputeEngine_YearAfterFirstDeath_SurvivorGetsSingleAmount()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 0m,
                taxFreeCash: 10_000m,
                taxableCash: 0m,
                spending: 0m,
                years: 2,
                currentAge: 70,
                deathAge: 70,
                married: true,
                spouseAge: 68,
                spouseDeathAge: 90);

            scenario.IncomeStreams.Add(new IncomeStream
            {
                OwnerId = spouse!.UserId,
                Name = "Pension",
                StartAge = 0,
                EndAge = 120,
                AnnualAmount = 40_000m,
                IsTaxable = true
            });

            var output = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse)
                .Run(1, "fixed");

            // Year of death: MFJ, both 65+ → 32,200 + 1,650×2 = 35,500.
            Assert.Equal(40_000m - 35_500m, output.Results[0].OrdinaryIncome);
            // Following year: Single, survivor 69 → 16,100 + 2,050 = 18,150.
            Assert.Equal(40_000m - 18_150m, output.Results[1].OrdinaryIncome);
        }

        private static TaxComputationResult Compute(
            TaxPolicy policy,
            FilingStatus status,
            decimal ordinary,
            IReadOnlyList<int> ages)
        {
            return new ConfigIncomeTaxStrategy().Compute(
                new TaxComputationInput
                {
                    Year = policy.Year,
                    FilingStatus = status,
                    OrdinaryIncome = ordinary,
                    CapitalGains = 0m,
                    SocialSecurityBenefits = 0m,
                    LivingOwnerAges = ages
                },
                policy);
        }

        private static ResultRow RunSingleWithTaxableIncome(int currentAge, decimal ordinaryIncome)
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 0m,
                taxFreeCash: 10_000m,
                taxableCash: 0m,
                spending: 0m,
                years: 1,
                currentAge: currentAge,
                deathAge: 90);

            scenario.IncomeStreams.Add(new IncomeStream
            {
                OwnerId = primary.UserId,
                Name = "Pension",
                StartAge = 0,
                EndAge = 120,
                AnnualAmount = ordinaryIncome,
                IsTaxable = true
            });

            return new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse)
                .Run(1, "fixed")
                .Results[0];
        }
    }
}
