using System.Collections.Generic;
using System.Windows;

namespace ExitPlan.UI.Views;

public partial class MissingDataFilesWindow : Window
{
    public MissingDataFilesWindow(string folder, IReadOnlyList<string> missingFiles)
    {
        InitializeComponent();
        FolderText.Text = folder;
        FilesList.ItemsSource = missingFiles;
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
