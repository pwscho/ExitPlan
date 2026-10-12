using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ExitPlan.Core
{
    // ============================================================
    // GUARDRAIL SETTINGS
    // ============================================================
    public class GuardrailSettings
    {
        // Baseline Portfolio Value (used for guardrail calculations)
        public decimal InitialPortfolioValue { get; set; } = 0m;

        // Lower guardrail
        public decimal LowerTrigger { get; set; } = 0.90m;          // 90% of initial portfolio
        public decimal LowerCut { get; set; } = -0.02m;             // -2% inflation
        public decimal MinSpendingFloor { get; set; } = 0.90m;      // 90% of initial spending
        // When false (default), the floor stays in nominal dollars.
        // When true, the floor grows with scenario.InflationRate each year.
        public bool InflateSpendingFloor { get; set; } = false;

        // When false (default), trigger tests compare the portfolio to InitialPortfolioValue.
        // When true, that baseline compounds with the spending inflation path:
        // year t uses IPV × (1+inflation)^(t − BaseYear), or the product of a per-year path.
        public bool IncreaseBaselineWithInflation { get; set; } = false;

        // Upper guardrail
        public decimal UpperTrigger { get; set; } = 1.10m;          // 110% of initial portfolio
        public decimal UpperBonus { get; set; } = 0.01m;            // +1% bonus inflation



        public GuardrailSettings DeepCopy()
        {
            return new GuardrailSettings
            {
                InitialPortfolioValue = this.InitialPortfolioValue,
                LowerTrigger = this.LowerTrigger,
                LowerCut = this.LowerCut,
                MinSpendingFloor = this.MinSpendingFloor,
                InflateSpendingFloor = this.InflateSpendingFloor,
                IncreaseBaselineWithInflation = this.IncreaseBaselineWithInflation,
                UpperTrigger = this.UpperTrigger,
                UpperBonus = this.UpperBonus
            };
        }

        /// <summary>
        /// Portfolio value the upper and lower trigger tests compare against
        /// at the start of a year. <paramref name="yearsSinceBaseYear"/> is t − BaseYear.
        /// When <paramref name="increaseWithInflation"/> is false, this is the initial portfolio value.
        /// When true, it is that value compounded by the spending inflation path:
        /// one rate per prior year, in order. A constant rate is that same rate repeated,
        /// which is IPV × (1 + inflation)^(t − BaseYear).
        /// </summary>
        public static decimal TriggerBaseline(
            decimal initialPortfolioValue,
            bool increaseWithInflation,
            int yearsSinceBaseYear,
            IReadOnlyList<decimal>? inflationByPriorYear)
        {
            if (!increaseWithInflation || yearsSinceBaseYear <= 0)
                return initialPortfolioValue;

            if (inflationByPriorYear == null || inflationByPriorYear.Count < yearsSinceBaseYear)
                throw new ArgumentException(
                    "Inflation path must include one rate for each year before this one.",
                    nameof(inflationByPriorYear));

            decimal factor = 1m;
            for (int i = 0; i < yearsSinceBaseYear; i++)
                factor *= 1m + inflationByPriorYear[i];

            return initialPortfolioValue * factor;
        }
    }

    // ============================================================
    // SCENARIO
    // ============================================================
    public class Scenario
    {
        public string Name { get; set; } = "New Scenario";

        public int YearsToCalculate { get; set; } = 30;

        // One seed for the whole Monte Carlo batch. Simulation N derives its path from
        // (MonteCarloSeed, N) so scenario comparisons reuse the same market years.
        public int MonteCarloSeed { get; set; } = 1;

        public int MonteCarloSimulations { get; set; } = 1000;

        public int RetirementAge { get; set; } = 65;

        public decimal AnnualSpending { get; set; }

        public string SpendingStrategy { get; set; } = "Fixed";

        public decimal InflationRate { get; set; } = 0.02m;

        public string TaxBracketInflationStrategy { get; set; } = "Fixed";
        public decimal TaxBracketInflationRate { get; set; } = 0.02m;

        // Toggle: enable or disable tax bracket inflation
        public bool EnableTaxBracketInflation { get; set; } = true;

        public decimal TaxesFromLastYear { get; set; } = 0;

        public decimal WithdrawalAdjustmentRate { get; set; } = 0.02m;

        // Rebalancing settings
        public bool RebalanceAnnually { get; set; } = false;

        public decimal TargetEquityPercentage { get; set; } = 0.70m;

        public decimal TargetBondPercentage { get; set; } = 0.30m;

        // Additional streams (user‑defined)
        public List<IncomeStream> IncomeStreams { get; set; } = new();
        public List<WithdrawalStream> WithdrawalStreams { get; set; } = new();
        public List<RothConversionStream> RothConversions { get; set; } = new();

        // Base year for tax policy lookup
        public int BaseYear { get; set; } = DateTime.Now.Year;

        // RMD policy (user‑editable)
        [JsonIgnore]
        public RmdPolicy RmdPolicy { get; set; }

        // ============================================================
        // NEW: Guardrails + Initial Portfolio Value
        // ============================================================
        public GuardrailSettings Guardrails { get; set; } = new GuardrailSettings();

        public decimal InitialPortfolioValue { get; set; } = 0m;

        /// <summary>
        /// Report-card metric weights keyed by metric name (same units as the UI, e.g. 25 = 25%).
        /// Empty means use the evaluator defaults. Persisted with the scenario.
        /// </summary>
        public Dictionary<string, decimal> ReportCardWeights { get; set; } = new();

        // ============================================================
        // Deep Copy
        // ============================================================
        public Scenario DeepCopy()
        {
            return new Scenario
            {
                Name = this.Name,
                YearsToCalculate = this.YearsToCalculate,
                MonteCarloSeed = this.MonteCarloSeed,
                MonteCarloSimulations = this.MonteCarloSimulations,
                RetirementAge = this.RetirementAge,
                AnnualSpending = this.AnnualSpending,
                SpendingStrategy = this.SpendingStrategy,
                InflationRate = this.InflationRate,
                TaxBracketInflationStrategy = this.TaxBracketInflationStrategy,
                TaxBracketInflationRate = this.TaxBracketInflationRate,
                EnableTaxBracketInflation = this.EnableTaxBracketInflation,
                TaxesFromLastYear = this.TaxesFromLastYear,
                WithdrawalAdjustmentRate = this.WithdrawalAdjustmentRate,

                RebalanceAnnually = this.RebalanceAnnually,
                TargetEquityPercentage = this.TargetEquityPercentage,
                TargetBondPercentage = this.TargetBondPercentage,

                BaseYear = this.BaseYear,

                RmdPolicy = this.RmdPolicy, // if mutable, consider cloning

                InitialPortfolioValue = this.InitialPortfolioValue,

                Guardrails = this.Guardrails.DeepCopy(),

                ReportCardWeights = this.ReportCardWeights is null
                    ? new Dictionary<string, decimal>()
                    : new Dictionary<string, decimal>(this.ReportCardWeights),

                IncomeStreams = this.IncomeStreams
                    .Select(i => new IncomeStream
                    {
                        Name = i.Name,
                        OwnerId = i.OwnerId,
                        StartAge = i.StartAge,
                        EndAge = i.EndAge,
                        AnnualAmount = i.AnnualAmount,
                        IsTaxable = i.IsTaxable
                    })
                    .ToList(),

                WithdrawalStreams = this.WithdrawalStreams
                    .Select(w => new WithdrawalStream
                    {
                        Name = w.Name,
                        OwnerId = w.OwnerId,
                        StartAge = w.StartAge,
                        EndAge = w.EndAge,
                        AnnualAmount = w.AnnualAmount
                    })
                    .ToList(),

                RothConversions = this.RothConversions
                    .Select(r => new RothConversionStream
                    {
                        Name = r.Name,
                        OwnerId = r.OwnerId,
                        StartAge = r.StartAge,
                        EndAge = r.EndAge,
                        AnnualAmount = r.AnnualAmount
                    })
                    .ToList()
            };
        }
    }
}



