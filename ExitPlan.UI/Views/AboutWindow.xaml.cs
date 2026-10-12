using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Navigation;
using ExitPlan.UI.ViewModels;

namespace ExitPlan.UI.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = new AboutViewModel();
    }

    private void Link_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppLog.Write("Could not open " + e.Uri, ex);
            MessageBox.Show(
                this,
                "Could not open the link." + Environment.NewLine + ex.Message,
                "ExitPlan",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        e.Handled = true;
    }

    private void ShowLicense_Click(object sender, RoutedEventArgs e)
    {
        var path = AboutViewModel.LicenseFilePath;
        if (!File.Exists(path))
        {
            MessageBox.Show(
                this,
                "LICENSE.md was not shipped with this copy.",
                "ExitPlan",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var text = File.ReadAllText(path);
            var window = new LicenseTextWindow(text) { Owner = this };
            window.ShowDialog();
        }
        catch (Exception ex)
        {
            AppLog.Write("Could not read LICENSE.md.", ex);
            MessageBox.Show(
                this,
                "Could not read LICENSE.md." + Environment.NewLine + ex.Message,
                "ExitPlan",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CopyDataFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AboutViewModel viewModel)
            CopyText(viewModel.ActiveDataFolder);
    }

    private void CopyAppState_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is AboutViewModel viewModel)
            CopyText(viewModel.AppStatePath);
    }

    private void CopyText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            AppLog.Write("Could not copy to the clipboard.", ex);
            MessageBox.Show(
                this,
                "Could not copy to the clipboard." + Environment.NewLine + ex.Message,
                "ExitPlan",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
