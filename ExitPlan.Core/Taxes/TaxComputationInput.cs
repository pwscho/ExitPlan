using System;
using System.Collections.Generic;

namespace ExitPlan.Core.Taxes
{
    public sealed class TaxComputationInput
    {
        public int Year { get; init; }
        public FilingStatus FilingStatus { get; init; }

        public decimal OrdinaryIncome { get; init; }
        public decimal CapitalGains { get; init; }
        public decimal SocialSecurityBenefits { get; init; }

        /// <summary>
        /// Integer ages of owners alive this year (CurrentAge + year offset,
        /// living when that age is &lt;= DeathAge). Used for IRC §63(f).
        /// </summary>
        public IReadOnlyList<int> LivingOwnerAges { get; init; } = Array.Empty<int>();
    }

    public sealed class TaxComputationResult
    {
        // Federal income tax only. IRMAA is assessed separately and paid as year N+2 cash.
        public decimal FederalTax { get; init; }
        public decimal Magi { get; init; }

        public decimal IrmaaSurcharge { get; init; }

        public decimal TaxableOrdinaryIncome { get; init; }
        public decimal TaxableCapitalGains { get; init; }
        public decimal TaxableSocialSecurity { get; init; }

        /// <summary>
        /// Ordinary taxable income split across that year's ordinary brackets.
        /// </summary>
        public IReadOnlyList<OrdinaryBracketFill> OrdinaryBracketFills { get; init; }
            = Array.Empty<OrdinaryBracketFill>();

        /// <summary>
        /// Dollars of ordinary taxable income that still fit in the current
        /// ordinary rate before the next cliff. Zero in the top listed rate.
        /// </summary>
        public decimal OrdinaryBracketHeadroom { get; init; }

        // Optional convenience property
        public decimal TotalFederalTax => FederalTax;
    }
}

