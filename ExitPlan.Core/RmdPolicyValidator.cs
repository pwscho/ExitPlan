using System.Collections.Generic;
using System.Linq;

namespace ExitPlan.Core
{
    public static class RmdPolicyValidator
    {
        public static IReadOnlyList<string> Validate(RmdPolicy policy)
        {
            if (policy == null)
                return new[] { "RMD policy is missing." };

            var rows = (policy.LifeExpectancyDivisors ?? new Dictionary<int, decimal>())
                .Select(pair => (pair.Key, pair.Value))
                .ToList();

            return Validate(policy.RmdStartAge, rows, policy.StartAgeBands);
        }

        public static IReadOnlyList<string> Validate(
            int rmdStartAge,
            IReadOnlyList<(int Age, decimal Period)> rows,
            IReadOnlyList<RmdStartAgeBand>? startAgeBands = null)
        {
            var errors = new List<string>();

            if (rmdStartAge <= 0 || rmdStartAge > 120)
                errors.Add("RMD start age must be between 1 and 120.");

            errors.AddRange(ValidateBands(startAgeBands));

            if (rows == null || rows.Count == 0)
            {
                errors.Add("Add at least one age / distribution period row.");
                return errors;
            }

            var seen = new HashSet<int>();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int display = i + 1;

                if (row.Age < 0 || row.Age > 120)
                    errors.Add($"Row {display}: age must be between 0 and 120.");

                if (!seen.Add(row.Age))
                    errors.Add($"Age {row.Age} is listed more than once.");

                if (row.Period <= 0m)
                    errors.Add($"Row {display}: distribution period must be greater than 0.");
            }

            return errors;
        }

        private static IReadOnlyList<string> ValidateBands(IReadOnlyList<RmdStartAgeBand>? bands)
        {
            var errors = new List<string>();
            if (bands == null || bands.Count == 0)
                return errors;

            for (int i = 0; i < bands.Count; i++)
            {
                var band = bands[i];
                int display = i + 1;

                if (band == null)
                {
                    errors.Add($"Start-age band {display} is missing.");
                    continue;
                }

                if (band.BornThrough.HasValue && band.BornFrom > band.BornThrough.Value)
                    errors.Add($"Start-age band {display}: BornFrom must be less than or equal to BornThrough.");

                if (!band.BornThrough.HasValue && i != bands.Count - 1)
                    errors.Add("Only the last start-age band may leave BornThrough blank.");

                if (band.StartAge <= 0 || band.StartAge > 120)
                    errors.Add($"Start-age band {display}: start age must be between 1 and 120.");
            }

            for (int i = 0; i < bands.Count; i++)
            {
                var left = bands[i];
                if (left == null)
                    continue;

                int leftEnd = left.BornThrough ?? int.MaxValue;
                for (int j = i + 1; j < bands.Count; j++)
                {
                    var right = bands[j];
                    if (right == null)
                        continue;

                    int rightEnd = right.BornThrough ?? int.MaxValue;
                    if (left.BornFrom <= rightEnd && right.BornFrom <= leftEnd)
                        errors.Add($"Start-age bands {i + 1} and {j + 1} overlap.");
                }
            }

            return errors;
        }
    }
}
