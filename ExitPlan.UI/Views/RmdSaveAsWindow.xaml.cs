using System.Windows;
using System.Windows.Controls;
using ExitPlan.Core;

namespace ExitPlan.UI.Views
{
    public partial class RmdSaveAsWindow : Window
    {
        private readonly HashSet<int> _existingYears;

        public int SelectedYear { get; private set; }

        public RmdSaveAsWindow(int suggestedYear, IEnumerable<int> existingYears)
        {
            InitializeComponent();
            _existingYears = existingYears.ToHashSet();
            YearBox.Text = suggestedYear.ToString();
            UpdatePreview();
        }

        private void YearBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

        private void UpdatePreview()
        {
            if (!int.TryParse(YearBox.Text, out int year) || year < 1900 || year > 3000)
            {
                FileNamePreview.Text = "RMD{year}.json";
                ErrorText.Text = "Enter a valid calendar year.";
                CreateButton.IsEnabled = false;
                return;
            }

            FileNamePreview.Text = RmdPolicyLoader.FileNameForYear(year);

            if (_existingYears.Contains(year))
            {
                ErrorText.Text = $"{RmdPolicyLoader.FileNameForYear(year)} already exists. Duplicate years are not allowed.";
                CreateButton.IsEnabled = false;
                return;
            }

            ErrorText.Text = string.Empty;
            CreateButton.IsEnabled = true;
            SelectedYear = year;
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (!CreateButton.IsEnabled)
                return;

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
