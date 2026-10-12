using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExitPlan.Core
{
    public static class RmdPolicyLoader
    {
        public static string GetRmdDataFolder() => AppDataPaths.RmdDataDirectory;

        public static string FileNameForYear(int year) => $"RMD{year}.json";

        public static IReadOnlyList<RmdPolicyFileInfo> ListAvailableFiles()
        {
            string folder = GetRmdDataFolder();
            if (!Directory.Exists(folder))
                return Array.Empty<RmdPolicyFileInfo>();

            return Directory.GetFiles(folder, "RMD*.json")
                .Select(path => new RmdPolicyFileInfo(
                    ExtractYearFromFilename(path),
                    Path.GetFileName(path) ?? string.Empty,
                    Path.GetFullPath(path)))
                .Where(file => file.Year > 0)
                .OrderByDescending(file => file.Year)
                .ToList();
        }

        public static RmdPolicy LoadFromPath(string path)
        {
            return RmdPolicyDocument.Load(path).ToPolicy();
        }

        public static void Save(string path, RmdPolicy policy)
        {
            var errors = RmdPolicyValidator.Validate(policy);
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

            RmdPolicyDocument.Save(path, policy);
        }

        public static string CreateYearFile(string folder, int year, RmdPolicyDocument source, RmdPolicy policy)
        {
            if (year < 1900 || year > 3000)
                throw new InvalidOperationException("Year must be a valid calendar year.");

            var errors = RmdPolicyValidator.Validate(policy);
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

            if (string.IsNullOrWhiteSpace(folder))
                throw new ArgumentException("Folder is required.", nameof(folder));

            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, FileNameForYear(year));
            if (File.Exists(path))
                throw new InvalidOperationException(
                    $"{FileNameForYear(year)} already exists. Duplicate years are not allowed.");

            var copy = source.CloneTo(path);
            copy.Apply(policy);
            copy.Save();
            return path;
        }

        public static RmdPolicy LoadForYear(int year)
        {
            string folderPath = GetRmdDataFolder();

            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException(
                    $"RMD policy folder not found: {folderPath}");

            // Look for files like RMD2025.json, RMD2026.json, etc.
            var files = Directory.GetFiles(folderPath, "RMD*.json");

            if (files.Length == 0)
                throw new FileNotFoundException(
                    $"No RMD policy files found in {folderPath}");

            // Extract available years
            var availableYears = files
                .Select(ExtractYearFromFilename)
                .Where(y => y > 0)
                .OrderBy(y => y)
                .ToList();

            if (availableYears.Count == 0)
                throw new Exception("No valid RMD policy years found.");

            // Find the best match: exact year or closest prior year
            int selectedYear = availableYears
                .Where(y => y <= year)
                .DefaultIfEmpty(availableYears.Max())
                .Max();

            string selectedFile = Path.Combine(folderPath, $"RMD{selectedYear}.json");

            if (!File.Exists(selectedFile))
                throw new FileNotFoundException(
                    $"RMD policy file not found: {selectedFile}");

            return LoadFromPath(selectedFile);
        }

        private static int ExtractYearFromFilename(string filePath)
        {
            string name = Path.GetFileNameWithoutExtension(filePath); // RMD2025
            string yearPart = name.Length > 3 ? name.Substring(3) : string.Empty;
            return int.TryParse(yearPart, out int year) ? year : -1;
        }
    }
}

