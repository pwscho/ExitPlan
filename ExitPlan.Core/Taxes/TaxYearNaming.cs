using System.Globalization;

namespace ExitPlan.Core.Taxes
{
    /// <summary>
    /// Hardwired Tax{YYYY}.json naming used by Save As. Callers never choose a path or extension.
    /// </summary>
    public static class TaxYearNaming
    {
        public const int MinYear = 2000;
        public const int MaxYear = 2100;

        public static string FileNameForYear(int year) => $"Tax{year}.json";

        public static string GetPath(string folder, int year) =>
            Path.Combine(folder, FileNameForYear(year));

        public static bool TryValidateYear(int year, out string error)
        {
            if (year < MinYear || year > MaxYear)
            {
                error = $"Year must be a 4-digit year between {MinYear} and {MaxYear}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static bool TryParseYear(string? text, out int year, out string error)
        {
            year = 0;
            var trimmed = text?.Trim() ?? string.Empty;
            if (trimmed.Length != 4 ||
                !int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out year))
            {
                year = 0;
                error = "Enter a 4-digit year.";
                return false;
            }

            return TryValidateYear(year, out error);
        }
    }
}
