using System.Linq;
using ExitPlan.Core;

namespace ExitPlan.UI.ViewModels
{
    public class SocialSecurityViewModel : ViewModelBase
    {
        private readonly AccountsViewModel _accountsVM;

        public SocialSecuritySettings Settings { get; }

        public Guid UserId => Settings.UserId;

        public int MinClaimAge => SocialSecurityClaiming.MinAge;
        public int MaxClaimAge => SocialSecurityClaiming.MaxAge;
        public IReadOnlyList<int> AllowedClaimAges => SocialSecurityClaiming.AllowedAges;

        public SocialSecurityViewModel(SocialSecuritySettings settings, AccountsViewModel accountsVM)
        {
            Settings = settings;
            _accountsVM = accountsVM;

            // Clamp persisted / JSON values so the bound control shows 62–70 after load.
            Settings.ClaimAge = SocialSecurityClaiming.Clamp(settings.ClaimAge);
        }

        public string Owner
        {
            get => Settings.Owner;
            set
            {
                if (Settings.Owner != value)
                {
                    Settings.Owner = value;
                    OnPropertyChanged(nameof(Owner));
                    UpdateAccountOwnerNames();
                }
            }
        }

        public int CurrentAge
        {
            get => Settings.CurrentAge;
            set
            {
                if (Settings.CurrentAge != value)
                {
                    Settings.CurrentAge = value;
                    OnPropertyChanged(nameof(CurrentAge));
                }
            }
        }

        public int ClaimAge
        {
            get => Settings.ClaimAge;
            set
            {
                var clamped = SocialSecurityClaiming.Clamp(value);
                if (Settings.ClaimAge != clamped)
                {
                    Settings.ClaimAge = clamped;
                    OnPropertyChanged(nameof(ClaimAge));
                }
                else if (value != clamped)
                {
                    // Out-of-range input is rejected; notify so the control snaps back.
                    OnPropertyChanged(nameof(ClaimAge));
                }
            }
        }

        public int DeathAge
        {
            get => Settings.DeathAge;
            set
            {
                if (Settings.DeathAge != value)
                {
                    Settings.DeathAge = value;
                    OnPropertyChanged(nameof(DeathAge));
                }
            }
        }

        public decimal BenefitAtFullRetirementAge
        {
            get => Settings.BenefitAtFullRetirementAge;
            set
            {
                if (Settings.BenefitAtFullRetirementAge != value)
                {
                    Settings.BenefitAtFullRetirementAge = value;
                    OnPropertyChanged(nameof(BenefitAtFullRetirementAge));
                }
            }
        }

        public decimal ColaRate
        {
            get => Settings.Cola;
            set
            {
                Settings.Cola = value;
                OnPropertyChanged();
            }
        }

        public bool IsSurvivorBenefit
        {
            get => Settings.IsSurvivorBenefit;
            set
            {
                if (Settings.IsSurvivorBenefit != value)
                {
                    Settings.IsSurvivorBenefit = value;
                    OnPropertyChanged(nameof(IsSurvivorBenefit));
                }
            }
        }

        public decimal SurvivorBenefitAmount
        {
            get => Settings.SurvivorBenefitAmount;
            set
            {
                if (Settings.SurvivorBenefitAmount != value)
                {
                    Settings.SurvivorBenefitAmount = value;
                    OnPropertyChanged(nameof(SurvivorBenefitAmount));
                }
            }
        }

        private void UpdateAccountOwnerNames()
        {
            if (_accountsVM == null)
                return;

            foreach (var acct in _accountsVM.Accounts.Where(a => a.OwnerId == UserId))
                acct.Owner = Settings.Owner;

            _accountsVM.Refresh();
        }
    }
}
