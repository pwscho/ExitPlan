using System;
using System.Collections.Generic;
using ExitPlan.Core.Taxes;

namespace ExitPlan.Core.Reporting
{
    /// <summary>
    /// Live snapshot of a generated report, ready to print. Values come from the
    /// current scenario, accounts, Social Security settings, and simulation results.
    /// </summary>
    public sealed class RetirementSummaryModel
    {
        public required string ScenarioName { get; init; }
        public required string ProductTitle { get; init; }
        public DateTime GeneratedAt { get; init; }

        public int PlanStartYear { get; init; }
        public int PlanYears { get; init; }
        public int PlanEndYear { get; init; }

        public IReadOnlyList<HouseholdMemberSummary> Members { get; init; } = Array.Empty<HouseholdMemberSummary>();

        public decimal StartingPortfolio { get; init; }
        public IReadOnlyList<PortfolioSlice> PortfolioSlices { get; init; } = Array.Empty<PortfolioSlice>();
        public IReadOnlyList<AccountLine> Accounts { get; init; } = Array.Empty<AccountLine>();

        public decimal BaseAnnualSpending { get; init; }
        public string SpendingStrategy { get; init; } = "Fixed";
        public decimal InflationRate { get; init; }
        public IReadOnlyList<NamedStreamLine> SpendingExtras { get; init; } = Array.Empty<NamedStreamLine>();
        public IReadOnlyList<SpendingYearPoint> SpendingByYear { get; init; } = Array.Empty<SpendingYearPoint>();
        public IReadOnlyList<PortfolioYearPoint> PortfolioByYear { get; init; } = Array.Empty<PortfolioYearPoint>();
        public IReadOnlyList<OrdinaryBracketYearPoint> OrdinaryBracketsByYear { get; init; } = Array.Empty<OrdinaryBracketYearPoint>();

        /// <summary>
        /// Guardrail years that raised the spending floor/rate on the fixed path.
        /// Zero when the strategy is not Guardrails or no raise fired.
        /// </summary>
        public int SpendingRaises { get; init; }

        /// <summary>
        /// Guardrail years that cut spending (including floor hits) on the fixed path.
        /// Zero when the strategy is not Guardrails or no cut fired.
        /// </summary>
        public int SpendingCuts { get; init; }

        public IReadOnlyList<NamedStreamLine> IncomeStreams { get; init; } = Array.Empty<NamedStreamLine>();
        public IReadOnlyList<NamedStreamLine> RothConversions { get; init; } = Array.Empty<NamedStreamLine>();
        public IReadOnlyList<SocialSecurityClaimLine> SocialSecurity { get; init; } = Array.Empty<SocialSecurityClaimLine>();
        /// <summary>
        /// Live guardrail settings when the scenario spending strategy is Guardrails; otherwise null.
        /// </summary>
        public GuardrailSummary? Guardrails { get; init; }

        public StressTestMetric? Historical { get; init; }
        public StressTestMetric? MonteCarlo { get; init; }
        public StressTestMetric? Fixed { get; init; }

        public ReportCard? ReportCard { get; init; }

        public bool HasResults { get; init; }
        public string BottomLine { get; init; } = string.Empty;
    }

    public sealed class HouseholdMemberSummary
    {
        public required string Name { get; init; }
        public string Role { get; init; } = string.Empty;
        public int CurrentAge { get; init; }
        public int ClaimAge { get; init; }
        public int DeathAge { get; init; }
        public int ClaimYear { get; init; }
        public int LongevityYear { get; init; }
    }

    public sealed class PortfolioSlice
    {
        public required string Label { get; init; }
        public decimal Value { get; init; }
        public decimal Share { get; init; }
        public string ColorHex { get; init; } = "#2E75B6";
    }

    public sealed class AccountLine
    {
        public required string Owner { get; init; }
        public required string TaxBucket { get; init; }
        public required string AssetClass { get; init; }
        public string Name { get; init; } = string.Empty;
        /// <summary>
        /// Spending / withdrawal-sequence bucket (1, 2, 3, …). Zero means unset.
        /// </summary>
        public int Bucket { get; init; }
        public int Priority { get; init; }
        /// <summary>Modeled return stored as a fraction (0.08 = 8%).</summary>
        public decimal RateOfReturn { get; init; }
        public decimal Value { get; init; }
    }

    public sealed class NamedStreamLine
    {
        public required string Name { get; init; }
        public string Owner { get; init; } = string.Empty;
        public int StartAge { get; init; }
        public int EndAge { get; init; }
        public decimal AnnualAmount { get; init; }
        public int StartYear { get; init; }
        public int EndYear { get; init; }
        public bool IsTaxable { get; init; }
    }

    public sealed class SpendingYearPoint
    {
        public int Year { get; init; }
        public decimal BaseSpending { get; init; }
        public decimal ExtraSpending { get; init; }
        public decimal TotalSpending => BaseSpending + ExtraSpending;
    }

    /// <summary>
    /// One year of the fixed simulation, for the Print Summary portfolio path
    /// (or portfolio vs guardrail baseline when Guardrails is selected).
    /// </summary>
    public sealed class PortfolioYearPoint
    {
        public int Year { get; init; }
        public decimal StartingBalance { get; init; }
        public decimal EndingBalance { get; init; }
        /// <summary>Start-of-year portfolio ÷ guardrail baseline.</summary>
        public decimal GuardrailPortfolioPercent { get; init; }
    }

    /// <summary>
    /// One year of ordinary-bracket fill from the fixed simulation.
    /// </summary>
    public sealed class OrdinaryBracketYearPoint
    {
        public int Year { get; init; }
        public decimal Headroom { get; init; }
        public IReadOnlyList<OrdinaryBracketFill> Fills { get; init; } = Array.Empty<OrdinaryBracketFill>();
    }

    public sealed class SocialSecurityClaimLine
    {
        public required string Name { get; init; }
        public int CurrentAge { get; init; }
        public int ClaimAge { get; init; }
        public int ClaimYear { get; init; }
        public decimal MonthlyPia { get; init; }
        public decimal Cola { get; init; }
        public int DeathAge { get; init; }
    }

    public sealed class StressTestMetric
    {
        public required string Name { get; init; }
        public decimal SuccessRate { get; init; }
        public int PathCount { get; init; }
        public decimal AverageEnding { get; init; }
        public decimal MedianEnding { get; init; }
        public decimal TenthPercentileEnding { get; init; }
        public decimal NinetiethPercentileEnding { get; init; }
        public decimal AverageTotalSpending { get; init; }
    }

    /// <summary>
    /// Snapshot of <see cref="GuardrailSettings"/> for Print Summary.
    /// Percent-like fields stay as stored fractions (0.90 = 90%).
    /// </summary>
    public sealed class GuardrailSummary
    {
        public decimal InitialPortfolioValue { get; init; }
        public decimal LowerTrigger { get; init; }
        public decimal LowerCut { get; init; }
        public decimal MinSpendingFloor { get; init; }
        public bool InflateSpendingFloor { get; init; }
        public bool IncreaseBaselineWithInflation { get; init; }
        public decimal UpperTrigger { get; init; }
        public decimal UpperBonus { get; init; }
    }
}
