using System.Collections.Generic;

namespace ExitPlan.Core.Taxes
{
    public sealed class TaxPolicy
    {
        public int Year { get; set; }

        // Filing status tables now include IRMAA brackets (added in FilingStatusTaxTable)
        public FilingStatusTaxTable Single { get; set; } = new();
        public FilingStatusTaxTable MarriedFilingJointly { get; set; } = new();

        /// <summary>
        /// Minimum integer age for the IRC §63(f) additional standard deduction.
        /// Shared by both filing statuses. Missing from JSON defaults to 65;
        /// extra dollars still default to 0 so older files are unchanged.
        /// </summary>
        public int AdditionalStandardDeductionAge { get; set; } = 65;

        // Social Security combined-income thresholds (IRC §86). Fixed by statute
        // and not inflation-indexed, unlike brackets, the standard deduction, and IRMAA.
        public decimal SingleSsBaseThreshold { get; set; }
        public decimal SingleSsSecondThreshold { get; set; }

        public decimal MarriedSsBaseThreshold { get; set; }
        public decimal MarriedSsSecondThreshold { get; set; }

        public decimal AdditionalStandardDeductionFor(
            FilingStatus filingStatus,
            IReadOnlyList<int>? livingOwnerAges)
        {
            var table = TableFor(filingStatus);
            decimal amount = table.AdditionalStandardDeduction;
            if (amount == 0m || livingOwnerAges == null || livingOwnerAges.Count == 0)
                return 0m;

            int qualifying = 0;
            for (int i = 0; i < livingOwnerAges.Count; i++)
            {
                if (livingOwnerAges[i] >= AdditionalStandardDeductionAge)
                    qualifying++;
            }

            if (qualifying == 0)
                return 0m;

            // MFJ: per qualifying spouse. Single: the unmarried amount once.
            return filingStatus == FilingStatus.MarriedFilingJointly
                ? amount * qualifying
                : amount;
        }

        public decimal EffectiveStandardDeduction(
            FilingStatus filingStatus,
            IReadOnlyList<int>? livingOwnerAges)
        {
            return TableFor(filingStatus).StandardDeduction
                   + AdditionalStandardDeductionFor(filingStatus, livingOwnerAges);
        }

        private FilingStatusTaxTable TableFor(FilingStatus filingStatus) =>
            filingStatus == FilingStatus.Single ? Single : MarriedFilingJointly;
    }
}
