using ExitPlan.UI.ViewModels;
using System.Windows;

namespace ExitPlan.UI.Views
{
    public partial class ResultsSummary : Window
    {
        public ResultsSummary(ResultsSummaryViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
        }
    }
}

