using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using ExitPlan.UI.ViewModels;

namespace ExitPlan.UI.Views
{
    public partial class SaveTaxYearWindow : Window
    {
        private static readonly Regex DigitsOnly = new("^[0-9]+$", RegexOptions.Compiled);

        public SaveTaxYearWindow(int suggestedYear, string destinationFolder)
        {
            InitializeComponent();
            DataContext = new SaveTaxYearViewModel(suggestedYear, destinationFolder, Accept);
            Loaded += (_, _) =>
            {
                YearBox.Focus();
                YearBox.SelectAll();
            };
        }

        public int ChosenYear => ((SaveTaxYearViewModel)DataContext).ChosenYear;

        private void Accept()
        {
            DialogResult = true;
        }

        private void YearBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !DigitsOnly.IsMatch(e.Text);
        }

        private void YearBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            var text = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            if (!DigitsOnly.IsMatch(text))
                e.CancelCommand();
        }
    }
}
