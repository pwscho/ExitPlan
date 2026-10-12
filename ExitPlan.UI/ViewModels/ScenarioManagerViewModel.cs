using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using ExitPlan.Core;
using ExitPlan.Core.Taxes;

namespace ExitPlan.UI.ViewModels
{
    public class ScenarioManagerViewModel : ViewModelBase
    {
        public ObservableCollection<ScenarioState> Scenarios { get; } = new();

        private ScenarioState? _selectedScenario;
        public ScenarioState? SelectedScenario
        {
            get => _selectedScenario;
            set
            {
                if (_selectedScenario != value)
                {
                    // Auto-save previous scenario before switching
                    if (_selectedScenario != null)
                        ScenarioSwitching?.Invoke(this, _selectedScenario);

                    _selectedScenario = value;
                    OnPropertyChanged();
                   // Debug.WriteLine($"SelectedScenario: state={value.GetHashCode()}");

                }
            }
        }

        public ICommand AddScenarioCommand { get; }
        public ICommand DuplicateScenarioCommand { get; }
        public ICommand DeleteScenarioCommand { get; }

        // Fired when switching scenarios so MainViewModel can auto-save
        public event EventHandler<ScenarioState>? ScenarioSwitching;

        public ScenarioManagerViewModel()
        {
            AddScenarioCommand = new RelayCommand(AddScenario);
            DuplicateScenarioCommand = new RelayCommand(DuplicateScenario, () => SelectedScenario != null);
            DeleteScenarioCommand = new RelayCommand(DeleteScenario, () => SelectedScenario != null);
        }

        private void AddScenario()
        {
            var newScenario = new ScenarioState
            {
                Name = $"Scenario {Scenarios.Count + 1}"
            };

            // Do NOT load tax policy here.
            // Tax policy is loaded dynamically in MainViewModel.LoadScenario()

            Scenarios.Add(newScenario);
            SelectedScenario = newScenario;
        }




        private void DuplicateScenario()
        {
            if (SelectedScenario == null)
                return;

            var copy = SelectedScenario.DeepCopy();
            copy.Name = SelectedScenario.Name + " (Copy)";

            Scenarios.Add(copy);
            SelectedScenario = copy;
        }

        private void DeleteScenario()
        {
            if (SelectedScenario == null)
                return;

            var toRemove = SelectedScenario;
            int index = Scenarios.IndexOf(toRemove);

            Scenarios.Remove(toRemove);

            if (Scenarios.Count == 0)
            {
                var newScenario = new ScenarioState { Name = "New Scenario" };
                Scenarios.Add(newScenario);
                SelectedScenario = newScenario;
                return;
            }

            if (index >= Scenarios.Count)
                index = Scenarios.Count - 1;

            SelectedScenario = Scenarios[index];
        }
    }
}

