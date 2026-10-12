using System.Collections.Generic;
using ExitPlan.Core.Taxes;

namespace ExitPlan.Core
{
    public class ResultRow
    {
        public int SimNo { get; set; }
        public int Year { get; set; }
        public int Age { get; set; }
        
        
        public string AgeDisplay => SpouseAge > 0
            ? $"{Age}/{SpouseAge}"
            : $"{Age}";

        public int SpouseAge { get; set; }
        public int Iteration { get; set; }

        public decimal StartingBalance { get; set; }
        public decimal EndingBalance { get; set; }

        public decimal Withdrawal { get; set; }
        public decimal SocialSecurityIncome { get; set; }
        public decimal Income { get; set; }
        public decimal InvestmentReturn { get; set; }

        public decimal Magi { get; set; }
        public decimal TaxableIncome { get; set; }
        public decimal OrdinaryIncome { get; set; }
        public decimal CapitalGains { get; set; }

        /// <summary>
        /// Filing status the engine used for this year's ordinary tax.
        /// </summary>
        public FilingStatus FilingStatus { get; set; }

        public string FilingStatusLabel =>
            FilingStatus == FilingStatus.MarriedFilingJointly ? "MFJ" : "Single";

        /// <summary>
        /// Ordinary taxable income split across this year's ordinary brackets.
        /// </summary>
        public List<OrdinaryBracketFill> OrdinaryBracketFills { get; set; } = new();

        /// <summary>
        /// Dollars still available in the current ordinary rate before the next cliff.
        /// </summary>
        public decimal OrdinaryBracketHeadroom { get; set; }

        public decimal TaxesDue { get; set; }
        public decimal TaxesPaid { get; set; }
        public decimal IrmaaPaid { get; set; }

        public decimal Rmds { get; set; }
        public decimal Conversions { get; set; }

        public decimal AccountWithdrawals { get; set; }     
        public decimal PercentWithdrawals { get; set; }

        public decimal BaseSpending { get; set; }
        public decimal AddedSpending { get; set; }

        /// <summary>
        /// Base spending if only <see cref="Scenario.InflationRate"/> had been applied
        /// each year (guardrails never fired).
        /// </summary>
        public decimal InflationOnlyBaseSpending { get; set; }

        /// <summary>
        /// Start-of-year portfolio divided by the guardrail baseline.
        /// The baseline is <see cref="GuardrailSettings.InitialPortfolioValue"/> unless
        /// <see cref="GuardrailSettings.IncreaseBaselineWithInflation"/> is set, in which
        /// case that value is compounded with the spending inflation path from the base year.
        /// </summary>
        public decimal GuardrailPortfolioPercent { get; set; }

        public GuardrailYearAction GuardrailAction { get; set; } = GuardrailYearAction.None;

        public string GuardrailActionLabel => GuardrailAction switch
        {
            GuardrailYearAction.Cut => "cut",
            GuardrailYearAction.FloorHit => "floor hit",
            GuardrailYearAction.Raise => "raise",
            _ => "none"
        };
    }
}

