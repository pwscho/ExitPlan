using System.Collections.ObjectModel;
using System.Windows.Input;
using ExitPlan.Core;

namespace ExitPlan.UI.ViewModels
{
    public class IncomeViewModel : ViewModelBase
    {
        private readonly Scenario _scenario;

        public IncomeViewModel(Scenario scenario)
        {
            _scenario = scenario;

            IncomeStreams = new ObservableCollection<IncomeStreamViewModel>(
                scenario.IncomeStreams.Select(i => new IncomeStreamViewModel(i)));

            AddIncomeStreamCommand = new RelayCommand(AddIncomeStream);

            RemoveIncomeStreamCommand = new RelayCommand(
                () => RemoveIncomeStream(SelectedIncomeStream),
                () => SelectedIncomeStream != null);
        }

        public ObservableCollection<IncomeStreamViewModel> IncomeStreams { get; }

        private IncomeStreamViewModel? _selectedIncomeStream;
        public IncomeStreamViewModel? SelectedIncomeStream
        {
            get => _selectedIncomeStream;
            set
            {
                _selectedIncomeStream = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddIncomeStreamCommand { get; }
        public ICommand RemoveIncomeStreamCommand { get; }

        private void AddIncomeStream()
        {
            var model = new IncomeStream();
            _scenario.IncomeStreams.Add(model);

            var vm = new IncomeStreamViewModel(model);
            IncomeStreams.Add(vm);
            SelectedIncomeStream = vm;
        }

        private void RemoveIncomeStream(IncomeStreamViewModel? vm)
        {
            if (vm == null) return;

            _scenario.IncomeStreams.Remove(vm.Model);
            IncomeStreams.Remove(vm);
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

    public class OwnerOption
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

}
