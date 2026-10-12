using System.Text.Json.Nodes;
using ExitPlan.Core;
using ExitPlan.Core.Engine;
using ExitPlan.Core.Services;

namespace ExitPlan.Core.Tests
{
    public class RmdStartAgeBandTests
    {
        [Fact]
        public void SeededPolicy_LoadsClosedBirthYearBands()
        {
            var policy = RmdPolicyLoader.LoadForYear(2025);

            Assert.Equal(75, policy.RmdStartAge);
            Assert.Equal(2, policy.StartAgeBands.Count);
            Assert.Equal(1951, policy.StartAgeBands[0].BornFrom);
            Assert.Equal(1959, policy.StartAgeBands[0].BornThrough);
            Assert.Equal(73, policy.StartAgeBands[0].StartAge);
            Assert.Equal(1960, policy.StartAgeBands[1].BornFrom);
            Assert.Null(policy.StartAgeBands[1].BornThrough);
            Assert.Equal(75, policy.StartAgeBands[1].StartAge);
        }

        [Fact]
        public void ImpliedBirthYear_1954_StartsAt73()
        {
            var policy = RmdPolicyLoader.LoadForYear(2025);
            int birthYear = RmdPolicy.ImpliedBirthYear(baseYear: 2026, currentAge: 72);

            Assert.Equal(1954, birthYear);
            Assert.Equal(73, policy.ResolveStartAge(birthYear));
            Assert.Equal(73, ExitPlan.Core.Engine.RmdEngine.ResolveStartAge(policy, 2026, 72));
        }

        [Fact]
        public void ImpliedBirthYear_1965_StartsAt75()
        {
            var policy = RmdPolicyLoader.LoadForYear(2025);
            int birthYear = RmdPolicy.ImpliedBirthYear(baseYear: 2039, currentAge: 74);

            Assert.Equal(1965, birthYear);
            Assert.Equal(75, policy.ResolveStartAge(birthYear));
            Assert.Equal(75, ExitPlan.Core.Engine.RmdEngine.ResolveStartAge(policy, 2039, 74));
        }

        [Fact]
        public void NoMatchingBand_UsesRmdStartAgeFallback()
        {
            var policy = RmdPolicyLoader.LoadForYear(2025);
            int birthYear = RmdPolicy.ImpliedBirthYear(baseYear: 2024, currentAge: 74);

            Assert.Equal(1950, birthYear);
            Assert.Equal(policy.RmdStartAge, policy.ResolveStartAge(birthYear));
            Assert.Equal(75, policy.ResolveStartAge(birthYear));
        }

        [Fact]
        public void LaterOpenBand_DoesNotAlsoMatchEarlierClosedStartAge()
        {
            var policy = RmdPolicyLoader.LoadForYear(2025);

            Assert.Equal(73, policy.ResolveStartAge(1951));
            Assert.Equal(73, policy.ResolveStartAge(1954));
            Assert.Equal(73, policy.ResolveStartAge(1959));
            Assert.Equal(75, policy.ResolveStartAge(1960));
            Assert.NotEqual(
                policy.StartAgeBands[1].StartAge,
                policy.ResolveStartAge(1954));
        }

        [Fact]
        public void AbsentStartAgeBands_KeepsSingleAgeBehavior()
        {
            var policy = new RmdPolicy
            {
                RmdStartAge = 73,
                LifeExpectancyDivisors = { [73] = 26.5m }
            };

            Assert.Empty(policy.StartAgeBands);
            Assert.Equal(73, policy.ResolveStartAge(1954));
            Assert.Equal(73, policy.ResolveStartAge(1965));
            Assert.Equal(73, policy.ResolveStartAge(1940));
        }

        [Fact]
        public void Validator_RejectsOverlappingBands()
        {
            var errors = RmdPolicyValidator.Validate(75, ValidDivisors(), new[]
            {
                new RmdStartAgeBand { BornFrom = 1951, BornThrough = 1959, StartAge = 73 },
                new RmdStartAgeBand { BornFrom = 1955, BornThrough = 1965, StartAge = 74 }
            });

            Assert.Contains(errors, e => e.Contains("overlap", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Validator_RejectsOpenEndedBandThatIsNotLast()
        {
            var errors = RmdPolicyValidator.Validate(75, ValidDivisors(), new[]
            {
                new RmdStartAgeBand { BornFrom = 1951, BornThrough = null, StartAge = 73 },
                new RmdStartAgeBand { BornFrom = 1960, BornThrough = null, StartAge = 75 }
            });

            Assert.Contains(errors, e => e.Contains("last", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Document_RoundTripsStartAgeBands_IncludingNullBornThrough()
        {
            using var folder = new TempRmdFolder();
            var path = folder.CopyFromExisting(2025);

            var document = RmdPolicyDocument.Load(path);
            var policy = document.ToPolicy();
            policy.StartAgeBands.Insert(0, new RmdStartAgeBand
            {
                BornFrom = 1940,
                BornThrough = 1950,
                StartAge = 72
            });
            document.Apply(policy);
            document.Save();

            var reloaded = RmdPolicyLoader.LoadFromPath(path);
            Assert.Equal(3, reloaded.StartAgeBands.Count);
            Assert.Equal(1940, reloaded.StartAgeBands[0].BornFrom);
            Assert.Equal(1950, reloaded.StartAgeBands[0].BornThrough);
            Assert.Equal(72, reloaded.StartAgeBands[0].StartAge);
            Assert.Null(reloaded.StartAgeBands[2].BornThrough);

            var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.Null(json["StartAgeBands"]![2]!["BornThrough"]);
        }

        [Fact]
        public void Document_AbsentStartAgeBands_LeavesListEmpty_AndSaveOmitsProperty()
        {
            using var folder = new TempRmdFolder();
            var path = folder.WriteJson(
                """
                {
                  "RmdStartAge": 73,
                  "LifeExpectancyDivisors": { "73": 26.5 }
                }
                """);

            var document = RmdPolicyDocument.Load(path);
            var policy = document.ToPolicy();
            Assert.Empty(policy.StartAgeBands);
            Assert.Equal(73, policy.ResolveStartAge(1965));

            document.Apply(policy);
            document.Save();

            var reloaded = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.False(reloaded.ContainsKey("StartAgeBands"));
            Assert.Equal(73, reloaded["RmdStartAge"]!.GetValue<int>());
        }

        [Fact]
        public void Engine_OwnerBorn1954_StartsRmdAt73()
        {
            var output = RunRmd(baseYear: 2026, currentAge: 72, years: 2, deferred: 265_000m);

            Assert.Equal(1954, RmdPolicy.ImpliedBirthYear(2026, 72));
            Assert.Equal(0m, output.Results[0].Rmds);
            Assert.Equal(10_000m, output.Results[1].Rmds);
        }

        [Fact]
        public void Engine_OwnerBorn1965_StartsRmdAt75()
        {
            var output = RunRmd(baseYear: 2039, currentAge: 74, years: 2, deferred: 246_000m);

            Assert.Equal(1965, RmdPolicy.ImpliedBirthYear(2039, 74));
            Assert.Equal(0m, output.Results[0].Rmds);
            Assert.Equal(10_000m, output.Results[1].Rmds);
        }

        [Fact]
        public void Engine_OwnerWithNoMatchingBand_UsesRmdStartAge()
        {
            var output = RunRmd(baseYear: 2024, currentAge: 74, years: 2, deferred: 246_000m);

            Assert.Equal(1950, RmdPolicy.ImpliedBirthYear(2024, 74));
            Assert.Equal(0m, output.Results[0].Rmds);
            Assert.Equal(10_000m, output.Results[1].Rmds);
        }

        private static SimulationOutput RunRmd(int baseYear, int currentAge, int years, decimal deferred)
        {
            var (scenario, accounts, primary, spouse) = TestHousehold.Create(
                taxDeferred: deferred,
                taxFreeCash: 5_000m,
                taxableCash: 1_000m,
                spending: 0m,
                years: years,
                currentAge: currentAge,
                deathAge: 95);

            scenario.BaseYear = baseYear;
            foreach (var account in accounts)
                account.RateOfReturn = 0m;

            var engine = new ComputeEngine(scenario, accounts, new FixedMarketService(), primary, spouse);
            return engine.Run(1, "fixed");
        }

        private static IReadOnlyList<(int Age, decimal Period)> ValidDivisors() =>
            new[] { (Age: 73, Period: 26.5m), (Age: 75, Period: 24.6m) };

        private sealed class TempRmdFolder : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ExitPlanRmdStartAgeBands",
                Guid.NewGuid().ToString("N"));

            public TempRmdFolder()
            {
                Directory.CreateDirectory(Path);
            }

            public string CopyFromExisting(int year)
            {
                var source = RmdPolicyLoader.ListAvailableFiles().Single(f => f.Year == year).FullPath;
                var dest = System.IO.Path.Combine(Path, $"RMD{year}.json");
                File.Copy(source, dest);
                return dest;
            }

            public string WriteJson(string json)
            {
                var dest = System.IO.Path.Combine(Path, "RMD2025.json");
                File.WriteAllText(dest, json);
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
