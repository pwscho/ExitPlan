using System.Windows;
using ExitPlan.UI.ViewModels;

namespace ExitPlan.UI.Views
{
    public partial class MonteCarloSettingsWindow : Window
    {
        public MonteCarloSettingsWindow(MonteCarloSettingsViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.AttachWindow(this);
        }
    }
}
