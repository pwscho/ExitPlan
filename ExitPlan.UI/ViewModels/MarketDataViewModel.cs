using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using ExitPlan.Core.MarketData;
using ExitPlan.UI.Views;

namespace ExitPlan.UI.ViewModels
{
    public class MarketDataViewModel : ViewModelBase
    {
        private MarketHistoryDocument? _document;
        private bool _loading;
        private bool _isDirty;
        private MarketYearRowViewModel? _selectedYear;
        private string _statusMessage = string.Empty;
        private string _validationMessage = string.Empty;
        private Window? _window;

        public MarketDataViewModel()
        {
            Years.CollectionChanged += OnYearsCollectionChanged;

            SaveCommand = new RelayCommand(Save);
            ReloadCommand = new RelayCommand(Reload);
            ViewJsonCommand = new RelayCommand(ViewJson, () => _document != null);
            AddYearCommand = new RelayCommand(AddYear);
            RemoveYearCommand = new RelayCommand(RemoveYear, () => SelectedYear != null);

            LoadFile();
        }

        public void AttachWindow(Window window) => _window = window;

        public ObservableCollection<MarketYearRowViewModel> Years { get; } = new();

        public MarketYearRowViewModel? SelectedYear
        {
            get => _selectedYear;
            set
            {
                _selectedYear = value;
                OnPropertyChanged();
                RemoveYearCommand.RaiseCanExecuteChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public string ValidationMessage
        {
            get => _validationMessage;
            private set
            {
                _validationMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasValidationErrors));
            }
        }

        public bool HasValidationErrors => !string.IsNullOrWhiteSpace(ValidationMessage);

        public bool IsDirty
        {
            get => _isDirty;
            private set
            {
                if (_isDirty == value)
                    return;
                _isDirty = value;
                OnPropertyChanged();
            }
        }

        public string MarketDataPath => MarketHistoryLoader.GetFilePath();

        public RelayCommand SaveCommand { get; }
        public RelayCommand ReloadCommand { get; }
        public RelayCommand ViewJsonCommand { get; }
        public RelayCommand AddYearCommand { get; }
        public RelayCommand RemoveYearCommand { get; }

        public bool ConfirmClose() => ConfirmLeave();

        private void LoadFile()
        {
            ValidationMessage = string.Empty;

            try
            {
                _document = MarketHistoryDocument.Load(MarketHistoryLoader.GetFilePath());
                ApplyHistoryToForm(_document.ToHistory());
                IsDirty = false;
                ViewJsonCommand.RaiseCanExecuteChanged();
                StatusMessage = "Editing MarketData.json";
            }
            catch (Exception ex)
            {
                _document = null;
                ViewJsonCommand.RaiseCanExecuteChanged();
                ValidationMessage = ex.Message;
                StatusMessage = "Could not load MarketData.json.";
            }
        }

        private void ApplyHistoryToForm(MarketHistory history)
        {
            _loading = true;
            try
            {
                foreach (var row in Years)
                    row.Changed -= OnRowChanged;
                Years.Clear();

                foreach (var year in history.Years)
                {
                    var row = new MarketYearRowViewModel(
                        year.Year,
                        year.EquityReturn,
                        year.BondReturn,
                        year.InflationRate);
                    row.Changed += OnRowChanged;
                    Years.Add(row);
                }
            }
            finally
            {
                _loading = false;
            }
        }

        private MarketHistory ToHistory()
        {
            return new MarketHistory
            {
                Years = Years.Select(row => new MarketYear
                {
                    Year = row.Year,
                    EquityReturn = row.EquityReturn,
                    BondReturn = row.BondReturn,
                    InflationRate = row.InflationRate
                }).ToList()
            };
        }

        private void Save()
        {
            TrySave(showSuccess: true);
        }

        private bool TrySave(bool showSuccess)
        {
            if (_document == null)
            {
                ValidationMessage = "MarketData.json is not loaded.";
                return false;
            }

            var history = ToHistory();
            var errors = MarketHistoryValidator.Validate(history);
            if (errors.Count > 0)
            {
                ValidationMessage = string.Join(Environment.NewLine, errors);
                return false;
            }

            try
            {
                _document.Apply(history);
                _document.Save();

                var reloaded = MarketHistoryLoader.LoadFromPath(_document.FilePath);
                ApplyHistoryToForm(reloaded);
                IsDirty = false;
                ValidationMessage = string.Empty;
                StatusMessage = "Saved MarketData.json";

                if (showSuccess)
                    MessageBox.Show(_window, "Saved MarketData.json.", "Market Data", MessageBoxButton.OK, MessageBoxImage.Information);

                return true;
            }
            catch (Exception ex)
            {
                ValidationMessage = ex.Message;
                return false;
            }
        }

        private void Reload()
        {
            if (IsDirty)
            {
                var result = MessageBox.Show(
                    _window,
                    "Reload the file from disk and discard unsaved changes?",
                    "Market Data",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;
            }

            LoadFile();
        }

        private void ViewJson()
        {
            if (_document == null)
                return;

            if (IsDirty)
            {
                var result = MessageBox.Show(
                    _window,
                    "Save your form changes before opening the JSON view?",
                    "Market Data",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Cancel)
                    return;

                if (result == MessageBoxResult.Yes && !TrySave(showSuccess: false))
                    return;
            }

            var editor = new JsonFallbackWindow("Market Data JSON", _document.FilePath);
            if (_window != null)
                editor.Owner = _window;
            editor.ShowDialog();
            LoadFile();
        }

        private void AddYear()
        {
            int year = Years.Count == 0 ? DateTime.Now.Year : Years.Max(y => y.Year) + 1;
            var row = new MarketYearRowViewModel(year, 0m, 0m, 0m);
            row.Changed += OnRowChanged;
            Years.Add(row);
            SelectedYear = row;
        }

        private void RemoveYear()
        {
            if (SelectedYear == null)
                return;
            SelectedYear.Changed -= OnRowChanged;
            Years.Remove(SelectedYear);
            SelectedYear = Years.LastOrDefault();
        }

        private bool ConfirmLeave()
        {
            if (!IsDirty)
                return true;

            var result = MessageBox.Show(
                _window,
                "You have unsaved market data changes. Save them first?",
                "Market Data",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return false;

            if (result == MessageBoxResult.Yes)
                return TrySave(showSuccess: false);

            return true;
        }

        private void OnYearsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!_loading)
                IsDirty = true;
        }

        private void OnRowChanged(object? sender, EventArgs e)
        {
            if (!_loading)
                IsDirty = true;
        }
    }
}
