using System.Collections.Generic;

namespace ExitPlan.Core
{
    public class RmdPolicy
    {
        // Fallback when no StartAgeBands entry matches the owner's implied birth year.
        public int RmdStartAge { get; set; }

        // Closed birth-year bands. First matching band wins. Bands must not overlap.
        public List<RmdStartAgeBand> StartAgeBands { get; set; } = new();

        // Uniform lifetime table: Age -> Divisor
        public Dictionary<int, decimal> LifeExpectancyDivisors { get; set; } = new();

        public static int ImpliedBirthYear(int baseYear, int currentAge) => baseYear - currentAge;

        public int ResolveStartAge(int birthYear)
        {
            if (StartAgeBands != null)
            {
                foreach (var band in StartAgeBands)
                {
                    if (band == null)
                        continue;

                    if (birthYear >= band.BornFrom &&
                        (!band.BornThrough.HasValue || birthYear <= band.BornThrough.Value))
                    {
                        return band.StartAge;
                    }
                }
            }

            return RmdStartAge;
        }
    }
}
