namespace ExitPlan.UI.ViewModels
{
    public class TaxBracketRowViewModel : ViewModelBase
    {
        private decimal _threshold;
        private decimal _rate;

        public event EventHandler? Changed;

        public decimal Threshold
        {
            get => _threshold;
            set
            {
                if (_threshold == value)
                    return;
                _threshold = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public decimal Rate
        {
            get => _rate;
            set
            {
                if (_rate == value)
                    return;
                _rate = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public TaxBracketRowViewModel()
        {
        }

        public TaxBracketRowViewModel(decimal threshold, decimal rate)
        {
            _threshold = threshold;
            _rate = rate;
        }
    }

    public class IrmaaBracketRowViewModel : ViewModelBase
    {
        private decimal _threshold;
        private decimal _monthlySurcharge;

        public event EventHandler? Changed;

        public decimal Threshold
        {
            get => _threshold;
            set
            {
                if (_threshold == value)
                    return;
                _threshold = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public decimal MonthlySurcharge
        {
            get => _monthlySurcharge;
            set
            {
                if (_monthlySurcharge == value)
                    return;
                _monthlySurcharge = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public IrmaaBracketRowViewModel()
        {
        }

        public IrmaaBracketRowViewModel(decimal threshold, decimal monthlySurcharge)
        {
            _threshold = threshold;
            _monthlySurcharge = monthlySurcharge;
        }
    }
}
