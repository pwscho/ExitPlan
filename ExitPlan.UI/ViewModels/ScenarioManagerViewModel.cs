using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
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
                    DuplicateScenarioCommand?.RaiseCanExecuteChanged();
                    DeleteScenarioCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public RelayCommand AddScenarioCommand { get; }
        public RelayCommand DuplicateScenarioCommand { get; }
        public RelayCommand DeleteScenarioCommand { get; }

        private readonly Func<string, bool> _confirmDelete;

        // Fired when switching scenarios so MainViewModel can auto-save
        public event EventHandler<ScenarioState>? ScenarioSwitching;

        public ScenarioManagerViewModel(Func<string, bool>? confirmDelete = null)
        {
            _confirmDelete = confirmDelete ?? ShowDeleteDialog;
            AddScenarioCommand = new RelayCommand(AddScenario);
            DuplicateScenarioCommand = new RelayCommand(DuplicateScenario, () => SelectedScenario != null);
            DeleteScenarioCommand = new RelayCommand(DeleteScenario, () => SelectedScenario != null);
        }

        private static bool ShowDeleteDialog(string message)
        {
            var defaultButton = ScenarioDeleteConfirmation.DefaultsToNo
                ? MessageBoxResult.No
                : MessageBoxResult.Yes;

            var owner = Application.Current?.MainWindow;
            var result = owner == null
                ? MessageBox.Show(
                    message,
                    ScenarioDeleteConfirmation.Title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    defaultButton)
                : MessageBox.Show(
                    owner,
                    message,
                    ScenarioDeleteConfirmation.Title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    defaultButton);

            return result == MessageBoxResult.Yes;
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

            if (!ScenarioDeleteConfirmation.TryDelete(Scenarios, SelectedScenario, _confirmDelete, out var selection))
                return;

            SelectedScenario = selection;
        }
    }
}

