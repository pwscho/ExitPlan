using System.Collections.Generic;

namespace ExitPlan.Core.Taxes
{
    public static class TaxPolicyValidator
    {
        public static IReadOnlyList<string> Validate(TaxPolicy policy)
        {
            var errors = new List<string>();
            if (policy == null)
            {
                errors.Add("Tax policy is missing.");
                return errors;
            }

            if (policy.Year < 1900 || policy.Year > 3000)
                errors.Add("Policy year must be a valid calendar year.");

            if (policy.AdditionalStandardDeductionAge < 0)
                errors.Add("Additional standard deduction age cannot be negative.");

            ValidateNonNegative(errors, "Single Social Security first threshold", policy.SingleSsBaseThreshold);
            ValidateNonNegative(errors, "Single Social Security second threshold", policy.SingleSsSecondThreshold);
            ValidateNonNegative(errors, "Married Social Security first threshold", policy.MarriedSsBaseThreshold);
            ValidateNonNegative(errors, "Married Social Security second threshold", policy.MarriedSsSecondThreshold);

            ValidateTable(errors, "Single", policy.Single);
            ValidateTable(errors, "Married filing jointly", policy.MarriedFilingJointly);

            return errors;
        }

        private static void ValidateTable(List<string> errors, string label, FilingStatusTaxTable? table)
        {
            if (table == null)
            {
                errors.Add($"{label}: filing status table is missing.");
                return;
            }

            ValidateNonNegative(errors, $"{label} standard deduction", table.StandardDeduction);
            ValidateNonNegative(errors, $"{label} additional standard deduction", table.AdditionalStandardDeduction);

            ValidateBrackets(errors, $"{label} ordinary income", table.OrdinaryBrackets, requireAtLeastOne: true);
            ValidateBrackets(errors, $"{label} capital gains", table.CapitalGainsBrackets, requireAtLeastOne: true);
            ValidateIrmaa(errors, label, table.IrmaaBrackets);
        }

        private static void ValidateBrackets(
            List<string> errors,
            string label,
            List<TaxBracket>? brackets,
            bool requireAtLeastOne)
        {
            if (brackets == null || brackets.Count == 0)
            {
                if (requireAtLeastOne)
                    errors.Add($"{label}: add at least one bracket.");
                return;
            }

            for (int i = 0; i < brackets.Count; i++)
            {
                var bracket = brackets[i];
                ValidateNonNegative(errors, $"{label} bracket {i + 1} income threshold", bracket.Threshold);

                if (bracket.Rate < 0m || bracket.Rate > 1m)
                    errors.Add($"{label} bracket {i + 1}: rate must be between 0% and 100%.");
            }
        }

        private static void ValidateIrmaa(List<string> errors, string label, List<IrmaaBracket>? brackets)
        {
            if (brackets == null || brackets.Count == 0)
            {
                errors.Add($"{label} IRMAA: add at least one MAGI cliff.");
                return;
            }

            for (int i = 0; i < brackets.Count; i++)
            {
                var bracket = brackets[i];
                ValidateNonNegative(errors, $"{label} IRMAA cliff {i + 1} MAGI threshold", bracket.Threshold);
                ValidateNonNegative(errors, $"{label} IRMAA cliff {i + 1} monthly surcharge", bracket.MonthlySurcharge);
            }
        }

        private static void ValidateNonNegative(List<string> errors, string label, decimal value)
        {
            if (value < 0m)
                errors.Add($"{label} cannot be negative.");
        }
    }
}
