using System.IO;
using ExitPlan.Core;
using Velopack;

namespace ExitPlan.UI;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Must be the first call. Velopack exits from inside Run() during install,
        // update, and uninstall hooks, so nothing above this line should do real work.
        // Auto-apply is off: an update is installed only after the user chooses Yes.
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .Run();

        DataLocationResolution location;
        try
        {
            location = DataLocationSettings.ActivateFromSavedChoice();
        }
        catch (Exception ex)
        {
            AppLog.Write("Could not apply the saved data folder. Using the default.", ex);
            if (string.IsNullOrWhiteSpace(AppDataPaths.OverrideRoot))
                AppDataPaths.OverrideRoot = AppDataPaths.DefaultRoot;
            location = DataLocationResolution.ForFolder(AppDataPaths.ActiveRoot, AppDataPaths.DefaultRoot);
        }

        if (location.ShouldTellUser)
            StartupNotices.DataFolderFallback = location.Notice;

        try
        {
            var migration = UserDataMigrator.MigrateToDefault();
            if (migration.Errors.Count > 0)
            {
                AppLog.Write(
                    "User data migration finished with errors." + Environment.NewLine +
                    string.Join(Environment.NewLine, migration.Errors));
            }
            else if (migration.CopiedFiles.Count > 0)
            {
                AppLog.Write($"Copied {migration.CopiedFiles.Count} user data file(s) to {migration.UserDataRoot}.");
            }
        }
        catch (Exception ex)
        {
            AppLog.Write("User data migration failed.", ex);
        }

        AppLog.Write(DataLocationSettings.FormatStartupLog(location));

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
