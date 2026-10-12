using ExitPlan.UI.Updates;
using ExitPlan.UI.ViewModels;
using ExitPlan.UI.Views;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ExitPlan.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.SaveCommand.Execute(null);

            base.OnClosing(e);
        }

        private void OpenTaxSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new TaxSettingsWindow(new TaxSettingsViewModel())
            {
                Owner = this
            };
            win.Show();
        }

        private void OpenRmdSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new RmdSettingsWindow(new RmdSettingsViewModel())
            {
                Owner = this
            };
            win.Show();
        }

        private void OpenMarketData_Click(object sender, RoutedEventArgs e)
        {
            var win = new MarketDataWindow(new MarketDataViewModel())
            {
                Owner = this
            };
            win.Show();
        }

        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            await UpdateChecker.CheckForUpdatesAsync(interactive: true);
        }



    }



}

