namespace ExitPlan.UI.ViewModels
{
    public class RmdDivisorRowViewModel : ViewModelBase
    {
        private int _age;
        private decimal _distributionPeriod;

        public event EventHandler? Changed;

        public int Age
        {
            get => _age;
            set
            {
                if (_age == value)
                    return;
                _age = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public decimal DistributionPeriod
        {
            get => _distributionPeriod;
            set
            {
                if (_distributionPeriod == value)
                    return;
                _distributionPeriod = value;
                OnPropertyChanged();
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public RmdDivisorRowViewModel()
        {
        }

        public RmdDivisorRowViewModel(int age, decimal distributionPeriod)
        {
            _age = age;
            _distributionPeriod = distributionPeriod;
        }
    }
}
