using System.Text.Json;
using ExitPlan.Core;
using ExitPlan.Core.Reporting;

namespace ExitPlan.Core.Tests
{
    public class ReportCardTests
    {
        [Fact]
        public void RecalculateOverall_UsesRelativeWeights()
        {
            var card = new ReportCard();
            card.Metrics.Add(new ReportCardMetric { Name = "A", Score = 100m, Weight = 1m });
            card.Metrics.Add(new ReportCardMetric { Name = "B", Score = 0m, Weight = 1m });

            card.RecalculateOverall();

            Assert.Equal(50m, card.OverallScore);
            Assert.Equal('F', card.OverallGrade);
        }

        [Fact]
        public void ListenForWeightEdits_RecalculatesOverallWhenWeightChanges()
        {
            var card = new ReportCard();
            card.Metrics.Add(new ReportCardMetric { Name = "A", Score = 100m, Weight = 1m });
            card.Metrics.Add(new ReportCardMetric { Name = "B", Score = 0m, Weight = 1m });
            card.RecalculateOverall();
            card.ListenForWeightEdits();

            card.Metrics[0].Weight = 3m;

            Assert.Equal(75m, card.OverallScore);
            Assert.Equal('C', card.OverallGrade);
        }

        [Fact]
        public void ApplySavedWeights_OverridesMatchingMetricsAndIgnoresUnknownNames()
        {
            var card = new ReportCard();
            card.Metrics.Add(new ReportCardMetric { Name = "Portfolio Longevity", Score = 100m, Weight = 25m });
            card.Metrics.Add(new ReportCardMetric { Name = "Income Coverage", Score = 0m, Weight = 15m });
            card.RecalculateOverall();

            card.ApplySavedWeights(new Dictionary<string, decimal>
            {
                ["Portfolio Longevity"] = 0m,
                ["Income Coverage"] = 10m,
                ["Unknown Metric"] = 99m
            });

            Assert.Equal(0m, card.Metrics[0].Weight);
            Assert.Equal(10m, card.Metrics[1].Weight);
            Assert.Equal(0m, card.OverallScore);
            Assert.Equal(2, card.Metrics.Count);
        }

        [Fact]
        public void ApplySavedWeights_EmptyOrNull_LeavesDefaults()
        {
            var card = new ReportCard();
            card.Metrics.Add(new ReportCardMetric { Name = "A", Score = 80m, Weight = 25m });
            card.RecalculateOverall();

            card.ApplySavedWeights(null);
            card.ApplySavedWeights(new Dictionary<string, decimal>());

            Assert.Equal(25m, card.Metrics[0].Weight);
            Assert.Equal(80m, card.OverallScore);
        }

        [Fact]
        public void Weight_ClampsNegativeValuesToZero()
        {
            var metric = new ReportCardMetric { Weight = 10m };
            metric.Weight = -5m;
            Assert.Equal(0m, metric.Weight);
        }

        [Fact]
        public void CaptureWeights_RoundTripsThroughApplySavedWeights()
        {
            var card = new ReportCard();
            card.Metrics.Add(new ReportCardMetric { Name = "A", Score = 100m, Weight = 40m });
            card.Metrics.Add(new ReportCardMetric { Name = "B", Score = 50m, Weight = 10m });

            var saved = card.CaptureWeights();

            var next = new ReportCard();
            next.Metrics.Add(new ReportCardMetric { Name = "A", Score = 100m, Weight = 1m });
            next.Metrics.Add(new ReportCardMetric { Name = "B", Score = 50m, Weight = 1m });
            next.ApplySavedWeights(saved);

            Assert.Equal(40m, next.Metrics[0].Weight);
            Assert.Equal(10m, next.Metrics[1].Weight);
            Assert.Equal(90m, next.OverallScore);
        }

        [Fact]
        public void Scenario_ReportCardWeights_RoundTripsThroughDeepCopyAndAppStateJson()
        {
            var scenario = new Scenario
            {
                Name = "Weights persist",
                ReportCardWeights = new Dictionary<string, decimal>
                {
                    ["Portfolio Longevity"] = 40m,
                    ["Sequence Risk"] = 5m
                }
            };

            var copiedScenario = scenario.DeepCopy();
            Assert.Equal(40m, copiedScenario.ReportCardWeights["Portfolio Longevity"]);
            Assert.Equal(5m, copiedScenario.ReportCardWeights["Sequence Risk"]);
            copiedScenario.ReportCardWeights["Portfolio Longevity"] = 1m;
            Assert.Equal(40m, scenario.ReportCardWeights["Portfolio Longevity"]);

            var appState = new AppState
            {
                Scenarios = [new ScenarioState { Scenario = scenario }]
            };

            var json = JsonSerializer.Serialize(appState);
            Assert.Contains("ReportCardWeights", json);
            Assert.Contains("Portfolio Longevity", json);

            var loaded = JsonSerializer.Deserialize<AppState>(json);
            Assert.NotNull(loaded);
            Assert.Equal(40m, loaded!.Scenarios[0].Scenario.ReportCardWeights["Portfolio Longevity"]);
            Assert.Equal(5m, loaded.Scenarios[0].Scenario.ReportCardWeights["Sequence Risk"]);
        }

        [Fact]
        public void Scenario_MissingReportCardWeights_DefaultsToEmptyAndDoesNotBreakLoad()
        {
            var loaded = JsonSerializer.Deserialize<Scenario>("{}");
            Assert.NotNull(loaded);
            Assert.NotNull(loaded!.ReportCardWeights);
            Assert.Empty(loaded.ReportCardWeights);

            var appState = JsonSerializer.Deserialize<AppState>("""{"Scenarios":[{"Scenario":{"Name":"Legacy"}}]}""");
            Assert.NotNull(appState);
            Assert.NotNull(appState!.Scenarios[0].Scenario.ReportCardWeights);
            Assert.Empty(appState.Scenarios[0].Scenario.ReportCardWeights);
        }
    }
}
