using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ExitPlan.Core
{
    public class ScenarioState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string ScenarioId { get; set; } = Guid.NewGuid().ToString();

        public Scenario Scenario { get; set; } = new Scenario();

        private int _yearsToCalculate;
        public int YearsToCalculate
        {
            get => _yearsToCalculate;
            set
            {
                if (_yearsToCalculate != value)
                {
                    _yearsToCalculate = value;
                    OnPropertyChanged(nameof(YearsToCalculate));
                }
            }
        }


        // Name now forwards to Scenario.Name and notifies UI
        public string Name
        {
            get => Scenario?.Name ?? "";
            set
            {
                if (Scenario != null && Scenario.Name != value)
                {
                    Scenario.Name = value;
                    OnPropertyChanged(); // refreshes Scenario Selector
                }
                Debug.WriteLine($"STATE Name setter: state={this.GetHashCode()}");

            }
        }

        public SocialSecuritySettings? PrimarySocialSecurity { get; set; }
        public SocialSecuritySettings? SpouseSocialSecurity { get; set; }
        public bool IsMarried { get; set; }

        public List<Account> Accounts { get; set; } = new();

        public ScenarioState DeepCopy()
        {
            return new ScenarioState
            {
                ScenarioId = Guid.NewGuid().ToString(),

                // Name now comes from Scenario.Name
                Scenario = this.Scenario.DeepCopy(),

                // These remain unchanged
                IsMarried = this.IsMarried,
                Accounts = this.Accounts.Select(a => a.DeepCopy()).ToList(),
                PrimarySocialSecurity = this.PrimarySocialSecurity?.DeepCopy(),
                SpouseSocialSecurity = this.SpouseSocialSecurity?.DeepCopy()
            };
        }
    }
}


