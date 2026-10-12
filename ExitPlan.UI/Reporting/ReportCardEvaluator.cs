using System;
using System.Collections.Generic;
using System.Linq;
using ExitPlan.Core.Simulation;

namespace ExitPlan.Core.Reporting
{
    /// <summary>
    /// Computes a ReportCard from simulation results.
    /// </summary>
    public static class ReportCardEvaluator
    {
        public static ReportCard Evaluate(ExitPlan.UI.Reporting.ReportData reportData, List<ExitPlan.Core.Account> accounts)
        {
            var card = new ReportCard();

            var fixedResults = reportData.FixedResults;
            var historicalResults = reportData.HistoricalResults;
            var monteCarloResults = reportData.MonteCarloResults;

            card.Metrics.Add(EvalPortfolioLongevity(monteCarloResults, historicalResults));
            card.Metrics.Add(EvalIncomeCoverage(fixedResults));
            card.Metrics.Add(EvalSequenceRisk(historicalResults));
            card.Metrics.Add(EvalTaxEfficiency(fixedResults));
            card.Metrics.Add(EvalWithdrawalRate(fixedResults, accounts));
            card.Metrics.Add(EvalTaxDiversification(accounts));
            card.Metrics.Add(EvalHealthcareExposure(fixedResults));
            card.Metrics.Add(EvalLegacyPotential(monteCarloResults, fixedResults));
            card.Metrics.Add(EvalSpendingPower(fixedResults));
            card.Metrics.Add(EvalRmdManagement(fixedResults));

            // Overlay any weights saved on the scenario, then score.
            card.ApplySavedWeights(reportData.Scenario?.ReportCardWeights);
            card.RecalculateOverall();
            card.ListenForWeightEdits();

            return card;
        }

        // ----------------------------------------------------------------
        // 1. Portfolio Longevity — 25%
        // ----------------------------------------------------------------
        private static ReportCardMetric EvalPortfolioLongevity(
            List<ExitPlan.Core.ResultRow> mcResults,
            List<ExitPlan.Core.ResultRow> historicalResults)
        {
            decimal mcSuccess = ComputeSuccessRate(mcResults);
            decimal score = mcSuccess;

            // Bonus if historical also survives
            decimal histSuccess = ComputeSuccessRate(historicalResults);
            if (histSuccess >= 95m)
                score = Math.Min(100m, score + 5m);

            string mcPct = mcSuccess.ToString("F1");
            string histPct = histSuccess.ToString("F1");

            return new ReportCardMetric
            {
                Name = "Portfolio Longevity",
                Description = "Probability your portfolio lasts through the planning horizon.",
                Score = score,
                Weight = 25m,
                Detail = $"Monte Carlo success rate: {mcPct}% | Historical success rate: {histPct}%"
            };
        }

        // ----------------------------------------------------------------
        // 2. Income Coverage Ratio — 15%
        // ----------------------------------------------------------------
        private static ReportCardMetric EvalIncomeCoverage(
            List<ExitPlan.Core.ResultRow> fixedResults)
        {
            // Average coverage in years 1-10 (early retirement)
            var earlyYears = fixedResults
                .Where(r => r.Iteration >= 1 && r.Iteration <= 10)
                .ToList();

            if (!earlyYears.Any())
                return Metric("Income Coverage", "Percentage of spending covered by guaranteed income (SS + pensions).", 0m, 15m, "No data available");

            decimal avgCoverage = earlyYears
                .Where(r => r.Withdrawal > 0m)
                .Select(r => (r.SocialSecurityIncome + r.Income) / r.Withdrawal * 100m)
                .Average();

            // Clamp 0-100
            avgCoverage = Math.Max(0m, Math.Min(100m, avgCoverage));

            decimal score = CoverageToScore(avgCoverage);

            return new ReportCardMetric
            {
                Name = "Income Coverage",
                Description = "Percentage of spending covered by guaranteed income (SS + pensions).",
                Score = score,
                Weight = 15m,
                Detail = $"Guaranteed income covers {avgCoverage:F1}% of annual spending (avg yrs 1-10)"
            };
        }

        // ----------------------------------------------------------------
        // 3. Sequence-of-Returns Risk — 15%
        // ----------------------------------------------------------------
        private static ReportCardMetric EvalSequenceRisk(
            List<ExitPlan.Core.ResultRow> historicalResults)
        {
            decimal histSuccess = ComputeSuccessRate(historicalResults);
            decimal score = histSuccess;

            int totalPeriods = historicalResults.GroupBy(r => r.SimNo).Count();
            int successes = (int)Math.Round(histSuccess / 100m * totalPeriods);

            return new ReportCardMetric
            {
                Name = "Sequence Risk",
                Description = "How robust the plan is across different historical market periods.",
                Score = score,
                Weight = 15m,
                Detail = $"Survived {successes} of {totalPeriods} historical market periods ({histSuccess:F1}%)"
            };
        }

        // ----------------------------------------------------------------
        // 4. Tax Efficiency — 10%
        // ----------------------------------------------------------------
        private static ReportCardMetric EvalTaxEfficiency(
            List<ExitPlan.Core.ResultRow> fixedResults)
        {
            decimal totalTaxes = fixedResults.Sum(r => r.TaxesPaid + r.IrmaaPaid);
            decimal totalIncome = fixedResults.Sum(r =>
                r.SocialSecurityIncome + r.Income + r.Withdrawal + r.InvestmentReturn);

            if (totalIncome <= 0m)
                return Metric("Tax Efficiency", "Effective tax rate as a percentage of total income.", 100m, 10m, "No income data");

            decimal effectiveRate = totalTaxes / totalIncome * 100m;
            decimal score = EffectiveTaxRateToScore(effectiveRate);

            return new ReportCardMetric
            {
                Name = "Tax Efficiency",
                Description = "Effective tax rate as a percentage of total lifetime income.",
                Score = score,
                Weight = 10m,
                Detail = $"Lifetime effective tax rate: {effectiveRate:F1}%"
            };
        }

        // ----------------------------------------------------------------
        // 5. Withdrawal Rate Sustainability — 10%
        // ----------------------------------------------------------------
        private static ReportCardMetric EvalWithdrawalRate(
            List<ExitPlan.Core.ResultRow> fixedResults,
            List<ExitPlan.Core.Account> accounts)
        {
            decimal initialPortfolio = accounts.Sum(a => a.Value);
            decimal year1Spending = fixedResults
                .Where(r => r.Iteration == 1)
                .Sum(r => r.Withdrawal);

            if (initialPortfolio <= 0m)
                return Metric("Withdrawal Rate", "Initial withdrawal rate relative to portfolio value.", 0m, 10m, "No portfolio data");

            decimal withdrawalRate = year1Spending / initialPortfolio * 100m;
            decimal score = WithdrawalRateToScore(withdrawalRate);

            return new ReportCardMetric
            {
                Name = "Withdrawal Rate",
                Description = "Initial withdrawal rate relative to portfolio value.",
                Score = score,
                Weight = 10m,
                Detail = $"Year 1 withdrawal rate: {withdrawalRate:F1}% (${year1Spending,0:F0} from ${initialPortfolio,0:F0} portfolio)"
            };
        }

        // ----------------------------------------------------------------
        // 6. Tax Diversification — 5%
        // ----------------------------------------------------------------
        private static ReportCardMetric EvalTaxDiversification(
            List<ExitPlan.Core.Account> accounts)
        {
            decimal total = accounts.Sum(a => a.Value);
            if (total <= 0m)
                return Metric("Tax Diversification", "Balance across tax-free, tax-deferred, and taxable accounts.", 0m, 5m, "No account data");

            decimal taxFree = accounts.Where(a => a.TaxBucket.Trim().Equals("Tax Free", StringComparison.OrdinalIgnoreCase)).Sum(a => a.Value);
            decimal taxDeferred = accounts.Where(a => a.TaxBucket.Trim().Equals("Tax Deferred", StringComparison.OrdinalIgnoreCase)).Sum(a => a.Value);
            decimal taxable = accounts.Where(a => a.TaxBucket.Trim().Equals("Taxable", StringComparison.OrdinalIgnoreCase)).Sum(a => a.Value);

            decimal[] pct = {
                taxFree / total * 100m,
                taxDeferred / total * 100m,
                taxable / total * 100m
            };

            decimal score = 100m;
            int emptyBuckets = 0;
            foreach (var p in pct)
            {
                if (p == 0m)
                {
                    emptyBuckets++;
                    score -= 25m;
                }
                else if (p < 20m || p > 50m)
                {
                    score -= 15m;
                }
            }
            score = Math.Max(0m, score);

            return new ReportCardMetric
            {
                Name = "Tax Diversification",
                Description = "Balance across tax-free, tax-deferred, and taxable buckets.",
                Score = score,
                Weight = 5m,
                Detail = $"Tax Free: {pct[0]:F1}% | Tax Deferred: {pct[1]:F1}% | Taxable: {pct[2]:F1}%"
            };
        }

        // ----------------------------------------------------------------
        // 7. Healthcare Cost Exposure (IRMAA) — 5%
        // ----------------------------------------------------------------
        private static ReportCardMetric EvalHealthcareExposure(
            List<ExitPlan.Core.ResultRow> fixedResults)
        {
            decimal totalIrmaa = fixedResults.Sum(r => r.IrmaaPaid);
            decimal totalSpending = fixedResults.Sum(r => r.BaseSpending);

            if (totalSpending <= 0m)
                return Metric("Healthcare Exposure", "IRMAA surcharge impact on total spending.", 100m, 5m, "No spending data");

            decimal irmaaPct = totalIrmaa / totalSpending * 100m;
            decimal score = IrmaaToScore(irmaaPct);

            return new ReportCardMetric
            {
                Name = "Healthcare Exposure",
                Description = "IRMAA surcharge impact on total retirement spending.",
                Score = score,
                Weight = 5m,
                Detail = $"Total IRMAA: ${totalIrmaa,0:F0} ({irmaaPct:F1}% of lifetime spending)"
            };
        }

        // ----------------------------------------------------------------
        // 8. Legacy / Inheritance Potential — 5%
        // ----------------------------------------------------------------
        private static ReportCardMetric EvalLegacyPotential(
            List<ExitPlan.Core.ResultRow> mcResults,
            List<ExitPlan.Core.ResultRow> fixedResults)
        {
            // Median ending balance from Monte Carlo
            var endings = mcResults
                .GroupBy(r => r.SimNo)
                .Select(g => g.OrderBy(x => x.Iteration).Last().EndingBalance)
                .OrderBy(v => v)
                .ToList();

            decimal medianEnding = endings.Count > 0
                ? endings[endings.Count / 2]
                : 0m;

            decimal lifetimeSpending = fixedResults.Sum(r => r.Withdrawal);

            if (lifetimeSpending <= 0m)
                return Metric("Legacy Potential", "Remaining portfolio relative to lifetime spending.", 0m, 5m, "No spending data");

            decimal ratio = medianEnding / lifetimeSpending * 100m;
            decimal score = LegacyRatioToScore(ratio);

            return new ReportCardMetric
            {
                Name = "Legacy Potential",
                Description = "Median remaining portfolio relative to lifetime spending.",
                Score = score,
                Weight = 5m,
                Detail = $"Median ending: ${medianEnding,0:F0} ({ratio:F0}% of ${lifetimeSpending,0:F0} lifetime spending)"
            };
        }

        // ----------------------------------------------------------------
        // 9. Spending Power / Inflation Protection — 5%
        // ----------------------------------------------------------------
        private static ReportCardMetric EvalSpendingPower(
            List<ExitPlan.Core.ResultRow> fixedResults)
        {
            if (fixedResults.Count < 2)
                return Metric("Spending Power", "Real purchasing power of spending in the final year vs. first year.", 100m, 5m, "Insufficient data");

            decimal firstSpending = fixedResults.First().BaseSpending;
            decimal lastSpending = fixedResults.Last().BaseSpending;
            int years = fixedResults.Count;

            // If spending stayed flat (no inflation adjustment), real power dropped
            // We compare nominal final spending to what it SHOULD be with inflation
            // Using average inflation implied by the spending growth
            if (firstSpending <= 0m)
                return Metric("Spending Power", "Real purchasing power of spending in the final year vs. first year.", 0m, 5m, "No initial spending");

            // If spending grew, compute the implied inflation rate
            // Then check if real purchasing power was maintained
            // Simple approach: if spending grew >= 0, it kept pace (guardrail cuts show up here)
            decimal growth = lastSpending / firstSpending;

            // Compute implied annual inflation from spending growth
            // If growth = 1, spending is flat (worst inflation case)
            // We compare to a 2.5% baseline inflation assumption
            decimal baselineInflation = 0.025m;
            decimal expectedMultiplier = 1m;
            for (int i = 0; i < years; i++)
                expectedMultiplier *= (1m + baselineInflation);

            decimal realPower = lastSpending / (firstSpending * expectedMultiplier) * 100m;
            realPower = Math.Min(100m, Math.Max(0m, realPower));

            decimal score = CoverageToScore(realPower);

            return new ReportCardMetric
            {
                Name = "Spending Power",
                Description = "Real purchasing power of spending at end vs. start (2.5% inflation baseline).",
                Score = score,
                Weight = 5m,
                Detail = $"Spending: ${firstSpending,0:F0} → ${lastSpending,0:F0} over {years} yrs ({realPower:F0}% real purchasing power)"
            };
        }

        // ----------------------------------------------------------------
        // 10. RMD Management — 5%
        // ----------------------------------------------------------------
        private static ReportCardMetric EvalRmdManagement(
            List<ExitPlan.Core.ResultRow> fixedResults)
        {
            var rmdYears = fixedResults.Where(r => r.Rmds > 0m).ToList();

            if (!rmdYears.Any())
                return new ReportCardMetric
                {
                    Name = "RMD Management",
                    Description = "How well RMDs are managed relative to tax-deferred balances.",
                    Score = 100m,
                    Weight = 5m,
                    Detail = "No RMDs triggered during simulation"
                };

            // Approximate RMD burden: average RMD as % of starting balance in RMD years
            decimal avgRatio = rmdYears
                .Where(r => r.StartingBalance > 0m)
                .Select(r => r.Rmds / r.StartingBalance * 100m)
                .DefaultIfEmpty(0m)
                .Average();

            decimal score = RmdRatioToScore(avgRatio);

            decimal totalRmds = rmdYears.Sum(r => r.Rmds);

            return new ReportCardMetric
            {
                Name = "RMD Management",
                Description = "RMD burden relative to portfolio during RMD years.",
                Score = score,
                Weight = 5m,
                Detail = $"Total RMDs: ${totalRmds,0:F0} | Avg RMD burden: {avgRatio:F1}% of portfolio"
            };
        }

        // ----------------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------------
        private static ReportCardMetric Metric(string name, string description, decimal score, decimal weight, string detail)
            => new ReportCardMetric { Name = name, Description = description, Score = Math.Max(0m, Math.Min(100m, score)), Weight = weight, Detail = detail };

        private static decimal ComputeSuccessRate(List<ExitPlan.Core.ResultRow> results)
        {
            if (!results.Any()) return 0m;
            var sims = results.GroupBy(r => r.SimNo);
            int total = sims.Count();
            int survived = sims.Count(g => SimulationSuccess.IsSuccessful(g));
            return total == 0 ? 0m : (decimal)survived / total * 100m;
        }

        private static decimal CoverageToScore(decimal coverage)
        {
            if (coverage >= 100m) return 100m;
            if (coverage >= 80m) return 80m + (coverage - 80m) / 20m * 20m; // 80-100 maps to 80-100
            if (coverage >= 60m) return 40m + (coverage - 60m) / 20m * 40m; // 60-80 maps to 40-80
            if (coverage >= 40m) return 20m + (coverage - 40m) / 20m * 20m; // 40-60 maps to 20-40
            if (coverage >= 20m) return coverage / 20m * 20m; // 20-40 maps to 0-20
            return coverage / 20m * 20m; // 0-20 maps to 0-20
        }

        private static decimal EffectiveTaxRateToScore(decimal rate)
        {
            if (rate < 10m) return 100m;
            if (rate <= 15m) return 80m + (15m - rate) / 5m * 20m;
            if (rate <= 20m) return 60m + (20m - rate) / 5m * 20m;
            if (rate <= 25m) return 40m + (25m - rate) / 5m * 20m;
            if (rate <= 30m) return 20m + (30m - rate) / 5m * 20m;
            return 0m;
        }

        private static decimal WithdrawalRateToScore(decimal rate)
        {
            if (rate <= 3.0m) return 100m;
            if (rate <= 3.5m) return 80m + (3.5m - rate) / 0.5m * 20m;
            if (rate <= 4.0m) return 60m + (4.0m - rate) / 0.5m * 20m;
            if (rate <= 4.5m) return 40m + (4.5m - rate) / 0.5m * 20m;
            if (rate <= 5.0m) return 20m + (5.0m - rate) / 0.5m * 20m;
            return 0m;
        }

        private static decimal IrmaaToScore(decimal pct)
        {
            if (pct <= 0m) return 100m;
            if (pct <= 1m) return 80m + (1m - pct) * 20m;
            if (pct <= 2m) return 60m + (2m - pct) * 20m;
            if (pct <= 4m) return 40m + (4m - pct) / 2m * 20m;
            if (pct <= 6m) return 20m + (6m - pct) / 2m * 20m;
            return 0m;
        }

        private static decimal LegacyRatioToScore(decimal ratio)
        {
            if (ratio >= 200m) return 100m;
            if (ratio >= 100m) return 80m + (ratio - 100m) / 100m * 20m;
            if (ratio >= 50m) return 60m + (ratio - 50m) / 50m * 20m;
            if (ratio >= 25m) return 40m + (ratio - 25m) / 25m * 20m;
            if (ratio >= 10m) return 20m + (ratio - 10m) / 15m * 20m;
            return ratio / 10m * 20m;
        }

        private static decimal RmdRatioToScore(decimal avgPct)
        {
            if (avgPct < 4m) return 100m;
            if (avgPct <= 5m) return 80m + (5m - avgPct) * 20m;
            if (avgPct <= 7m) return 60m + (7m - avgPct) / 2m * 20m;
            if (avgPct <= 10m) return 40m + (10m - avgPct) / 3m * 20m;
            if (avgPct <= 15m) return 20m + (15m - avgPct) / 5m * 20m;
            return 0m;
        }
    }
}
