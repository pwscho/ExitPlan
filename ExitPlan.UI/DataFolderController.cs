using System.Windows;
using ExitPlan.Core;

namespace ExitPlan.UI;

internal static class DataFolderController
{
    public static bool ConfirmMissing(Window owner, string folder, out string prepared)
    {
        prepared = AppDataPaths.NormalizeChosenFolder(folder);
        var missing = BundledDataSeed.ListMissing(prepared, BundledDataSeed.BundleRoot);
        if (missing.Count == 0)
            return true;

        var dialog = new Views.MissingDataFilesWindow(prepared, missing)
        {
            Owner = owner
        };
        return dialog.ShowDialog() == true;
    }

    public static bool CopyMissingBundled(string prepared, out string error)
    {
        var result = BundledDataSeed.CopyMissing(prepared, BundledDataSeed.BundleRoot);
        if (result.Errors.Count == 0)
        {
            error = string.Empty;
            return true;
        }

        error = string.Join(Environment.NewLine, result.Errors);
        return false;
    }
}
