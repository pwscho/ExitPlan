using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ExitPlan.Core.Taxes
{
    public static class TaxPolicyLoader
    {
        private const string TaxDataFolderName = "TaxData";

        public static TaxPolicy LoadForYear(int year)
        {
            string folder = ResolveTaxDataFolder();

            string preferredFile = year <= 2025
                ? Path.Combine(folder, "Tax2025.json")
                : Path.Combine(folder, $"Tax{year}.json");

            if (File.Exists(preferredFile))
                return LoadFromFile(preferredFile);

            if (year >= 2026)
            {
                string tax2026 = Path.Combine(folder, "Tax2026.json");
                if (File.Exists(tax2026))
                    return LoadFromFile(tax2026);
            }

            var availableYears = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "Tax*.json")
                    .Select(ExtractYearFromFilename)
                    .Where(y => y > 0 && y <= year)
                    .OrderByDescending(y => y)
                    .ToList()
                : new List<int>();

            if (availableYears.Count == 0)
                throw new Exception($"No tax policy files found for year {year} or earlier.");

            int fallbackYear = availableYears.First();
            return LoadFromFile(Path.Combine(folder, $"Tax{fallbackYear}.json"));
        }

        public static TaxPolicy LoadForYear(int year, decimal inflationMultiplier)
        {
            var policy = LoadForYear(year);

            InflateFilingStatus(policy.Single, inflationMultiplier);
            InflateFilingStatus(policy.MarriedFilingJointly, inflationMultiplier);

            // IRC §86 combined-income thresholds are fixed by statute
            // ($25,000 / $34,000 single; $32,000 / $44,000 MFJ) and are not
            // inflation-indexed. The amounts in Tax*.json stay at those
            // nominal dollars every year.
            return policy;
        }

        public static string GetTaxDataFolder() => ResolveTaxDataFolder();

        public static IReadOnlyList<TaxPolicyFileInfo> ListAvailableFiles()
            => ListAvailableFiles(ResolveTaxDataFolder());

        public static IReadOnlyList<TaxPolicyFileInfo> ListAvailableFiles(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return Array.Empty<TaxPolicyFileInfo>();

            return Directory.GetFiles(folder, "Tax*.json")
                .Select(path => new TaxPolicyFileInfo(
                    ExtractYearFromFilename(path),
                    Path.GetFileName(path) ?? string.Empty,
                    Path.GetFullPath(path)))
                .Where(file => file.Year > 0)
                .OrderByDescending(file => file.Year)
                .ToList();
        }

        /// <summary>
        /// Writes the current policy as Tax{year}.json next to the source document.
        /// Sets the JSON Year field to <paramref name="newYear"/>. Existing files are never overwritten.
        /// </summary>
        public static TaxPolicyFileInfo SaveAsNewYear(TaxPolicyDocument source, TaxPolicy policy, int newYear)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(policy);

            if (!TaxYearNaming.TryValidateYear(newYear, out var yearError))
                throw new InvalidOperationException(yearError);

            policy.Year = newYear;
            var errors = TaxPolicyValidator.Validate(policy);
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

            var folder = Path.GetDirectoryName(source.FilePath);
            if (string.IsNullOrWhiteSpace(folder))
                folder = ResolveTaxDataFolder();

            var fileName = TaxYearNaming.FileNameForYear(newYear);
            var destPath = Path.Combine(folder, fileName);
            if (File.Exists(destPath))
            {
                throw new InvalidOperationException(
                    $"{fileName} already exists. Choose a different year — existing tax files are not overwritten.");
            }

            source.Apply(policy);
            source.SaveTo(destPath);

            return new TaxPolicyFileInfo(newYear, fileName, Path.GetFullPath(destPath));
        }

        public static TaxPolicy LoadFromPath(string path) => LoadFromFile(path);

        public static void Save(string path, TaxPolicy policy)
        {
            var errors = TaxPolicyValidator.Validate(policy);
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

            TaxPolicyDocument.Save(path, policy);
        }

        private static TaxPolicy LoadFromFile(string path)
        {
            string json = File.ReadAllText(path);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                IncludeFields = true
            };

            var policy = JsonSerializer.Deserialize<TaxPolicy>(json, options);

            if (policy == null)
                throw new Exception($"Failed to deserialize tax policy file: {path}");

            return policy;
        }

        private static int ExtractYearFromFilename(string filePath)
        {
            string file = Path.GetFileNameWithoutExtension(filePath);
            string yearPart = file.Length > 3 ? file.Substring(3) : string.Empty;
            return int.TryParse(yearPart, out int year) ? year : -1;
        }

        private static string ResolveTaxDataFolder()
        {
            if (!string.IsNullOrWhiteSpace(AppDataPaths.OverrideRoot))
                return AppDataPaths.TaxDataDirectory;

            string[] candidates =
            {
                Path.Combine(AppContext.BaseDirectory, TaxDataFolderName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TaxDataFolderName),
                Path.Combine(Directory.GetCurrentDirectory(), TaxDataFolderName)
            };

            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate) && Directory.GetFiles(candidate, "Tax*.json").Length > 0)
                    return candidate;
            }

            return candidates[0];
        }

        private static void InflateFilingStatus(FilingStatusTaxTable table, decimal multiplier)
        {
            table.StandardDeduction *= multiplier;
            table.AdditionalStandardDeduction *= multiplier;

            foreach (var bracket in table.OrdinaryBrackets)
                bracket.Threshold *= multiplier;

            foreach (var bracket in table.CapitalGainsBrackets)
                bracket.Threshold *= multiplier;

            foreach (var bracket in table.IrmaaBrackets)
            {
                bracket.Threshold *= multiplier;
                bracket.MonthlySurcharge *= multiplier;
            }
        }
    }
}
