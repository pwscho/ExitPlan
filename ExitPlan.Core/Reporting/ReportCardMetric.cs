using System.ComponentModel;

namespace ExitPlan.Core.Reporting
{
    /// <summary>
    /// Single metric on the retirement report card.
    /// Score 0-100, mapped to a letter grade A-F.
    /// </summary>
    public class ReportCardMetric : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        private decimal _score;
        public decimal Score
        {
            get => _score;
            set { _score = value; OnPropertyChanged(nameof(Score)); OnPropertyChanged(nameof(Grade)); OnPropertyChanged(nameof(IsFailing)); OnPropertyChanged(nameof(ScoreDisplay)); }
        }

        public char Grade => ScoreToGrade(Score);

        private decimal _weight;
        public decimal Weight
        {
            get => _weight;
            set
            {
                var next = value < 0m ? 0m : value;
                if (_weight == next)
                    return;
                _weight = next;
                OnPropertyChanged(nameof(Weight));
            }
        }

        public bool IsFailing => Score < 70m;
        public string ScoreDisplay => $"{Score:F0}%";

        // Human-readable detail explaining the numbers behind the score
        public string Detail { get; set; } = string.Empty;

        private static char ScoreToGrade(decimal score)
        {
            if (score >= 90m) return 'A';
            if (score >= 80m) return 'B';
            if (score >= 70m) return 'C';
            if (score >= 60m) return 'D';
            return 'F';
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
