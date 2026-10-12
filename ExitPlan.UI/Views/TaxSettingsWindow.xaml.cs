using System.ComponentModel;
using System.Windows;
using ExitPlan.UI.ViewModels;

namespace ExitPlan.UI.Views
{
    public partial class TaxSettingsWindow : Window
    {
        public TaxSettingsWindow(TaxSettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.AttachWindow(this);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DataContext is TaxSettingsViewModel vm && !vm.ConfirmClose())
                e.Cancel = true;

            base.OnClosing(e);
        }
    }
}
