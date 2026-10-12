using ExitPlan.Core;
using ExitPlan.Core.Engine;
using ExitPlan.Core.Services;

namespace ExitPlan.Core.Tests
{
    public class RebalanceTests
    {
        [Fact]
        public void TwoOwners_EachTaxBucketRebalancesToItsOwnSleeve_CashStaysPut()
        {
            // Spouse rows are first. The old engine took the first Equities row and the
            // first Bonds row in each tax bucket, so only the spouse would move.
            var (primary, spouse, accounts) = Household(spouseFirst: true);
            var scenario = ScenarioAt(0.60m, 0.40m);

            Set(accounts, primary, "Tax Deferred", "Equities", 80_000m);
            Set(accounts, primary, "Tax Deferred", "Bonds", 20_000m);
            Set(accounts, spouse, "Tax Deferred", "Equities", 20_000m);
            Set(accounts, spouse, "Tax Deferred", "Bonds", 80_000m);

            Set(accounts, primary, "Taxable", "Equities", 10_000m);
            Set(accounts, primary, "Taxable", "Bonds", 90_000m);
            Set(accounts, spouse, "Taxable", "Equities", 90_000m);
            Set(accounts, spouse, "Taxable", "Bonds", 10_000m);

            Set(accounts, primary, "Tax Free", "Equities", 50_000m);
            Set(accounts, primary, "Tax Free", "Bonds", 50_000m);
            Set(accounts, spouse, "Tax Free", "Equities", 0m);
            Set(accounts, spouse, "Tax Free", "Bonds", 40_000m);

            Set(accounts, primary, "Tax Deferred", "Cash", 5_000m);
            Set(accounts, spouse, "Taxable", "Cash", 7_500m, costBasis: 7_500m);
            Set(accounts, primary, "Tax Free", "Cash", 1_250m);

            new RebalancingEngine().Rebalance(accounts, scenario);

            AssertSleeve(accounts, primary, "Tax Deferred", 60_000m, 40_000m);
            AssertSleeve(accounts, spouse, "Tax Deferred", 60_000m, 40_000m);
            AssertSleeve(accounts, primary, "Taxable", 60_000m, 40_000m);
            AssertSleeve(accounts, spouse, "Taxable", 60_000m, 40_000m);
            AssertSleeve(accounts, primary, "Tax Free", 60_000m, 40_000m);
            AssertSleeve(accounts, spouse, "Tax Free", 24_000m, 16_000m);

            Assert.Equal(5_000m, Get(accounts, primary, "Tax Deferred", "Cash").Value);
            Assert.Equal(7_500m, Get(accounts, spouse, "Taxable", "Cash").Value);
            Assert.Equal(7_500m, Get(accounts, spouse, "Taxable", "Cash").CostBasis);
            Assert.Equal(1_250m, Get(accounts, primary, "Tax Free", "Cash").Value);
        }

        [Fact]
        public void TaxableCostBasis_MovesInProportionToValue_AndIsNotARealizedSale()
        {
            var (primary, spouse, accounts) = Household(spouseFirst: false);
            var scenario = ScenarioAt(0.60m, 0.40m);

            Set(accounts, primary, "Taxable", "Equities", 100_000m, costBasis: 40_000m);
            Set(accounts, primary, "Taxable", "Bonds", 0m, costBasis: 0m);

            Set(accounts, spouse, "Taxable", "Equities", 0m, costBasis: 0m);
            Set(accounts, spouse, "Taxable", "Bonds", 200_000m, costBasis: 50_000m);

            new RebalancingEngine().Rebalance(accounts, scenario);

            var primaryEquity = Get(accounts, primary, "Taxable", "Equities");
            var primaryBonds = Get(accounts, primary, "Taxable", "Bonds");
            Assert.Equal(60_000m, primaryEquity.Value);
            Assert.Equal(40_000m, primaryBonds.Value);
            Assert.Equal(24_000m, primaryEquity.CostBasis);
            Assert.Equal(16_000m, primaryBonds.CostBasis);
            Assert.Equal(40_000m, primaryEquity.CostBasis + primaryBonds.CostBasis);

            var spouseEquity = Get(accounts, spouse, "Taxable", "Equities");
            var spouseBonds = Get(accounts, spouse, "Taxable", "Bonds");
            Assert.Equal(120_000m, spouseEquity.Value);
            Assert.Equal(80_000m, spouseBonds.Value);
            Assert.Equal(30_000m, spouseEquity.CostBasis);
            Assert.Equal(20_000m, spouseBonds.CostBasis);
            Assert.Equal(50_000m, spouseEquity.CostBasis + spouseBonds.CostBasis);
        }

        [Fact]
        public void NonTaxableCostBasis_StaysOnTheOriginalRow()
        {
            var (primary, _, accounts) = Household(spouseFirst: false);
            var scenario = ScenarioAt(0.50m, 0.50m);

            Set(accounts, primary, "Tax Deferred", "Equities", 100_000m, costBasis: 100_000m);
            Set(accounts, primary, "Tax Deferred", "Bonds", 0m, costBasis: 0m);
            Set(accounts, primary, "Tax Free", "Equities", 80_000m, costBasis: 10_000m);
            Set(accounts, primary, "Tax Free", "Bonds", 20_000m, costBasis: 20_000m);

            new RebalancingEngine().Rebalance(accounts, scenario);

            Assert.Equal(50_000m, Get(accounts, primary, "Tax Deferred", "Equities").Value);
            Assert.Equal(50_000m, Get(accounts, primary, "Tax Deferred", "Bonds").Value);
            Assert.Equal(100_000m, Get(accounts, primary, "Tax Deferred", "Equities").CostBasis);
            Assert.Equal(0m, Get(accounts, primary, "Tax Deferred", "Bonds").CostBasis);

            Assert.Equal(50_000m, Get(accounts, primary, "Tax Free", "Equities").Value);
            Assert.Equal(50_000m, Get(accounts, primary, "Tax Free", "Bonds").Value);
            Assert.Equal(10_000m, Get(accounts, primary, "Tax Free", "Equities").CostBasis);
            Assert.Equal(20_000m, Get(accounts, primary, "Tax Free", "Bonds").CostBasis);
        }

        [Fact]
        public void FullPositionMove_TakesTheEntireCostBasis()
        {
            var (primary, _, accounts) = Household(spouseFirst: false);

            Set(accounts, primary, "Taxable", "Equities", 25_000m, costBasis: 11_000m);
            Set(accounts, primary, "Taxable", "Bonds", 75_000m, costBasis: 75_000m);

            new RebalancingEngine().Rebalance(accounts, ScenarioAt(1m, 0m));

            Assert.Equal(100_000m, Get(accounts, primary, "Taxable", "Equities").Value);
            Assert.Equal(0m, Get(accounts, primary, "Taxable", "Bonds").Value);
            Assert.Equal(86_000m, Get(accounts, primary, "Taxable", "Equities").CostBasis);
            Assert.Equal(0m, Get(accounts, primary, "Taxable", "Bonds").CostBasis);
        }

        [Fact]
        public void MissingBondRow_SkipsThatOwnerBucket_OtherOwnerStillRebalances()
        {
            var (primary, spouse, accounts) = Household(spouseFirst: true);
            accounts.RemoveAll(a =>
                a.OwnerId == primary.UserId &&
                a.TaxBucket == "Taxable" &&
                a.AssetClass == "Bonds");

            Set(accounts, primary, "Taxable", "Equities", 100_000m, costBasis: 40_000m);
            Set(accounts, spouse, "Taxable", "Equities", 16_000m, costBasis: 16_000m);
            Set(accounts, spouse, "Taxable", "Bonds", 4_000m, costBasis: 1_000m);

            new RebalancingEngine().Rebalance(accounts, ScenarioAt(0.50m, 0.50m));

            Assert.Equal(100_000m, Get(accounts, primary, "Taxable", "Equities").Value);
            Assert.Equal(40_000m, Get(accounts, primary, "Taxable", "Equities").CostBasis);
            Assert.Equal(10_000m, Get(accounts, spouse, "Taxable", "Equities").Value);
            Assert.Equal(10_000m, Get(accounts, spouse, "Taxable", "Bonds").Value);
            Assert.Equal(10_000m, Get(accounts, spouse, "Taxable", "Equities").CostBasis);
            Assert.Equal(7_000m, Get(accounts, spouse, "Taxable", "Bonds").CostBasis);
        }

        [Fact]
        public void AllocationsThatDoNotSumToOne_AreRejected_AndBalancesStayPut()
        {
            var (primary, _, accounts) = Household(spouseFirst: false);
            Set(accounts, primary, "Tax Deferred", "Equities", 80_000m);
            Set(accounts, primary, "Tax Deferred", "Bonds", 20_000m);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                new RebalancingEngine().Rebalance(accounts, ScenarioAt(0.60m, 0.30m)));

            Assert.Contains("sum to 100%", ex.Message);
            Assert.Contains("Cash is not rebalanced", ex.Message);
            Assert.Contains("60%", ex.Message);
            Assert.Contains("30%", ex.Message);
            Assert.Equal(80_000m, Get(accounts, primary, "Tax Deferred", "Equities").Value);
            Assert.Equal(20_000m, Get(accounts, primary, "Tax Deferred", "Bonds").Value);

            Assert.Throws<InvalidOperationException>(() =>
                new RebalancingEngine().Rebalance(accounts, ScenarioAt(1.2m, -0.2m)));

            Assert.Throws<InvalidOperationException>(() =>
                new RebalancingEngine().Rebalance(accounts, ScenarioAt(0m, 0m)));
        }

        [Fact]
        public void ComputeEngine_RebalanceAnnually_UsesEachOwnersSleeve()
        {
            var (primary, spouse, accounts) = Household(spouseFirst: true);
            var scenario = ScenarioAt(0.60m, 0.40m);
            scenario.RebalanceAnnually = true;
            scenario.YearsToCalculate = 1;
            scenario.AnnualSpending = 0m;
            scenario.InflationRate = 0m;
            scenario.EnableTaxBracketInflation = false;

            Set(accounts, primary, "Tax Deferred", "Equities", 80_000m);
            Set(accounts, primary, "Tax Deferred", "Bonds", 20_000m);
            Set(accounts, spouse, "Tax Deferred", "Equities", 20_000m);
            Set(accounts, spouse, "Tax Deferred", "Bonds", 80_000m);
            Set(accounts, primary, "Taxable", "Cash", 3_000m);
            Set(accounts, spouse, "Tax Free", "Cash", 4_000m);

            decimal starting = accounts.Sum(a => a.Value);

            var output = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse)
                .Run(1, "fixed");

            Assert.Equal(80_000m, Get(accounts, primary, "Tax Deferred", "Equities").Value);
            Assert.Equal(60_000m, Ending(output, primary.Owner, "Tax Deferred", "Equities"));
            Assert.Equal(40_000m, Ending(output, primary.Owner, "Tax Deferred", "Bonds"));
            Assert.Equal(60_000m, Ending(output, spouse.Owner, "Tax Deferred", "Equities"));
            Assert.Equal(40_000m, Ending(output, spouse.Owner, "Tax Deferred", "Bonds"));
            Assert.Equal(3_000m, Ending(output, primary.Owner, "Taxable", "Cash"));
            Assert.Equal(4_000m, Ending(output, spouse.Owner, "Tax Free", "Cash"));
            Assert.Equal(starting, output.Accounts.Where(a => a.Year == 1).Sum(a => a.EndingValue));
        }

        [Fact]
        public void ComputeEngine_RebalanceAnnually_RejectsEquityPlusBondNot100()
        {
            var (primary, spouse, accounts) = Household(spouseFirst: false);
            var scenario = ScenarioAt(0.80m, 0.10m);
            scenario.RebalanceAnnually = true;
            scenario.YearsToCalculate = 1;
            scenario.AnnualSpending = 0m;

            Set(accounts, primary, "Tax Deferred", "Equities", 50_000m);
            Set(accounts, spouse, "Tax Deferred", "Equities", 50_000m);

            var ex = Assert.Throws<InvalidOperationException>(() =>
                new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse)
                    .Run(1, "fixed"));

            Assert.Contains("sum to 100%", ex.Message);
            Assert.Equal(50_000m, Get(accounts, primary, "Tax Deferred", "Equities").Value);
            Assert.Equal(50_000m, Get(accounts, spouse, "Tax Deferred", "Equities").Value);
        }

        [Fact]
        public void ComputeEngine_RebalanceOff_LeavesAMismatchedAllocationAlone()
        {
            var (primary, spouse, accounts) = Household(spouseFirst: true);
            var scenario = ScenarioAt(0.80m, 0.10m);
            scenario.RebalanceAnnually = false;
            scenario.YearsToCalculate = 1;
            scenario.AnnualSpending = 0m;
            scenario.InflationRate = 0m;
            scenario.EnableTaxBracketInflation = false;

            Set(accounts, primary, "Tax Deferred", "Equities", 80_000m);
            Set(accounts, primary, "Tax Deferred", "Bonds", 20_000m);
            Set(accounts, spouse, "Tax Deferred", "Equities", 10_000m);
            Set(accounts, spouse, "Tax Deferred", "Bonds", 90_000m);

            var output = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse)
                .Run(1, "fixed");

            Assert.Equal(80_000m, Ending(output, primary.Owner, "Tax Deferred", "Equities"));
            Assert.Equal(20_000m, Ending(output, primary.Owner, "Tax Deferred", "Bonds"));
            Assert.Equal(10_000m, Ending(output, spouse.Owner, "Tax Deferred", "Equities"));
            Assert.Equal(90_000m, Ending(output, spouse.Owner, "Tax Deferred", "Bonds"));
        }

        private static Scenario ScenarioAt(decimal equity, decimal bond) => new()
        {
            Name = "Rebalance",
            YearsToCalculate = 1,
            BaseYear = 2026,
            AnnualSpending = 0m,
            SpendingStrategy = "Fixed",
            InflationRate = 0m,
            EnableTaxBracketInflation = false,
            TargetEquityPercentage = equity,
            TargetBondPercentage = bond,
            RebalanceAnnually = true
        };

        private static (SocialSecuritySettings Primary, SocialSecuritySettings Spouse, List<Account> Accounts)
            Household(bool spouseFirst)
        {
            var primary = new SocialSecuritySettings
            {
                Owner = "Primary",
                CurrentAge = 60,
                ClaimAge = 67,
                DeathAge = 95,
                BenefitAtFullRetirementAge = 0m,
                Cola = 0m
            };
            var spouse = new SocialSecuritySettings
            {
                Owner = "Spouse",
                CurrentAge = 58,
                ClaimAge = 67,
                DeathAge = 95,
                BenefitAtFullRetirementAge = 0m,
                Cola = 0m
            };

            var first = spouseFirst ? spouse : primary;
            var second = spouseFirst ? primary : spouse;
            var accounts = DefaultAccountsInitializer.CreateDefaultAccountsFor(first.UserId, first.Owner, 1);
            accounts.AddRange(DefaultAccountsInitializer.CreateDefaultAccountsFor(second.UserId, second.Owner, 20));
            foreach (var account in accounts)
                account.RateOfReturn = 0m;

            return (primary, spouse, accounts);
        }

        private static void Set(
            List<Account> accounts,
            SocialSecuritySettings owner,
            string bucket,
            string asset,
            decimal value,
            decimal? costBasis = null)
        {
            var account = Get(accounts, owner, bucket, asset);
            account.Value = value;
            account.CostBasis = costBasis ?? 0m;
        }

        private static Account Get(
            List<Account> accounts,
            SocialSecuritySettings owner,
            string bucket,
            string asset) =>
            accounts.First(a =>
                a.OwnerId == owner.UserId &&
                a.TaxBucket == bucket &&
                a.AssetClass == asset);

        private static void AssertSleeve(
            List<Account> accounts,
            SocialSecuritySettings owner,
            string bucket,
            decimal equity,
            decimal bonds)
        {
            Assert.Equal(equity, Get(accounts, owner, bucket, "Equities").Value);
            Assert.Equal(bonds, Get(accounts, owner, bucket, "Bonds").Value);
        }

        private static decimal Ending(SimulationOutput output, string owner, string bucket, string asset) =>
            output.Accounts.Single(a =>
                a.Year == 1 &&
                a.Owner == owner &&
                a.TaxBucket == bucket &&
                a.AssetClass == asset).EndingValue;
    }
}
