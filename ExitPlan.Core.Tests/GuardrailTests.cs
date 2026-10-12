using System.Text.Json;
using ExitPlan.Core;
using ExitPlan.Core.Engine;
using ExitPlan.Core.Services;
using ExitPlan.Core.Simulation;

namespace ExitPlan.Core.Tests
{
    public class GuardrailTests
    {
        [Fact]
        public void InitialPortfolioValue_WhenZero_IsInitializedFromStartingBalances()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 400_000m,
                taxFreeCash: 100_000m,
                taxableCash: 50_000m,
                spending: 10_000m,
                years: 2);

            scenario.SpendingStrategy = "Guardrails";
            scenario.Guardrails.InitialPortfolioValue = 0m;
            scenario.InitialPortfolioValue = 0m;

            decimal expected = accounts.Sum(a => a.Value);

            var engine = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse);
            engine.Run(1, "fixed");

            Assert.Equal(expected, scenario.Guardrails.InitialPortfolioValue);
            Assert.Equal(expected, scenario.InitialPortfolioValue);
            Assert.True(scenario.Guardrails.InitialPortfolioValue > 0m);
        }

        [Fact]
        public void Guardrails_WhenPortfolioDrops_SpendsDifferentlyThanFixed()
        {
            decimal spending = 80_000m;
            int years = 4;

            var (fixedScenario, fixedAccounts, fixedPrimary, _) = TestHousehold.Create(
                taxDeferred: 200_000m,
                taxFreeCash: 20_000m,
                taxableCash: 20_000m,
                spending: spending,
                years: years);

            var (railScenario, railAccounts, railPrimary, _) = TestHousehold.Create(
                taxDeferred: 200_000m,
                taxFreeCash: 20_000m,
                taxableCash: 20_000m,
                spending: spending,
                years: years);

            fixedScenario.SpendingStrategy = "Fixed";
            fixedScenario.InflationRate = 0.02m;

            railScenario.SpendingStrategy = "Guardrails";
            railScenario.InflationRate = 0.02m;
            railScenario.Guardrails.InitialPortfolioValue = 0m;
            railScenario.Guardrails.LowerTrigger = 0.90m;
            railScenario.Guardrails.LowerCut = -0.10m;
            railScenario.Guardrails.MinSpendingFloor = 0.50m;

            var runner = new SimulationRunner();
            var fixedRows = runner.RunFixed(fixedScenario, fixedAccounts, fixedPrimary, null);
            var railRows = runner.RunFixed(railScenario, railAccounts, railPrimary, null);

            Assert.Equal(fixedRows[0].BaseSpending, railRows[0].BaseSpending);
            Assert.True(
                railRows.Last().BaseSpending < fixedRows.Last().BaseSpending,
                "After a portfolio drop, Guardrails should cut spending below Fixed inflation.");
        }

        [Fact]
        public void Guardrails_InflateSpendingFloorFalse_FloorStaysNominal()
        {
            const decimal spending = 100_000m;
            const decimal floorPct = 0.90m;
            const decimal inflation = 0.02m;
            const int years = 6;

            var rows = RunFloorScenario(
                spending, floorPct, inflation, years, inflateFloor: false);

            decimal nominalFloor = spending * floorPct;

            Assert.Equal(spending, rows[0].BaseSpending);
            Assert.Equal(nominalFloor, rows[1].BaseSpending);
            Assert.Equal(nominalFloor, rows.Last().BaseSpending);
            foreach (var row in rows.Skip(1))
                Assert.Equal(nominalFloor, row.BaseSpending);
        }

        [Fact]
        public void Guardrails_InflateSpendingFloorTrue_FloorGrowsWithInflation()
        {
            const decimal spending = 100_000m;
            const decimal floorPct = 0.90m;
            const decimal inflation = 0.02m;
            const int years = 6;

            var rows = RunFloorScenario(
                spending, floorPct, inflation, years, inflateFloor: true);

            decimal nominalFloor = spending * floorPct;

            Assert.Equal(spending, rows[0].BaseSpending);
            Assert.Equal(nominalFloor, rows[1].BaseSpending);

            // Year k (0-based) spending sits on the floor inflated for k-1 years:
            // AnnualSpending * MinSpendingFloor * (1 + inflation)^(k-1).
            decimal expectedLast = nominalFloor;
            for (int i = 0; i < years - 2; i++)
                expectedLast *= (1 + inflation);

            Assert.Equal(expectedLast, rows.Last().BaseSpending);
            Assert.True(
                rows.Last().BaseSpending > nominalFloor,
                "Inflated floor should exceed the nominal floor after several years.");
        }

        [Fact]
        public void Guardrails_YearResults_DistinguishNoneCutFloorHitAndRaise()
        {
            const decimal spending = 40_000m;
            const decimal inflation = 0.02m;

            var none = RunActionScenario(
                spending, inflation, years: 3,
                taxDeferred: 400_000m, taxFree: 50_000m, taxable: 50_000m,
                baseline: 500_000m,
                lowerTrigger: 0.80m, lowerCut: -0.10m, floor: 0.50m,
                upperTrigger: 1.20m, upperBonus: 0.01m);

            Assert.All(none, row => Assert.Equal(GuardrailYearAction.None, row.GuardrailAction));
            Assert.All(none, row => Assert.Equal("none", row.GuardrailActionLabel));
            Assert.Equal(none[0].BaseSpending, none[0].InflationOnlyBaseSpending);
            Assert.Equal(none.Last().BaseSpending, none.Last().InflationOnlyBaseSpending);
            Assert.Equal(1.00m, none[0].GuardrailPortfolioPercent);

            var cut = RunActionScenario(
                spending, inflation, years: 3,
                taxDeferred: 80_000m, taxFree: 10_000m, taxable: 10_000m,
                baseline: 1_000_000m,
                lowerTrigger: 0.90m, lowerCut: -0.10m, floor: 0.50m,
                upperTrigger: 1.10m, upperBonus: 0.01m);

            Assert.Equal(GuardrailYearAction.Cut, cut[0].GuardrailAction);
            Assert.Equal("cut", cut[0].GuardrailActionLabel);
            Assert.Equal(0.10m, cut[0].GuardrailPortfolioPercent);
            Assert.Equal(cut[0].BaseSpending, cut[0].InflationOnlyBaseSpending);
            Assert.Equal(spending * (1 + inflation), cut[1].InflationOnlyBaseSpending);
            Assert.Equal(spending * (1 - 0.10m), cut[1].BaseSpending);
            Assert.True(cut[1].BaseSpending < cut[1].InflationOnlyBaseSpending);

            var floorHit = RunActionScenario(
                spending, inflation, years: 3,
                taxDeferred: 80_000m, taxFree: 10_000m, taxable: 10_000m,
                baseline: 1_000_000m,
                lowerTrigger: 0.90m, lowerCut: -0.50m, floor: 0.90m,
                upperTrigger: 1.10m, upperBonus: 0.01m);

            Assert.Equal(GuardrailYearAction.FloorHit, floorHit[0].GuardrailAction);
            Assert.Equal("floor hit", floorHit[0].GuardrailActionLabel);
            Assert.Equal(spending * 0.90m, floorHit[1].BaseSpending);
            Assert.True(floorHit[1].BaseSpending < floorHit[1].InflationOnlyBaseSpending);
            Assert.True(
                floorHit[1].BaseSpending > spending * (1 - 0.50m),
                "Floor hit should stop above a full LowerCut.");

            var raise = RunActionScenario(
                spending, inflation, years: 3,
                taxDeferred: 400_000m, taxFree: 50_000m, taxable: 50_000m,
                baseline: 200_000m,
                lowerTrigger: 0.90m, lowerCut: -0.10m, floor: 0.50m,
                upperTrigger: 1.10m, upperBonus: 0.01m);

            Assert.Equal(GuardrailYearAction.Raise, raise[0].GuardrailAction);
            Assert.Equal("raise", raise[0].GuardrailActionLabel);
            Assert.Equal(2.50m, raise[0].GuardrailPortfolioPercent);
            Assert.Equal(spending * (1 + inflation + 0.01m), raise[1].BaseSpending);
            Assert.True(raise[1].BaseSpending > raise[1].InflationOnlyBaseSpending);
        }

        [Fact]
        public void IncreaseBaselineWithInflation_Off_MatchesUnsetResultsAndFlatBaseline()
        {
            var unset = RunActionScenario(
                spending: 40_000m, inflation: 0.02m, years: 4,
                taxDeferred: 80_000m, taxFree: 10_000m, taxable: 10_000m,
                baseline: 1_000_000m,
                lowerTrigger: 0.90m, lowerCut: -0.10m, floor: 0.50m,
                upperTrigger: 1.10m, upperBonus: 0.01m);

            var off = RunActionScenario(
                spending: 40_000m, inflation: 0.02m, years: 4,
                taxDeferred: 80_000m, taxFree: 10_000m, taxable: 10_000m,
                baseline: 1_000_000m,
                lowerTrigger: 0.90m, lowerCut: -0.10m, floor: 0.50m,
                upperTrigger: 1.10m, upperBonus: 0.01m,
                increaseBaseline: false);

            Assert.Equal(unset.Count, off.Count);
            for (int i = 0; i < unset.Count; i++)
            {
                Assert.Equal(unset[i].EndingBalance, off[i].EndingBalance);
                Assert.Equal(unset[i].BaseSpending, off[i].BaseSpending);
                Assert.Equal(unset[i].GuardrailAction, off[i].GuardrailAction);
                Assert.Equal(unset[i].GuardrailPortfolioPercent, off[i].GuardrailPortfolioPercent);
                Assert.Equal(off[i].StartingBalance / 1_000_000m, off[i].GuardrailPortfolioPercent);
            }
        }

        [Fact]
        public void IncreaseBaselineWithInflation_On_CompoundsConstantScenarioInflation()
        {
            const decimal ipv = 1_000_000m;
            const decimal inflation = 0.02m;
            const int years = 4;

            var rows = RunStablePortfolio(
                increaseBaseline: true,
                inflation: inflation,
                simulation: "fixed",
                market: new FixedMarketService(),
                portfolio: ipv,
                ipv: ipv,
                years: years);

            decimal factor = 1m;
            for (int year = 0; year < years; year++)
            {
                decimal baseline = ipv * factor;
                Assert.Equal(rows[year].StartingBalance / baseline, rows[year].GuardrailPortfolioPercent);
                factor *= 1m + inflation;
            }

            // (1.02)^3, the baseline at the start of year index 3 (t − BaseYear = 3).
            decimal expectedYear3 = ipv;
            for (int i = 0; i < 3; i++)
                expectedYear3 *= 1m + inflation;
            Assert.Equal(rows[3].StartingBalance / expectedYear3, rows[3].GuardrailPortfolioPercent);
            Assert.NotEqual(1m, rows[3].GuardrailPortfolioPercent);
        }

        [Fact]
        public void IncreaseBaselineWithInflation_On_StopsUpperTriggerOnceBaselineCatchesPortfolio()
        {
            const decimal ipv = 1_000_000m;
            const decimal portfolio = 1_300_000m;
            const decimal inflation = 0.20m;

            var off = RunStablePortfolio(
                increaseBaseline: false,
                inflation: inflation,
                simulation: "fixed",
                market: new FixedMarketService(),
                portfolio: portfolio,
                ipv: ipv,
                years: 2,
                lower: 0.80m,
                upper: 1.20m);

            var on = RunStablePortfolio(
                increaseBaseline: true,
                inflation: inflation,
                simulation: "fixed",
                market: new FixedMarketService(),
                portfolio: portfolio,
                ipv: ipv,
                years: 2,
                lower: 0.80m,
                upper: 1.20m);

            Assert.Equal(GuardrailYearAction.Raise, off[0].GuardrailAction);
            Assert.Equal(GuardrailYearAction.Raise, on[0].GuardrailAction);
            Assert.Equal(portfolio / ipv, off[0].GuardrailPortfolioPercent);
            Assert.Equal(off[0].GuardrailPortfolioPercent, on[0].GuardrailPortfolioPercent);

            Assert.Equal(GuardrailYearAction.Raise, off[1].GuardrailAction);
            Assert.Equal(portfolio / ipv, off[1].GuardrailPortfolioPercent);

            decimal inflated = ipv * (1m + inflation);
            Assert.Equal(portfolio / inflated, on[1].GuardrailPortfolioPercent);
            Assert.Equal(GuardrailYearAction.None, on[1].GuardrailAction);
        }

        [Fact]
        public void IncreaseBaselineWithInflation_On_DoesNotChangeCutOrFloorWhenTriggerStillFires()
        {
            var off = RunActionScenario(
                spending: 40_000m, inflation: 0.02m, years: 4,
                taxDeferred: 80_000m, taxFree: 10_000m, taxable: 10_000m,
                baseline: 1_000_000m,
                lowerTrigger: 0.90m, lowerCut: -0.10m, floor: 0.50m,
                upperTrigger: 1.10m, upperBonus: 0.01m,
                increaseBaseline: false);

            var on = RunActionScenario(
                spending: 40_000m, inflation: 0.02m, years: 4,
                taxDeferred: 80_000m, taxFree: 10_000m, taxable: 10_000m,
                baseline: 1_000_000m,
                lowerTrigger: 0.90m, lowerCut: -0.10m, floor: 0.50m,
                upperTrigger: 1.10m, upperBonus: 0.01m,
                increaseBaseline: true);

            Assert.Equal(off.Select(r => r.BaseSpending), on.Select(r => r.BaseSpending));
            Assert.Equal(off.Select(r => r.GuardrailAction), on.Select(r => r.GuardrailAction));
            Assert.Equal(off[0].GuardrailPortfolioPercent, on[0].GuardrailPortfolioPercent);
            Assert.NotEqual(off[1].GuardrailPortfolioPercent, on[1].GuardrailPortfolioPercent);
            Assert.Equal(on[1].StartingBalance / (1_000_000m * 1.02m), on[1].GuardrailPortfolioPercent);

            var floorOff = RunFloorScenario(100_000m, 0.90m, 0.02m, years: 6, inflateFloor: false);
            var floorOn = RunFloorScenario(100_000m, 0.90m, 0.02m, years: 6, inflateFloor: false, inflateBaseline: true);
            Assert.Equal(floorOff.Select(r => r.BaseSpending), floorOn.Select(r => r.BaseSpending));
            Assert.All(floorOn.Skip(1), row => Assert.Equal(90_000m, row.BaseSpending));

            var inflatedFloorOff = RunFloorScenario(100_000m, 0.90m, 0.02m, years: 6, inflateFloor: true);
            var inflatedFloorOn = RunFloorScenario(100_000m, 0.90m, 0.02m, years: 6, inflateFloor: true, inflateBaseline: true);
            Assert.Equal(inflatedFloorOff.Select(r => r.BaseSpending), inflatedFloorOn.Select(r => r.BaseSpending));
        }

        [Fact]
        public void IncreaseBaselineWithInflation_On_CompoundsHistoricalInflationPath()
        {
            var path = new decimal[] { 0.10m, 0.20m, 0.00m, 0.05m };
            var market = new PathInflationMarket(path);
            const decimal ipv = 1_000_000m;

            var rows = RunStablePortfolio(
                increaseBaseline: true,
                inflation: 0.99m,
                simulation: "historical",
                market: market,
                portfolio: ipv,
                ipv: ipv,
                years: path.Length);

            decimal factor = 1m;
            for (int year = 0; year < path.Length; year++)
            {
                Assert.Equal(ipv * factor, GuardrailSettings.TriggerBaseline(ipv, true, year, path));
                Assert.Equal(rows[year].StartingBalance / (ipv * factor), rows[year].GuardrailPortfolioPercent);
                factor *= 1m + path[year];
            }

            var ignored = RunStablePortfolio(
                increaseBaseline: false,
                inflation: 0.99m,
                simulation: "historical",
                market: market,
                portfolio: ipv,
                ipv: ipv,
                years: path.Length);

            Assert.All(ignored, row => Assert.Equal(1m, row.GuardrailPortfolioPercent));
        }

        [Fact]
        public void TriggerBaseline_CompoundsConstantAndVaryingPaths()
        {
            const decimal ipv = 800_000m;
            var constant = new[] { 0.02m, 0.02m, 0.02m, 0.02m, 0.02m };

            Assert.Equal(ipv, GuardrailSettings.TriggerBaseline(ipv, false, 4, constant));
            Assert.Equal(ipv, GuardrailSettings.TriggerBaseline(ipv, true, 0, constant));

            decimal powered = ipv;
            for (int year = 0; year <= constant.Length; year++)
            {
                Assert.Equal(powered, GuardrailSettings.TriggerBaseline(ipv, true, year, constant));
                if (year < constant.Length)
                    powered *= 1.02m;
            }

            var varying = new[] { 0.02m, 0.03m, -0.01m };
            Assert.Equal(ipv * 1.02m, GuardrailSettings.TriggerBaseline(ipv, true, 1, varying));
            Assert.Equal(ipv * 1.02m * 1.03m, GuardrailSettings.TriggerBaseline(ipv, true, 2, varying));
            Assert.Equal(ipv * 1.02m * 1.03m * 0.99m, GuardrailSettings.TriggerBaseline(ipv, true, 3, varying));

            Assert.Throws<ArgumentException>(() =>
                GuardrailSettings.TriggerBaseline(ipv, true, 2, new[] { 0.02m }));
        }

        [Fact]
        public void IncreaseBaselineWithInflation_RoundTripsThroughDeepCopyAndJson_AndDefaultsFalse()
        {
            var settings = new GuardrailSettings
            {
                IncreaseBaselineWithInflation = true
            };

            Assert.True(settings.DeepCopy().IncreaseBaselineWithInflation);
            Assert.False(new GuardrailSettings().IncreaseBaselineWithInflation);

            var scenario = new Scenario
            {
                Name = "Baseline persist",
                Guardrails = settings
            };
            Assert.True(scenario.DeepCopy().Guardrails.IncreaseBaselineWithInflation);

            var json = JsonSerializer.Serialize(scenario);
            Assert.Contains("IncreaseBaselineWithInflation", json);
            var loaded = JsonSerializer.Deserialize<Scenario>(json);
            Assert.True(loaded!.Guardrails.IncreaseBaselineWithInflation);

            var legacy = JsonSerializer.Deserialize<GuardrailSettings>("""{"InflateSpendingFloor":true}""");
            Assert.NotNull(legacy);
            Assert.False(legacy!.IncreaseBaselineWithInflation);
            Assert.True(legacy.InflateSpendingFloor);
        }

        [Fact]
        public void SampleAppState_Guardrails_Off_MatchesMaster2067Balance_On_Compounds()
        {
            var path = FindRepoFile(Path.Combine("ExitPlan.UI", "Data", "AppState.json"));
            var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(path));
            Assert.NotNull(state);
            var source = state!.Scenarios[0];

            var off = RunSample(source, increaseBaseline: false);
            var on = RunSample(source, increaseBaseline: true);

            var off2067 = off.Single(r => r.Year == 2067);
            var on2067 = on.Single(r => r.Year == 2067);

            // Checked-in sample AppState.json, spending strategy set to Guardrails,
            // default triggers, equities 8%, bonds 2%, deterministic. Pins are the
            // engine's 2067 ending balances for that fixture.
            Assert.Equal(796016.1784717107407546700744m, off2067.EndingBalance);
            Assert.Equal(1808052.6749746723266249136642m, on2067.EndingBalance);
        }

        [Fact]
        public void InflateSpendingFloor_RoundTripsThroughDeepCopyAndJson()
        {
            var settings = new GuardrailSettings
            {
                MinSpendingFloor = 0.85m,
                InflateSpendingFloor = true
            };

            var copiedSettings = settings.DeepCopy();
            Assert.True(copiedSettings.InflateSpendingFloor);
            Assert.Equal(0.85m, copiedSettings.MinSpendingFloor);

            var scenario = new Scenario
            {
                Name = "Floor persist",
                AnnualSpending = 120_000m,
                Guardrails = settings
            };

            var copiedScenario = scenario.DeepCopy();
            Assert.True(copiedScenario.Guardrails.InflateSpendingFloor);

            var appState = new AppState
            {
                Scenarios =
                [
                    new ScenarioState
                    {
                        Scenario = scenario
                    }
                ]
            };

            var json = JsonSerializer.Serialize(appState);
            Assert.Contains("InflateSpendingFloor", json);

            var loaded = JsonSerializer.Deserialize<AppState>(json);
            Assert.NotNull(loaded);
            Assert.True(loaded!.Scenarios[0].Scenario.Guardrails.InflateSpendingFloor);

            // Old saves without the property stay at the backward-compatible default.
            var legacy = JsonSerializer.Deserialize<GuardrailSettings>("""{"MinSpendingFloor":0.9}""");
            Assert.NotNull(legacy);
            Assert.False(legacy!.InflateSpendingFloor);
        }

        private static List<ResultRow> RunActionScenario(
            decimal spending,
            decimal inflation,
            int years,
            decimal taxDeferred,
            decimal taxFree,
            decimal taxable,
            decimal baseline,
            decimal lowerTrigger,
            decimal lowerCut,
            decimal floor,
            decimal upperTrigger,
            decimal upperBonus,
            bool increaseBaseline = false)
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(
                taxDeferred: taxDeferred,
                taxFreeCash: taxFree,
                taxableCash: taxable,
                spending: spending,
                years: years);

            scenario.SpendingStrategy = "Guardrails";
            scenario.InflationRate = inflation;
            scenario.Guardrails.InitialPortfolioValue = baseline;
            scenario.InitialPortfolioValue = baseline;
            scenario.Guardrails.LowerTrigger = lowerTrigger;
            scenario.Guardrails.LowerCut = lowerCut;
            scenario.Guardrails.MinSpendingFloor = floor;
            scenario.Guardrails.UpperTrigger = upperTrigger;
            scenario.Guardrails.UpperBonus = upperBonus;
            scenario.Guardrails.IncreaseBaselineWithInflation = increaseBaseline;

            return new SimulationRunner().RunFixed(scenario, accounts, primary, null);
        }

        private static List<ResultRow> RunFloorScenario(
            decimal spending,
            decimal floorPct,
            decimal inflation,
            int years,
            bool inflateFloor,
            bool inflateBaseline = false)
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(
                taxDeferred: 80_000m,
                taxFreeCash: 10_000m,
                taxableCash: 10_000m,
                spending: spending,
                years: years);

            scenario.SpendingStrategy = "Guardrails";
            scenario.InflationRate = inflation;
            // Keep the portfolio below the lower trigger every year so cuts apply
            // and spending lands on the floor after year 0.
            scenario.Guardrails.InitialPortfolioValue = 1_000_000m;
            scenario.InitialPortfolioValue = 1_000_000m;
            scenario.Guardrails.LowerTrigger = 0.90m;
            scenario.Guardrails.LowerCut = -0.50m;
            scenario.Guardrails.MinSpendingFloor = floorPct;
            scenario.Guardrails.InflateSpendingFloor = inflateFloor;
            scenario.Guardrails.IncreaseBaselineWithInflation = inflateBaseline;

            return new SimulationRunner().RunFixed(scenario, accounts, primary, null);
        }

        private static List<ResultRow> RunStablePortfolio(
            bool increaseBaseline,
            decimal inflation,
            string simulation,
            IMarketService market,
            decimal portfolio,
            decimal ipv,
            int years,
            decimal lower = 0.50m,
            decimal upper = 2.00m)
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(
                taxDeferred: 0m,
                taxFreeCash: 0m,
                taxableCash: portfolio,
                spending: 0m,
                years: years);

            scenario.SpendingStrategy = "Guardrails";
            scenario.InflationRate = inflation;
            scenario.Guardrails.InitialPortfolioValue = ipv;
            scenario.InitialPortfolioValue = ipv;
            scenario.Guardrails.LowerTrigger = lower;
            scenario.Guardrails.LowerCut = -0.10m;
            scenario.Guardrails.MinSpendingFloor = 0.50m;
            scenario.Guardrails.UpperTrigger = upper;
            scenario.Guardrails.UpperBonus = 0.01m;
            scenario.Guardrails.IncreaseBaselineWithInflation = increaseBaseline;
            scenario.Guardrails.InflateSpendingFloor = false;

            return new ComputeEngine(scenario, accounts, market, primary, null)
                .Run(1, simulation)
                .Results;
        }

        private static List<ResultRow> RunSample(ScenarioState source, bool increaseBaseline)
        {
            var copy = source.DeepCopy();
            copy.Scenario.SpendingStrategy = "Guardrails";
            copy.Scenario.Guardrails.IncreaseBaselineWithInflation = increaseBaseline;
            return new SimulationRunner().RunFixed(
                copy.Scenario,
                copy.Accounts,
                copy.PrimarySocialSecurity!,
                copy.SpouseSocialSecurity);
        }

        private static string FindRepoFile(string relativePath)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException(relativePath);
        }

        private sealed class PathInflationMarket : IMarketService
        {
            private readonly decimal[] _inflation;

            public PathInflationMarket(decimal[] inflation) => _inflation = inflation;

            public decimal GetEquityReturnForYear(int yearIndex) => 0m;
            public decimal GetBondReturnForYear(int yearIndex) => 0m;
            public decimal GetInflationForYear(int yearIndex) => _inflation[yearIndex];
        }
    }
}
