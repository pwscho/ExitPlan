using System.Windows;
using ExitPlan.UI.ViewModels;

namespace ExitPlan.UI.Views
{
    public partial class GuardrailEditorWindow : Window
    {
        public GuardrailEditorWindow(GuardrailEditorViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.AttachWindow(this);
        }
    }
}

