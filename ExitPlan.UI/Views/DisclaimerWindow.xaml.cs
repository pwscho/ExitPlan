using System.Windows;

namespace ExitPlan.UI.Views
{
    public partial class DisclaimerWindow : Window
    {
        public DisclaimerWindow()
        {
            InitializeComponent();
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
