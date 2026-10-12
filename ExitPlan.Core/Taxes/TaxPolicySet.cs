using System.Collections.Generic;

namespace ExitPlan.Core.Taxes
{
    public sealed class TaxPolicySet
    {
        public Dictionary<int, TaxPolicy> PoliciesByYear { get; set; } = new();
    }
}

