using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ExitPlan.Core.Reporting
{
    /// <summary>
    /// Retirement plan report card — 10 metrics, each graded A-F,
    /// with a weighted overall score.
    /// </summary>
    public class ReportCard : INotifyPropertyChanged
    {
        public List<ReportCardMetric> Metrics { get; } = new();

        private decimal _overallScore;
        public decimal OverallScore
        {
            get => _overallScore;
            set { _overallScore = value; OnPropertyChanged(nameof(OverallScore)); OnPropertyChanged(nameof(OverallGrade)); OnPropertyChanged(nameof(OverallScoreDisplay)); OnPropertyChanged(nameof(IsOverallFailing)); }
        }

        public char OverallGrade => ScoreToGrade(OverallScore);
        public string OverallScoreDisplay => $"{OverallScore:F0}%";
        public bool IsOverallFailing => OverallScore < 70m;

        /// <summary>
        /// Raised after a metric weight changes and the overall score is recalculated.
        /// </summary>
        public event EventHandler? WeightsChanged;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private bool _listeningForWeightEdits;

        /// <summary>
        /// Recalculate the weighted overall score from the current metric scores and weights.
        /// Weights are relative; they do not need to sum to 100.
        /// </summary>
        public void RecalculateOverall()
        {
            decimal totalWeight = Metrics.Sum(m => m.Weight);
            OverallScore = totalWeight > 0
                ? Metrics.Sum(m => m.Score * m.Weight) / totalWeight
                : 0m;
        }

        /// <summary>
        /// Overlay saved scenario weights onto metrics of the same name, then recalculate.
        /// Unknown names are ignored; metrics without a saved weight keep their default.
        /// </summary>
        public void ApplySavedWeights(IReadOnlyDictionary<string, decimal>? weights)
        {
            if (weights == null || weights.Count == 0)
                return;

            foreach (var metric in Metrics)
            {
                if (!string.IsNullOrEmpty(metric.Name) && weights.TryGetValue(metric.Name, out var weight))
                    metric.Weight = weight;
            }

            RecalculateOverall();
        }

        public Dictionary<string, decimal> CaptureWeights()
        {
            var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var metric in Metrics)
            {
                if (!string.IsNullOrWhiteSpace(metric.Name))
                    result[metric.Name] = metric.Weight;
            }
            return result;
        }

        /// <summary>
        /// Keep the overall grade in sync when the user edits a Weight cell.
        /// Safe to call more than once.
        /// </summary>
        public void ListenForWeightEdits()
        {
            if (_listeningForWeightEdits)
                return;

            _listeningForWeightEdits = true;
            foreach (var metric in Metrics)
                metric.PropertyChanged += OnMetricChanged;
        }

        private void OnMetricChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ReportCardMetric.Weight))
                return;

            RecalculateOverall();
            WeightsChanged?.Invoke(this, EventArgs.Empty);
        }

        private static char ScoreToGrade(decimal score)
        {
            if (score >= 90m) return 'A';
            if (score >= 80m) return 'B';
            if (score >= 70m) return 'C';
            if (score >= 60m) return 'D';
            return 'F';
        }
    }
}
