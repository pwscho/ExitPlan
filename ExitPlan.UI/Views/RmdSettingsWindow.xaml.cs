using System.ComponentModel;
using System.Windows;
using ExitPlan.UI.ViewModels;

namespace ExitPlan.UI.Views
{
    public partial class RmdSettingsWindow : Window
    {
        public RmdSettingsWindow(RmdSettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.AttachWindow(this);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DataContext is RmdSettingsViewModel vm && !vm.ConfirmClose())
                e.Cancel = true;

            base.OnClosing(e);
        }
    }
}
