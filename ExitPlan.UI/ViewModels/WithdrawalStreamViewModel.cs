using ExitPlan.UI.ViewModels;

public class WithdrawalStreamViewModel : ViewModelBase
{
    private readonly WithdrawalStream _model;

    public WithdrawalStreamViewModel(WithdrawalStream model)
    {
        _model = model;
        _ownerId = model.OwnerId;
    }

    public WithdrawalStream Model => _model;

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
}
