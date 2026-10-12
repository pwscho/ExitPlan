using System.Collections.ObjectModel;
using System.Windows.Input;
using ExitPlan.Core;

namespace ExitPlan.UI.ViewModels
{
    public class AccountsViewModel : ViewModelBase
    {
        public ObservableCollection<Account> Accounts { get; }

        // NEW: Required for DataGrid.SelectedItem binding
        private Account _selectedAccount;
        public Account SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                _selectedAccount = value;
                OnPropertyChanged();
            }
        }

        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }

        public AccountsViewModel(ObservableCollection<Account> accounts)
        {
            Accounts = accounts;

            MoveUpCommand = new RelayCommand<Account>(MoveAccountUp);
            MoveDownCommand = new RelayCommand<Account>(MoveAccountDown);

            SortByPriority();
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(Accounts));
        }

        private void MoveAccountUp(Account acct)
        {
            if (acct == null || acct.Priority <= 1)
                return;

            var above = Accounts.FirstOrDefault(a => a.Priority == acct.Priority - 1);
            if (above == null)
                return;

            int temp = acct.Priority;
            acct.Priority = above.Priority;
            above.Priority = temp;

            SortByPriority();
        }

        private void MoveAccountDown(Account acct)
        {
            if (acct == null)
                return;

            int maxPriority = Accounts.Count;

            if (acct.Priority >= maxPriority)
                return;

            var below = Accounts.FirstOrDefault(a => a.Priority == acct.Priority + 1);
            if (below == null)
                return;

            int temp = acct.Priority;
            acct.Priority = below.Priority;
            below.Priority = temp;

            SortByPriority();
        }

        private void SortByPriority()
        {
            var sorted = Accounts.OrderBy(a => a.Priority).ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                if (!ReferenceEquals(Accounts[i], sorted[i]))
                    Accounts.Move(Accounts.IndexOf(sorted[i]), i);
            }
        }
    }
}
