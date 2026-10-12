using System.Collections.ObjectModel;
using System.Collections.Specialized;
using ExitPlan.Core.Taxes;

namespace ExitPlan.UI.ViewModels
{
    public class FilingStatusEditorViewModel : ViewModelBase
    {
        private decimal _standardDeduction;
        private decimal _additionalStandardDeduction;
        private TaxBracketRowViewModel? _selectedOrdinaryBracket;
        private TaxBracketRowViewModel? _selectedCapitalGainsBracket;
        private IrmaaBracketRowViewModel? _selectedIrmaaBracket;

        public event EventHandler? Changed;

        public FilingStatusEditorViewModel(string title, bool additionalDeductionIsPerSpouse = false)
        {
            Title = title;
            AdditionalStandardDeductionLabel = additionalDeductionIsPerSpouse
                ? "Additional standard deduction per spouse ($)"
                : "Additional standard deduction ($)";
            AdditionalStandardDeductionHint = additionalDeductionIsPerSpouse
                ? "IRC §63(f). Added once for each living spouse who is at least the additional-standard-deduction age. Inflates with the same tax-bracket inflation as the standard deduction."
                : "IRC §63(f). Added if the living filer is at least the additional-standard-deduction age. Inflates with the same tax-bracket inflation as the standard deduction.";
            OrdinaryBrackets.CollectionChanged += OnBracketCollectionChanged;
            CapitalGainsBrackets.CollectionChanged += OnBracketCollectionChanged;
            IrmaaBrackets.CollectionChanged += OnBracketCollectionChanged;

            AddOrdinaryBracketCommand = new RelayCommand(AddOrdinaryBracket);
            RemoveOrdinaryBracketCommand = new RelayCommand(RemoveOrdinaryBracket, () => SelectedOrdinaryBracket != null);
            AddCapitalGainsBracketCommand = new RelayCommand(AddCapitalGainsBracket);
            RemoveCapitalGainsBracketCommand = new RelayCommand(RemoveCapitalGainsBracket, () => SelectedCapitalGainsBracket != null);
            AddIrmaaBracketCommand = new RelayCommand(AddIrmaaBracket);
            RemoveIrmaaBracketCommand = new RelayCommand(RemoveIrmaaBracket, () => SelectedIrmaaBracket != null);
        }

        public string Title { get; }
        public string AdditionalStandardDeductionLabel { get; }
        public string AdditionalStandardDeductionHint { get; }

        public decimal StandardDeduction
        {
            get => _standardDeduction;
            set
            {
                if (_standardDeduction == value)
                    return;
                _standardDeduction = value;
                OnPropertyChanged();
                RaiseChanged();
            }
        }

        public decimal AdditionalStandardDeduction
        {
            get => _additionalStandardDeduction;
            set
            {
                if (_additionalStandardDeduction == value)
                    return;
                _additionalStandardDeduction = value;
                OnPropertyChanged();
                RaiseChanged();
            }
        }

        public ObservableCollection<TaxBracketRowViewModel> OrdinaryBrackets { get; } = new();
        public ObservableCollection<TaxBracketRowViewModel> CapitalGainsBrackets { get; } = new();
        public ObservableCollection<IrmaaBracketRowViewModel> IrmaaBrackets { get; } = new();

        public TaxBracketRowViewModel? SelectedOrdinaryBracket
        {
            get => _selectedOrdinaryBracket;
            set
            {
                _selectedOrdinaryBracket = value;
                OnPropertyChanged();
                RemoveOrdinaryBracketCommand.RaiseCanExecuteChanged();
            }
        }

        public TaxBracketRowViewModel? SelectedCapitalGainsBracket
        {
            get => _selectedCapitalGainsBracket;
            set
            {
                _selectedCapitalGainsBracket = value;
                OnPropertyChanged();
                RemoveCapitalGainsBracketCommand.RaiseCanExecuteChanged();
            }
        }

        public IrmaaBracketRowViewModel? SelectedIrmaaBracket
        {
            get => _selectedIrmaaBracket;
            set
            {
                _selectedIrmaaBracket = value;
                OnPropertyChanged();
                RemoveIrmaaBracketCommand.RaiseCanExecuteChanged();
            }
        }

        public RelayCommand AddOrdinaryBracketCommand { get; }
        public RelayCommand RemoveOrdinaryBracketCommand { get; }
        public RelayCommand AddCapitalGainsBracketCommand { get; }
        public RelayCommand RemoveCapitalGainsBracketCommand { get; }
        public RelayCommand AddIrmaaBracketCommand { get; }
        public RelayCommand RemoveIrmaaBracketCommand { get; }

        public void Load(FilingStatusTaxTable table)
        {
            _standardDeduction = table.StandardDeduction;
            _additionalStandardDeduction = table.AdditionalStandardDeduction;
            OnPropertyChanged(nameof(StandardDeduction));
            OnPropertyChanged(nameof(AdditionalStandardDeduction));

            ReplaceBrackets(OrdinaryBrackets, table.OrdinaryBrackets);
            ReplaceBrackets(CapitalGainsBrackets, table.CapitalGainsBrackets);
            ReplaceIrmaa(table.IrmaaBrackets);
        }

        public FilingStatusTaxTable ToTable()
        {
            return new FilingStatusTaxTable
            {
                StandardDeduction = StandardDeduction,
                AdditionalStandardDeduction = AdditionalStandardDeduction,
                OrdinaryBrackets = OrdinaryBrackets
                    .Select(row => new TaxBracket { Threshold = row.Threshold, Rate = row.Rate })
                    .ToList(),
                CapitalGainsBrackets = CapitalGainsBrackets
                    .Select(row => new TaxBracket { Threshold = row.Threshold, Rate = row.Rate })
                    .ToList(),
                IrmaaBrackets = IrmaaBrackets
                    .Select(row => new IrmaaBracket
                    {
                        Threshold = row.Threshold,
                        MonthlySurcharge = row.MonthlySurcharge
                    })
                    .ToList()
            };
        }

        private void ReplaceBrackets(
            ObservableCollection<TaxBracketRowViewModel> target,
            IEnumerable<TaxBracket> source)
        {
            foreach (var row in target)
                row.Changed -= OnRowChanged;

            target.Clear();
            foreach (var bracket in source.OrderBy(b => b.Threshold))
            {
                var row = new TaxBracketRowViewModel(bracket.Threshold, bracket.Rate);
                row.Changed += OnRowChanged;
                target.Add(row);
            }
        }

        private void ReplaceIrmaa(IEnumerable<IrmaaBracket> source)
        {
            foreach (var row in IrmaaBrackets)
                row.Changed -= OnRowChanged;

            IrmaaBrackets.Clear();
            foreach (var bracket in source.OrderBy(b => b.Threshold))
            {
                var row = new IrmaaBracketRowViewModel(bracket.Threshold, bracket.MonthlySurcharge);
                row.Changed += OnRowChanged;
                IrmaaBrackets.Add(row);
            }
        }

        private void AddOrdinaryBracket()
        {
            var row = new TaxBracketRowViewModel();
            row.Changed += OnRowChanged;
            OrdinaryBrackets.Add(row);
            SelectedOrdinaryBracket = row;
        }

        private void RemoveOrdinaryBracket()
        {
            if (SelectedOrdinaryBracket == null)
                return;
            SelectedOrdinaryBracket.Changed -= OnRowChanged;
            OrdinaryBrackets.Remove(SelectedOrdinaryBracket);
            SelectedOrdinaryBracket = OrdinaryBrackets.LastOrDefault();
        }

        private void AddCapitalGainsBracket()
        {
            var row = new TaxBracketRowViewModel();
            row.Changed += OnRowChanged;
            CapitalGainsBrackets.Add(row);
            SelectedCapitalGainsBracket = row;
        }

        private void RemoveCapitalGainsBracket()
        {
            if (SelectedCapitalGainsBracket == null)
                return;
            SelectedCapitalGainsBracket.Changed -= OnRowChanged;
            CapitalGainsBrackets.Remove(SelectedCapitalGainsBracket);
            SelectedCapitalGainsBracket = CapitalGainsBrackets.LastOrDefault();
        }

        private void AddIrmaaBracket()
        {
            var row = new IrmaaBracketRowViewModel();
            row.Changed += OnRowChanged;
            IrmaaBrackets.Add(row);
            SelectedIrmaaBracket = row;
        }

        private void RemoveIrmaaBracket()
        {
            if (SelectedIrmaaBracket == null)
                return;
            SelectedIrmaaBracket.Changed -= OnRowChanged;
            IrmaaBrackets.Remove(SelectedIrmaaBracket);
            SelectedIrmaaBracket = IrmaaBrackets.LastOrDefault();
        }

        private void OnBracketCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RaiseChanged();
        }

        private void OnRowChanged(object? sender, EventArgs e) => RaiseChanged();

        private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
