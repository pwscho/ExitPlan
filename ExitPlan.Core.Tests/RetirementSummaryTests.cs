using ExitPlan.Core;
using ExitPlan.Core.MarketData;
using ExitPlan.Core.Reporting;
using ExitPlan.Core.Simulation;

namespace ExitPlan.Core.Tests
{
    public class RetirementSummaryTests
    {
        [Fact]
        public void Build_UsesLiveHouseholdAndPortfolio_NotHardCodedNames()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 1_000_000m,
                taxFreeCash: 80_000m,
                taxableCash: 120_000m,
                spending: 90_000m,
                years: 5,
                currentAge: 62,
                deathAge: 90,
                married: true,
                spouseAge: 60,
                spouseDeathAge: 92);

            scenario.Name = "Lake House Plan";
            scenario.BaseYear = 2027;
            primary.Owner = "Alex";
            spouse!.Owner = "Jordan";
            spouse.ClaimAge = 67;
            TestHousehold.Set(accounts, spouse.UserId, "Tax Deferred", "Equities", 500_000m);

            var model = RetirementSummaryBuilder.Build(
                scenario,
                accounts,
                primary,
                spouse,
                Array.Empty<ResultRow>(),
                Array.Empty<ResultRow>(),
                Array.Empty<ResultRow>(),
                reportCard: null,
                generatedAt: new DateTime(2026, 9, 14));

            Assert.Equal("Lake House Plan", model.ScenarioName);
            Assert.Equal(2027, model.PlanStartYear);
            Assert.Equal(2031, model.PlanEndYear);
            Assert.Contains(model.Members, m => m.Name == "Alex" && m.CurrentAge == 62 && m.DeathAge == 90);
            Assert.Contains(model.Members, m => m.Name == "Jordan" && m.CurrentAge == 60 && m.DeathAge == 92);
            Assert.Contains("Alex", model.BottomLine);
            Assert.Contains("Jordan", model.BottomLine);
            Assert.Equal(1_000_000m + 80_000m + 120_000m + 500_000m + 10_000m, model.StartingPortfolio);
            Assert.True(model.PortfolioSlices.Count >= 2);
            Assert.Equal(1m, model.PortfolioSlices.Sum(s => s.Share), 4);
            Assert.False(model.HasResults);
            Assert.Null(model.Historical);
            Assert.Null(model.MonteCarlo);
            Assert.Null(model.Fixed);
            Assert.Null(model.Guardrails);
            Assert.Empty(model.PortfolioByYear);
            Assert.Empty(model.OrdinaryBracketsByYear);
            Assert.Equal(0, model.SpendingRaises);
            Assert.Equal(0, model.SpendingCuts);
            Assert.Equal("Raises: 0 · Cuts: 0", RetirementSummaryFormat.SpendingPowerAdjustments(model.SpendingRaises, model.SpendingCuts));
        }

        [Fact]
        public void Build_MapsSocialSecurityClaimYearFromLiveAges()
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(
                currentAge: 64,
                deathAge: 90);
            scenario.BaseYear = 2027;
            primary.Owner = "Riley";
            primary.ClaimAge = 70;
            primary.BenefitAtFullRetirementAge = 3_200m;
            primary.Cola = 0.025m;

            var model = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                Array.Empty<ResultRow>(), Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);

            var ss = Assert.Single(model.SocialSecurity);
            Assert.Equal("Riley", ss.Name);
            Assert.Equal(70, ss.ClaimAge);
            Assert.Equal(2033, ss.ClaimYear);
            Assert.Equal(3_200m, ss.MonthlyPia);
            Assert.Equal(0.025m, ss.Cola);
        }

        [Fact]
        public void Build_IncludesSpendingExtrasFromWithdrawalStreams()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                spending: 70_000m,
                currentAge: 60,
                married: true,
                spouseAge: 54);
            primary.Owner = "Casey";
            spouse!.Owner = "Morgan";
            scenario.BaseYear = 2026;
            scenario.WithdrawalStreams.Add(new WithdrawalStream
            {
                Name = "College",
                OwnerId = primary.UserId,
                StartAge = 62,
                EndAge = 65,
                AnnualAmount = 25_000m
            });

            var model = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, spouse,
                Array.Empty<ResultRow>(), Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);

            var extra = Assert.Single(model.SpendingExtras);
            Assert.Equal("College", extra.Name);
            Assert.Equal("Casey", extra.Owner);
            Assert.Equal(25_000m, extra.AnnualAmount);
            Assert.Equal(2028, extra.StartYear);
            Assert.Equal(2031, extra.EndYear);
        }

        [Fact]
        public void Build_ComputesSuccessFromSimulationResults_AndNaWhenMissing()
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(spending: 10_000m, years: 2);
            var fixedRows = new List<ResultRow>
            {
                new() { SimNo = 1, Year = 2026, Iteration = 1, EndingBalance = 100_000m, Withdrawal = 10_000m, BaseSpending = 10_000m, AddedSpending = 2_000m },
                new() { SimNo = 1, Year = 2027, Iteration = 2, EndingBalance = 90_000m, Withdrawal = 10_200m, BaseSpending = 10_200m, AddedSpending = 0m }
            };
            var historicalRows = new List<ResultRow>
            {
                new() { SimNo = 1, Year = 2026, Iteration = 1, EndingBalance = 1m },
                new() { SimNo = 1, Year = 2027, Iteration = 2, EndingBalance = 1m },
                new() { SimNo = 2, Year = 2026, Iteration = 1, EndingBalance = 1m },
                new() { SimNo = 2, Year = 2027, Iteration = 2, EndingBalance = 0m }
            };

            var model = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                fixedRows, historicalRows, Array.Empty<ResultRow>(),
                null);

            Assert.True(model.HasResults);
            Assert.NotNull(model.Fixed);
            Assert.Equal(1m, model.Fixed!.SuccessRate);
            Assert.Equal(1, model.Fixed.PathCount);
            Assert.NotNull(model.Historical);
            Assert.Equal(0.5m, model.Historical!.SuccessRate);
            Assert.Equal(2, model.Historical.PathCount);
            Assert.Null(model.MonteCarlo);
            Assert.Equal("n/a", RetirementSummaryFormat.SuccessOrNa(model.MonteCarlo));
            Assert.Equal(2, model.SpendingByYear.Count);
            Assert.Equal(2_000m, model.SpendingByYear[0].ExtraSpending);
            Assert.Equal(0m, model.SpendingByYear[1].ExtraSpending);
            Assert.Equal(2, model.PortfolioByYear.Count);
            Assert.Equal(100_000m, model.PortfolioByYear[0].EndingBalance);
            Assert.Equal(90_000m, model.PortfolioByYear[1].EndingBalance);
            Assert.Equal(0, model.SpendingRaises);
            Assert.Equal(0, model.SpendingCuts);
        }

        [Fact]
        public void CompactCurrency_UsesMillionsAndThousands()
        {
            Assert.Equal("$1.95M", RetirementSummaryFormat.CompactCurrency(1_950_000m));
            Assert.Equal("$90k", RetirementSummaryFormat.CompactCurrency(90_000m));
        }

        [Fact]
        public void GeneratePdf_WritesPdfFromLiveModel()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: 400_000m,
                taxFreeCash: 50_000m,
                taxableCash: 50_000m,
                spending: 20_000m,
                years: 4,
                married: true);
            scenario.Name = "Print Summary Fixture";
            scenario.BaseYear = 2027;
            scenario.MonteCarloSimulations = 3;
            scenario.MonteCarloSeed = 7;
            scenario.WithdrawalStreams.Add(new WithdrawalStream
            {
                Name = "Bridge",
                OwnerId = primary.UserId,
                StartAge = primary.CurrentAge,
                EndAge = primary.CurrentAge + 1,
                AnnualAmount = 8_000m
            });

            var history = MarketHistoryLoader.Load();
            var runner = new SimulationRunner();
            var fixedRows = runner.RunFixed(scenario, accounts, primary, spouse);
            var historicalRows = runner.RunHistorical(scenario, accounts, primary, spouse, history);
            var mcRows = runner.RunMonteCarlo(scenario, accounts, primary, spouse, history, simulations: 3, seed: 7);

            var model = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, spouse,
                fixedRows, historicalRows, mcRows,
                reportCard: null,
                generatedAt: new DateTime(2026, 9, 14));

            byte[] pdf = RetirementSummaryPdf.Generate(model);

            Assert.True(pdf.Length > 1000);
            Assert.Equal('%', (char)pdf[0]);
            Assert.Equal('P', (char)pdf[1]);
            Assert.Equal('D', (char)pdf[2]);
            Assert.Equal('F', (char)pdf[3]);
            Assert.True(model.HasResults);
            Assert.NotNull(model.Historical);
            Assert.NotNull(model.MonteCarlo);
            Assert.NotNull(model.Fixed);
            Assert.Null(model.Guardrails);
        }

        [Fact]
        public void Build_MapsTaxStatusSeparateFromSpendingBucket_PlusPriorityAndReturn()
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(
                taxDeferred: 400_000m,
                taxFreeCash: 50_000m,
                taxableCash: 80_000m);

            var deferred = TestHousehold.Get(accounts, primary.UserId, "Tax Deferred", "Equities");
            deferred.Bucket = 2;
            deferred.Priority = 4;
            deferred.RateOfReturn = 0.08m;

            var cash = TestHousehold.Get(accounts, primary.UserId, "Taxable", "Cash");
            cash.Bucket = 0;
            cash.Priority = 9;
            cash.RateOfReturn = 0.015m;

            var model = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                Array.Empty<ResultRow>(), Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);

            var deferredLine = Assert.Single(model.Accounts, a => a.TaxBucket == "Tax Deferred" && a.AssetClass == "Equities");
            Assert.Equal("Primary", deferredLine.Owner);
            Assert.Equal(2, deferredLine.Bucket);
            Assert.Equal(4, deferredLine.Priority);
            Assert.Equal(0.08m, deferredLine.RateOfReturn);
            Assert.Equal(400_000m, deferredLine.Value);

            var cashLine = Assert.Single(model.Accounts, a => a.TaxBucket == "Taxable" && a.AssetClass == "Cash");
            Assert.Equal(0, cashLine.Bucket);
            Assert.Equal(9, cashLine.Priority);
            Assert.Equal(0.015m, cashLine.RateOfReturn);
            Assert.Equal("1.5%", RetirementSummaryFormat.Percent(cashLine.RateOfReturn, 1));
            Assert.Equal("8.0%", RetirementSummaryFormat.Percent(deferredLine.RateOfReturn, 1));
        }

        [Fact]
        public void Build_IncludesIncomeStreamsWithTaxableFlag_AndEmptyWhenNone()
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                spending: 70_000m,
                currentAge: 62,
                married: true,
                spouseAge: 60);
            primary.Owner = "Alex";
            spouse!.Owner = "Jordan";
            scenario.BaseYear = 2026;

            var empty = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, spouse,
                Array.Empty<ResultRow>(), Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);
            Assert.Empty(empty.IncomeStreams);

            scenario.IncomeStreams.Add(new IncomeStream
            {
                Name = "Pension",
                OwnerId = primary.UserId,
                StartAge = 65,
                EndAge = 90,
                AnnualAmount = 24_000m,
                IsTaxable = true
            });
            scenario.IncomeStreams.Add(new IncomeStream
            {
                Name = "Rental",
                OwnerId = spouse.UserId,
                StartAge = 62,
                EndAge = 80,
                AnnualAmount = 0m,
                IsTaxable = false
            });

            var model = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, spouse,
                Array.Empty<ResultRow>(), Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);

            Assert.Equal(2, model.IncomeStreams.Count);
            var pension = Assert.Single(model.IncomeStreams, i => i.Name == "Pension");
            Assert.Equal("Alex", pension.Owner);
            Assert.Equal(65, pension.StartAge);
            Assert.Equal(90, pension.EndAge);
            Assert.Equal(24_000m, pension.AnnualAmount);
            Assert.True(pension.IsTaxable);
            Assert.Equal(2029, pension.StartYear);

            var rental = Assert.Single(model.IncomeStreams, i => i.Name == "Rental");
            Assert.Equal("Jordan", rental.Owner);
            Assert.Equal(0m, rental.AnnualAmount);
            Assert.False(rental.IsTaxable);
        }

        [Fact]
        public void Build_IncludesZeroAmountRothConversions_AndEmptyWhenNone()
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(currentAge: 60);
            primary.Owner = "Riley";
            scenario.BaseYear = 2026;

            var empty = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                Array.Empty<ResultRow>(), Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);
            Assert.Empty(empty.RothConversions);

            scenario.RothConversions.Add(new RothConversionStream
            {
                Name = "Fill 22%",
                OwnerId = primary.UserId,
                StartAge = 62,
                EndAge = 70,
                AnnualAmount = 0m
            });

            var model = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                Array.Empty<ResultRow>(), Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);

            var conversion = Assert.Single(model.RothConversions);
            Assert.Equal("Fill 22%", conversion.Name);
            Assert.Equal("Riley", conversion.Owner);
            Assert.Equal(62, conversion.StartAge);
            Assert.Equal(70, conversion.EndAge);
            Assert.Equal(0m, conversion.AnnualAmount);
            Assert.Equal(2028, conversion.StartYear);
            Assert.Equal(2036, conversion.EndYear);
        }

        [Fact]
        public void GeneratePdf_WritesIncomeAndRothSectionsFromLiveModel()
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(
                taxDeferred: 200_000m,
                taxFreeCash: 40_000m,
                taxableCash: 30_000m,
                spending: 15_000m,
                years: 3);
            scenario.Name = "Income Roth Fixture";
            TestHousehold.Get(accounts, primary.UserId, "Tax Deferred", "Equities").Bucket = 3;
            scenario.IncomeStreams.Add(new IncomeStream
            {
                Name = "Payroll",
                OwnerId = primary.UserId,
                StartAge = primary.CurrentAge,
                EndAge = primary.CurrentAge + 2,
                AnnualAmount = 50_000m,
                IsTaxable = true
            });
            scenario.RothConversions.Add(new RothConversionStream
            {
                Name = "Bracket fill",
                OwnerId = primary.UserId,
                StartAge = primary.CurrentAge,
                EndAge = primary.CurrentAge + 1,
                AnnualAmount = 0m
            });

            var model = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                Array.Empty<ResultRow>(), Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null,
                generatedAt: new DateTime(2026, 9, 16));

            Assert.Single(model.IncomeStreams);
            Assert.Single(model.RothConversions);
            Assert.Contains(model.Accounts, a => a.Bucket == 3 && a.TaxBucket == "Tax Deferred");

            byte[] pdf = RetirementSummaryPdf.Generate(model);

            Assert.True(pdf.Length > 1000);
            Assert.Equal('%', (char)pdf[0]);
            Assert.Equal('P', (char)pdf[1]);
            Assert.Equal('D', (char)pdf[2]);
            Assert.Equal('F', (char)pdf[3]);
        }

        [Fact]
        public void Build_IncludesGuardrails_OnlyWhenSpendingStrategyIsGuardrails()
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(
                taxDeferred: 400_000m,
                taxFreeCash: 50_000m,
                taxableCash: 50_000m,
                spending: 40_000m);

            scenario.Guardrails.InitialPortfolioValue = 500_000m;
            scenario.Guardrails.LowerTrigger = 0.88m;
            scenario.Guardrails.LowerCut = -0.03m;
            scenario.Guardrails.MinSpendingFloor = 0.75m;
            scenario.Guardrails.InflateSpendingFloor = true;
            scenario.Guardrails.IncreaseBaselineWithInflation = true;
            scenario.Guardrails.UpperTrigger = 1.20m;
            scenario.Guardrails.UpperBonus = 0.015m;
            scenario.InitialPortfolioValue = 500_000m;

            scenario.SpendingStrategy = "Fixed";
            var off = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                Array.Empty<ResultRow>(), Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);
            Assert.Null(off.Guardrails);
            Assert.Equal("Fixed", off.SpendingStrategy);

            byte[] offPdf = RetirementSummaryPdf.Generate(off);
            Assert.True(offPdf.Length > 1000);
            Assert.Equal('%', (char)offPdf[0]);
            Assert.Equal('P', (char)offPdf[1]);
            Assert.Equal('D', (char)offPdf[2]);
            Assert.Equal('F', (char)offPdf[3]);

            scenario.SpendingStrategy = "Historical";
            var historical = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                Array.Empty<ResultRow>(), Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);
            Assert.Null(historical.Guardrails);
            Assert.Equal("Historical", historical.SpendingStrategy);

            scenario.SpendingStrategy = "Guardrails";
            var on = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                Array.Empty<ResultRow>(), Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);
            Assert.NotNull(on.Guardrails);
            Assert.Equal(500_000m, on.Guardrails!.InitialPortfolioValue);
            Assert.Equal(0.88m, on.Guardrails.LowerTrigger);
            Assert.Equal(-0.03m, on.Guardrails.LowerCut);
            Assert.Equal(0.75m, on.Guardrails.MinSpendingFloor);
            Assert.True(on.Guardrails.InflateSpendingFloor);
            Assert.True(on.Guardrails.IncreaseBaselineWithInflation);
            Assert.Equal(1.20m, on.Guardrails.UpperTrigger);
            Assert.Equal(0.015m, on.Guardrails.UpperBonus);
            Assert.Equal("88%", RetirementSummaryFormat.PercentTrim(on.Guardrails.LowerTrigger));
            Assert.Equal("3%", RetirementSummaryFormat.PercentTrim(on.Guardrails.LowerCut));
            Assert.Equal("1.5%", RetirementSummaryFormat.PercentTrim(on.Guardrails.UpperBonus));
            Assert.Equal("120%", RetirementSummaryFormat.PercentTrim(on.Guardrails.UpperTrigger));
            Assert.Equal("75%", RetirementSummaryFormat.PercentTrim(on.Guardrails.MinSpendingFloor));
            Assert.Equal("$500k", RetirementSummaryFormat.CompactCurrency(on.Guardrails.InitialPortfolioValue));

            byte[] onPdf = RetirementSummaryPdf.Generate(on);
            Assert.True(onPdf.Length > 1000);
            Assert.Equal('%', (char)onPdf[0]);
            Assert.Equal('P', (char)onPdf[1]);
            Assert.Equal('D', (char)onPdf[2]);
            Assert.Equal('F', (char)onPdf[3]);
            Assert.True(onPdf.Length > offPdf.Length);
        }

        [Fact]
        public void Build_CountsGuardrailRaisesAndCuts_IncludingFloorHitsAsCuts()
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(spending: 10_000m, years: 4);
            scenario.SpendingStrategy = "Guardrails";
            var rows = new List<ResultRow>
            {
                new() { SimNo = 1, Year = 2026, Iteration = 1, EndingBalance = 100_000m, GuardrailAction = GuardrailYearAction.None, GuardrailPortfolioPercent = 1.00m },
                new() { SimNo = 1, Year = 2027, Iteration = 2, EndingBalance = 80_000m, GuardrailAction = GuardrailYearAction.Cut, GuardrailPortfolioPercent = 0.70m },
                new() { SimNo = 1, Year = 2028, Iteration = 3, EndingBalance = 70_000m, GuardrailAction = GuardrailYearAction.FloorHit, GuardrailPortfolioPercent = 0.55m },
                new() { SimNo = 1, Year = 2029, Iteration = 4, EndingBalance = 120_000m, GuardrailAction = GuardrailYearAction.Raise, GuardrailPortfolioPercent = 1.25m }
            };

            var model = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                rows, Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);

            Assert.Equal(1, model.SpendingRaises);
            Assert.Equal(2, model.SpendingCuts);
            Assert.Equal("Raises: 1 · Cuts: 2", RetirementSummaryFormat.SpendingPowerAdjustments(model.SpendingRaises, model.SpendingCuts));
            Assert.Equal(
                "Spending held  ·  Raises: 1 · Cuts: 2",
                RetirementSummaryFormat.AppendSpendingPowerAdjustments("Spending held", 1, 2));
            Assert.Equal(4, model.PortfolioByYear.Count);
            Assert.Equal(0.70m, model.PortfolioByYear[1].GuardrailPortfolioPercent);
            Assert.Equal(1.25m, model.PortfolioByYear[3].GuardrailPortfolioPercent);
            Assert.NotNull(model.Guardrails);
        }

        [Fact]
        public void Build_MapsLivePortfolioPathAndOrdinaryBrackets_FromFixedSimulation()
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(
                taxDeferred: 0m,
                taxFreeCash: 10_000m,
                taxableCash: 0m,
                spending: 0m,
                years: 2,
                currentAge: 60);
            scenario.IncomeStreams.Add(new IncomeStream
            {
                OwnerId = primary.UserId,
                Name = "Pension",
                StartAge = 0,
                EndAge = 120,
                AnnualAmount = 40_000m,
                IsTaxable = true
            });

            var fixedRows = new SimulationRunner().RunFixed(scenario, accounts, primary, null);
            var model = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                fixedRows, Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);

            Assert.Equal(fixedRows.Count, model.PortfolioByYear.Count);
            Assert.Equal(fixedRows[0].EndingBalance, model.PortfolioByYear[0].EndingBalance);
            Assert.Equal(fixedRows[0].StartingBalance, model.PortfolioByYear[0].StartingBalance);
            Assert.Equal(fixedRows.Count, model.OrdinaryBracketsByYear.Count);
            Assert.Equal(12_400m, model.OrdinaryBracketsByYear[0].Fills.Single(f => f.Rate == 0.10m).Filled);
            Assert.Equal(11_500m, model.OrdinaryBracketsByYear[0].Fills.Single(f => f.Rate == 0.12m).Filled);
            Assert.Equal(26_500m, model.OrdinaryBracketsByYear[0].Headroom);
            Assert.Equal(0, model.SpendingRaises);
            Assert.Equal(0, model.SpendingCuts);

            byte[] pdf = RetirementSummaryPdf.Generate(model);
            Assert.True(pdf.Length > 1000);
            Assert.Equal('%', (char)pdf[0]);
            Assert.Equal('P', (char)pdf[1]);
            Assert.Equal('D', (char)pdf[2]);
            Assert.Equal('F', (char)pdf[3]);
        }

        [Fact]
        public void GeneratePdf_IncludesGuardrailPortfolioAndRaiseCutCounts_FromLiveGuardrailRun()
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(
                taxDeferred: 80_000m,
                taxFreeCash: 10_000m,
                taxableCash: 10_000m,
                spending: 40_000m,
                years: 4);
            scenario.SpendingStrategy = "Guardrails";
            scenario.InflationRate = 0.02m;
            scenario.Guardrails.InitialPortfolioValue = 1_000_000m;
            scenario.InitialPortfolioValue = 1_000_000m;
            scenario.Guardrails.LowerTrigger = 0.90m;
            scenario.Guardrails.LowerCut = -0.10m;
            scenario.Guardrails.MinSpendingFloor = 0.50m;
            scenario.Guardrails.UpperTrigger = 1.10m;
            scenario.Guardrails.UpperBonus = 0.01m;

            var fixedRows = new SimulationRunner().RunFixed(scenario, accounts, primary, null);
            var card = new ReportCard();
            card.Metrics.Add(new ReportCardMetric
            {
                Name = "Spending Power",
                Score = 80m,
                Weight = 5m,
                Detail = "Spending: $40,000 → $36,000 over 4 yrs"
            });
            card.RecalculateOverall();

            var model = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                fixedRows, Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                card,
                generatedAt: new DateTime(2026, 9, 17));

            Assert.NotNull(model.Guardrails);
            Assert.Equal(fixedRows.Count, model.PortfolioByYear.Count);
            Assert.Equal(fixedRows[0].GuardrailPortfolioPercent, model.PortfolioByYear[0].GuardrailPortfolioPercent);
            Assert.True(model.SpendingCuts > 0);
            Assert.Equal(0, model.SpendingRaises);
            Assert.NotEmpty(model.OrdinaryBracketsByYear);
            Assert.Equal(
                $"Spending: $40,000 → $36,000 over 4 yrs  ·  Raises: {model.SpendingRaises} · Cuts: {model.SpendingCuts}",
                RetirementSummaryFormat.AppendSpendingPowerAdjustments(card.Metrics[0].Detail, model.SpendingRaises, model.SpendingCuts));

            string svg = RetirementSummaryCharts.GuardrailPortfolioSvg(model.PortfolioByYear, model.Guardrails!);
            Assert.Contains("stroke='#1F4E79'", svg);
            Assert.Contains("stroke='#C45C5C'", svg);
            Assert.Contains("stroke='#2E8A8A'", svg);

            string taxSvg = RetirementSummaryCharts.OrdinaryBracketFillSvg(model.OrdinaryBracketsByYear);
            Assert.Contains("<rect", taxSvg);

            byte[] pdf = RetirementSummaryPdf.Generate(model);
            Assert.True(pdf.Length > 1000);
            Assert.Equal('%', (char)pdf[0]);
            Assert.Equal('P', (char)pdf[1]);
            Assert.Equal('D', (char)pdf[2]);
            Assert.Equal('F', (char)pdf[3]);
        }

        [Fact]
        public void GeneratePdf_FixedStrategy_UsesPortfolioPathNotGuardrailPercentChart()
        {
            var (scenario, accounts, primary, _) = TestHousehold.Create(
                taxDeferred: 200_000m,
                taxFreeCash: 40_000m,
                taxableCash: 30_000m,
                spending: 15_000m,
                years: 3);
            scenario.SpendingStrategy = "Fixed";

            var fixedRows = new SimulationRunner().RunFixed(scenario, accounts, primary, null);
            var model = RetirementSummaryBuilder.Build(
                scenario, accounts, primary, null,
                fixedRows, Array.Empty<ResultRow>(), Array.Empty<ResultRow>(),
                null);

            Assert.Null(model.Guardrails);
            Assert.Equal(0, model.SpendingRaises);
            Assert.Equal(0, model.SpendingCuts);
            Assert.Equal(fixedRows.Select(r => r.EndingBalance).ToList(), model.PortfolioByYear.Select(p => p.EndingBalance).ToList());

            string svg = RetirementSummaryCharts.PortfolioPathSvg(model.PortfolioByYear);
            Assert.Contains("stroke='#2E5A88'", svg);
            Assert.DoesNotContain("stroke='#C45C5C'", svg);

            byte[] pdf = RetirementSummaryPdf.Generate(model);
            Assert.True(pdf.Length > 1000);
            Assert.Equal('%', (char)pdf[0]);
            Assert.Equal('P', (char)pdf[1]);
            Assert.Equal('D', (char)pdf[2]);
            Assert.Equal('F', (char)pdf[3]);
        }
    }
}
