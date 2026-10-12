using ExitPlan.Core;
using ExitPlan.UI.ViewModels;
using System.Collections.ObjectModel;
using System.Windows.Input;

public class WithdrawalsViewModel : ViewModelBase
{
    private readonly Scenario _scenario;

    public WithdrawalsViewModel(Scenario scenario)
    {
        _scenario = scenario;

        WithdrawalStreams = new ObservableCollection<WithdrawalStreamViewModel>(
            scenario.WithdrawalStreams.Select(w => new WithdrawalStreamViewModel(w)));

        AddWithdrawalStreamCommand = new RelayCommand(AddWithdrawalStream);

        RemoveWithdrawalStreamCommand = new RelayCommand(
            () => RemoveWithdrawalStream(SelectedWithdrawalStream),
            () => SelectedWithdrawalStream != null);
    }

    public ObservableCollection<WithdrawalStreamViewModel> WithdrawalStreams { get; }

    private WithdrawalStreamViewModel? _selectedWithdrawalStream;
    public WithdrawalStreamViewModel? SelectedWithdrawalStream
    {
        get => _selectedWithdrawalStream;
        set
        {
            _selectedWithdrawalStream = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddWithdrawalStreamCommand { get; }
    public ICommand RemoveWithdrawalStreamCommand { get; }

    private void AddWithdrawalStream()
    {
        var model = new WithdrawalStream();
        _scenario.WithdrawalStreams.Add(model);

        var vm = new WithdrawalStreamViewModel(model);
        WithdrawalStreams.Add(vm);
        SelectedWithdrawalStream = vm;
    }

    private void RemoveWithdrawalStream(WithdrawalStreamViewModel? vm)
    {
        if (vm == null) return;

        _scenario.WithdrawalStreams.Remove(vm.Model);
        WithdrawalStreams.Remove(vm);
    }

    public ObservableCollection<OwnerOption> OwnerOptions { get; } = new();

    public void RefreshOwnerOptions(
        string primaryName,
        Guid primaryId,
        string? spouseName,
        Guid? spouseId)
    {
        OwnerOptions.Clear();
        OwnerOptions.Add(new OwnerOption { Id = primaryId, Name = primaryName });

        if (spouseId.HasValue && spouseName != null)
            OwnerOptions.Add(new OwnerOption { Id = spouseId.Value, Name = spouseName });

        OnPropertyChanged(nameof(OwnerOptions));
    }
}

