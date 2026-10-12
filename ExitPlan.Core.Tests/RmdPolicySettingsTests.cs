using System.Text.Json.Nodes;
using ExitPlan.Core;

namespace ExitPlan.Core.Tests
{
    public class RmdPolicySettingsTests
    {
        [Fact]
        public void ListAvailableFiles_IncludesExistingRmdYearFile_HighestYearFirst()
        {
            var files = RmdPolicyLoader.ListAvailableFiles();

            Assert.Contains(files, f => f.Year == 2025 && f.FileName == "RMD2025.json");
            Assert.True(files.Count >= 1);
            Assert.Equal(files.Max(f => f.Year), files[0].Year);
            Assert.All(files, f => Assert.True(File.Exists(f.FullPath)));
        }

        [Fact]
        public void LoadForYear_StillSelectsExactOrClosestPriorFile()
        {
            var for2025 = RmdPolicyLoader.LoadForYear(2025);
            var for2024 = RmdPolicyLoader.LoadForYear(2024);
            var for2026 = RmdPolicyLoader.LoadForYear(2026);

            Assert.Equal(75, for2025.RmdStartAge);
            Assert.Equal(2, for2025.StartAgeBands.Count);
            Assert.Equal(24.6m, for2025.LifeExpectancyDivisors[75]);
            Assert.Equal(for2025.RmdStartAge, for2024.RmdStartAge);
            Assert.Equal(for2025.RmdStartAge, for2026.RmdStartAge);
            Assert.Equal(for2025.LifeExpectancyDivisors[73], for2026.LifeExpectancyDivisors[73]);
        }

        [Fact]
        public void FileNameForYear_MatchesEngineConvention()
        {
            Assert.Equal("RMD2025.json", RmdPolicyLoader.FileNameForYear(2025));
            Assert.Equal("RMD2026.json", RmdPolicyLoader.FileNameForYear(2026));
        }

        [Fact]
        public void SaveAndReload_RoundTripsKnownFields()
        {
            using var folder = new TempRmdFolder();
            var path = folder.CopyFromExisting(2025);

            var document = RmdPolicyDocument.Load(path);
            var policy = document.ToPolicy();
            policy.RmdStartAge = 73;
            policy.LifeExpectancyDivisors[75] = 24.5m;
            policy.LifeExpectancyDivisors[72] = 27.4m;
            policy.LifeExpectancyDivisors.Remove(120);
            document.Apply(policy);
            document.Save();

            var reloaded = RmdPolicyLoader.LoadFromPath(path);
            Assert.Equal(73, reloaded.RmdStartAge);
            Assert.Equal(2, reloaded.StartAgeBands.Count);
            Assert.Equal(1951, reloaded.StartAgeBands[0].BornFrom);
            Assert.Equal(1959, reloaded.StartAgeBands[0].BornThrough);
            Assert.Equal(73, reloaded.StartAgeBands[0].StartAge);
            Assert.Null(reloaded.StartAgeBands[1].BornThrough);
            Assert.Equal(75, reloaded.StartAgeBands[1].StartAge);
            Assert.Equal(24.5m, reloaded.LifeExpectancyDivisors[75]);
            Assert.Equal(27.4m, reloaded.LifeExpectancyDivisors[72]);
            Assert.False(reloaded.LifeExpectancyDivisors.ContainsKey(120));
            Assert.Equal(26.5m, reloaded.LifeExpectancyDivisors[73]);
        }

        [Fact]
        public void Save_PreservesUnknownJsonProperties()
        {
            using var folder = new TempRmdFolder();
            var path = folder.CopyFromExisting(2025);

            var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            node["FutureField"] = "keep-me";
            node["LifeExpectancyDivisors"]!["Note"] = "uniform lifetime";
            File.WriteAllText(path, node.ToJsonString());

            var document = RmdPolicyDocument.Load(path);
            var policy = document.ToPolicy();
            policy.RmdStartAge = 73;
            document.Apply(policy);
            document.Save();

            var reloaded = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.Equal("keep-me", reloaded["FutureField"]!.GetValue<string>());
            Assert.Equal("uniform lifetime", reloaded["LifeExpectancyDivisors"]!["Note"]!.GetValue<string>());
            Assert.Equal(73, reloaded["RmdStartAge"]!.GetValue<int>());
            Assert.Equal(24.6m, reloaded["LifeExpectancyDivisors"]!["75"]!.GetValue<decimal>());
        }

        [Fact]
        public void Validator_RejectsNegativeEmptyAndDuplicateNonsense()
        {
            var policy = RmdPolicyLoader.LoadForYear(2025);
            policy.RmdStartAge = -1;
            policy.LifeExpectancyDivisors.Clear();

            var emptyErrors = RmdPolicyValidator.Validate(policy);
            Assert.Contains(emptyErrors, e => e.Contains("start age", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(emptyErrors, e => e.Contains("at least one", StringComparison.OrdinalIgnoreCase));

            var rowErrors = RmdPolicyValidator.Validate(75, new[]
            {
                (Age: 75, Period: 24.6m),
                (Age: 75, Period: 0m),
                (Age: -4, Period: 10m)
            });

            Assert.Contains(rowErrors, e => e.Contains("more than once", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(rowErrors, e => e.Contains("greater than 0", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(rowErrors, e => e.Contains("age must be", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void LoaderSave_RejectsInvalidPolicy_AndLeavesFileUnchanged()
        {
            using var folder = new TempRmdFolder();
            var path = folder.CopyFromExisting(2025);
            var original = File.ReadAllText(path);

            var policy = RmdPolicyLoader.LoadFromPath(path);
            policy.RmdStartAge = -5;

            Assert.Throws<InvalidOperationException>(() => RmdPolicyLoader.Save(path, policy));
            Assert.Equal(original, File.ReadAllText(path));
        }

        [Fact]
        public void CreateYearFile_WritesLockedName_AndBlocksDuplicates()
        {
            using var folder = new TempRmdFolder();
            var path = folder.CopyFromExisting(2025);
            var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            node["FutureField"] = "keep-me";
            File.WriteAllText(path, node.ToJsonString());

            var source = RmdPolicyDocument.Load(path);
            var policy = source.ToPolicy();
            policy.RmdStartAge = 73;

            var created = RmdPolicyLoader.CreateYearFile(folder.Path, 2026, source, policy);

            Assert.Equal(Path.Combine(folder.Path, "RMD2026.json"), created);
            Assert.True(File.Exists(created));

            var loaded = RmdPolicyLoader.LoadFromPath(created);
            Assert.Equal(73, loaded.RmdStartAge);
            Assert.Equal(24.6m, loaded.LifeExpectancyDivisors[75]);

            var createdNode = JsonNode.Parse(File.ReadAllText(created))!.AsObject();
            Assert.Equal("keep-me", createdNode["FutureField"]!.GetValue<string>());

            var original = RmdPolicyLoader.LoadFromPath(path);
            Assert.Equal(75, original.RmdStartAge);

            var duplicate = Assert.Throws<InvalidOperationException>(() =>
                RmdPolicyLoader.CreateYearFile(folder.Path, 2026, source, policy));
            Assert.Contains("already exists", duplicate.Message, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class TempRmdFolder : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ExitPlanRmdSettings",
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

            public void Dispose()
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
        }
    }
}
