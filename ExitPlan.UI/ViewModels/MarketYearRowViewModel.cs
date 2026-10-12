namespace ExitPlan.UI.ViewModels
{
    public class MarketYearRowViewModel : ViewModelBase
    {
        private int _year;
        private decimal _equityReturn;
        private decimal _bondReturn;
        private decimal _inflationRate;

        public event EventHandler? Changed;

        public int Year
        {
            get => _year;
            set
            {
                if (_year == value)
                    return;
                _year = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public decimal EquityReturn
        {
            get => _equityReturn;
            set
            {
                if (_equityReturn == value)
                    return;
                _equityReturn = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public decimal BondReturn
        {
            get => _bondReturn;
            set
            {
                if (_bondReturn == value)
                    return;
                _bondReturn = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public decimal InflationRate
        {
            get => _inflationRate;
            set
            {
                if (_inflationRate == value)
                    return;
                _inflationRate = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public MarketYearRowViewModel()
        {
        }

        public MarketYearRowViewModel(int year, decimal equityReturn, decimal bondReturn, decimal inflationRate)
        {
            _year = year;
            _equityReturn = equityReturn;
            _bondReturn = bondReturn;
            _inflationRate = inflationRate;
        }
    }
}
