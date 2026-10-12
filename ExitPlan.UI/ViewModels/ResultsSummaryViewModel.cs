using ClosedXML.Excel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using Microsoft.Win32;
using ExitPlan.Core;
using ExitPlan.Core.Reporting;
using ExitPlan.Core.Simulation;
using ExitPlan.Core.Taxes;
using ExitPlan.UI.Reporting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ExitPlan.UI.ViewModels
{
    public class ResultsSummaryViewModel : ViewModelBase
    {
        // ------------------------------------------------------------
        // Raw Data
        // ------------------------------------------------------------
        public Scenario Scenario { get; }

        public List<Account> Accounts { get; }
        public SocialSecuritySettings? Primary { get; }
        public SocialSecuritySettings? Spouse { get; }

        public List<ResultRow> FixedResults { get; }
        public List<ResultRow> HistoricalResults { get; }
        public List<ResultRow> MonteCarloResults { get; }

        // ------------------------------------------------------------
        // Bindable Lists (for DataGrids)
        // ------------------------------------------------------------
        public List<ResultRow> ListOfFixedResults => FixedResults;
        public List<ResultRow> ListOfHistoricalResults => HistoricalResults;
        public List<ResultRow> ListOfMonteCarloResults => MonteCarloResults;

        // ------------------------------------------------------------
        // Charts (LiveChartsCore v2)
        // ------------------------------------------------------------
        public ObservableCollection<ISeries> MonteCarloSeries { get; set; }
        public List<string> MonteCarloLabels { get; set; }

        public ObservableCollection<ISeries> PercentileSeries { get; set; }
        public List<string> TrajectoryYears { get; set; }
        public Func<double, string> CurrencyFormatter { get; set; }

        public List<HistogramBin> MonteCarloEndingBalanceHistogram { get; }

        public ObservableCollection<ISeries> StackedIncomeSeries { get; set; }
        public List<string> StackedIncomeLabels { get; set; }

        public ObservableCollection<ISeries> StackedSpendingSeries { get; set; }
        public List<string> StackedSpendingLabels { get; set; }

        public ObservableCollection<ISeries> StackedSpendingSourcesSeries { get; set; }
        public List<string> StackedSpendingSourcesLabels { get; set; }

        public ObservableCollection<ISeries> HistoricalEndingSeries { get; set; }
        public List<string> HistoricalEndingLabels { get; set; }
        public Func<double, string> DollarFormatter { get; set; }

        public ObservableCollection<ISeries> FailureByYearSeries { get; set; }
        public List<string> FailureByYearLabels { get; set; }

        public bool ShowGuardrailCharts { get; }
        public ObservableCollection<ISeries> GuardrailSpendingSeries { get; set; }
        public ObservableCollection<Axis> GuardrailSpendingAxesX { get; set; }
        public ObservableCollection<Axis> GuardrailSpendingAxesY { get; set; }
        public ObservableCollection<ISeries> GuardrailPortfolioSeries { get; set; }
        public ObservableCollection<Axis> GuardrailPortfolioAxesX { get; set; }
        public ObservableCollection<Axis> GuardrailPortfolioAxesY { get; set; }
        public List<string> GuardrailYearLabels { get; set; }

        public ObservableCollection<ISeries> OrdinaryBracketFillSeries { get; set; }
        public ObservableCollection<Axis> OrdinaryBracketFillAxesX { get; set; }
        public ObservableCollection<Axis> OrdinaryBracketFillAxesY { get; set; }
        public List<string> OrdinaryBracketYearLabels { get; set; }

        // ------------------------------------------------------------
        // Axis collections (for XAML binding)
        // ------------------------------------------------------------
        public ObservableCollection<Axis> HistogramAxesX { get; set; }
        public ObservableCollection<Axis> HistogramAxesY { get; set; }
        public ObservableCollection<Axis> PercentileAxesX { get; set; }
        public ObservableCollection<Axis> PercentileAxesY { get; set; }
        public ObservableCollection<Axis> FailureAxesX { get; set; }
        public ObservableCollection<Axis> FailureAxesY { get; set; }
        public ObservableCollection<Axis> StackedIncomeAxesX { get; set; }
        public ObservableCollection<Axis> StackedIncomeAxesY { get; set; }
        public ObservableCollection<Axis> StackedSpendingAxesX { get; set; }
        public ObservableCollection<Axis> StackedSpendingAxesY { get; set; }
        public ObservableCollection<Axis> StackedSpendingSourcesAxesX { get; set; }
        public ObservableCollection<Axis> StackedSpendingSourcesAxesY { get; set; }
        public ObservableCollection<Axis> HistoricalEndingAxesX { get; set; }
        public ObservableCollection<Axis> HistoricalEndingAxesY { get; set; }

        // ------------------------------------------------------------
        // Summary Statistics
        // ------------------------------------------------------------
        public decimal FixedSuccessRate { get; }
        public decimal SequenceSuccessRate { get; }
        public decimal RandomSuccessRate { get; }

        public decimal FixedAvgBalance { get; }
        public decimal SequenceAvgBalance { get; }
        public decimal RandomAvgBalance { get; }

        public decimal FixedMedBalance { get; }
        public decimal SequenceMedBalance { get; }
        public decimal RandomMedBalance { get; }

        public decimal Sequence10thPercentile { get; }
        public decimal Sequence90thPercentile { get; }

        public decimal Random10thPercentile { get; }
        public decimal Random90thPercentile { get; }

        public decimal FixedTaxesPaid { get; }
        public decimal SequenceTaxesPaid { get; }
        public decimal RandomTaxesPaid { get; }

        public decimal FixedTotalSpending { get; }
        public decimal HistoricalAvgTotalSpending { get; }
        public decimal MonteCarloAvgTotalSpending { get; }

        public ICommand ExportToExcelCommand { get; }
        public ICommand PrintSummaryCommand { get; }

        public bool HasReport =>
            (FixedResults?.Count ?? 0) > 0
            || (HistoricalResults?.Count ?? 0) > 0
            || (MonteCarloResults?.Count ?? 0) > 0;

        // ------------------------------------------------------------
        // Report Card
        // ------------------------------------------------------------
        public ExitPlan.Core.Reporting.ReportCard? ReportCard { get; set; }

        // ------------------------------------------------------------
        // Color palette
        // ------------------------------------------------------------
        private static readonly SKColor SteelBlue = new SKColor(0x46, 0x82, 0xB4);
        private static readonly SKColor ForestGreen = new SKColor(0x22, 0x8B, 0x22);
        private static readonly SKColor Orange = new SKColor(0xFF, 0xA5, 0x00);
        private static readonly SKColor IndianRed = new SKColor(0xCD, 0x5C, 0x5C);
        private static readonly SKColor DarkBlue = new SKColor(0x00, 0x00, 0x8B);
        private static readonly SKColor LightSteelBlue = new SKColor(0xB0, 0xC4, 0xDE);
        private static readonly SKColor Bracket10 = new SKColor(0x2E, 0x8B, 0x57);
        private static readonly SKColor Bracket12 = new SKColor(0x3C, 0xB3, 0x71);
        private static readonly SKColor Bracket22 = new SKColor(0xDA, 0xA5, 0x20);
        private static readonly SKColor Bracket24 = new SKColor(0xFF, 0x8C, 0x00);
        private static readonly SKColor Bracket32 = new SKColor(0xE0, 0x65, 0x00);
        private static readonly SKColor Bracket35 = new SKColor(0xCD, 0x5C, 0x5C);
        private static readonly SKColor Bracket37 = new SKColor(0x8B, 0x00, 0x00);
        private static readonly SKColor BracketRoom = new SKColor(0xD3, 0xD3, 0xD3);

        // ------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------
        public ResultsSummaryViewModel(ReportData data)
        {
            Scenario = data.Scenario;
            Accounts = data.Accounts ?? new List<Account>();
            Primary = data.Primary;
            Spouse = data.Spouse;

            FixedResults = data.FixedResults;
            HistoricalResults = data.HistoricalResults;
            MonteCarloResults = data.MonteCarloResults;

            // Report Card — keep overall grade in sync and persist weight edits on the scenario
            ReportCard = data.ReportCard;
            if (ReportCard != null)
            {
                ReportCard.ListenForWeightEdits();
                ReportCard.WeightsChanged += (_, _) => PersistReportCardWeights();
            }

            // Summary Stats
            FixedSuccessRate = ComputeSuccessRate(FixedResults);
            SequenceSuccessRate = ComputeSuccessRate(HistoricalResults);
            RandomSuccessRate = ComputeSuccessRate(MonteCarloResults);

            FixedAvgBalance = ComputeAverageEndingBalance(FixedResults);
            SequenceAvgBalance = ComputeAverageEndingBalance(HistoricalResults);
            RandomAvgBalance = ComputeAverageEndingBalance(MonteCarloResults);

            FixedMedBalance = ComputeMedianEndingBalance(FixedResults);
            SequenceMedBalance = ComputeMedianEndingBalance(HistoricalResults);
            RandomMedBalance = ComputeMedianEndingBalance(MonteCarloResults);

            Sequence10thPercentile = ComputePercentile(HistoricalResults, 0.10m);
            Sequence90thPercentile = ComputePercentile(HistoricalResults, 0.90m);

            Random10thPercentile = ComputePercentile(MonteCarloResults, 0.10m);
            Random90thPercentile = ComputePercentile(MonteCarloResults, 0.90m);

            FixedTaxesPaid = ComputeAverageTaxes(FixedResults);
            SequenceTaxesPaid = ComputeAverageTaxes(HistoricalResults);
            RandomTaxesPaid = ComputeAverageTaxes(MonteCarloResults);

            FixedTotalSpending = ComputeAverageTotalSpending(FixedResults);
            HistoricalAvgTotalSpending = ComputeAverageTotalSpending(HistoricalResults);
            MonteCarloAvgTotalSpending = ComputeAverageTotalSpending(MonteCarloResults);

            // Monte Carlo Histogram (P10–P90; long tails omitted)
            var endings = MonteCarloResults
                .GroupBy(r => r.SimNo)
                .Select(sim => sim.Last().EndingBalance)
                .ToList();

            var clippedEndings = EndingBalanceHistogramBuilder.ClipToInclusivePercentileRange(endings, 0.10m, 0.90m);

            // Dynamic bin count using Sturges' rule: ceil(log2(n) + 1)
            var binCount = (int)Math.Ceiling(Math.Log10(Math.Max(1, endings.Count)) / Math.Log10(2)) + 1;
            binCount = Math.Max(50, Math.Min(binCount, 100)); // clamp between 50 and 100

            var histogramBuilder = new EndingBalanceHistogramBuilder();
            MonteCarloEndingBalanceHistogram = histogramBuilder.Build(clippedEndings, binCount: binCount);
            BuildLiveChartsHistogram(MonteCarloEndingBalanceHistogram);

            // Percentile Trajectory Chart
            BuildPercentileTrajectoryChart(MonteCarloResults);

            // Stacked charts
            BuildStackedIncomeChart();
            BuildStackedSpendingChart();
            BuildStackedSpendingSourcesChart();

            // Historical Bar Chart
            BuildHistoricalEndingBalanceChart();

            // Failure By Year Chart
            BuildFailureByYearChart();

            ShowGuardrailCharts = string.Equals(
                Scenario?.SpendingStrategy,
                "Guardrails",
                StringComparison.OrdinalIgnoreCase);
            BuildGuardrailCharts();
            BuildOrdinaryBracketFillChart();

            // Excel Export Command
            ExportToExcelCommand = new RelayCommand(ExportToExcel);
            PrintSummaryCommand = new RelayCommand(PrintSummary);
        }

        // ------------------------------------------------------------
        // Helper Methods
        // ------------------------------------------------------------
        private decimal ComputeSuccessRate(List<ResultRow> list)
        {
            if (list == null || list.Count == 0)
                return 0;

            var sims = list.GroupBy(r => r.SimNo);

            int total = sims.Count();
            int survived = sims.Count(sim => SimulationSuccess.IsSuccessful(sim));

            return total == 0 ? 0 : (decimal)survived / total;
        }

        private decimal ComputeAverageEndingBalance(List<ResultRow> list)
        {
            if (list == null || list.Count == 0)
                return 0;

            return list
                .GroupBy(r => r.SimNo)
                .Select(sim => sim.Last().EndingBalance)
                .Average();
        }

        private decimal ComputeMedianEndingBalance(List<ResultRow> list)
        {
            if (list == null || list.Count == 0)
                return 0;

            var endings = list
                .GroupBy(r => r.SimNo)
                .Select(sim => sim.Last().EndingBalance)
                .OrderBy(x => x)
                .ToList();

            int count = endings.Count;
            if (count == 0)
                return 0;

            if (count % 2 == 1)
                return endings[count / 2];

            return (endings[count / 2] + endings[count / 2 - 1]) / 2m;
        }

        private decimal ComputePercentile(List<ResultRow> list, decimal percentile)
        {
            if (list == null || list.Count == 0)
                return 0;

            var endings = list
                .GroupBy(r => r.SimNo)
                .Select(sim => sim.Last().EndingBalance)
                .OrderBy(x => x)
                .ToList();

            if (endings.Count == 0)
                return 0;

            int index = (int)Math.Floor(percentile * (endings.Count - 1));
            return endings[index];
        }

        private decimal ComputeAverageTaxes(List<ResultRow> list)
        {
            if (list == null || list.Count == 0)
                return 0;

            return list
                .GroupBy(r => r.SimNo)
                .Select(sim => sim.Sum(x => x.TaxesPaid))
                .Average();
        }

        private decimal ComputeAverageTotalSpending(List<ResultRow> list)
        {
            if (list == null || list.Count == 0)
                return 0;

            return list
                .GroupBy(r => r.SimNo)
                .Select(sim =>
                    sim.Sum(x =>
                        x.Withdrawal
                        ))
                .Average();
        }

        // ------------------------------------------------------------
        // Histogram (LiveChartsCore v2)
        // ------------------------------------------------------------
        private void BuildLiveChartsHistogram(List<HistogramBin> bins)
        {
            
            if (bins == null || bins.Count == 0)
            {
                MonteCarloSeries = new ObservableCollection<ISeries>();
                MonteCarloLabels = new List<string>();
                return;
            }

            var counts = bins.Select(b => (double)b.Count).ToArray();

            MonteCarloLabels = bins
                .Select(b => b.BinStart.ToString("C0"))
                .ToList();

            MonteCarloSeries = new ObservableCollection<ISeries>
            {
                new ColumnSeries<double>
                {
                    Values = counts.ToList(),
                    Fill = new SolidColorPaint { Color = SteelBlue },
                    Stroke = null,
                    MaxBarWidth = 20
                }
            };

            // Axes
            HistogramAxesX = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Labels = MonteCarloLabels,
                    LabelsRotation = 45,
                }
            };
            HistogramAxesY = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Labeler = value => value.ToString("N0"),
                }
            };
        }

        // ------------------------------------------------------------
        // Percentile Trajectory Chart (LiveChartsCore v2)
        // ------------------------------------------------------------
        private void BuildPercentileTrajectoryChart(List<ResultRow> monteCarloResults)
        {
            var sims = monteCarloResults
                .GroupBy(r => r.SimNo)
                .OrderBy(g => g.Key)
                .ToList();

            int yearCount = sims.First().Count();

            var matrix = sims
                .Select(sim => sim.OrderBy(r => r.Year)
                .Select(r => r.EndingBalance)
                .ToList())
                .ToList();

            var p10 = new List<double>();
            var p50 = new List<double>();
            var p90 = new List<double>();

            for (int year = 0; year < yearCount; year++)
            {
                var values = matrix.Select(row => row[year]).OrderBy(v => v).ToList();
                int count = values.Count;

                p10.Add((double)values[(int)(0.10m * (count - 1))]);
                p50.Add((double)values[(int)(0.50m * (count - 1))]);
                p90.Add((double)values[(int)(0.90m * (count - 1))]);
            }

            TrajectoryYears = sims.First()
                .OrderBy(r => r.Year)
                .Select(r => r.Year.ToString())
                .ToList();

            PercentileSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<double>
                {
                    Values = p10,
                    GeometrySize = 3,
                    Stroke = new SolidColorPaint { Color = IndianRed, StrokeThickness = 3 },
                    Fill = null,
                },
                new LineSeries<double>
                {
                    Values = p50,
                    GeometrySize = 3,
                    Stroke = new SolidColorPaint { Color = DarkBlue, StrokeThickness = 3 },
                    Fill = null,
                },
                new LineSeries<double>
                {
                    Values = p90,
                    GeometrySize = 3,
                    Stroke = new SolidColorPaint { Color = ForestGreen, StrokeThickness = 3 },
                    Fill = null,
                }
            };

            CurrencyFormatter = value => value.ToString("C0");

            // Axes with labelers
            var startYear = TrajectoryYears.FirstOrDefault()?.Replace("Year ", "");
            PercentileAxesX = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Labels = TrajectoryYears,
                    LabelsRotation = 0,
                }
            };
            PercentileAxesY = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Labeler = value => value.ToString("C0"),
                }
            };
        }

        // ------------------------------------------------------------
        // Stacked Income Chart (LiveChartsCore v2)
        // ------------------------------------------------------------
        private void BuildStackedIncomeChart()
        {
            var grouped = FixedResults
                .GroupBy(r => r.Year)
                .OrderBy(g => g.Key)
                .ToList();

            StackedIncomeLabels = grouped
                .Select(g => g.Key.ToString())
                .ToList();

            var income = grouped
                .Select(g => g.Sum(r => (double)r.Income))
                .ToList();

            var investmentReturn = grouped
                .Select(g => g.Sum(r => (double)r.InvestmentReturn))
                .ToList();

            var socialSecurity = grouped
                .Select(g => g.Sum(r => (double)r.SocialSecurityIncome))
                .ToList();

            StackedIncomeSeries = new ObservableCollection<ISeries>
            {
                new StackedAreaSeries<double>
                {
                    Name = "Income",
                    Values = income,
                    Fill = new SolidColorPaint { Color = SteelBlue },

                },
                new StackedAreaSeries<double>
                {
                    Name = "Investment Return",
                    Values = investmentReturn,
                    Fill = new SolidColorPaint { Color = ForestGreen },

                },
                new StackedAreaSeries<double>
                {
                    Name = "Social Security",
                    Values = socialSecurity,
                    Fill = new SolidColorPaint { Color = Orange },

                }
            };

            StackedIncomeAxesX = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Labels = StackedIncomeLabels,
                }
            };
            StackedIncomeAxesY = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Labeler = value => value.ToString("C0"),
                }
            };
        }

        // ------------------------------------------------------------
        // Stacked Spending Chart (LiveChartsCore v2)
        // ------------------------------------------------------------
        private void BuildStackedSpendingChart()
        {
            var grouped = FixedResults
                .GroupBy(r => r.Year)
                .OrderBy(g => g.Key)
                .ToList();

            StackedSpendingLabels = grouped
                .Select(g => g.Key.ToString())
                .ToList();

            var withdrawals = grouped
                .Select(g => g.Sum(r => (double)r.Withdrawal))
                .ToList();

            var taxesPaid = grouped
                .Select(g => g.Sum(r => (double)(r.TaxesPaid - r.IrmaaPaid)))
                .ToList();

            var irmaaPaid = grouped
                .Select(g => g.Sum(r => (double)r.IrmaaPaid))
                .ToList();

            StackedSpendingSeries = new ObservableCollection<ISeries>
            {
                new StackedAreaSeries<double>
                {
                    Name = "Withdrawals",
                    Values = withdrawals,
                    Fill = new SolidColorPaint { Color = SteelBlue },

                },
                new StackedAreaSeries<double>
                {
                    Name = "Taxes Paid",
                    Values = taxesPaid,
                    Fill = new SolidColorPaint { Color = ForestGreen },

                },
                new StackedAreaSeries<double>
                {
                    Name  = "IRMAA Paid",
                    Values = irmaaPaid,
                    Fill = new SolidColorPaint { Color = Orange },

                }
            };

            StackedSpendingAxesX = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Labels = StackedSpendingLabels,
                }
            };
            StackedSpendingAxesY = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Labeler = value => value.ToString("C0"),
                }
            };
        }

        // ------------------------------------------------------------
        // Stacked Spending Sources Chart (LiveChartsCore v2)
        // ------------------------------------------------------------
        private void BuildStackedSpendingSourcesChart()
        {
            var grouped = FixedResults
                .GroupBy(r => r.Year)
                .OrderBy(g => g.Key)
                .ToList();

            StackedSpendingSourcesLabels = grouped
                .Select(g => g.Key.ToString())
                .ToList();

            var withdrawals = grouped
                .Select(g => g.Sum(r =>
                    (double)r.Withdrawal +
                    (double)r.TaxesPaid -
                    (double)r.SocialSecurityIncome -
                    (double)r.Income))
                .ToList();

            var social = grouped
                .Select(g => g.Sum(r => (double)(r.SocialSecurityIncome)))
                .ToList();

            var income = grouped
                .Select(g => g.Sum(r => (double)r.Income))
                .ToList();

            StackedSpendingSourcesSeries = new ObservableCollection<ISeries>
            {
                new StackedAreaSeries<double>
                {
                    Name = "Income",
                    Values = income,
                    Fill = new SolidColorPaint { Color = SteelBlue },

                },
                new StackedAreaSeries<double>
                {
                    Name = "Social Security",   
                    Values = social,
                    Fill = new SolidColorPaint { Color = ForestGreen },

                },
                new StackedAreaSeries<double>
                {
                    Name = "Withdrawals",
                    Values = withdrawals,
                    Fill = new SolidColorPaint { Color = Orange },

                }
            };

            StackedSpendingSourcesAxesX = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Labels = StackedSpendingSourcesLabels,
                }
            };
            StackedSpendingSourcesAxesY = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Labeler = value => value.ToString("C0"),
                }
            };
        }

        // ------------------------------------------------------------
        // Guardrail charts (fixed simulation)
        // ------------------------------------------------------------
        private void BuildGuardrailCharts()
        {
            GuardrailYearLabels = new List<string>();
            GuardrailSpendingSeries = new ObservableCollection<ISeries>();
            GuardrailPortfolioSeries = new ObservableCollection<ISeries>();
            GuardrailSpendingAxesX = new ObservableCollection<Axis>();
            GuardrailSpendingAxesY = new ObservableCollection<Axis>();
            GuardrailPortfolioAxesX = new ObservableCollection<Axis>();
            GuardrailPortfolioAxesY = new ObservableCollection<Axis>();

            if (FixedResults == null || FixedResults.Count == 0)
                return;

            var rows = FixedResults
                .OrderBy(r => r.Year)
                .ToList();

            GuardrailYearLabels = rows
                .Select(r => r.Year.ToString())
                .ToList();

            var inflationOnly = rows
                .Select(r => (double)r.InflationOnlyBaseSpending)
                .ToList();
            var actual = rows
                .Select(r => (double)r.BaseSpending)
                .ToList();
            var portfolioPct = rows
                .Select(r => (double)r.GuardrailPortfolioPercent)
                .ToList();

            decimal lower = Scenario?.Guardrails?.LowerTrigger ?? 0.90m;
            decimal upper = Scenario?.Guardrails?.UpperTrigger ?? 1.10m;
            var lowerLine = Enumerable.Repeat((double)lower, rows.Count).ToList();
            var upperLine = Enumerable.Repeat((double)upper, rows.Count).ToList();

            GuardrailSpendingSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<double>
                {
                    Name = "Inflation only",
                    Values = inflationOnly,
                    GeometrySize = 4,
                    Stroke = new SolidColorPaint { Color = SteelBlue, StrokeThickness = 3 },
                    Fill = null,
                },
                new LineSeries<double>
                {
                    Name = "Actual",
                    Values = actual,
                    GeometrySize = 4,
                    Stroke = new SolidColorPaint { Color = IndianRed, StrokeThickness = 3 },
                    Fill = null,
                }
            };

            GuardrailSpendingAxesX = new ObservableCollection<Axis>
            {
                new Axis { Labels = GuardrailYearLabels }
            };
            GuardrailSpendingAxesY = new ObservableCollection<Axis>
            {
                new Axis { Labeler = value => value.ToString("C0") }
            };

            GuardrailPortfolioSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<double>
                {
                    Name = "Portfolio / baseline",
                    Values = portfolioPct,
                    GeometrySize = 4,
                    Stroke = new SolidColorPaint { Color = DarkBlue, StrokeThickness = 3 },
                    Fill = null,
                },
                new LineSeries<double>
                {
                    Name = "Lower trigger",
                    Values = lowerLine,
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint { Color = IndianRed, StrokeThickness = 2 },
                    Fill = null,
                },
                new LineSeries<double>
                {
                    Name = "Upper trigger",
                    Values = upperLine,
                    GeometrySize = 0,
                    Stroke = new SolidColorPaint { Color = ForestGreen, StrokeThickness = 2 },
                    Fill = null,
                }
            };

            GuardrailPortfolioAxesX = new ObservableCollection<Axis>
            {
                new Axis { Labels = GuardrailYearLabels }
            };
            GuardrailPortfolioAxesY = new ObservableCollection<Axis>
            {
                new Axis { Labeler = value => value.ToString("P0") }
            };
        }

        // ------------------------------------------------------------
        // Ordinary tax-bracket fill (fixed simulation)
        // ------------------------------------------------------------
        private void BuildOrdinaryBracketFillChart()
        {
            OrdinaryBracketYearLabels = new List<string>();
            OrdinaryBracketFillSeries = new ObservableCollection<ISeries>();
            OrdinaryBracketFillAxesX = new ObservableCollection<Axis>();
            OrdinaryBracketFillAxesY = new ObservableCollection<Axis>();

            if (FixedResults == null || FixedResults.Count == 0)
                return;

            var rows = FixedResults
                .OrderBy(r => r.Year)
                .ToList();

            OrdinaryBracketYearLabels = rows
                .Select(r => r.Year.ToString())
                .ToList();

            var rates = rows
                .SelectMany(r => r.OrdinaryBracketFills ?? Enumerable.Empty<OrdinaryBracketFill>())
                .Select(f => f.Rate)
                .Distinct()
                .OrderBy(rate => rate)
                .ToList();

            var series = new ObservableCollection<ISeries>();

            foreach (var rate in rates)
            {
                var values = rows
                    .Select(r => (double)FilledForRate(r, rate))
                    .ToList();

                series.Add(new StackedColumnSeries<double>
                {
                    Name = FormatOrdinaryRate(rate),
                    Values = values,
                    Fill = new SolidColorPaint { Color = ColorForOrdinaryRate(rate) },
                    Stroke = null,
                    MaxBarWidth = 28
                });
            }

            series.Add(new StackedColumnSeries<double>
            {
                Name = "Room to next rate",
                Values = rows.Select(r => (double)r.OrdinaryBracketHeadroom).ToList(),
                Fill = new SolidColorPaint { Color = BracketRoom },
                Stroke = null,
                MaxBarWidth = 28
            });

            OrdinaryBracketFillSeries = series;
            OrdinaryBracketFillAxesX = new ObservableCollection<Axis>
            {
                new Axis { Labels = OrdinaryBracketYearLabels }
            };
            OrdinaryBracketFillAxesY = new ObservableCollection<Axis>
            {
                new Axis { Labeler = value => value.ToString("C0") }
            };
        }

        private static decimal FilledForRate(ResultRow row, decimal rate)
        {
            if (row.OrdinaryBracketFills == null)
                return 0m;

            foreach (var fill in row.OrdinaryBracketFills)
            {
                if (fill.Rate == rate)
                    return fill.Filled;
            }

            return 0m;
        }

        private static string FormatOrdinaryRate(decimal rate) => $"{rate * 100m:0}%";

        private static SKColor ColorForOrdinaryRate(decimal rate)
        {
            if (rate == 0.10m) return Bracket10;
            if (rate == 0.12m) return Bracket12;
            if (rate == 0.22m) return Bracket22;
            if (rate == 0.24m) return Bracket24;
            if (rate == 0.32m) return Bracket32;
            if (rate == 0.35m) return Bracket35;
            if (rate == 0.37m) return Bracket37;
            return SteelBlue;
        }

        // ------------------------------------------------------------
        // Historical Ending Balance Chart (LiveChartsCore v2)
        // ------------------------------------------------------------
        public void BuildHistoricalEndingBalanceChart()
        {
            DollarFormatter = value => value.ToString("C0");

            var finalRows = HistoricalResults
                .GroupBy(r => r.SimNo)
                .Select(g => g.OrderByDescending(r => r.Year).First())
                .OrderBy(r => r.SimNo)
                .ToList();

            int simCount = finalRows.Count;
            int firstYear = 1928;

            HistoricalEndingLabels = Enumerable
                .Range(firstYear, simCount)
                .Select(y => y.ToString())
                .ToList();

            var endingBalances = finalRows
                .Select(r => (double)r.EndingBalance)
                .ToList();

            HistoricalEndingSeries = new ObservableCollection<ISeries>
            {
                new ColumnSeries<double>
                {
                    Values = endingBalances,
                    Fill = new SolidColorPaint { Color = LightSteelBlue },
                    Stroke = new SolidColorPaint { Color = SteelBlue }
                }
            };

            HistoricalEndingAxesX = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Labels = HistoricalEndingLabels,
                }
            };
            HistoricalEndingAxesY = new ObservableCollection<Axis>
            {
                new Axis
                {
                    Labeler = value => value.ToString("C0"),
                }
            };
        }

        // ------------------------------------------------------------
        // Failure By Year Chart (LiveChartsCore v2)
        // ------------------------------------------------------------
        private void BuildFailureByYearChart()
        {
            if (ListOfMonteCarloResults == null || ListOfMonteCarloResults.Count == 0)
            {
                Debug.WriteLine("No Monte Carlo results available.");
                return;
            }

            var grouped = ListOfMonteCarloResults
                .GroupBy(r => r.SimNo);

            var failuresByYear = new Dictionary<int, int>();

            foreach (var sim in grouped)
            {
                var ordered = sim.OrderBy(r => r.Year).ToList();

                var failRow = ordered.FirstOrDefault(r => r.StartingBalance <= 0);
                if (failRow == null)
                    continue;

                int simYear = failRow.Year - ordered.First().Year + 1;

                if (!failuresByYear.ContainsKey(simYear))
                    failuresByYear[simYear] = 0;

                failuresByYear[simYear]++;
            }

            int maxSimYears = failuresByYear.Keys.Max();

            FailureByYearLabels = Enumerable.Range(1, maxSimYears)
                .Select(i => i.ToString())
                .ToList();

            var failureData = Enumerable.Range(1, maxSimYears)
                .Select(i => failuresByYear.ContainsKey(i) ? (double)failuresByYear[i] : 0.0)
                .ToList();

            FailureByYearSeries = new ObservableCollection<ISeries>
            {
                new ColumnSeries<double>
                {
                    Values = failureData,
                    Fill = new SolidColorPaint { Color = IndianRed }
                }
            };
        }

        private void PersistReportCardWeights()
        {
            if (ReportCard == null || Scenario == null)
                return;

            Scenario.ReportCardWeights = ReportCard.CaptureWeights();
        }

        // ------------------------------------------------------------
        // Print Summary (PDF)
        // ------------------------------------------------------------
        private void PrintSummary()
        {
            if (!HasReport)
            {
                MessageBox.Show(
                    "Print Summary is available after Generate Report has finished. Run Generate Report on the Results tab first.",
                    "Print Summary",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf",
                FileName = DefaultPdfFileName(Scenario?.Name)
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var model = RetirementSummaryBuilder.Build(
                    Scenario ?? new Scenario(),
                    Accounts,
                    Primary,
                    Spouse,
                    FixedResults,
                    HistoricalResults,
                    MonteCarloResults,
                    ReportCard);

                RetirementSummaryPdf.Save(model, dialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not save the PDF:\n{ex.Message}",
                    "Print Summary",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static string DefaultPdfFileName(string? scenarioName)
        {
            var name = string.IsNullOrWhiteSpace(scenarioName) ? "Scenario" : scenarioName.Trim();
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
                cleaned = "Scenario";
            return $"{cleaned}-Retirement-Plan-Summary.pdf";
        }

        // ------------------------------------------------------------
        // Excel Export
        // ------------------------------------------------------------
        private void ExportToExcel()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "SimulationResults.xlsx"
            };

            if (dialog.ShowDialog() != true)
                return;

            using (var wb = new XLWorkbook())
            {
                var wsScenario = wb.Worksheets.Add("Scenario");
                wsScenario.Cell(1, 1).Value = "Scenario Details";
                wsScenario.Cell(1, 1).Style.Font.Bold = true;

                int row = 3;
                wsScenario.Cell(row++, 1).Value = $"Base Year: {Scenario.BaseYear}";
                wsScenario.Cell(row++, 1).Value = $"Years: {Scenario.YearsToCalculate}";
                wsScenario.Cell(row++, 1).Value = $"Monte Carlo Simulations: {Scenario.MonteCarloSimulations}";
                wsScenario.Cell(row++, 1).Value = $"Monte Carlo Seed: {Scenario.MonteCarloSeed}";
                wsScenario.Cell(row++, 1).Value = $"Inflation Rate: {Scenario.InflationRate:P2}";
                wsScenario.Cell(row++, 1).Value = $"Withdrawal Rate: {Scenario.AnnualSpending:C2}";
                wsScenario.Cell(row++, 1).Value =
                    $"Increase baseline with inflation: {(Scenario.Guardrails?.IncreaseBaselineWithInflation == true ? "Yes" : "No")}";
                wsScenario.Columns().AdjustToContents();

                WriteResultRowsSheet(wb, "Fixed Results", FixedResults);
                WriteResultRowsSheet(wb, "Historical Results", HistoricalResults);
                WriteResultRowsSheet(wb, "Monte Carlo Results", MonteCarloResults);

                wb.SaveAs(dialog.FileName);
            }
        }

        private void WriteResultRowsSheet(XLWorkbook wb, string sheetName, List<ResultRow> rows)
        {
            var ws = wb.Worksheets.Add(sheetName);

            ws.Cell(1, 1).Value = "SimNo";
            ws.Cell(1, 2).Value = "Year";
            ws.Cell(1, 3).Value = "Age";
            ws.Cell(1, 4).Value = "SpouseAge";
            ws.Cell(1, 5).Value = "Iteration";
            ws.Cell(1, 6).Value = "StartingBalance";
            ws.Cell(1, 7).Value = "EndingBalance";
            ws.Cell(1, 8).Value = "Withdrawal";
            ws.Cell(1, 9).Value = "SocialSecurityIncome";
            ws.Cell(1, 10).Value = "Income";
            ws.Cell(1, 11).Value = "InvestmentReturn";
            ws.Cell(1, 12).Value = "Magi";
            ws.Cell(1, 13).Value = "TaxableIncome";
            ws.Cell(1, 14).Value = "OrdinaryIncome";
            ws.Cell(1, 15).Value = "FilingStatus";
            ws.Cell(1, 16).Value = "OrdinaryBracketHeadroom";
            ws.Cell(1, 17).Value = "CapitalGains";
            ws.Cell(1, 18).Value = "TaxesDue";
            ws.Cell(1, 19).Value = "TaxesPaid";
            ws.Cell(1, 20).Value = "IrmaaPaid";
            ws.Cell(1, 21).Value = "Rmds";
            ws.Cell(1, 22).Value = "Conversions";
            ws.Cell(1, 23).Value = "BaseSpending";
            ws.Cell(1, 24).Value = "InflationOnlyBaseSpending";
            ws.Cell(1, 25).Value = "GuardrailAction";
            ws.Cell(1, 26).Value = "GuardrailPortfolioPercent";

            int row = 2;

            foreach (var r in rows)
            {
                ws.Cell(row, 1).Value = r.SimNo;
                ws.Cell(row, 2).Value = r.Year;
                ws.Cell(row, 3).Value = r.Age;
                ws.Cell(row, 4).Value = r.SpouseAge;
                ws.Cell(row, 5).Value = r.Iteration;
                ws.Cell(row, 6).Value = r.StartingBalance;
                ws.Cell(row, 7).Value = r.EndingBalance;
                ws.Cell(row, 8).Value = r.Withdrawal;
                ws.Cell(row, 9).Value = r.SocialSecurityIncome;
                ws.Cell(row, 10).Value = r.Income;
                ws.Cell(row, 11).Value = r.InvestmentReturn;
                ws.Cell(row, 12).Value = r.Magi;
                ws.Cell(row, 13).Value = r.TaxableIncome;
                ws.Cell(row, 14).Value = r.OrdinaryIncome;
                ws.Cell(row, 15).Value = r.FilingStatusLabel;
                ws.Cell(row, 16).Value = r.OrdinaryBracketHeadroom;
                ws.Cell(row, 17).Value = r.CapitalGains;
                ws.Cell(row, 18).Value = r.TaxesDue;
                ws.Cell(row, 19).Value = r.TaxesPaid;
                ws.Cell(row, 20).Value = r.IrmaaPaid;
                ws.Cell(row, 21).Value = r.Rmds;
                ws.Cell(row, 22).Value = r.Conversions;
                ws.Cell(row, 23).Value = r.BaseSpending;
                ws.Cell(row, 24).Value = r.InflationOnlyBaseSpending;
                ws.Cell(row, 25).Value = r.GuardrailActionLabel;
                ws.Cell(row, 26).Value = r.GuardrailPortfolioPercent;

                row++;
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);
        }
    }
}
