using System.Windows;
using System.Windows.Input;

namespace ExitPlan.UI.ViewModels
{
    public class MonteCarloSettingsViewModel : ViewModelBase
    {
        private readonly ScenarioViewModel _scenarioVm;
        private Window _window;

        private int _simulations;
        private int _seed;

        public MonteCarloSettingsViewModel(ScenarioViewModel scenarioVm)
        {
            _scenarioVm = scenarioVm;
            _simulations = scenarioVm.MonteCarloSimulations;
            _seed = scenarioVm.MonteCarloSeed;

            SaveCommand = new RelayCommand(SaveAndClose);
            RandomizeMonteCarloSeedCommand = new RelayCommand(RandomizeMonteCarloSeed);
        }

        public void AttachWindow(Window window) => _window = window;

        public int MonteCarloSimulations
        {
            get => _simulations;
            set
            {
                var count = Math.Max(1, value);
                if (_simulations != count)
                {
                    _simulations = count;
                    OnPropertyChanged();
                }
            }
        }

        public int MonteCarloSeed
        {
            get => _seed;
            set
            {
                if (_seed != value)
                {
                    _seed = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand RandomizeMonteCarloSeedCommand { get; }

        public void RandomizeMonteCarloSeed()
        {
            int next;
            do
            {
                next = Random.Shared.Next(1, int.MaxValue);
            } while (next == _seed);

            MonteCarloSeed = next;
        }

        private void SaveAndClose()
        {
            _scenarioVm.MonteCarloSimulations = MonteCarloSimulations;
            _scenarioVm.MonteCarloSeed = MonteCarloSeed;

            _window.DialogResult = true;
            _window.Close();
        }
    }
}
