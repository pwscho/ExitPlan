using System.Collections.Generic;
using System.Linq;

namespace ExitPlan.Core.MarketData
{
    public static class MarketHistoryValidator
    {
        public static IReadOnlyList<string> Validate(MarketHistory history)
        {
            var errors = new List<string>();
            if (history == null)
            {
                errors.Add("Market data is missing.");
                return errors;
            }

            if (history.Years == null || history.Years.Count == 0)
            {
                errors.Add("Add at least one market year.");
                return errors;
            }

            var seen = new HashSet<int>();
            for (int i = 0; i < history.Years.Count; i++)
            {
                var row = history.Years[i];
                int display = i + 1;

                if (row.Year < 1800 || row.Year > 3000)
                    errors.Add($"Row {display}: year must be a valid calendar year.");

                if (!seen.Add(row.Year))
                    errors.Add($"Year {row.Year} is listed more than once.");
            }

            return errors;
        }
    }
}
