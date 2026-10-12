using System.ComponentModel;
using System.Windows;
using ExitPlan.UI.ViewModels;

namespace ExitPlan.UI.Views
{
    public partial class MarketDataWindow : Window
    {
        public MarketDataWindow(MarketDataViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.AttachWindow(this);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DataContext is MarketDataViewModel vm && !vm.ConfirmClose())
                e.Cancel = true;

            base.OnClosing(e);
        }
    }
}
