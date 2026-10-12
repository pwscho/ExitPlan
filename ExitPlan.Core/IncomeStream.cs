using System;

namespace ExitPlan.Core
{
    public class IncomeStream
    {
        public Guid OwnerId { get; set; }   // required for persistence

        public string Name { get; set; } = string.Empty;

        public int StartAge { get; set; }

        public int EndAge { get; set; }

        public decimal AnnualAmount { get; set; }

        public bool IsTaxable { get; set; }
    }
}

