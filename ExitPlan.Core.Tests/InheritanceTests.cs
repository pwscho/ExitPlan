using ExitPlan.Core;
using ExitPlan.Core.Engine;
using ExitPlan.Core.Services;
using ExitPlan.Core.Simulation;

namespace ExitPlan.Core.Tests
{
    public class InheritanceTests
    {
        [Fact]
        public void LastToDie_DoesNotTransferBalances()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 0m,
                taxFreeCash: 80_000m,
                taxableCash: 0m,
                spending: 0m,
                years: 3,
                currentAge: 80,
                deathAge: 80,
                married: true,
                spouseAge: 78,
                spouseDeathAge: 80);

            TestHousehold.Set(accounts, spouse!.UserId, "Tax Free", "Cash", 70_000m);

            var engine = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse);
            var output = engine.Run(1, "fixed");

            // Year 1: first death transfers Primary → Spouse.
            var afterFirstDeath = output.Accounts.Where(a => a.Year == 1).ToList();
            Assert.Equal(0m, afterFirstDeath.Where(a => a.Owner == primary.Owner).Sum(a => a.EndingValue));
            Assert.Equal(150_000m, afterFirstDeath.Where(a => a.Owner == spouse.Owner).Sum(a => a.EndingValue));

            // Year 3: spouse is last-to-die. Balances stay with the deceased survivor.
            var lastYear = output.Accounts.Where(a => a.Year == 3).ToList();
            decimal primaryEnding = lastYear.Where(a => a.Owner == primary.Owner).Sum(a => a.EndingValue);
            decimal spouseEnding = lastYear.Where(a => a.Owner == spouse.Owner).Sum(a => a.EndingValue);

            Assert.Equal(0m, primaryEnding);
            Assert.Equal(150_000m, spouseEnding);
        }

        [Fact]
        public void BothDeadSameYear_DoesNotTransferBalances()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 0m,
                taxFreeCash: 100_000m,
                taxableCash: 0m,
                spending: 0m,
                years: 1,
                currentAge: 85,
                deathAge: 85,
                married: true,
                spouseAge: 85,
                spouseDeathAge: 85);

            TestHousehold.Set(accounts, spouse!.UserId, "Tax Free", "Cash", 40_000m);

            var engine = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse);
            var output = engine.Run(1, "fixed");

            var year1 = output.Accounts.Where(a => a.Year == 1).ToList();
            decimal primaryEnding = year1.Where(a => a.Owner == primary.Owner).Sum(a => a.EndingValue);
            decimal spouseEnding = year1.Where(a => a.Owner == spouse.Owner).Sum(a => a.EndingValue);

            Assert.Equal(100_000m, primaryEnding);
            Assert.Equal(40_000m, spouseEnding);
        }

        [Fact]
        public void FirstDeath_TransfersToSurvivor()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 0m,
                taxFreeCash: 60_000m,
                taxableCash: 0m,
                spending: 0m,
                years: 2,
                currentAge: 80,
                deathAge: 80,
                married: true,
                spouseAge: 78,
                spouseDeathAge: 90);

            TestHousehold.Set(accounts, spouse!.UserId, "Tax Free", "Cash", 10_000m);

            var engine = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse);
            var output = engine.Run(1, "fixed");

            var year1 = output.Accounts.Where(a => a.Year == 1).ToList();
            decimal primaryEnding = year1.Where(a => a.Owner == primary.Owner).Sum(a => a.EndingValue);
            decimal spouseEnding = year1.Where(a => a.Owner == spouse.Owner).Sum(a => a.EndingValue);

            Assert.Equal(0m, primaryEnding);
            Assert.Equal(70_000m, spouseEnding);
        }

        [Fact]
        public void Success_UsesLastYearEndingBalance()
        {
            var rows = new[]
            {
                new ResultRow { SimNo = 1, Iteration = 1, StartingBalance = 10m, EndingBalance = 0m },
                new ResultRow { SimNo = 1, Iteration = 2, StartingBalance = 0m, EndingBalance = 5m }
            };

            Assert.True(SimulationSuccess.IsSuccessful(rows));

            var failed = new[]
            {
                new ResultRow { SimNo = 1, Iteration = 1, StartingBalance = 10m, EndingBalance = 1m },
                new ResultRow { SimNo = 1, Iteration = 2, StartingBalance = 1m, EndingBalance = 0m }
            };

            Assert.False(SimulationSuccess.IsSuccessful(failed));
        }
    }
}
