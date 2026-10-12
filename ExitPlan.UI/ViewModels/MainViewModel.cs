using ExitPlan.Core;
using ExitPlan.Core.Taxes;
using ExitPlan.Core.Engine;
using ExitPlan.Core.Services;
using ExitPlan.UI.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using ExitPlan.UI.Reporting;
using System.Windows.Input;
using ExitPlan.Core.MarketData;

namespace ExitPlan.UI.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private AppState _state;

        public ScenarioManagerViewModel ScenarioManager { get; }
        public ScenarioViewModel ScenarioVM { get; private set; }
        public AccountsViewModel AccountsVM { get; private set; }
        public IncomeViewModel IncomeVM { get; private set; }
        public WithdrawalsViewModel WithdrawalsVM { get; private set; }
        public RothConversionViewModel RothConversionVM { get; private set; }

        public SocialSecurityHouseholdViewModel SocialSecurityVM { get; private set; }

        public ICommand RunSimulationCommand { get; }
        public RelayCommand SaveCommand { get; }
        public ICommand RunReportCommand { get; }

        public ObservableCollection<ResultRow> Results { get; } = new();
        public ObservableCollection<AccountBalanceRow> AccountBalances { get; } = new();
        public ObservableCollection<AccountSummaryRow> AccountSummary { get; } = new();

       
        public MainViewModel()
        {
            _state = DataService.Load();

            ScenarioManager = new ScenarioManagerViewModel();

            foreach (var s in _state.Scenarios)
                ScenarioManager.Scenarios.Add(s);

            if (ScenarioManager.Scenarios.Count == 0)
            {
                var defaultScenario = new ScenarioState { Name = "My First Scenario" };
                ScenarioManager.Scenarios.Add(defaultScenario);
            }

            ScenarioManager.SelectedScenario =
                ScenarioManager.Scenarios.FirstOrDefault(s => s.ScenarioId == _state.LastSelectedScenarioId)
                ?? ScenarioManager.Scenarios.First();

            LoadScenario(ScenarioManager.SelectedScenario);

            ScenarioManager.ScenarioSwitching += (s, oldScenario) =>
            {
                SaveScenario();
            };

            ScenarioManager.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ScenarioManager.SelectedScenario))
                {
                    if (ScenarioManager.SelectedScenario != null)
                        LoadScenario(ScenarioManager.SelectedScenario);
                }
            };

            RunSimulationCommand = new AsyncRelayCommand(RunSimulation);
            SaveCommand = new RelayCommand(SaveScenario);
            RunReportCommand = new AsyncRelayCommand(RunReport);

        }

        private void LoadScenario(ScenarioState scenario)
        {
            ScenarioVM = new ScenarioViewModel(scenario);

            AccountsVM = new AccountsViewModel(
                new ObservableCollection<Account>(scenario.Accounts)
            );

            SocialSecurityVM = new SocialSecurityHouseholdViewModel(AccountsVM, scenario);

            IncomeVM = new IncomeViewModel(scenario.Scenario);
            WithdrawalsVM = new WithdrawalsViewModel(scenario.Scenario);
            RothConversionVM = new RothConversionViewModel(scenario.Scenario);

            RefreshOwnerLists();

            SocialSecurityVM.OwnerListChanged += () =>
            {
                RefreshOwnerLists();
                RecalculateYears();
            };

            SocialSecurityVM.Primary.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SocialSecurityViewModel.CurrentAge) ||
                    e.PropertyName == nameof(SocialSecurityViewModel.ClaimAge) ||
                    e.PropertyName == nameof(SocialSecurityViewModel.DeathAge))
                {
                    RecalculateYears();
                }
            };

            if (SocialSecurityVM.IsMarried && SocialSecurityVM.Spouse != null)
            {
                SocialSecurityVM.Spouse.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SocialSecurityViewModel.CurrentAge) ||
                        e.PropertyName == nameof(SocialSecurityViewModel.ClaimAge) ||
                        e.PropertyName == nameof(SocialSecurityViewModel.DeathAge))
                    {
                        RecalculateYears();
                    }
                };
            }

            RecalculateYears();

            OnPropertyChanged(nameof(ScenarioVM));
            OnPropertyChanged(nameof(AccountsVM));
            OnPropertyChanged(nameof(SocialSecurityVM));
            OnPropertyChanged(nameof(IncomeVM));
            OnPropertyChanged(nameof(WithdrawalsVM));
            OnPropertyChanged(nameof(RothConversionVM));
        }

        private void RefreshOwnerLists()
        {
            IncomeVM.RefreshOwnerOptions(
                SocialSecurityVM.Primary.Owner,
                SocialSecurityVM.Primary.UserId,
                SocialSecurityVM.IsMarried ? SocialSecurityVM.Spouse?.Owner : null,
                SocialSecurityVM.IsMarried ? SocialSecurityVM.Spouse?.UserId : null
            );

            WithdrawalsVM.RefreshOwnerOptions(
                SocialSecurityVM.Primary.Owner,
                SocialSecurityVM.Primary.UserId,
                SocialSecurityVM.IsMarried ? SocialSecurityVM.Spouse?.Owner : null,
                SocialSecurityVM.IsMarried ? SocialSecurityVM.Spouse?.UserId : null
            );

            RothConversionVM.RefreshOwnerOptions(
                SocialSecurityVM.Primary.Owner,
                SocialSecurityVM.Primary.UserId,
                SocialSecurityVM.IsMarried ? SocialSecurityVM.Spouse?.Owner : null,
                SocialSecurityVM.IsMarried ? SocialSecurityVM.Spouse?.UserId : null
            );
        }

        private void RecalculateYears()
        {
            int primaryYears = SocialSecurityVM.Primary.DeathAge - SocialSecurityVM.Primary.CurrentAge;

            int spouseYears = 0;
            if (SocialSecurityVM.IsMarried && SocialSecurityVM.Spouse != null)
            {
                spouseYears = SocialSecurityVM.Spouse.DeathAge - SocialSecurityVM.Spouse.CurrentAge;
            }
            
            ScenarioVM.Scenario.YearsToCalculate = Math.Max(primaryYears, spouseYears);
            ScenarioVM.NotifyYearsChanged();
        }

        private void SaveScenario()
        {
            var selected = ScenarioManager.SelectedScenario;
            if (selected == null)
                return;

            selected.Accounts = AccountsVM.Accounts.ToList();

            _state.LastSelectedScenarioId = selected.ScenarioId;

            _state.Scenarios = ScenarioManager.Scenarios.ToList();

            DataService.Save(_state);
        }

        private async Task RunReport()
        {
            if (ScenarioVM == null || AccountsVM == null || SocialSecurityVM == null)
                return;

            var busy = new ExitPlan.UI.Views.BusyWindow()
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            busy.Show();

            // Allow WPF to render the BusyWindow before heavy work starts
            await Task.Yield();

            try
            {
                // Run the heavy report generation OFF the UI thread
                var report = await Task.Run(() =>
                {
                    var builder = new ReportBuilder();

                    return builder.BuildReport(
                        ScenarioVM.Scenario,
                        AccountsVM.Accounts.ToList(),
                        SocialSecurityVM.Primary.Settings,
                        SocialSecurityVM.IsMarried ? SocialSecurityVM.Spouse?.Settings : null,
                        MarketHistoryLoader.Load()
                    );
                });

                // Back on UI thread: show the results window
                var vm = new ResultsSummaryViewModel(report);
                var window = new ResultsSummary(vm);
                window.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Report failed:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                busy.Close();
            }
        }

        // ---------------------------------------------------------------------
        // NEW FLAT SIMULATION ENGINE
        // ---------------------------------------------------------------------
        private async Task RunSimulation()
        {
            if (ScenarioVM == null || AccountsVM == null || SocialSecurityVM == null)
                return;


            try
            {
                // Run ComputeEngine.Run() on a background thread
                var output = await Task.Run(() =>
                {
                    var engine = new ComputeEngine(
                        ScenarioVM.Scenario,
                        AccountsVM.Accounts.ToList(),
                        new FixedMarketService(),
                        SocialSecurityVM.Primary.Settings,
                        SocialSecurityVM.IsMarried ? SocialSecurityVM.Spouse?.Settings : null
                    );

                    return engine.Run(1, "fixed");
                });

                // UI updates
                Results.Clear();
                AccountBalances.Clear();
                AccountSummary.Clear();

                foreach (var row in output.Results)
                    Results.Add(row);

                foreach (var acct in output.Accounts)
                {
                    AccountBalances.Add(new AccountBalanceRow
                    {
                        AccountName = $"{acct.Owner} - {acct.TaxBucket} - {acct.AssetClass}",
                        Owner = acct.Owner,
                        TaxBucket = acct.TaxBucket,
                        AssetClass = acct.AssetClass,
                        Year = acct.Year,
                        Age = acct.Age,
                        StartingBalance = acct.StartingValue,
                        InvestmentReturn = acct.InvestmentReturn,
                        EndingBalance = acct.EndingValue
                    });
                }

                var years = output.Accounts
                    .Select(a => a.Year)
                    .Distinct()
                    .OrderBy(y => y);

                foreach (var year in years)
                {
                    var summaryRow = new AccountSummaryRow { Year = year };

                    var groups = output.Accounts
                        .Where(a => a.Year == year)
                        .GroupBy(a => $"{a.Owner} - {a.TaxBucket}");

                    foreach (var g in groups)
                        summaryRow.Totals[g.Key] = g.Sum(a => a.EndingValue);

                    AccountSummary.Add(summaryRow);
                }

                ScenarioVM.NotifyInitialPortfolioValueChanged();

                var resultsView = FindResultsView(Application.Current.MainWindow);
                resultsView?.BuildSummaryColumns();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Simulation failed:\n{ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {

            }
        }


        private ResultsView FindResultsView(DependencyObject parent)
        {
            if (parent == null)
                return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is ResultsView rv)
                    return rv;

                var result = FindResultsView(child);
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}


