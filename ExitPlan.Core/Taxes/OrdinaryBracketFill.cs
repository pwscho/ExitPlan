using System;
using System.Collections.Generic;
using System.Linq;

namespace ExitPlan.Core.Taxes
{
    /// <summary>
    /// How much of one ordinary-income bracket was filled by taxable ordinary income.
    /// </summary>
    public sealed class OrdinaryBracketFill
    {
        public decimal Rate { get; init; }
        public decimal Filled { get; init; }
        public decimal Capacity { get; init; }
        public decimal UpperThreshold { get; init; }

        public string RateLabel => $"{Rate * 100m:0}%";
    }

    public sealed class OrdinaryBracketAllocation
    {
        public decimal OrdinaryTax { get; init; }
        public decimal Headroom { get; init; }
        public IReadOnlyList<OrdinaryBracketFill> Fills { get; init; } = Array.Empty<OrdinaryBracketFill>();
    }

    /// <summary>
    /// Splits taxable ordinary income across ordinary brackets the same way
    /// <see cref="ConfigIncomeTaxStrategy"/> taxes it, and reports remaining
    /// room before the next rate (conversion headroom). The top listed rate
    /// has no next cliff, so headroom is zero there.
    /// </summary>
    public static class OrdinaryBracketAllocator
    {
        public static OrdinaryBracketAllocation Allocate(
            decimal ordinaryTaxable,
            IEnumerable<TaxBracket>? brackets)
        {
            var ordered = (brackets ?? Enumerable.Empty<TaxBracket>())
                .OrderBy(b => b.Threshold)
                .ToList();

            decimal amount = ordinaryTaxable < 0m ? 0m : ordinaryTaxable;
            decimal remaining = amount;
            decimal lower = 0m;
            decimal tax = 0m;
            var fills = new List<OrdinaryBracketFill>(ordered.Count);

            foreach (var bracket in ordered)
            {
                decimal capacity = bracket.Threshold - lower;
                if (capacity < 0m)
                    capacity = 0m;

                decimal filled = remaining <= 0m ? 0m : Math.Min(remaining, capacity);
                tax += filled * bracket.Rate;
                remaining -= filled;

                fills.Add(new OrdinaryBracketFill
                {
                    Rate = bracket.Rate,
                    Filled = filled,
                    Capacity = capacity,
                    UpperThreshold = bracket.Threshold
                });

                lower = bracket.Threshold;
            }

            if (remaining > 0m && fills.Count > 0)
            {
                var last = fills[^1];
                tax += remaining * last.Rate;
                fills[^1] = new OrdinaryBracketFill
                {
                    Rate = last.Rate,
                    Filled = last.Filled + remaining,
                    Capacity = last.Capacity,
                    UpperThreshold = last.UpperThreshold
                };
            }

            return new OrdinaryBracketAllocation
            {
                OrdinaryTax = tax,
                Headroom = ComputeHeadroom(fills),
                Fills = fills
            };
        }

        private static decimal ComputeHeadroom(IReadOnlyList<OrdinaryBracketFill> fills)
        {
            if (fills.Count == 0)
                return 0m;

            int current = -1;
            for (int i = 0; i < fills.Count; i++)
            {
                if (fills[i].Filled > 0m)
                    current = i;
            }

            // Zero ordinary income: room is the width of the first (lowest) rate.
            if (current < 0)
                current = 0;

            // Last listed rate has no next ordinary cliff.
            if (current == fills.Count - 1)
                return 0m;

            var fill = fills[current];
            decimal room = fill.Capacity - fill.Filled;
            return room < 0m ? 0m : room;
        }
    }
}
