using ExitPlan.Core;
using ExitPlan.UI.Views;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace ExitPlan.UI.ViewModels
{
    public class ScenarioViewModel : ViewModelBase
    {
        private readonly ScenarioState _state;

        private string _previousStrategy;

        public ICommand GuardrailEditorWindow { get; }

        public ICommand RandomizeMonteCarloSeedCommand { get; }

        public ICommand OpenMonteCarloSettingsCommand { get; }


        public ScenarioViewModel(ScenarioState state)
        {
            _state = state;

            IncomeVM = new IncomeViewModel(_state.Scenario);
            WithdrawalsVM = new WithdrawalsViewModel(_state.Scenario);

            _previousStrategy = _state.Scenario.SpendingStrategy;

            // Excel Export Command
            GuardrailEditorWindow= new RelayCommand(OpenGuardrailEditor);
            RandomizeMonteCarloSeedCommand = new RelayCommand(RandomizeMonteCarloSeed);
            OpenMonteCarloSettingsCommand = new RelayCommand(OpenMonteCarloSettings);
        }

        public Scenario Scenario => _state.Scenario;

        public string Name
        {
            get => _state.Name;
            set
            {
                if (_state.Name != value)
                {
                    _state.Name = value;
                    OnPropertyChanged();
                }
            }
        }

        public int BaseYear
        {
            get => Scenario.BaseYear;
            set
            {
                if (Scenario.BaseYear != value)
                {
                    Scenario.BaseYear = value;
                    OnPropertyChanged();
                }
            }
        }

        public int YearsToCalculate
        {
            get => Scenario.YearsToCalculate;
            set
            {
                if (Scenario.YearsToCalculate != value)
                {
                    Scenario.YearsToCalculate = value;
                    OnPropertyChanged();
                }
            }
        }

        public void NotifyYearsChanged()
        {
            OnPropertyChanged(nameof(YearsToCalculate));
        }

        public int MonteCarloSimulations
        {
            get => Scenario.MonteCarloSimulations;
            set
            {
                var count = Math.Max(1, value);
                if (Scenario.MonteCarloSimulations != count)
                {
                    Scenario.MonteCarloSimulations = count;
                    OnPropertyChanged();
                }
            }
        }

        public int MonteCarloSeed
        {
            get => Scenario.MonteCarloSeed;
            set
            {
                if (Scenario.MonteCarloSeed != value)
                {
                    Scenario.MonteCarloSeed = value;
                    OnPropertyChanged();
                }
            }
        }

        public void RandomizeMonteCarloSeed()
        {
            int next;
            do
            {
                next = Random.Shared.Next(1, int.MaxValue);
            } while (next == Scenario.MonteCarloSeed);

            MonteCarloSeed = next;
        }

        private void OpenMonteCarloSettings()
        {
            var vm = new MonteCarloSettingsViewModel(this);
            var window = new MonteCarloSettingsWindow(vm)
            {
                Owner = Application.Current.MainWindow
            };

            window.ShowDialog();
        }

        public int RetirementAge
        {
            get => Scenario.RetirementAge;
            set
            {
                Scenario.RetirementAge = value;
                OnPropertyChanged();
            }
        }

        public decimal AnnualSpending
        {
            get => Scenario.AnnualSpending;
            set
            {
                Scenario.AnnualSpending = value;
                OnPropertyChanged();
            }
        }

        // ============================================================
        // UPDATED: Spending Strategy with Guardrail Popup Logic
        // ============================================================
        public string SpendingStrategy
        {
            get => Scenario.SpendingStrategy;
            set
            {
                if (Scenario.SpendingStrategy != value)
                {
                    _previousStrategy = Scenario.SpendingStrategy;
                    Scenario.SpendingStrategy = value;
                    OnPropertyChanged();
                   
                    if (value == "Guardrails")
                        OpenGuardrailEditor();
                }
            }
        }

        private void OpenGuardrailEditor()
        {
            var vm = new GuardrailEditorViewModel(Scenario.Guardrails);
            var window = new GuardrailEditorWindow(vm)
            {
                Owner = Application.Current.MainWindow
            };

            bool? result = window.ShowDialog();

            if (result != true)
            {
                // User cancelled → revert strategy
                Scenario.SpendingStrategy = _previousStrategy;
                OnPropertyChanged(nameof(SpendingStrategy));
            }
            else
            {
                Scenario.InitialPortfolioValue = Scenario.Guardrails.InitialPortfolioValue;
                OnPropertyChanged(nameof(InitialPortfolioValue));
            }
        }

        // ============================================================

        public decimal InflationRate
        {
            get => Scenario.InflationRate;
            set
            {
                Scenario.InflationRate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InflationRatePct));
            }
        }

        public decimal InflationRatePct
        {
            get => Scenario.InflationRate * 100m;
            set
            {
                Scenario.InflationRate = Math.Abs(value) / 100m;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InflationRate));
            }
        }

        public decimal TaxesFromLastYear
        {
            get => Scenario.TaxesFromLastYear;
            set
            {
                Scenario.TaxesFromLastYear = value;
                OnPropertyChanged();
            }
        }

        public decimal InitialPortfolioValue
        {
            get => Scenario.Guardrails.InitialPortfolioValue;
            set
            {
                Scenario.Guardrails.InitialPortfolioValue = value;
                Scenario.InitialPortfolioValue = value;
                OnPropertyChanged();
            }
        }

        public void NotifyInitialPortfolioValueChanged()
        {
            OnPropertyChanged(nameof(InitialPortfolioValue));
        }

        public decimal WithdrawalAdjustmentRate
        {
            get => Scenario.WithdrawalAdjustmentRate;
            set
            {
                Scenario.WithdrawalAdjustmentRate = value;
                OnPropertyChanged();
            }
        }

        public bool RebalanceAnnually
        {
            get => Scenario.RebalanceAnnually;
            set
            {
                Scenario.RebalanceAnnually = value;
                OnPropertyChanged();
            }
        }

        public bool EnableTaxBracketInflation
        {
            get => Scenario.EnableTaxBracketInflation;
            set
            {
                Scenario.EnableTaxBracketInflation = value;
                OnPropertyChanged();
            }
        }

        public decimal TargetEquityPercentage
        {
            get => Scenario.TargetEquityPercentage;
            set
            {
                var eq = Math.Clamp(value, 0m, 1m);

                Scenario.TargetEquityPercentage = eq;
                Scenario.TargetBondPercentage = 1.0m - eq;

                OnPropertyChanged(nameof(TargetEquityPercentage));
                OnPropertyChanged(nameof(TargetBondPercentage));
                OnPropertyChanged(nameof(TargetEquityPct));
                OnPropertyChanged(nameof(TargetBondPct));
            }
        }

        public decimal TargetEquityPct
        {
            get => Scenario.TargetEquityPercentage * 100m;
            set
            {
                var eq = Math.Clamp(Math.Abs(value), 0m, 100m) / 100m;

                Scenario.TargetEquityPercentage = eq;
                Scenario.TargetBondPercentage = 1.0m - eq;

                OnPropertyChanged(nameof(TargetEquityPct));
                OnPropertyChanged(nameof(TargetBondPct));
                OnPropertyChanged(nameof(TargetEquityPercentage));
                OnPropertyChanged(nameof(TargetBondPercentage));
            }
        }

        public decimal TargetBondPercentage
        {
            get => Scenario.TargetBondPercentage;
            set
            {
                var bo = Math.Clamp(value, 0m, 1m);

                Scenario.TargetBondPercentage = bo;
                Scenario.TargetEquityPercentage = 1.0m - bo;

                OnPropertyChanged(nameof(TargetBondPercentage));
                OnPropertyChanged(nameof(TargetEquityPercentage));
                OnPropertyChanged(nameof(TargetBondPct));
                OnPropertyChanged(nameof(TargetEquityPct));
            }
        }

        public decimal TargetBondPct
        {
            get => Scenario.TargetBondPercentage * 100m;
            set
            {
                var bo = Math.Clamp(Math.Abs(value), 0m, 100m) / 100m;

                Scenario.TargetBondPercentage = bo;
                Scenario.TargetEquityPercentage = 1.0m - bo;

                OnPropertyChanged(nameof(TargetBondPct));
                OnPropertyChanged(nameof(TargetEquityPct));
                OnPropertyChanged(nameof(TargetBondPercentage));
                OnPropertyChanged(nameof(TargetEquityPercentage));
            }
        }

        public IncomeViewModel IncomeVM { get; }
        public WithdrawalsViewModel WithdrawalsVM { get; }

        public void RefreshOwnerOptions(
            string primaryName,
            Guid primaryId,
            string? spouseName,
            Guid? spouseId)
        {
            IncomeVM.RefreshOwnerOptions(primaryName, primaryId, spouseName, spouseId);
            WithdrawalsVM.RefreshOwnerOptions(primaryName, primaryId, spouseName, spouseId);
        }
    }
}
