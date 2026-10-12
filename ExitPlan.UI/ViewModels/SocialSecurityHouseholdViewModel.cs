using System.Linq;
using ExitPlan.Core;

namespace ExitPlan.UI.ViewModels
{
    public class SocialSecurityHouseholdViewModel : ViewModelBase
    {
        private readonly AccountsViewModel _accountsVM;
        private readonly ScenarioState _scenarioState;

        public event Action? OwnerListChanged;
        public event Action? AgeDataChanged;   // NEW

        public SocialSecurityViewModel Primary { get; }
        public SocialSecurityViewModel Spouse { get; }

        /// <summary>Ages 62–70 shown by the Claim Age ComboBox (Min/Max of the allowed range).</summary>
        public IReadOnlyList<int> ClaimAgeOptions => SocialSecurityClaiming.AllowedAges;
        public int MinClaimAge => SocialSecurityClaiming.MinAge;
        public int MaxClaimAge => SocialSecurityClaiming.MaxAge;

        public bool IsMarried
        {
            get => _scenarioState.IsMarried;
            set
            {
                if (_scenarioState.IsMarried != value)
                {
                    _scenarioState.IsMarried = value;
                    OnPropertyChanged();

                    if (value)
                        EnsureSpouseAccounts();

                    AgeDataChanged?.Invoke();   // marriage affects lifespan
                }
            }
        }

        public SocialSecurityHouseholdViewModel(
            AccountsViewModel accountsVM,
            ScenarioState scenarioState)
        {
            _accountsVM = accountsVM;
            _scenarioState = scenarioState;

            var primarySettings = _scenarioState.PrimarySocialSecurity
                                 ?? new SocialSecuritySettings();
            _scenarioState.PrimarySocialSecurity = primarySettings;

            Primary = new SocialSecurityViewModel(primarySettings, accountsVM);

            Primary.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SocialSecurityViewModel.Owner))
                    OwnerListChanged?.Invoke();

                if (e.PropertyName == nameof(SocialSecurityViewModel.CurrentAge) ||
                    e.PropertyName == nameof(SocialSecurityViewModel.ClaimAge) ||
                    e.PropertyName == nameof(SocialSecurityViewModel.DeathAge))
                {
                    AgeDataChanged?.Invoke();
                }
            };

            EnsurePrimaryAccounts();

            var spouseSettings = _scenarioState.SpouseSocialSecurity
                                ?? new SocialSecuritySettings();
            _scenarioState.SpouseSocialSecurity = spouseSettings;

            Spouse = new SocialSecurityViewModel(spouseSettings, accountsVM);

            Spouse.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SocialSecurityViewModel.Owner))
                    OwnerListChanged?.Invoke();

                if (e.PropertyName == nameof(SocialSecurityViewModel.CurrentAge) ||
                    e.PropertyName == nameof(SocialSecurityViewModel.ClaimAge) ||
                    e.PropertyName == nameof(SocialSecurityViewModel.DeathAge))
                {
                    AgeDataChanged?.Invoke();
                }
            };
        }

        private void EnsurePrimaryAccounts()
        {
            bool hasAccounts =
                _accountsVM.Accounts.Any(a => a.OwnerId == Primary.UserId);

            if (hasAccounts)
                return;

            int startingPriority = _accountsVM.Accounts.Count + 1;

            foreach (var acct in DefaultAccountsInitializer.CreateDefaultAccountsFor(
                Primary.UserId,
                Primary.Owner,
                startingPriority))
            {
                _accountsVM.Accounts.Add(acct);
            }
        }

        private void EnsureSpouseAccounts()
        {
            bool hasAccounts =
                _accountsVM.Accounts.Any(a => a.OwnerId == Spouse.UserId);

            if (hasAccounts)
                return;

            int startingPriority = _accountsVM.Accounts.Count + 1;

            foreach (var acct in DefaultAccountsInitializer.CreateDefaultAccountsFor(
                Spouse.UserId,
                Spouse.Owner,
                startingPriority))
            {
                _accountsVM.Accounts.Add(acct);
            }
        }
    }
}
