using System.Text.Json;
using ExitPlan.Core;
using ExitPlan.Core.MarketData;
using ExitPlan.Core.Services;
using ExitPlan.Core.Simulation;

namespace ExitPlan.Core.Tests
{
    public class MonteCarloSeedTests
    {
        [Fact]
        public void RunMonteCarlo_SameSeed_IsReproducible()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 300_000m,
                taxFreeCash: 40_000m,
                taxableCash: 40_000m,
                spending: 15_000m,
                years: 5);

            var history = MarketHistoryLoader.Load();
            var runner = new SimulationRunner();

            var first = runner.RunMonteCarlo(scenario, accounts, primary, spouse, history, simulations: 4, seed: 4242);
            var second = runner.RunMonteCarlo(scenario, accounts, primary, spouse, history, simulations: 4, seed: 4242);

            Assert.Equal(first.Count, second.Count);
            Assert.Equal(
                first.Select(r => r.EndingBalance).ToList(),
                second.Select(r => r.EndingBalance).ToList());
        }

        [Fact]
        public void RunMonteCarlo_UsesScenarioSeed_WhenOverrideOmitted()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 300_000m,
                taxFreeCash: 40_000m,
                taxableCash: 40_000m,
                spending: 15_000m,
                years: 5);
            scenario.MonteCarloSeed = 4242;

            var history = MarketHistoryLoader.Load();
            var runner = new SimulationRunner();

            var fromScenario = runner.RunMonteCarlo(scenario, accounts, primary, spouse, history, simulations: 4);
            var fromOverride = runner.RunMonteCarlo(scenario, accounts, primary, spouse, history, simulations: 4, seed: 4242);

            Assert.Equal(
                fromOverride.Select(r => r.EndingBalance).ToList(),
                fromScenario.Select(r => r.EndingBalance).ToList());
        }

        [Fact]
        public void RunMonteCarlo_DifferentSeeds_CanDiverge()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 300_000m,
                taxFreeCash: 40_000m,
                taxableCash: 40_000m,
                spending: 15_000m,
                years: 5);

            var history = MarketHistoryLoader.Load();
            var runner = new SimulationRunner();

            var seedA = runner.RunMonteCarlo(scenario, accounts, primary, spouse, history, simulations: 8, seed: 1);
            var seedB = runner.RunMonteCarlo(scenario, accounts, primary, spouse, history, simulations: 8, seed: 99);

            Assert.NotEqual(
                seedA.Select(r => r.EndingBalance).ToList(),
                seedB.Select(r => r.EndingBalance).ToList());
        }

        [Fact]
        public void MonteCarloMarketService_SameSeed_SameYearReturns_RegardlessOfCallCount()
        {
            var history = MarketHistoryLoader.Load();
            var a = new MonteCarloMarketService(history, seed: 7);
            var b = new MonteCarloMarketService(history, seed: 7);

            // Extra equity lookups mimic more accounts; inflation-first mimics Historical spending.
            for (int i = 0; i < 20; i++)
                a.GetEquityReturnForYear(0);
            a.GetInflationForYear(0);
            a.GetBondReturnForYear(0);

            Assert.Equal(a.GetInflationForYear(0), b.GetInflationForYear(0));
            Assert.Equal(a.GetEquityReturnForYear(0), b.GetEquityReturnForYear(0));
            Assert.Equal(a.GetBondReturnForYear(0), b.GetBondReturnForYear(0));
            Assert.Equal(a.GetEquityReturnForYear(1), b.GetEquityReturnForYear(1));
            Assert.Equal(a.GetBondReturnForYear(1), b.GetBondReturnForYear(1));
            Assert.Equal(a.GetInflationForYear(1), b.GetInflationForYear(1));
        }

        [Fact]
        public void MonteCarloMarketService_DifferentSeed_CanDiverge()
        {
            var history = MarketHistoryLoader.Load();
            var a = new MonteCarloMarketService(history, seed: 1);
            var b = new MonteCarloMarketService(history, seed: 99);

            var equityA = Enumerable.Range(0, 12).Select(y => a.GetEquityReturnForYear(y)).ToList();
            var equityB = Enumerable.Range(0, 12).Select(y => b.GetEquityReturnForYear(y)).ToList();

            Assert.NotEqual(equityA, equityB);
        }

        [Fact]
        public void Scenario_MonteCarloSeed_RoundTripsThroughDeepCopyAndAppStateJson()
        {
            var scenario = new Scenario
            {
                Name = "Seed persist",
                MonteCarloSeed = 4242,
                MonteCarloSimulations = 250
            };

            var copiedScenario = scenario.DeepCopy();
            Assert.Equal(4242, copiedScenario.MonteCarloSeed);
            Assert.Equal(250, copiedScenario.MonteCarloSimulations);

            var state = new ScenarioState { Scenario = scenario };
            var copiedState = state.DeepCopy();
            Assert.Equal(4242, copiedState.Scenario.MonteCarloSeed);
            Assert.Equal(250, copiedState.Scenario.MonteCarloSimulations);

            var appState = new AppState
            {
                Scenarios = [new ScenarioState { Scenario = scenario }]
            };

            var json = JsonSerializer.Serialize(appState);
            Assert.Contains("MonteCarloSeed", json);
            Assert.Contains("4242", json);

            var loaded = JsonSerializer.Deserialize<AppState>(json);
            Assert.NotNull(loaded);
            Assert.Equal(4242, loaded!.Scenarios[0].Scenario.MonteCarloSeed);
            Assert.Equal(250, loaded.Scenarios[0].Scenario.MonteCarloSimulations);
        }

        [Fact]
        public void Scenario_MissingMonteCarloSeed_DefaultsToOneAndPersistsOnSave()
        {
            var loaded = JsonSerializer.Deserialize<Scenario>("{}");
            Assert.NotNull(loaded);
            Assert.Equal(1, loaded!.MonteCarloSeed);
            Assert.Equal(1000, loaded.MonteCarloSimulations);

            var appState = JsonSerializer.Deserialize<AppState>("""{"Scenarios":[{"Scenario":{"Name":"Legacy"}}]}""");
            Assert.NotNull(appState);
            Assert.Equal(1, appState!.Scenarios[0].Scenario.MonteCarloSeed);
            Assert.Equal(1000, appState.Scenarios[0].Scenario.MonteCarloSimulations);

            var persisted = JsonSerializer.Serialize(appState);
            var reloaded = JsonSerializer.Deserialize<AppState>(persisted);
            Assert.NotNull(reloaded);
            Assert.Equal(1, reloaded!.Scenarios[0].Scenario.MonteCarloSeed);
            Assert.Equal(1000, reloaded.Scenarios[0].Scenario.MonteCarloSimulations);
        }
    }
}
