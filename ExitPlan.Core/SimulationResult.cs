using ExitPlan.Core;
using System.Collections.Generic;

public class SimulationResult
{
    // Social Security summary
    public decimal PrimarySocialSecurity { get; set; }
    public decimal? SpouseSocialSecurity { get; set; }

    // Year-by-year SS + income
    public decimal[] IncomeByYear { get; set; }
    public decimal[] SocialSecurityByYear { get; set; }
    public decimal[] OrdinaryIncomeByYear { get; set; }
    public decimal[] CapitalGainsByYear { get; set; }

    // Withdrawals + taxes
    public decimal[] WithdrawalsByYear { get; set; }
    public decimal[] TaxableIncomeByYear { get; set; }
    public decimal[] TaxesByYear { get; set; }
    public decimal[] TaxesPaidByYear { get; set; }

    // NEW: IRMAA tracking
    // IRMAA assessed in the current year (based on MAGI)
    public decimal[] IrmaaByYear { get; set; }

    // RMDS and ROTH Conversions
    public decimal[] RmdsByYear { get; set; }
    public decimal[] ConversionsByYear { get; set; }


    // IRMAA actually paid in the year (N+2 pipeline)
    public decimal[] IrmaaPaidByYear { get; set; }

    // Ages for reporting (optional but useful)
    public int PrimaryCurrentAge { get; set; }
    public int PrimaryClaimAge { get; set; }
    public int PrimaryDeathAge { get; set; }

    public int SpouseCurrentAge { get; set; }
    public int SpouseClaimAge { get; set; }
    public int SpouseDeathAge { get; set; }

    // Projection snapshots (one list, each containing per-year values)
    public List<AccountProjection> AccountProjections { get; set; }
        = new List<AccountProjection>();

    // Helper for UI
    public decimal GetSocialSecurityIncomeForYear(int year)
    {
        if (SocialSecurityByYear == null || year < 0 || year >= SocialSecurityByYear.Length)
            return 0m;

        return SocialSecurityByYear[year];
    }

    // Constructor helper for engine
    public SimulationResult(int years)
    {
        IncomeByYear = new decimal[years];

        SocialSecurityByYear = new decimal[years];
        OrdinaryIncomeByYear = new decimal[years];
        CapitalGainsByYear = new decimal[years];

        WithdrawalsByYear = new decimal[years];
        TaxableIncomeByYear = new decimal[years];
        TaxesByYear = new decimal[years];
        TaxesPaidByYear = new decimal[years];



        // NEW: IRMAA arrays
        IrmaaByYear = new decimal[years];
        IrmaaPaidByYear = new decimal[years];

        RmdsByYear = new decimal[years];
        ConversionsByYear = new decimal[years];

    }

    // Parameterless constructor for XAML binding or serialization
    public SimulationResult() { }
}


