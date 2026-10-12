using ExitPlan.Core;

namespace ExitPlan.UI.ViewModels
{
    public class RmdStartAgeBandRowViewModel : ViewModelBase
    {
        private int _bornFrom;
        private int? _bornThrough;
        private int _startAge;

        public event EventHandler? Changed;

        public int BornFrom
        {
            get => _bornFrom;
            set
            {
                if (_bornFrom == value)
                    return;
                _bornFrom = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public int? BornThrough
        {
            get => _bornThrough;
            set
            {
                if (_bornThrough == value)
                    return;
                _bornThrough = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BornThroughText));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public string BornThroughText
        {
            get => _bornThrough?.ToString() ?? string.Empty;
            set
            {
                int? parsed = null;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    if (!int.TryParse(value, out int year))
                        return;
                    parsed = year;
                }

                if (_bornThrough == parsed)
                    return;

                _bornThrough = parsed;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BornThrough));
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public int StartAge
        {
            get => _startAge;
            set
            {
                if (_startAge == value)
                    return;
                _startAge = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public RmdStartAgeBandRowViewModel()
        {
        }

        public RmdStartAgeBandRowViewModel(int bornFrom, int? bornThrough, int startAge)
        {
            _bornFrom = bornFrom;
            _bornThrough = bornThrough;
            _startAge = startAge;
        }

        public RmdStartAgeBand ToBand() => new()
        {
            BornFrom = BornFrom,
            BornThrough = BornThrough,
            StartAge = StartAge
        };
    }
}
