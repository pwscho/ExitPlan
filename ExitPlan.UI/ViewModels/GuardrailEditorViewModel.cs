using ExitPlan.Core;
using System.Windows;
using System.Windows.Input;

namespace ExitPlan.UI.ViewModels
{
    public class GuardrailEditorViewModel : ViewModelBase
    {
        private readonly GuardrailSettings _settings;
        private Window _window;

        // Backing fields — user sees friendly percentages
        private decimal _initialPortfolioValue;
        private decimal _lowerTriggerPct;
        private decimal _lowerCutPct;
        private decimal _minSpendingFloorPct;
        private bool _inflateSpendingFloor;
        private bool _increaseBaselineWithInflation;
        private decimal _upperTriggerPct;
        private decimal _upperBonusPct;

        public GuardrailEditorViewModel(GuardrailSettings settings)
        {
            _settings = settings;

            // Convert model decimals → user-friendly percentages
            _initialPortfolioValue = settings.InitialPortfolioValue;
            _lowerTriggerPct       = settings.LowerTrigger       * 100m;   // 0.90  → 90
            _lowerCutPct           = Math.Abs(settings.LowerCut)  * 100m;   // -0.02 → 2
            _minSpendingFloorPct   = settings.MinSpendingFloor   * 100m;   // 0.90  → 90
            _inflateSpendingFloor  = settings.InflateSpendingFloor;
            _increaseBaselineWithInflation = settings.IncreaseBaselineWithInflation;
            _upperTriggerPct       = settings.UpperTrigger       * 100m;   // 1.10  → 110
            _upperBonusPct         = settings.UpperBonus         * 100m;   // 0.01  → 1

            SaveCommand = new RelayCommand(SaveAndClose);
        }

        public void AttachWindow(Window window) => _window = window;

        public decimal InitialPortfolioValue
        {
            get => _initialPortfolioValue;
            set { _initialPortfolioValue = value; OnPropertyChanged(); }
        }

        public decimal LowerTriggerPct
        {
            get => _lowerTriggerPct;
            set { _lowerTriggerPct = value; OnPropertyChanged(); }
        }

        public decimal LowerCutPct
        {
            get => _lowerCutPct;
            set { _lowerCutPct = value; OnPropertyChanged(); }
        }

        public decimal MinSpendingFloorPct
        {
            get => _minSpendingFloorPct;
            set { _minSpendingFloorPct = value; OnPropertyChanged(); }
        }

        public bool InflateSpendingFloor
        {
            get => _inflateSpendingFloor;
            set { _inflateSpendingFloor = value; OnPropertyChanged(); }
        }

        public bool IncreaseBaselineWithInflation
        {
            get => _increaseBaselineWithInflation;
            set { _increaseBaselineWithInflation = value; OnPropertyChanged(); }
        }

        public decimal UpperTriggerPct
        {
            get => _upperTriggerPct;
            set { _upperTriggerPct = value; OnPropertyChanged(); }
        }

        public decimal UpperBonusPct
        {
            get => _upperBonusPct;
            set { _upperBonusPct = value; OnPropertyChanged(); }
        }

        public ICommand SaveCommand { get; }

        private void SaveAndClose()
        {
            // Convert user-friendly percentages → model decimals
            _settings.InitialPortfolioValue = InitialPortfolioValue;
            // Keep the scenario-level copy in sync so a saved run is not 0.
            _settings.LowerTrigger          = LowerTriggerPct        / 100m;   // 90  → 0.90
            _settings.LowerCut              = -Math.Abs(LowerCutPct) / 100m;   // 2   → -0.02
            _settings.MinSpendingFloor      = MinSpendingFloorPct    / 100m;   // 90  → 0.90
            _settings.InflateSpendingFloor  = InflateSpendingFloor;
            _settings.IncreaseBaselineWithInflation = IncreaseBaselineWithInflation;
            _settings.UpperTrigger          = UpperTriggerPct        / 100m;   // 110 → 1.10
            _settings.UpperBonus            = Math.Abs(UpperBonusPct) / 100m;  // 1   → 0.01

            _window.DialogResult = true;
            _window.Close();
        }
    }
}
