using System.Text.Json.Nodes;
using ExitPlan.Core.Taxes;

namespace ExitPlan.Core.Tests
{
    public class TaxPolicySettingsTests
    {
        [Fact]
        public void ListAvailableFiles_IncludesExistingTaxYearFiles_HighestYearFirst()
        {
            var files = TaxPolicyLoader.ListAvailableFiles();

            Assert.Contains(files, f => f.Year == 2025 && f.FileName == "Tax2025.json");
            Assert.Contains(files, f => f.Year == 2026 && f.FileName == "Tax2026.json");
            Assert.True(files.Count >= 2);
            Assert.Equal(files.Max(f => f.Year), files[0].Year);
            Assert.All(files, f => Assert.True(File.Exists(f.FullPath)));
        }

        [Fact]
        public void LoadForYear_StillSelectsExactOrLatestPriorFile()
        {
            var tax2026 = TaxPolicyLoader.LoadForYear(2026);
            var tax2025 = TaxPolicyLoader.LoadForYear(2025);
            var tax2024 = TaxPolicyLoader.LoadForYear(2024);

            Assert.Equal(2026, tax2026.Year);
            Assert.Equal(16_100m, tax2026.Single.StandardDeduction);
            Assert.Equal(2025, tax2025.Year);
            Assert.Equal(tax2025.Year, tax2024.Year);
        }

        [Fact]
        public void SaveAndReload_RoundTripsKnownFields()
        {
            using var folder = new TempTaxFolder();
            var path = folder.CopyFromExisting(2026);

            var document = TaxPolicyDocument.Load(path);
            var policy = document.ToPolicy();
            policy.Single.StandardDeduction = 17_250m;
            policy.Single.AdditionalStandardDeduction = 2_200m;
            policy.MarriedFilingJointly.AdditionalStandardDeduction = 1_800m;
            policy.AdditionalStandardDeductionAge = 66;
            policy.Single.OrdinaryBrackets[0].Rate = 0.11m;
            policy.MarriedSsSecondThreshold = 45_000m;
            document.Apply(policy);
            document.Save();

            var reloaded = TaxPolicyLoader.LoadFromPath(path);
            Assert.Equal(2026, reloaded.Year);
            Assert.Equal(17_250m, reloaded.Single.StandardDeduction);
            Assert.Equal(2_200m, reloaded.Single.AdditionalStandardDeduction);
            Assert.Equal(1_800m, reloaded.MarriedFilingJointly.AdditionalStandardDeduction);
            Assert.Equal(66, reloaded.AdditionalStandardDeductionAge);
            Assert.Equal(0.11m, reloaded.Single.OrdinaryBrackets[0].Rate);
            Assert.Equal(45_000m, reloaded.MarriedSsSecondThreshold);
            Assert.Equal(policy.Single.OrdinaryBrackets.Count, reloaded.Single.OrdinaryBrackets.Count);
            Assert.Equal(policy.Single.IrmaaBrackets[1].MonthlySurcharge, reloaded.Single.IrmaaBrackets[1].MonthlySurcharge);
        }

        [Fact]
        public void Save_PreservesUnknownJsonProperties()
        {
            using var folder = new TempTaxFolder();
            var path = folder.CopyFromExisting(2026);

            var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            node["FutureField"] = "keep-me";
            node["Single"]!["ExtraNote"] = 42;
            node["Single"]!["OrdinaryBrackets"]![0]!["Note"] = "cliff";
            File.WriteAllText(path, node.ToJsonString());

            var document = TaxPolicyDocument.Load(path);
            var policy = document.ToPolicy();
            policy.Single.StandardDeduction = 17_000m;
            document.Apply(policy);
            document.Save();

            var reloaded = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.Equal("keep-me", reloaded["FutureField"]!.GetValue<string>());
            Assert.Equal(42, reloaded["Single"]!["ExtraNote"]!.GetValue<int>());
            Assert.Equal("cliff", reloaded["Single"]!["OrdinaryBrackets"]![0]!["Note"]!.GetValue<string>());
            Assert.Equal(17_000m, reloaded["Single"]!["StandardDeduction"]!.GetValue<decimal>());
        }

        [Fact]
        public void Validator_RejectsNegativeAndEmptyNonsense()
        {
            var policy = TaxPolicyLoader.LoadForYear(2026);
            policy.Single.StandardDeduction = -1m;
            policy.Single.AdditionalStandardDeduction = -50m;
            policy.AdditionalStandardDeductionAge = -1;
            policy.Single.OrdinaryBrackets.Clear();
            policy.MarriedFilingJointly.CapitalGainsBrackets[0].Rate = 1.5m;
            policy.MarriedFilingJointly.IrmaaBrackets[0].MonthlySurcharge = -5m;
            policy.SingleSsBaseThreshold = -10m;

            var errors = TaxPolicyValidator.Validate(policy);

            Assert.Contains(errors, e => e.Contains("standard deduction", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(errors, e => e.Contains("additional standard deduction", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(errors, e => e.Contains("Additional standard deduction age", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(errors, e => e.Contains("ordinary income", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(errors, e => e.Contains("rate must be between", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(errors, e => e.Contains("monthly surcharge", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(errors, e => e.Contains("Social Security first threshold", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void LoaderSave_RejectsInvalidPolicy_AndLeavesFileUnchanged()
        {
            using var folder = new TempTaxFolder();
            var path = folder.CopyFromExisting(2025);
            var original = File.ReadAllText(path);

            var policy = TaxPolicyLoader.LoadFromPath(path);
            policy.Single.StandardDeduction = -100m;

            Assert.Throws<InvalidOperationException>(() => TaxPolicyLoader.Save(path, policy));
            Assert.Equal(original, File.ReadAllText(path));
        }

        [Theory]
        [InlineData(2000, "Tax2000.json")]
        [InlineData(2027, "Tax2027.json")]
        [InlineData(2100, "Tax2100.json")]
        public void TaxYearNaming_UsesExactTaxYearFileName(int year, string expected)
        {
            Assert.Equal(expected, TaxYearNaming.FileNameForYear(year));
            Assert.True(TaxYearNaming.TryValidateYear(year, out var error));
            Assert.Equal(string.Empty, error);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("27")]
        [InlineData("202")]
        [InlineData("20270")]
        [InlineData("abcd")]
        [InlineData("20.7")]
        [InlineData("+2027")]
        public void TaxYearNaming_RejectsNonFourDigitYearText(string? text)
        {
            Assert.False(TaxYearNaming.TryParseYear(text, out var year, out var error));
            Assert.Equal(0, year);
            Assert.Contains("4-digit", error, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(1999)]
        [InlineData(2101)]
        [InlineData(0)]
        public void TaxYearNaming_RejectsYearOutside2000To2100(int year)
        {
            Assert.False(TaxYearNaming.TryValidateYear(year, out var error));
            Assert.Contains("2000", error);
            Assert.Contains("2100", error);
        }

        [Fact]
        public void TaxYearNaming_ParsesTrimmedFourDigitYear()
        {
            Assert.True(TaxYearNaming.TryParseYear(" 2027 ", out var year, out var error));
            Assert.Equal(2027, year);
            Assert.Equal(string.Empty, error);
        }

        [Fact]
        public void SaveAsNewYear_WritesTaxYearFile_SetsYearField_AndRoundTrips()
        {
            using var folder = new TempTaxFolder();
            var sourcePath = folder.CopyFromExisting(2026);
            var originalSource = File.ReadAllText(sourcePath);

            var document = TaxPolicyDocument.Load(sourcePath);
            var policy = document.ToPolicy();
            policy.Single.StandardDeduction = 18_250m;
            policy.MarriedSsSecondThreshold = 46_000m;

            var saved = TaxPolicyLoader.SaveAsNewYear(document, policy, 2027);

            Assert.Equal(2027, saved.Year);
            Assert.Equal("Tax2027.json", saved.FileName);
            Assert.Equal("Tax2027.json", Path.GetFileName(saved.FullPath));
            Assert.Equal(Path.GetFullPath(folder.Path), Path.GetDirectoryName(saved.FullPath));
            Assert.True(File.Exists(saved.FullPath));
            Assert.Equal(originalSource, File.ReadAllText(sourcePath));

            var reloaded = TaxPolicyLoader.LoadFromPath(saved.FullPath);
            Assert.Equal(2027, reloaded.Year);
            Assert.Equal(18_250m, reloaded.Single.StandardDeduction);
            Assert.Equal(46_000m, reloaded.MarriedSsSecondThreshold);
            Assert.Equal(policy.Single.OrdinaryBrackets.Count, reloaded.Single.OrdinaryBrackets.Count);
            Assert.Equal(policy.Single.IrmaaBrackets[1].MonthlySurcharge, reloaded.Single.IrmaaBrackets[1].MonthlySurcharge);
        }

        [Fact]
        public void SaveAsNewYear_RejectsDuplicateYear_AndLeavesExistingFileUnchanged()
        {
            using var folder = new TempTaxFolder();
            var sourcePath = folder.CopyFromExisting(2026);
            var document = TaxPolicyDocument.Load(sourcePath);
            var first = TaxPolicyLoader.SaveAsNewYear(document, document.ToPolicy(), 2027);
            var existing = File.ReadAllText(first.FullPath);

            var again = TaxPolicyDocument.Load(sourcePath);
            var changed = again.ToPolicy();
            changed.Single.StandardDeduction = 1m;

            var ex = Assert.Throws<InvalidOperationException>(() =>
                TaxPolicyLoader.SaveAsNewYear(again, changed, 2027));

            Assert.Contains("Tax2027.json", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not overwritten", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(existing, File.ReadAllText(first.FullPath));
        }

        [Fact]
        public void SaveAsNewYear_RejectsInvalidYear_AndDoesNotCreateFile()
        {
            using var folder = new TempTaxFolder();
            var sourcePath = folder.CopyFromExisting(2026);
            var document = TaxPolicyDocument.Load(sourcePath);

            Assert.Throws<InvalidOperationException>(() =>
                TaxPolicyLoader.SaveAsNewYear(document, document.ToPolicy(), 1999));
            Assert.False(File.Exists(Path.Combine(folder.Path, "Tax1999.json")));

            Assert.Throws<InvalidOperationException>(() =>
                TaxPolicyLoader.SaveAsNewYear(document, document.ToPolicy(), 2101));
            Assert.False(File.Exists(Path.Combine(folder.Path, "Tax2101.json")));
        }

        [Fact]
        public void SaveAsNewYear_RejectsInvalidPolicy_AndDoesNotCreateFile()
        {
            using var folder = new TempTaxFolder();
            var sourcePath = folder.CopyFromExisting(2026);
            var document = TaxPolicyDocument.Load(sourcePath);
            var policy = document.ToPolicy();
            policy.Single.StandardDeduction = -100m;

            Assert.Throws<InvalidOperationException>(() =>
                TaxPolicyLoader.SaveAsNewYear(document, policy, 2028));
            Assert.False(File.Exists(Path.Combine(folder.Path, "Tax2028.json")));
        }

        [Fact]
        public void SaveAsNewYear_PreservesUnknownJsonProperties()
        {
            using var folder = new TempTaxFolder();
            var sourcePath = folder.CopyFromExisting(2026);

            var node = JsonNode.Parse(File.ReadAllText(sourcePath))!.AsObject();
            node["FutureField"] = "keep-me";
            node["Single"]!["ExtraNote"] = 42;
            File.WriteAllText(sourcePath, node.ToJsonString());

            var document = TaxPolicyDocument.Load(sourcePath);
            var saved = TaxPolicyLoader.SaveAsNewYear(document, document.ToPolicy(), 2029);

            var reloaded = JsonNode.Parse(File.ReadAllText(saved.FullPath))!.AsObject();
            Assert.Equal(2029, reloaded["Year"]!.GetValue<int>());
            Assert.Equal("keep-me", reloaded["FutureField"]!.GetValue<string>());
            Assert.Equal(42, reloaded["Single"]!["ExtraNote"]!.GetValue<int>());
        }

        [Fact]
        public void ListAvailableFiles_PicksUpNewlySavedTaxYearFile()
        {
            using var folder = new TempTaxFolder();
            folder.CopyFromExisting(2026);
            var document = TaxPolicyDocument.Load(Path.Combine(folder.Path, "Tax2026.json"));

            TaxPolicyLoader.SaveAsNewYear(document, document.ToPolicy(), 2027);

            var files = TaxPolicyLoader.ListAvailableFiles(folder.Path);
            Assert.Contains(files, f => f.Year == 2026 && f.FileName == "Tax2026.json");
            Assert.Contains(files, f => f.Year == 2027 && f.FileName == "Tax2027.json");
            Assert.Equal(2027, files[0].Year);
        }

        private sealed class TempTaxFolder : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ExitPlanTaxSettings",
                Guid.NewGuid().ToString("N"));

            public TempTaxFolder()
            {
                Directory.CreateDirectory(Path);
            }

            public string CopyFromExisting(int year)
            {
                var source = TaxPolicyLoader.ListAvailableFiles().Single(f => f.Year == year).FullPath;
                var dest = System.IO.Path.Combine(Path, $"Tax{year}.json");
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
