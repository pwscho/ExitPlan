using System.Text.Json.Nodes;
using ExitPlan.Core.MarketData;

namespace ExitPlan.Core.Tests
{
    public class MarketDataSettingsTests
    {
        [Fact]
        public void Load_UsesCanonicalMarketDataFile()
        {
            var history = MarketHistoryLoader.Load();
            var fromPath = MarketHistoryLoader.LoadFromPath(MarketHistoryLoader.GetFilePath());

            Assert.True(File.Exists(MarketHistoryLoader.GetFilePath()));
            Assert.Equal("MarketData.json", Path.GetFileName(MarketHistoryLoader.GetFilePath()));
            Assert.Equal(history.Years.Count, fromPath.Years.Count);
            Assert.Contains(history.Years, y => y.Year == 1928);
            Assert.Contains(history.Years, y => y.Year == 2025);
            Assert.Equal(0.007m, history.Years.Single(y => y.Year == 1940).InflationRate);
        }

        [Fact]
        public void SaveAndReload_RoundTripsKnownFields_AndPreservesOrder()
        {
            using var folder = new TempMarketFolder();
            var path = folder.CopyCanonical();

            var document = MarketHistoryDocument.Load(path);
            var history = document.ToHistory();
            int originalCount = history.Years.Count;
            int originalFirstYear = history.Years[0].Year;

            var year1940 = history.Years.Single(y => y.Year == 1940);
            year1940.InflationRate = 0.009m;
            year1940.EquityReturn = -0.11m;
            history.Years.Add(new MarketYear
            {
                Year = 2026,
                EquityReturn = 0.10m,
                BondReturn = 0.03m,
                InflationRate = 0.02m
            });

            document.Apply(history);
            document.Save();

            var reloaded = MarketHistoryLoader.LoadFromPath(path);
            Assert.Equal(originalCount + 1, reloaded.Years.Count);
            Assert.Equal(originalFirstYear, reloaded.Years[0].Year);
            Assert.Equal(0.009m, reloaded.Years.Single(y => y.Year == 1940).InflationRate);
            Assert.Equal(-0.11m, reloaded.Years.Single(y => y.Year == 1940).EquityReturn);
            Assert.Equal(0.054m, reloaded.Years.Single(y => y.Year == 1940).BondReturn);
            Assert.Equal(0.10m, reloaded.Years.Single(y => y.Year == 2026).EquityReturn);
        }

        [Fact]
        public void Save_PreservesUnknownJsonProperties()
        {
            using var folder = new TempMarketFolder();
            var path = folder.CopyCanonical();

            var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            node["Source"] = "Ibbotson";
            var year1940 = node["Years"]!.AsArray().Single(y => y!["Year"]!.GetValue<int>() == 1940)!;
            year1940["Note"] = "keep-me";
            File.WriteAllText(path, node.ToJsonString());

            var document = MarketHistoryDocument.Load(path);
            var history = document.ToHistory();
            history.Years.Single(y => y.Year == 1940).InflationRate = 0.008m;
            document.Apply(history);
            document.Save();

            var reloaded = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.Equal("Ibbotson", reloaded["Source"]!.GetValue<string>());
            var saved1940 = reloaded["Years"]!.AsArray().Single(y => y!["Year"]!.GetValue<int>() == 1940)!;
            Assert.Equal("keep-me", saved1940["Note"]!.GetValue<string>());
            Assert.Equal(0.008m, saved1940["InflationRate"]!.GetValue<decimal>());
        }

        [Fact]
        public void Validator_RejectsEmptyAndDuplicateYears_ButAllowsNegativeReturns()
        {
            var history = MarketHistoryLoader.Load();
            history.Years.Clear();
            var emptyErrors = MarketHistoryValidator.Validate(history);
            Assert.Contains(emptyErrors, e => e.Contains("at least one", StringComparison.OrdinalIgnoreCase));

            var invalid = new MarketHistory
            {
                Years =
                {
                    new MarketYear { Year = 1940, EquityReturn = -0.5m, BondReturn = -0.02m, InflationRate = -0.01m },
                    new MarketYear { Year = 1940, EquityReturn = 0.1m },
                    new MarketYear { Year = 12, EquityReturn = 0.1m }
                }
            };

            var errors = MarketHistoryValidator.Validate(invalid);
            Assert.Contains(errors, e => e.Contains("more than once", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(errors, e => e.Contains("valid calendar year", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(errors, e => e.Contains("negative", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void LoaderSave_RejectsInvalidHistory_AndLeavesFileUnchanged()
        {
            using var folder = new TempMarketFolder();
            var path = folder.CopyCanonical();
            var original = File.ReadAllText(path);

            var history = MarketHistoryLoader.LoadFromPath(path);
            history.Years.Clear();

            Assert.Throws<InvalidOperationException>(() => MarketHistoryLoader.Save(path, history));
            Assert.Equal(original, File.ReadAllText(path));
        }

        private sealed class TempMarketFolder : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ExitPlanMarketSettings",
                Guid.NewGuid().ToString("N"));

            public TempMarketFolder()
            {
                Directory.CreateDirectory(Path);
            }

            public string CopyCanonical()
            {
                var dest = System.IO.Path.Combine(Path, "MarketData.json");
                File.Copy(MarketHistoryLoader.GetFilePath(), dest);
                return dest;
            }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
        }
    }
}
