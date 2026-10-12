using ExitPlan.Core;
using ExitPlan.Core.Engine;
using ExitPlan.Core.Services;
using ExitPlan.Core.Taxes;

namespace ExitPlan.Core.Tests
{
    public class OrdinaryBracketFillTests
    {
        [Fact]
        public void KnownOrdinaryIncome_FillsKnownSingle2026Brackets_AndLeavesExpectedHeadroom()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            var brackets = policy.Single.OrdinaryBrackets;

            // 20,000 ordinary taxable: 12,400 at 10%, 7,600 at 12%.
            // 12% runs to 50,400, so room to the 22% cliff is 30,400.
            var allocation = OrdinaryBracketAllocator.Allocate(20_000m, brackets);

            Assert.Equal(12_400m, FilledAt(allocation, 0.10m));
            Assert.Equal(7_600m, FilledAt(allocation, 0.12m));
            Assert.Equal(0m, FilledAt(allocation, 0.22m));
            Assert.Equal(30_400m, allocation.Headroom);
            Assert.Equal("10%", allocation.Fills[0].RateLabel);
            Assert.Equal("12%", allocation.Fills[1].RateLabel);

            decimal expectedTax = 12_400m * 0.10m + 7_600m * 0.12m;
            Assert.Equal(expectedTax, allocation.OrdinaryTax);
        }

        [Fact]
        public void AtExactBracketCliff_HeadroomIsZero()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            var allocation = OrdinaryBracketAllocator.Allocate(12_400m, policy.Single.OrdinaryBrackets);

            Assert.Equal(12_400m, FilledAt(allocation, 0.10m));
            Assert.Equal(0m, FilledAt(allocation, 0.12m));
            Assert.Equal(0m, allocation.Headroom);
        }

        [Fact]
        public void ZeroOrdinaryIncome_HeadroomIsFirstBracketWidth()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            var allocation = OrdinaryBracketAllocator.Allocate(0m, policy.Single.OrdinaryBrackets);

            Assert.All(allocation.Fills, fill => Assert.Equal(0m, fill.Filled));
            Assert.Equal(12_400m, allocation.Headroom);
            Assert.Equal(0m, allocation.OrdinaryTax);
        }

        [Fact]
        public void TopListedRate_HasNoNextCliff()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            var allocation = OrdinaryBracketAllocator.Allocate(700_000m, policy.Single.OrdinaryBrackets);

            Assert.True(FilledAt(allocation, 0.37m) > 0m);
            Assert.Equal(0m, allocation.Headroom);
        }

        [Fact]
        public void Strategy_UsesTaxableOrdinaryAfterDeduction_ForFillsAndHeadroom()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            var strategy = new ConfigIncomeTaxStrategy();

            // Single 2026, under 65: 40,000 − 16,100 = 23,900 taxable ordinary.
            var result = strategy.Compute(
                new TaxComputationInput
                {
                    Year = 2026,
                    FilingStatus = FilingStatus.Single,
                    OrdinaryIncome = 40_000m,
                    CapitalGains = 0m,
                    SocialSecurityBenefits = 0m,
                    LivingOwnerAges = new[] { 60 }
                },
                policy);

            Assert.Equal(23_900m, result.TaxableOrdinaryIncome);
            Assert.Equal(12_400m, FilledAt(result, 0.10m));
            Assert.Equal(11_500m, FilledAt(result, 0.12m));
            Assert.Equal(26_500m, result.OrdinaryBracketHeadroom);
            Assert.Equal("22%", result.OrdinaryBracketFills.Single(f => f.Rate == 0.22m).RateLabel);
        }

        [Fact]
        public void ComputeEngine_WritesFillsAndHeadroom_ForKnownPension()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 0m,
                taxFreeCash: 10_000m,
                taxableCash: 0m,
                spending: 0m,
                years: 1,
                currentAge: 60,
                deathAge: 90);

            scenario.IncomeStreams.Add(new IncomeStream
            {
                OwnerId = primary.UserId,
                Name = "Pension",
                StartAge = 0,
                EndAge = 120,
                AnnualAmount = 40_000m,
                IsTaxable = true
            });

            var row = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse)
                .Run(1, "fixed")
                .Results[0];

            Assert.Equal(FilingStatus.Single, row.FilingStatus);
            Assert.Equal("Single", row.FilingStatusLabel);
            Assert.Equal(23_900m, row.OrdinaryIncome);
            Assert.Equal(12_400m, row.OrdinaryBracketFills.Single(f => f.Rate == 0.10m).Filled);
            Assert.Equal(11_500m, row.OrdinaryBracketFills.Single(f => f.Rate == 0.12m).Filled);
            Assert.Equal(26_500m, row.OrdinaryBracketHeadroom);
        }

        [Fact]
        public void ComputeEngine_YearAfterFirstDeath_UsesSingleBracketsForHeadroom()
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

            var deathYear = output.Results[0];
            var survivorYear = output.Results[1];

            Assert.Equal(FilingStatus.MarriedFilingJointly, deathYear.FilingStatus);
            Assert.Equal("MFJ", deathYear.FilingStatusLabel);
            Assert.Equal(40_000m - 35_500m, deathYear.OrdinaryIncome);
            Assert.Equal(4_500m, deathYear.OrdinaryIncome);
            Assert.Equal(4_500m, deathYear.OrdinaryBracketFills.Single(f => f.Rate == 0.10m).Filled);
            Assert.Equal(24_800m - 4_500m, deathYear.OrdinaryBracketHeadroom);

            Assert.Equal(FilingStatus.Single, survivorYear.FilingStatus);
            Assert.Equal("Single", survivorYear.FilingStatusLabel);
            Assert.Equal(21_850m, survivorYear.OrdinaryIncome);
            Assert.Equal(12_400m, survivorYear.OrdinaryBracketFills.Single(f => f.Rate == 0.10m).Filled);
            Assert.Equal(9_450m, survivorYear.OrdinaryBracketFills.Single(f => f.Rate == 0.12m).Filled);
            // 12% Single ceiling is 50,400, so room to 22% is 50,400 − 21,850.
            Assert.Equal(50_400m - 21_850m, survivorYear.OrdinaryBracketHeadroom);
        }

        private static decimal FilledAt(OrdinaryBracketAllocation allocation, decimal rate) =>
            allocation.Fills.Single(f => f.Rate == rate).Filled;

        private static decimal FilledAt(TaxComputationResult result, decimal rate) =>
            result.OrdinaryBracketFills.Single(f => f.Rate == rate).Filled;
    }
}
