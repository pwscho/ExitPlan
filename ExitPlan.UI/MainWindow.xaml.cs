using ExitPlan.Core;
using ExitPlan.UI.Updates;
using ExitPlan.UI.ViewModels;
using ExitPlan.UI.Views;
using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        private void OpenAppState_Click(object sender, RoutedEventArgs e)
        {
            var initialDirectory = Directory.Exists(AppDataPaths.DataDirectory)
                ? AppDataPaths.DataDirectory
                : AppDataPaths.ActiveRoot;

            var dialog = new OpenFileDialog
            {
                Title = "Open AppState.json",
                Filter = "AppState.json|AppState.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
                FileName = AppDataPaths.AppStateFileName,
                CheckFileExists = true
            };
            if (Directory.Exists(initialDirectory))
                dialog.InitialDirectory = initialDirectory;

            if (dialog.ShowDialog(this) != true)
                return;

            if (!AppDataPaths.TryResolveDataFolderFromAppStateFile(dialog.FileName, out var folder, out var error))
            {
                MessageBox.Show(this, error, "Open AppState.json", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SwitchToFolder(folder, forgetSavedChoice: false);
        }

        private void ChooseDataFolder_Click(object sender, RoutedEventArgs e)
        {
            var initial = Directory.Exists(AppDataPaths.ActiveRoot)
                ? AppDataPaths.ActiveRoot
                : AppDataPaths.DefaultRoot;

            var dialog = new OpenFolderDialog
            {
                Title = "Choose the data folder that contains Data, TaxData, RMDData, and MarketData",
                Multiselect = false
            };
            if (Directory.Exists(initial))
                dialog.InitialDirectory = initial;

            if (dialog.ShowDialog(this) != true || string.IsNullOrWhiteSpace(dialog.FolderName))
                return;

            SwitchToFolder(dialog.FolderName, forgetSavedChoice: false);
        }

        private void ResetDataFolder_Click(object sender, RoutedEventArgs e)
        {
            SwitchToFolder(AppDataPaths.DefaultRoot, forgetSavedChoice: true);
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folder = AppDataPaths.ActiveRoot;
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = folder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AppLog.Write("Could not open the data folder.", ex);
                MessageBox.Show(
                    this,
                    "Could not open the data folder." + Environment.NewLine + ex.Message,
                    "Open Data Folder",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SwitchToFolder(string folder, bool forgetSavedChoice)
        {
            if (!DataFolderController.ConfirmMissing(this, folder, out var prepared))
                return;

            try
            {
                if (DataContext is MainViewModel current)
                    current.SaveCommand.Execute(null);
            }
            catch (Exception ex)
            {
                AppLog.Write("Could not save the current scenario before switching data folders.", ex);
                MessageBox.Show(
                    this,
                    "Could not save the current scenario." + Environment.NewLine + ex.Message,
                    "ExitPlan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (!CloseOwnedWindows())
                return;

            if (!DataFolderController.CopyMissingBundled(prepared, out var copyError))
            {
                AppLog.Write("Could not create missing data files." + Environment.NewLine + copyError);
                MessageBox.Show(
                    this,
                    "ExitPlan could not create the missing files:" + Environment.NewLine + Environment.NewLine + copyError,
                    "ExitPlan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var previous = AppDataPaths.OverrideRoot;
            AppDataPaths.OverrideRoot = prepared;
            MainViewModel replacement;
            try
            {
                replacement = new MainViewModel();
            }
            catch (Exception ex)
            {
                AppDataPaths.OverrideRoot = previous;
                AppLog.Write("Could not load the data folder.", ex);
                MessageBox.Show(
                    this,
                    "Could not load that data folder." + Environment.NewLine + ex.Message,
                    "ExitPlan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            try
            {
                if (forgetSavedChoice)
                    DataLocationSettings.Clear(AppDataPaths.SettingsFilePath);
                else
                    DataLocationSettings.Save(AppDataPaths.SettingsFilePath, prepared);
            }
            catch (Exception ex)
            {
                AppLog.Write("Could not remember the data folder.", ex);
                MessageBox.Show(
                    this,
                    "ExitPlan switched to that folder for this session, but could not save the choice."
                    + Environment.NewLine + ex.Message,
                    "ExitPlan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            DataContext = replacement;
            AppLog.Write(
                "Switched data folder." + Environment.NewLine
                + DataLocationSettings.FormatStartupLog(
                    DataLocationResolution.ForFolder(prepared, AppDataPaths.DefaultRoot)));
        }

        private bool CloseOwnedWindows()
        {
            foreach (var window in OwnedWindows.OfType<Window>().ToList())
            {
                window.Close();
                if (window.IsVisible)
                    return false;
            }

            return true;
        }



    }



}

