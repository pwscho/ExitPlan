using System;
using System.Collections.Generic;
using System.Linq;
using ExitPlan.Core.Simulation;
using ExitPlan.Core.Taxes;

namespace ExitPlan.Core.Reporting
{
    public static class RetirementSummaryBuilder
    {
        private static readonly string[] SlicePalette =
        {
            "#2E5A88",
            "#4A9B9B",
            "#E8B84A",
            "#7BA3C9",
            "#70AD47",
            "#C47A3A",
            "#8E6C9A",
            "#5C6B73"
        };

        public static RetirementSummaryModel Build(
            Scenario scenario,
            IReadOnlyList<Account> accounts,
            SocialSecuritySettings? primary,
            SocialSecuritySettings? spouse,
            IReadOnlyList<ResultRow> fixedResults,
            IReadOnlyList<ResultRow> historicalResults,
            IReadOnlyList<ResultRow> monteCarloResults,
            ReportCard? reportCard,
            DateTime? generatedAt = null)
        {
            scenario ??= new Scenario();
            accounts ??= Array.Empty<Account>();
            fixedResults ??= Array.Empty<ResultRow>();
            historicalResults ??= Array.Empty<ResultRow>();
            monteCarloResults ??= Array.Empty<ResultRow>();

            int startYear = scenario.BaseYear;
            int years = Math.Max(0, scenario.YearsToCalculate);
            var members = BuildMembers(primary, spouse, startYear);
            var accountLines = BuildAccountLines(accounts);
            decimal portfolio = accountLines.Sum(a => a.Value);
            var slices = BuildPortfolioSlices(accountLines, portfolio);
            var extras = BuildStreams(
                scenario.WithdrawalStreams,
                primary,
                spouse,
                startYear,
                s => true);
            var income = BuildStreams(
                scenario.IncomeStreams?.Select(i => new StreamSource(
                    i.Name, i.OwnerId, i.StartAge, i.EndAge, i.AnnualAmount, i.IsTaxable)),
                primary,
                spouse,
                startYear,
                s => true,
                includeZeroAmounts: true);
            var conversions = BuildStreams(
                scenario.RothConversions?.Select(r => new StreamSource(
                    r.Name, r.OwnerId, r.StartAge, r.EndAge, r.AnnualAmount, false)),
                primary,
                spouse,
                startYear,
                s => true,
                includeZeroAmounts: true);
            var spendingByYear = BuildSpendingByYear(fixedResults);
            var portfolioByYear = BuildPortfolioByYear(fixedResults);
            var ordinaryBracketsByYear = BuildOrdinaryBracketsByYear(fixedResults);
            var (spendingRaises, spendingCuts) = CountSpendingAdjustments(fixedResults);
            var socialSecurity = BuildSocialSecurity(primary, spouse, startYear);

            var historical = BuildStressTest("Historical", historicalResults);
            var monteCarlo = BuildStressTest("Monte Carlo", monteCarloResults);
            var fixedMetric = BuildStressTest("Fixed market", fixedResults);

            bool hasResults = fixedResults.Count > 0
                || historicalResults.Count > 0
                || monteCarloResults.Count > 0;

            return new RetirementSummaryModel
            {
                ScenarioName = string.IsNullOrWhiteSpace(scenario.Name) ? "Untitled scenario" : scenario.Name,
                ProductTitle = ProductInfo.Title,
                GeneratedAt = generatedAt ?? DateTime.Now,
                PlanStartYear = startYear,
                PlanYears = years,
                PlanEndYear = startYear + Math.Max(0, years - 1),
                Members = members,
                StartingPortfolio = portfolio,
                PortfolioSlices = slices,
                Accounts = accountLines,
                BaseAnnualSpending = scenario.AnnualSpending,
                SpendingStrategy = string.IsNullOrWhiteSpace(scenario.SpendingStrategy) ? "Fixed" : scenario.SpendingStrategy,
                InflationRate = scenario.InflationRate,
                SpendingExtras = extras,
                SpendingByYear = spendingByYear,
                PortfolioByYear = portfolioByYear,
                OrdinaryBracketsByYear = ordinaryBracketsByYear,
                SpendingRaises = spendingRaises,
                SpendingCuts = spendingCuts,
                IncomeStreams = income,
                RothConversions = conversions,
                SocialSecurity = socialSecurity,
                Guardrails = BuildGuardrails(scenario),
                Historical = historical,
                MonteCarlo = monteCarlo,
                Fixed = fixedMetric,
                ReportCard = reportCard,
                HasResults = hasResults,
                BottomLine = BuildBottomLine(
                    scenario,
                    members,
                    portfolio,
                    extras.Count,
                    historical,
                    monteCarlo,
                    fixedMetric,
                    hasResults)
            };
        }

        private static IReadOnlyList<HouseholdMemberSummary> BuildMembers(
            SocialSecuritySettings? primary,
            SocialSecuritySettings? spouse,
            int startYear)
        {
            var list = new List<HouseholdMemberSummary>();
            if (primary != null)
                list.Add(ToMember(primary, "Primary", startYear));
            if (spouse != null)
                list.Add(ToMember(spouse, "Spouse", startYear));
            return list;
        }

        private static HouseholdMemberSummary ToMember(SocialSecuritySettings settings, string role, int startYear)
        {
            int yearsToClaim = Math.Max(0, settings.ClaimAge - settings.CurrentAge);
            int yearsToLongevity = Math.Max(0, settings.DeathAge - settings.CurrentAge);
            return new HouseholdMemberSummary
            {
                Name = DisplayName(settings.Owner, role),
                Role = role,
                CurrentAge = settings.CurrentAge,
                ClaimAge = settings.ClaimAge,
                DeathAge = settings.DeathAge,
                ClaimYear = startYear + yearsToClaim,
                LongevityYear = startYear + yearsToLongevity
            };
        }

        private static GuardrailSummary? BuildGuardrails(Scenario scenario)
        {
            if (!string.Equals(scenario.SpendingStrategy, "Guardrails", StringComparison.OrdinalIgnoreCase))
                return null;

            var g = scenario.Guardrails ?? new GuardrailSettings();
            return new GuardrailSummary
            {
                InitialPortfolioValue = g.InitialPortfolioValue,
                LowerTrigger = g.LowerTrigger,
                LowerCut = g.LowerCut,
                MinSpendingFloor = g.MinSpendingFloor,
                InflateSpendingFloor = g.InflateSpendingFloor,
                IncreaseBaselineWithInflation = g.IncreaseBaselineWithInflation,
                UpperTrigger = g.UpperTrigger,
                UpperBonus = g.UpperBonus
            };
        }

        private static IReadOnlyList<AccountLine> BuildAccountLines(IReadOnlyList<Account> accounts)
        {
            return accounts
                .Where(a => a != null && a.Value != 0m)
                .Select(a => new AccountLine
                {
                    Owner = string.IsNullOrWhiteSpace(a.Owner) ? "Household" : a.Owner.Trim(),
                    TaxBucket = string.IsNullOrWhiteSpace(a.TaxBucket) ? "Unknown" : a.TaxBucket.Trim(),
                    AssetClass = string.IsNullOrWhiteSpace(a.AssetClass) ? "" : a.AssetClass.Trim(),
                    Name = a.Name?.Trim() ?? string.Empty,
                    Bucket = a.Bucket,
                    Priority = a.Priority,
                    RateOfReturn = a.RateOfReturn,
                    Value = a.Value
                })
                .OrderByDescending(a => a.Value)
                .ToList();
        }

        private static IReadOnlyList<PortfolioSlice> BuildPortfolioSlices(
            IReadOnlyList<AccountLine> accounts,
            decimal portfolio)
        {
            if (portfolio <= 0m || accounts.Count == 0)
                return Array.Empty<PortfolioSlice>();

            bool multipleOwners = accounts.Select(a => a.Owner).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;

            var grouped = accounts
                .GroupBy(a => multipleOwners ? $"{a.Owner} {BucketLabel(a.TaxBucket)}" : BucketLabel(a.TaxBucket), StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    Label = g.Key,
                    Value = g.Sum(x => x.Value)
                })
                .OrderByDescending(g => g.Value)
                .ToList();

            var slices = new List<PortfolioSlice>();
            decimal other = 0m;
            int colorIndex = 0;

            foreach (var group in grouped)
            {
                decimal share = group.Value / portfolio;
                if (share < 0.02m && grouped.Count > 4)
                {
                    other += group.Value;
                    continue;
                }

                slices.Add(new PortfolioSlice
                {
                    Label = group.Label,
                    Value = group.Value,
                    Share = share,
                    ColorHex = SlicePalette[colorIndex % SlicePalette.Length]
                });
                colorIndex++;
            }

            if (other > 0m)
            {
                slices.Add(new PortfolioSlice
                {
                    Label = "Other",
                    Value = other,
                    Share = other / portfolio,
                    ColorHex = SlicePalette[colorIndex % SlicePalette.Length]
                });
            }

            return slices;
        }

        private static string BucketLabel(string taxBucket)
        {
            if (taxBucket.Equals("Tax Deferred", StringComparison.OrdinalIgnoreCase) ||
                taxBucket.Equals("TaxDeferred", StringComparison.OrdinalIgnoreCase))
                return "tax-deferred";
            if (taxBucket.Equals("Tax Free", StringComparison.OrdinalIgnoreCase) ||
                taxBucket.Equals("TaxFree", StringComparison.OrdinalIgnoreCase))
                return "Roth / tax-free";
            if (taxBucket.Equals("Taxable", StringComparison.OrdinalIgnoreCase))
                return "taxable";
            return taxBucket;
        }

        private readonly record struct StreamSource(
            string Name,
            Guid OwnerId,
            int StartAge,
            int EndAge,
            decimal AnnualAmount,
            bool IsTaxable);

        private static IReadOnlyList<NamedStreamLine> BuildStreams(
            IEnumerable<WithdrawalStream>? streams,
            SocialSecuritySettings? primary,
            SocialSecuritySettings? spouse,
            int startYear,
            Func<StreamSource, bool> predicate)
        {
            return BuildStreams(
                streams?.Select(s => new StreamSource(s.Name, s.OwnerId, s.StartAge, s.EndAge, s.AnnualAmount, false)),
                primary,
                spouse,
                startYear,
                predicate);
        }

        private static IReadOnlyList<NamedStreamLine> BuildStreams(
            IEnumerable<StreamSource>? streams,
            SocialSecuritySettings? primary,
            SocialSecuritySettings? spouse,
            int startYear,
            Func<StreamSource, bool> predicate,
            bool includeZeroAmounts = false)
        {
            if (streams == null)
                return Array.Empty<NamedStreamLine>();

            return streams
                .Where(s => (includeZeroAmounts || s.AnnualAmount != 0m) && predicate(s))
                .Select(s =>
                {
                    var owner = FindOwner(s.OwnerId, primary, spouse);
                    int startOffset = owner == null ? 0 : Math.Max(0, s.StartAge - owner.CurrentAge);
                    int endOffset = owner == null ? 0 : Math.Max(startOffset, s.EndAge - owner.CurrentAge);
                    return new NamedStreamLine
                    {
                        Name = string.IsNullOrWhiteSpace(s.Name) ? "Untitled" : s.Name.Trim(),
                        Owner = owner == null ? string.Empty : DisplayName(owner.Owner, string.Empty),
                        StartAge = s.StartAge,
                        EndAge = s.EndAge,
                        AnnualAmount = s.AnnualAmount,
                        StartYear = owner == null ? 0 : startYear + startOffset,
                        EndYear = owner == null ? 0 : startYear + endOffset,
                        IsTaxable = s.IsTaxable
                    };
                })
                .OrderBy(s => s.StartYear)
                .ThenBy(s => s.Name)
                .ToList();
        }

        private static IReadOnlyList<SpendingYearPoint> BuildSpendingByYear(IReadOnlyList<ResultRow> fixedResults)
        {
            return OnePath(fixedResults)
                .Select(r => new SpendingYearPoint
                {
                    Year = r.Year,
                    BaseSpending = r.BaseSpending,
                    ExtraSpending = r.AddedSpending
                })
                .ToList();
        }

        private static IReadOnlyList<PortfolioYearPoint> BuildPortfolioByYear(IReadOnlyList<ResultRow> fixedResults)
        {
            return OnePath(fixedResults)
                .Select(r => new PortfolioYearPoint
                {
                    Year = r.Year,
                    StartingBalance = r.StartingBalance,
                    EndingBalance = r.EndingBalance,
                    GuardrailPortfolioPercent = r.GuardrailPortfolioPercent
                })
                .ToList();
        }

        private static IReadOnlyList<OrdinaryBracketYearPoint> BuildOrdinaryBracketsByYear(IReadOnlyList<ResultRow> fixedResults)
        {
            return OnePath(fixedResults)
                .Select(r => new OrdinaryBracketYearPoint
                {
                    Year = r.Year,
                    Headroom = r.OrdinaryBracketHeadroom,
                    Fills = (r.OrdinaryBracketFills ?? new List<OrdinaryBracketFill>()).ToList()
                })
                .ToList();
        }

        /// <summary>
        /// Count guardrail raise vs cut years on the fixed path.
        /// A floor hit counts as a cut (spending was reduced to the floor).
        /// </summary>
        private static (int Raises, int Cuts) CountSpendingAdjustments(IReadOnlyList<ResultRow> fixedResults)
        {
            int raises = 0;
            int cuts = 0;
            foreach (var row in OnePath(fixedResults))
            {
                switch (row.GuardrailAction)
                {
                    case GuardrailYearAction.Raise:
                        raises++;
                        break;
                    case GuardrailYearAction.Cut:
                    case GuardrailYearAction.FloorHit:
                        cuts++;
                        break;
                }
            }

            return (raises, cuts);
        }

        private static IReadOnlyList<ResultRow> OnePath(IReadOnlyList<ResultRow> rows)
        {
            if (rows == null || rows.Count == 0)
                return Array.Empty<ResultRow>();

            int sim = rows[0].SimNo;
            return rows
                .Where(r => r.SimNo == sim)
                .OrderBy(r => r.Year)
                .ThenBy(r => r.Iteration)
                .ToList();
        }

        private static IReadOnlyList<SocialSecurityClaimLine> BuildSocialSecurity(
            SocialSecuritySettings? primary,
            SocialSecuritySettings? spouse,
            int startYear)
        {
            var list = new List<SocialSecurityClaimLine>();
            if (primary != null)
                list.Add(ToClaim(primary, startYear));
            if (spouse != null)
                list.Add(ToClaim(spouse, startYear));
            return list;
        }

        private static SocialSecurityClaimLine ToClaim(SocialSecuritySettings settings, int startYear)
        {
            int yearsToClaim = Math.Max(0, settings.ClaimAge - settings.CurrentAge);
            return new SocialSecurityClaimLine
            {
                Name = DisplayName(settings.Owner, "Member"),
                CurrentAge = settings.CurrentAge,
                ClaimAge = settings.ClaimAge,
                ClaimYear = startYear + yearsToClaim,
                MonthlyPia = settings.BenefitAtFullRetirementAge,
                Cola = settings.Cola,
                DeathAge = settings.DeathAge
            };
        }

        private static StressTestMetric? BuildStressTest(string name, IReadOnlyList<ResultRow> rows)
        {
            if (rows == null || rows.Count == 0)
                return null;

            var sims = rows.GroupBy(r => r.SimNo).ToList();
            if (sims.Count == 0)
                return null;

            var endings = sims
                .Select(sim => sim.OrderBy(r => r.Iteration).Last().EndingBalance)
                .OrderBy(v => v)
                .ToList();

            int survived = sims.Count(sim => SimulationSuccess.IsSuccessful(sim));
            decimal spending = sims
                .Select(sim => sim.Sum(r => r.Withdrawal))
                .Average();

            return new StressTestMetric
            {
                Name = name,
                SuccessRate = (decimal)survived / sims.Count,
                PathCount = sims.Count,
                AverageEnding = endings.Average(),
                MedianEnding = Percentile(endings, 0.50m),
                TenthPercentileEnding = Percentile(endings, 0.10m),
                NinetiethPercentileEnding = Percentile(endings, 0.90m),
                AverageTotalSpending = spending
            };
        }

        private static decimal Percentile(List<decimal> sorted, decimal percentile)
        {
            if (sorted.Count == 0)
                return 0m;
            int index = (int)Math.Floor(percentile * (sorted.Count - 1));
            index = Math.Clamp(index, 0, sorted.Count - 1);
            return sorted[index];
        }

        private static string BuildBottomLine(
            Scenario scenario,
            IReadOnlyList<HouseholdMemberSummary> members,
            decimal portfolio,
            int extraCount,
            StressTestMetric? historical,
            StressTestMetric? monteCarlo,
            StressTestMetric? fixedMetric,
            bool hasResults)
        {
            string who = members.Count == 0
                ? "This household"
                : members.Count == 1
                    ? members[0].Name
                    : $"{members[0].Name} and {members[1].Name}";

            if (!hasResults)
            {
                return $"{who} start in {scenario.BaseYear} with a {RetirementSummaryFormat.CompactCurrency(portfolio)} portfolio. Generate a report to fill stress-test results for this scenario.";
            }

            string extras = extraCount == 0
                ? "with no extra spending streams"
                : extraCount == 1
                    ? "plus 1 extra spending stream"
                    : $"plus {extraCount} extra spending streams";

            var tests = new List<string>();
            AppendTest(tests, historical);
            AppendTest(tests, monteCarlo);
            AppendTest(tests, fixedMetric);
            string stress = tests.Count == 0
                ? "Stress-test success rates are n/a because those paths were not run."
                : "Stress-test success: " + string.Join("; ", tests) + ".";

            return $"{who} start in {scenario.BaseYear} with a {RetirementSummaryFormat.CompactCurrency(portfolio)} portfolio, funding {RetirementSummaryFormat.CompactCurrency(scenario.AnnualSpending)}/year ({scenario.SpendingStrategy}, {RetirementSummaryFormat.Percent(scenario.InflationRate, 1)} inflation) {extras}. {stress}";
        }

        private static void AppendTest(List<string> tests, StressTestMetric? metric)
        {
            if (metric == null)
                return;
            tests.Add($"{metric.Name.ToLowerInvariant()} {RetirementSummaryFormat.Percent(metric.SuccessRate, 0)} ({RetirementSummaryFormat.PathCountLabel(metric)})");
        }

        private static SocialSecuritySettings? FindOwner(
            Guid ownerId,
            SocialSecuritySettings? primary,
            SocialSecuritySettings? spouse)
        {
            if (primary != null && primary.UserId == ownerId)
                return primary;
            if (spouse != null && spouse.UserId == ownerId)
                return spouse;
            return null;
        }

        private static string DisplayName(string? owner, string fallback)
        {
            return string.IsNullOrWhiteSpace(owner) ? fallback : owner.Trim();
        }
    }
}
