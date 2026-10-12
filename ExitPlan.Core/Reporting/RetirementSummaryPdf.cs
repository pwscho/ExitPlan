using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ExitPlan.Core.Reporting
{
    public static class RetirementSummaryPdf
    {
        private static readonly string Navy = "#1F4E79";
        private static readonly string Teal = "#2E8A8A";
        private static readonly string Gold = "#C9962A";
        private static readonly string Muted = "#5F6B73";
        private static readonly string Line = "#D5DEE7";
        private static readonly string Panel = "#F4F7FA";
        private static readonly string TitleBar = "#1B3A5A";

        static RetirementSummaryPdf()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public static void Save(RetirementSummaryModel model, string path)
        {
            Create(model).GeneratePdf(path);
        }

        public static byte[] Generate(RetirementSummaryModel model)
        {
            return Create(model).GeneratePdf();
        }

        private static IDocument Create(RetirementSummaryModel model)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.MarginHorizontal(40);
                    page.MarginTop(28);
                    page.MarginBottom(32);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(t => t.FontSize(10).FontColor("#243542"));

                    page.Header().PaddingBottom(10).Element(c => ComposeHeader(c, model));
                    page.Content().Column(column =>
                    {
                        column.Spacing(10);
                        column.Item().Element(c => ComposeCoordinator(c, model));
                        column.Item().Background(Panel).Border(1).BorderColor(Line).Padding(10)
                            .Text(model.BottomLine).FontSize(10).FontColor("#1F3344");

                        column.Item().Element(c => ComposePortfolio(c, model));
                        column.Item().Element(c => ComposeSpending(c, model));
                        if (model.Guardrails != null)
                            column.Item().ShowEntire().Element(c => ComposeGuardrails(c, model.Guardrails));
                        column.Item().ShowEntire().Element(c => ComposeIncome(c, model));
                        column.Item().ShowEntire().Element(c => ComposeSocialSecurity(c, model));
                        column.Item().ShowEntire().Element(c => ComposeRothConversions(c, model));
                        column.Item().ShowEntire().Element(c => ComposeStressTests(c, model));
                        column.Item().ShowEntire().Element(c => ComposeTimeline(c, model));
                        column.Item().ShowEntire().Element(c => ComposePortfolioPath(c, model));
                        column.Item().ShowEntire().Element(c => ComposeOrdinaryBrackets(c, model));
                        column.Item().Element(c => ComposeReportCard(c, model));
                    });
                    page.Footer().Element(ComposeFooter);
                });
            });
        }

        private static void ComposeHeader(IContainer container, RetirementSummaryModel model)
        {
            container.Background(TitleBar).PaddingVertical(10).PaddingHorizontal(14).Row(row =>
            {
                row.RelativeItem().Column(text =>
                {
                    text.Item().Text("Retirement Plan Summary").FontSize(18).SemiBold().FontColor(Colors.White);
                    text.Item().Text($"{model.ScenarioName}  ·  {model.ProductTitle}")
                        .FontSize(9).FontColor("#D6E4F0");
                });
                row.ConstantItem(150).AlignRight().AlignMiddle().Text(model.GeneratedAt.ToString("MMMM d, yyyy"))
                    .FontSize(9).FontColor("#D6E4F0");
            });
        }

        private static void ComposeFooter(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Text("Figures come from this scenario’s Generate Report run. Not a guarantee of future results, and not financial, tax, or Social Security advice.")
                    .FontSize(8).FontColor(Muted);
                row.ConstantItem(90).AlignRight().Text(text =>
                {
                    text.Span("Page ").FontSize(8).FontColor(Muted);
                    text.CurrentPageNumber().FontSize(8).FontColor(Muted);
                    text.Span(" of ").FontSize(8).FontColor(Muted);
                    text.TotalPages().FontSize(8).FontColor(Muted);
                });
            });
        }

        private static void ComposeCoordinator(IContainer container, RetirementSummaryModel model)
        {
            container.Border(1).BorderColor(Line).Background(Panel).Padding(12).Row(row =>
            {
                row.RelativeItem().Element(c => CoordinatorCell(c, "Household", HouseholdLine(model)));
                row.ConstantItem(12);
                row.RelativeItem().Element(c => CoordinatorCell(c, "Plan start",
                    $"{model.PlanStartYear}\n{model.PlanYears} years through {model.PlanEndYear}"));
                row.ConstantItem(12);
                row.RelativeItem().Element(c => CoordinatorCell(c, "Longevity", LongevityLine(model)));
            });
        }

        private static void CoordinatorCell(IContainer container, string label, string value)
        {
            container.Column(col =>
            {
                col.Item().Text(label.ToUpperInvariant()).FontSize(8).FontColor(Teal).SemiBold();
                col.Item().PaddingTop(4).Text(value).FontSize(11).FontColor(Navy).SemiBold();
            });
        }

        private static string HouseholdLine(RetirementSummaryModel model)
        {
            if (model.Members.Count == 0)
                return "Owners not entered";
            return string.Join("\n", model.Members.Select(m => $"{m.Name}  ·  age {m.CurrentAge}"));
        }

        private static string LongevityLine(RetirementSummaryModel model)
        {
            if (model.Members.Count == 0)
                return "n/a";
            return string.Join("\n", model.Members.Select(m => $"{m.Name} to age {m.DeathAge} ({m.LongevityYear})"));
        }

        private static void ComposePortfolio(IContainer container, RetirementSummaryModel model)
        {
            container.Column(column =>
            {
                column.Item().Element(c => SectionTitle(c, $"Starting portfolio  ·  {RetirementSummaryFormat.CompactCurrency(model.StartingPortfolio)} ({model.PlanStartYear})"));

                if (model.PortfolioSlices.Count == 0)
                {
                    column.Item().Text("No positive account balances in this scenario.").FontColor(Muted);
                    return;
                }

                column.Item().Row(row =>
                {
                    row.ConstantItem(200).Height(152).Svg(RetirementSummaryCharts.DonutSvg(model.PortfolioSlices));
                    row.RelativeItem().PaddingLeft(8).AlignMiddle().Column(legend =>
                    {
                        foreach (var slice in model.PortfolioSlices)
                        {
                            legend.Item().PaddingBottom(6).Row(item =>
                            {
                                item.ConstantItem(10).Height(10).Background(slice.ColorHex);
                                item.RelativeItem().PaddingLeft(6).Text(
                                    $"{slice.Label}  ·  {RetirementSummaryFormat.Percent(slice.Share, 0)}  ·  {RetirementSummaryFormat.CompactCurrency(slice.Value)}")
                                    .FontSize(9);
                            });
                        }
                    });
                });

                column.Item().Element(c => ComposeAccountTable(c, model.Accounts));
            });
        }

        private static void ComposeAccountTable(IContainer container, IReadOnlyList<AccountLine> accounts)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.35f);
                    columns.RelativeColumn(1.05f);
                    columns.RelativeColumn(1.25f);
                    columns.RelativeColumn(0.7f);
                    columns.RelativeColumn(0.75f);
                    columns.RelativeColumn(0.95f);
                    columns.RelativeColumn(0.95f);
                });

                HeaderCell(table, "Owner");
                HeaderCell(table, "Asset class");
                HeaderCell(table, "Tax Status");
                HeaderCell(table, "Bucket");
                HeaderCell(table, "Priority");
                HeaderCell(table, "Rate of Return", alignRight: true);
                HeaderCell(table, "Value", alignRight: true);

                foreach (var account in accounts)
                {
                    BodyCell(table, account.Owner);
                    BodyCell(table, account.AssetClass);
                    BodyCell(table, account.TaxBucket);
                    BodyCell(table, BucketDisplay(account.Bucket));
                    BodyCell(table, account.Priority.ToString(CultureInfo.InvariantCulture));
                    BodyCell(table, RetirementSummaryFormat.Percent(account.RateOfReturn, 1), alignRight: true);
                    BodyCell(table, RetirementSummaryFormat.CompactCurrency(account.Value), alignRight: true);
                }
            });
        }

        private static string BucketDisplay(int bucket)
        {
            return bucket > 0 ? bucket.ToString(CultureInfo.InvariantCulture) : "n/a";
        }

        private static void ComposeSpending(IContainer container, RetirementSummaryModel model)
        {
            container.Column(column =>
            {
                column.Item().Element(c => SectionTitle(c, "Spending"));
                column.Item().Text(
                    $"Base need {RetirementSummaryFormat.CompactCurrency(model.BaseAnnualSpending)}/year  ·  {model.SpendingStrategy}  ·  inflation {RetirementSummaryFormat.Percent(model.InflationRate, 1)}")
                    .FontSize(10);

                if (model.SpendingExtras.Count == 0)
                {
                    column.Item().PaddingTop(4).Text("No extra spending streams in this scenario.").FontColor(Muted).FontSize(9);
                    return;
                }

                column.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.6f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(1.3f);
                        columns.RelativeColumn(1f);
                    });
                    HeaderCell(table, "Extra");
                    HeaderCell(table, "Owner");
                    HeaderCell(table, "Ages / years");
                    HeaderCell(table, "Amount / year", alignRight: true);

                    foreach (var extra in model.SpendingExtras)
                    {
                        BodyCell(table, extra.Name);
                        BodyCell(table, string.IsNullOrWhiteSpace(extra.Owner) ? "—" : extra.Owner);
                        BodyCell(table, StreamWindow(extra));
                        BodyCell(table, RetirementSummaryFormat.CompactCurrency(extra.AnnualAmount), alignRight: true);
                    }
                });
            });
        }

        private static void ComposeGuardrails(IContainer container, GuardrailSummary g)
        {
            container.Column(column =>
            {
                column.Item().Element(c => SectionTitle(c, "Guardrails"));
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2.2f);
                        columns.RelativeColumn(1.2f);
                    });
                    HeaderCell(table, "Setting");
                    HeaderCell(table, "Value", alignRight: true);

                    GuardrailRow(table, "Initial Portfolio Value", RetirementSummaryFormat.CompactCurrency(g.InitialPortfolioValue));
                    GuardrailRow(table, "Lower Trigger", RetirementSummaryFormat.PercentTrim(g.LowerTrigger));
                    GuardrailRow(table, "Inflation Cut", RetirementSummaryFormat.PercentTrim(g.LowerCut));
                    GuardrailRow(table, "Min Spending Floor", RetirementSummaryFormat.PercentTrim(g.MinSpendingFloor));
                    GuardrailRow(table, "Increase floor with inflation", g.InflateSpendingFloor ? "Yes" : "No");
                    GuardrailRow(table, "Increase baseline with inflation", g.IncreaseBaselineWithInflation ? "Yes" : "No");
                    GuardrailRow(table, "Upper Trigger", RetirementSummaryFormat.PercentTrim(g.UpperTrigger));
                    GuardrailRow(table, "Bonus Inflation", RetirementSummaryFormat.PercentTrim(g.UpperBonus));
                });
            });
        }

        private static void GuardrailRow(TableDescriptor table, string setting, string value)
        {
            BodyCell(table, setting);
            BodyCell(table, value, alignRight: true);
        }

        private static void ComposeIncome(IContainer container, RetirementSummaryModel model)
        {
            container.Column(column =>
            {
                column.Item().Element(c => SectionTitle(c, "Income"));
                if (model.IncomeStreams.Count == 0)
                {
                    column.Item().Text("None").FontColor(Muted).FontSize(9);
                    return;
                }

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.5f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(1.35f);
                        columns.RelativeColumn(1.05f);
                        columns.RelativeColumn(0.9f);
                    });
                    HeaderCell(table, "Name");
                    HeaderCell(table, "Owner");
                    HeaderCell(table, "Ages / years");
                    HeaderCell(table, "Amount / year", alignRight: true);
                    HeaderCell(table, "Taxable");

                    foreach (var line in model.IncomeStreams)
                    {
                        BodyCell(table, line.Name);
                        BodyCell(table, string.IsNullOrWhiteSpace(line.Owner) ? "—" : line.Owner);
                        BodyCell(table, StreamWindow(line));
                        BodyCell(table, RetirementSummaryFormat.CompactCurrency(line.AnnualAmount), alignRight: true);
                        BodyCell(table, line.IsTaxable ? "Yes" : "No");
                    }
                });
            });
        }

        private static void ComposeSocialSecurity(IContainer container, RetirementSummaryModel model)
        {
            container.Column(column =>
            {
                column.Item().Element(c => SectionTitle(c, "Social Security claim strategy"));
                if (model.SocialSecurity.Count == 0)
                {
                    column.Item().Text("No Social Security claimants in this scenario.").FontColor(Muted);
                    return;
                }

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.3f);
                        columns.RelativeColumn(0.8f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(1.1f);
                        columns.RelativeColumn(0.9f);
                        columns.RelativeColumn(0.8f);
                    });
                    HeaderCell(table, "Person");
                    HeaderCell(table, "Now");
                    HeaderCell(table, "Claim");
                    HeaderCell(table, "PIA / month");
                    HeaderCell(table, "COLA");
                    HeaderCell(table, "Longevity");

                    foreach (var ss in model.SocialSecurity)
                    {
                        BodyCell(table, ss.Name);
                        BodyCell(table, $"age {ss.CurrentAge}");
                        BodyCell(table, $"age {ss.ClaimAge} ({ss.ClaimYear})");
                        BodyCell(table, ss.MonthlyPia.ToString("C0", CultureInfo.CurrentCulture));
                        BodyCell(table, RetirementSummaryFormat.Percent(ss.Cola, 1));
                        BodyCell(table, $"age {ss.DeathAge}");
                    }
                });
            });
        }

        private static void ComposeStressTests(IContainer container, RetirementSummaryModel model)
        {
            container.Column(column =>
            {
                column.Item().Element(c => SectionTitle(c, "Stress-test success"));
                column.Item().Text("Success means the last simulated year still has a positive ending balance. Historical and Monte Carlo show n/a when that path was not run.")
                    .FontSize(8).FontColor(Muted);

                column.Item().PaddingTop(6).Height(180).Svg(
                    RetirementSummaryCharts.SuccessBarsSvg(model.Historical, model.MonteCarlo, model.Fixed));

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.3f);
                        columns.RelativeColumn(1f);
                        columns.RelativeColumn(1f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.2f);
                    });
                    HeaderCell(table, "Test");
                    HeaderCell(table, "Success");
                    HeaderCell(table, "Paths");
                    HeaderCell(table, "Median ending", alignRight: true);
                    HeaderCell(table, "10th / 90th", alignRight: true);
                    HeaderCell(table, "Avg spending", alignRight: true);

                    AddMetricRow(table, "Historical", model.Historical);
                    AddMetricRow(table, "Monte Carlo", model.MonteCarlo);
                    AddMetricRow(table, "Fixed market", model.Fixed);
                });
            });
        }

        private static void AddMetricRow(TableDescriptor table, string name, StressTestMetric? metric)
        {
            if (metric == null)
            {
                BodyCell(table, name);
                BodyCell(table, "n/a");
                BodyCell(table, "not run");
                BodyCell(table, "n/a", alignRight: true);
                BodyCell(table, "n/a", alignRight: true);
                BodyCell(table, "n/a", alignRight: true);
                return;
            }

            BodyCell(table, metric.Name);
            BodyCell(table, RetirementSummaryFormat.Percent(metric.SuccessRate, 0));
            BodyCell(table, RetirementSummaryFormat.PathCountLabel(metric));
            BodyCell(table, RetirementSummaryFormat.CompactCurrency(metric.MedianEnding), alignRight: true);
            BodyCell(table, $"{RetirementSummaryFormat.CompactCurrency(metric.TenthPercentileEnding)} / {RetirementSummaryFormat.CompactCurrency(metric.NinetiethPercentileEnding)}", alignRight: true);
            BodyCell(table, RetirementSummaryFormat.CompactCurrency(metric.AverageTotalSpending), alignRight: true);
        }

        private static void ComposeTimeline(IContainer container, RetirementSummaryModel model)
        {
            container.Column(column =>
            {
                bool hasExtras = model.SpendingByYear.Any(p => p.ExtraSpending > 0m) || model.SpendingExtras.Count > 0;
                string title = hasExtras
                    ? "Early years cost more — extras then fall away"
                    : "Base spending over the plan";
                column.Item().Element(c => SectionTitle(c, title));

                if (model.SpendingByYear.Count == 0)
                {
                    column.Item().Text("No year-by-year spending yet. Generate a report to plot the cash-need path.")
                        .FontColor(Muted);
                    return;
                }

                column.Item().Row(row =>
                {
                    row.AutoItem().Height(10).Width(14).Background(Navy);
                    row.AutoItem().PaddingLeft(4).PaddingRight(12).Text("Base (inflating)").FontSize(8).FontColor(Muted);
                    if (hasExtras)
                    {
                        row.AutoItem().Height(10).Width(14).Background(Gold);
                        row.AutoItem().PaddingLeft(4).Text("Temporary extras").FontSize(8).FontColor(Muted);
                    }
                });

                column.Item().Height(180).Svg(
                    RetirementSummaryCharts.SpendingTimelineSvg(model.SpendingByYear));
            });
        }

        private static void ComposePortfolioPath(IContainer container, RetirementSummaryModel model)
        {
            container.Column(column =>
            {
                bool guardrails = model.Guardrails != null;
                string title = guardrails
                    ? "Portfolio vs Guardrail Baseline"
                    : "Portfolio value over the plan";
                column.Item().Element(c => SectionTitle(c, title));

                if (model.PortfolioByYear.Count == 0)
                {
                    column.Item().Text("No year-by-year portfolio yet. Generate a report to plot the path.")
                        .FontColor(Muted);
                    return;
                }

                if (guardrails)
                {
                    column.Item().Text("Start-of-year portfolio as a percent of the guardrail baseline, with the lower and upper trigger lines.")
                        .FontSize(8).FontColor(Muted);
                    column.Item().PaddingTop(4).Row(row =>
                    {
                        row.AutoItem().Height(10).Width(14).Background(Navy);
                        row.AutoItem().PaddingLeft(4).PaddingRight(12).Text("Portfolio / baseline").FontSize(8).FontColor(Muted);
                        row.AutoItem().Height(10).Width(14).Background("#C45C5C");
                        row.AutoItem().PaddingLeft(4).PaddingRight(12).Text("Lower trigger").FontSize(8).FontColor(Muted);
                        row.AutoItem().Height(10).Width(14).Background(Teal);
                        row.AutoItem().PaddingLeft(4).Text("Upper trigger").FontSize(8).FontColor(Muted);
                    });
                    column.Item().Height(180).Svg(
                        RetirementSummaryCharts.GuardrailPortfolioSvg(model.PortfolioByYear, model.Guardrails!));
                }
                else
                {
                    column.Item().Text("Ending portfolio value each year of the fixed simulation.")
                        .FontSize(8).FontColor(Muted);
                    column.Item().PaddingTop(4).Row(row =>
                    {
                        row.AutoItem().Height(10).Width(14).Background(Navy);
                        row.AutoItem().PaddingLeft(4).Text("Portfolio").FontSize(8).FontColor(Muted);
                    });
                    column.Item().Height(180).Svg(
                        RetirementSummaryCharts.PortfolioPathSvg(model.PortfolioByYear));
                }
            });
        }

        private static void ComposeOrdinaryBrackets(IContainer container, RetirementSummaryModel model)
        {
            container.Column(column =>
            {
                column.Item().Element(c => SectionTitle(c, "Ordinary tax brackets"));

                if (model.OrdinaryBracketsByYear.Count == 0)
                {
                    column.Item().Text("No ordinary-bracket fill yet. Generate a report to plot the brackets.")
                        .FontColor(Muted);
                    return;
                }

                column.Item().Text("Each bar is that year’s ordinary taxable income stacked into the ordinary rates. Gray is remaining room before the next rate.")
                    .FontSize(8).FontColor(Muted);

                var rates = RetirementSummaryCharts.OrdinaryRatesIn(model.OrdinaryBracketsByYear);
                column.Item().PaddingTop(4).Row(row =>
                {
                    foreach (var rate in rates)
                    {
                        row.AutoItem().Height(10).Width(14).Background(RetirementSummaryCharts.ColorForOrdinaryRate(rate));
                        row.AutoItem().PaddingLeft(4).PaddingRight(10)
                            .Text($"{rate * 100m:0}%").FontSize(8).FontColor(Muted);
                    }

                    row.AutoItem().Height(10).Width(14).Background("#C5D0DA");
                    row.AutoItem().PaddingLeft(4).Text("Room to next rate").FontSize(8).FontColor(Muted);
                });

                column.Item().Height(180).Svg(
                    RetirementSummaryCharts.OrdinaryBracketFillSvg(model.OrdinaryBracketsByYear));
            });
        }

        private static void ComposeReportCard(IContainer container, RetirementSummaryModel model)
        {
            container.Column(column =>
            {
                column.Item().Element(c => SectionTitle(c, "Report card"));
                if (model.ReportCard == null || model.ReportCard.Metrics.Count == 0)
                {
                    column.Item().Text("Report card is n/a for this run.").FontColor(Muted);
                    column.Item().PaddingTop(4)
                        .Text($"Spending Power details  ·  {RetirementSummaryFormat.SpendingPowerAdjustments(model.SpendingRaises, model.SpendingCuts)}")
                        .FontSize(9).FontColor(Muted);
                    return;
                }

                column.Item().Text($"Overall retirement readiness  {model.ReportCard.OverallGrade}  ({model.ReportCard.OverallScoreDisplay})")
                    .FontSize(11).SemiBold().FontColor(Navy);

                column.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.6f);
                        columns.ConstantColumn(44);
                        columns.ConstantColumn(44);
                        columns.ConstantColumn(50);
                        columns.RelativeColumn(2.4f);
                    });
                    HeaderCell(table, "Metric");
                    HeaderCell(table, "Grade");
                    HeaderCell(table, "Score");
                    HeaderCell(table, "Weight");
                    HeaderCell(table, "Details");

                    foreach (var metric in model.ReportCard.Metrics)
                    {
                        BodyCell(table, metric.Name);
                        BodyCell(table, metric.Grade.ToString());
                        BodyCell(table, metric.ScoreDisplay);
                        BodyCell(table, $"{metric.Weight:0}%");
                        BodyCell(table, SpendingPowerDetails(model, metric));
                    }
                });
            });
        }

        private static string SpendingPowerDetails(RetirementSummaryModel model, ReportCardMetric metric)
        {
            if (!string.Equals(metric.Name, "Spending Power", StringComparison.OrdinalIgnoreCase))
                return metric.Detail ?? string.Empty;

            return RetirementSummaryFormat.AppendSpendingPowerAdjustments(metric.Detail, model.SpendingRaises, model.SpendingCuts);
        }

        private static void ComposeRothConversions(IContainer container, RetirementSummaryModel model)
        {
            container.Column(column =>
            {
                column.Item().Element(c => SectionTitle(c, "Roth Conversion Strategy"));
                if (model.RothConversions.Count == 0)
                {
                    column.Item().Text("None").FontColor(Muted).FontSize(9);
                    return;
                }

                column.Item().Element(c => StreamTable(c, model.RothConversions));
            });
        }

        private static void StreamTable(IContainer container, IReadOnlyList<NamedStreamLine> lines)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1.6f);
                    columns.RelativeColumn(1.1f);
                    columns.RelativeColumn(1.3f);
                    columns.RelativeColumn(1f);
                });
                HeaderCell(table, "Name");
                HeaderCell(table, "Owner");
                HeaderCell(table, "Ages / years");
                HeaderCell(table, "Amount / year", alignRight: true);
                foreach (var line in lines)
                {
                    BodyCell(table, line.Name);
                    BodyCell(table, string.IsNullOrWhiteSpace(line.Owner) ? "—" : line.Owner);
                    BodyCell(table, StreamWindow(line));
                    BodyCell(table, RetirementSummaryFormat.CompactCurrency(line.AnnualAmount), alignRight: true);
                }
            });
        }

        private static string StreamWindow(NamedStreamLine line)
        {
            string ages = line.StartAge == line.EndAge
                ? $"age {line.StartAge}"
                : $"ages {line.StartAge}–{line.EndAge}";
            if (line.StartYear <= 0)
                return ages;
            string years = line.StartYear == line.EndYear
                ? line.StartYear.ToString()
                : $"{line.StartYear}–{line.EndYear}";
            return $"{ages}  ·  {years}";
        }

        private static void SectionTitle(IContainer container, string title)
        {
            container.PaddingBottom(6).BorderBottom(1).BorderColor(Line)
                .Text(title).FontSize(13).SemiBold().FontColor(Navy);
        }

        private static void HeaderCell(TableDescriptor table, string text, bool alignRight = false)
        {
            var cell = table.Cell().Background("#E8EEF4").PaddingVertical(4).PaddingHorizontal(4);
            var label = alignRight ? cell.AlignRight().Text(text) : cell.Text(text);
            label.FontSize(8).SemiBold().FontColor(Navy);
        }

        private static void BodyCell(TableDescriptor table, string text, bool alignRight = false)
        {
            var cell = table.Cell().BorderBottom(0.5f).BorderColor(Line).PaddingVertical(3).PaddingHorizontal(4);
            var label = alignRight ? cell.AlignRight().Text(text) : cell.Text(text);
            label.FontSize(8);
        }
    }
}
