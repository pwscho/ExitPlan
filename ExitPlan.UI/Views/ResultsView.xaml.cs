using ExitPlan.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ExitPlan.UI.Views
{
    public partial class ResultsView : UserControl
    {
        public ResultsView()
        {
            InitializeComponent();
        }

        // Called by MainViewModel AFTER AccountSummary is populated
        public void BuildSummaryColumns()
        {
            if (DataContext is not MainViewModel vm)
                return;

            if (vm.AccountSummary.Count == 0)
                return;

            // Find the SummaryGrid by name
            var grid = this.FindName("SummaryGrid") as DataGrid;
            if (grid == null)
                return;

            // Clear all dynamic columns (keep only the first static "Year" column)
            while (grid.Columns.Count > 1)
                grid.Columns.RemoveAt(1);

            // Build dynamic columns from the first row's keys
            var keys = vm.AccountSummary[0].Totals.Keys;

            foreach (var key in keys)
            {
                var col = new DataGridTextColumn
                {
                    Header = key,
                    Binding = new Binding($"Totals[{key}]")
                    {
                        StringFormat = "C0"
                    }
                };

                grid.Columns.Add(col);
            }
        }
    }
}

