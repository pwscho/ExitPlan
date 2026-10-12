using System.Collections.Generic;

namespace ExitPlan.Core.Taxes
{
    public sealed class FilingStatusTaxTable
    {
        public decimal StandardDeduction { get; set; }

        /// <summary>
        /// IRC §63(f) extra standard deduction for this filing status.
        /// Single: amount if the living filer is at least AdditionalStandardDeductionAge.
        /// MarriedFilingJointly: amount per living spouse who meets that age.
        /// Missing or 0 leaves the base standard deduction unchanged (old files).
        /// </summary>
        public decimal AdditionalStandardDeduction { get; set; }

        public List<TaxBracket> OrdinaryBrackets { get; set; } = new();
        public List<TaxBracket> CapitalGainsBrackets { get; set; } = new();

        // NEW: IRMAA surcharge brackets (MAGI-based)
        public List<IrmaaBracket> IrmaaBrackets { get; set; } = new();
    }

    // NEW: IRMAA bracket definition
    public sealed class IrmaaBracket
    {
        public decimal Threshold { get; set; }
        public decimal MonthlySurcharge { get; set; }
    }
}
