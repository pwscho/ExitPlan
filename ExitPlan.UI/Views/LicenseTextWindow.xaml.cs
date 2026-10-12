using System.Windows;

namespace ExitPlan.UI.Views;

public partial class LicenseTextWindow : Window
{
    public LicenseTextWindow(string text)
    {
        InitializeComponent();
        LicenseText.Text = text;
    }
}
