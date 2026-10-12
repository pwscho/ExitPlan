using ExitPlan.Core;

namespace ExitPlan.UI.ViewModels
{
    public class IncomeStreamViewModel : ViewModelBase
    {
        private readonly IncomeStream _model;

        public IncomeStreamViewModel(IncomeStream model)
        {
            _model = model;

            // Load OwnerId from model
            _ownerId = model.OwnerId;
        }

        public IncomeStream Model => _model;

        public string Name
        {
            get => _model.Name;
            set
            {
                if (_model.Name != value)
                {
                    _model.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        private Guid _ownerId;
        public Guid OwnerId
        {
            get => _ownerId;
            set
            {
                if (_ownerId != value)
                {
                    _ownerId = value;

                    // CRITICAL: persist change back to model
                    _model.OwnerId = value;

                    OnPropertyChanged();
                }
            }
        }

        public int StartAge
        {
            get => _model.StartAge;
            set
            {
                if (_model.StartAge != value)
                {
                    _model.StartAge = value;
                    OnPropertyChanged();
                }
            }
        }

        public int EndAge
        {
            get => _model.EndAge;
            set
            {
                if (_model.EndAge != value)
                {
                    _model.EndAge = value;
                    OnPropertyChanged();
                }
            }
        }

        public decimal AnnualAmount
        {
            get => _model.AnnualAmount;
            set
            {
                if (_model.AnnualAmount != value)
                {
                    _model.AnnualAmount = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsTaxable
        {
            get => _model.IsTaxable;
            set
            {
                if (_model.IsTaxable != value)
                {
                    _model.IsTaxable = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}
