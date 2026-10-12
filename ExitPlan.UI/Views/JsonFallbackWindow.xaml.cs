using Microsoft.Win32;
using System.IO;
using System.Text;
using System.Windows;

namespace ExitPlan.UI.Views
{
    public partial class JsonFallbackWindow : Window
    {
        private string? _currentFilePath;

        public JsonFallbackWindow(string title, string? initialPath = null)
        {
            InitializeComponent();
            Title = title;

            if (!string.IsNullOrWhiteSpace(initialPath) && File.Exists(initialPath))
            {
                _currentFilePath = initialPath;
                FilePathText.Text = initialPath;
                Editor.Text = File.ReadAllText(initialPath, Encoding.UTF8);
            }
        }

        private void OpenJson_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json",
                Title = "Select JSON File"
            };

            if (dlg.ShowDialog() == true)
            {
                _currentFilePath = dlg.FileName;
                FilePathText.Text = dlg.FileName;
                Editor.Text = File.ReadAllText(dlg.FileName, Encoding.UTF8);
            }
        }

        private void SaveJson_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFilePath == null)
            {
                MessageBox.Show("No file loaded.");
                return;
            }

            File.WriteAllText(_currentFilePath, Editor.Text, Encoding.UTF8);
            MessageBox.Show("Saved.");
        }
    }
}
