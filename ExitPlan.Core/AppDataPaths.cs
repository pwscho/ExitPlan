namespace ExitPlan.Core;

/// <summary>
/// Locations of files the user can edit (scenarios, tax years, RMD tables, market history).
/// The UI sets <see cref="OverrideRoot"/> at startup to the active data folder. Tests and
/// unpackaged library calls leave it unset and keep reading the copies next to the executable.
/// A saved choice lives in <see cref="SettingsFilePath"/>, under %LocalAppData%\ExitPlan,
/// which is outside the Velopack <c>current</c> folder an update replaces.
/// </summary>
public static class AppDataPaths
{
    public const string DataFolderName = "Data";
    public const string TaxDataFolderName = "TaxData";
    public const string RmdDataFolderName = "RMDData";
    public const string MarketDataFolderName = "MarketData";
    public const string AppStateFileName = "AppState.json";
    public const string MarketDataFileName = "MarketData.json";
    public const string LogFolderName = "Logs";
    public const string SettingsFileName = "data-location.json";

    /// <summary>
    /// When set, loaders read and write under this directory instead of <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    public static string? OverrideRoot { get; set; }

    public static string ActiveRoot =>
        string.IsNullOrWhiteSpace(OverrideRoot) ? AppContext.BaseDirectory : OverrideRoot!;

    public static string DataDirectory => Path.Combine(ActiveRoot, DataFolderName);

    public static string AppStatePath => Path.Combine(DataDirectory, AppStateFileName);

    public static string TaxDataDirectory => Path.Combine(ActiveRoot, TaxDataFolderName);

    public static string RmdDataDirectory => Path.Combine(ActiveRoot, RmdDataFolderName);

    public static string MarketDataDirectory => Path.Combine(ActiveRoot, MarketDataFolderName);

    public static string MarketDataPath => Path.Combine(MarketDataDirectory, MarketDataFileName);

    public static string LogDirectory => Path.Combine(ActiveRoot, LogFolderName);

    /// <summary>
    /// Documents\ExitPlan, unless EXITPLAN_DATA_DIR is set. If Documents is unavailable,
    /// %LocalAppData%\ExitPlan.
    /// </summary>
    public static string DefaultRoot
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable("EXITPLAN_DATA_DIR");
            if (!string.IsNullOrWhiteSpace(configured))
                return configured.Trim();

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(documents))
                return Path.Combine(documents, "ExitPlan");

            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "ExitPlan");
        }
    }

    /// <summary>
    /// %LocalAppData%\ExitPlan\data-location.json. Velopack updates replace
    /// %LocalAppData%\ExitPlan\current and leave this file in place.
    /// </summary>
    public static string SettingsFilePath
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "ExitPlan", SettingsFileName);
        }
    }

    public static bool IsDefaultFolder(string folder) => PathsEqual(folder, DefaultRoot);

    public static bool PathsEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        try
        {
            return string.Equals(
                TrimTrailingSeparator(Path.GetFullPath(left)),
                TrimTrailingSeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return string.Equals(
                TrimTrailingSeparator(left),
                TrimTrailingSeparator(right),
                StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// AppState.json lives at {data folder}\Data\AppState.json. Picking that file selects the data folder.
    /// </summary>
    public static bool TryResolveDataFolderFromAppStateFile(
        string appStateFile,
        out string dataFolder,
        out string error)
    {
        dataFolder = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(appStateFile))
        {
            error = "Choose an AppState.json file.";
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(appStateFile);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        if (!File.Exists(full))
        {
            error = "That file does not exist.";
            return false;
        }

        if (!Path.GetFileName(full).Equals(AppStateFileName, StringComparison.OrdinalIgnoreCase))
        {
            error = "Choose a file named AppState.json.";
            return false;
        }

        var dataDirectory = Path.GetDirectoryName(full);
        if (string.IsNullOrWhiteSpace(dataDirectory)
            || !Path.GetFileName(TrimTrailingSeparator(dataDirectory))
                .Equals(DataFolderName, StringComparison.OrdinalIgnoreCase))
        {
            error = "AppState.json has to sit inside a Data folder, for example Documents\\ExitPlan\\Data\\AppState.json.";
            return false;
        }

        var root = Path.GetDirectoryName(TrimTrailingSeparator(dataDirectory));
        if (string.IsNullOrWhiteSpace(root))
        {
            error = "Could not determine the data folder for that file.";
            return false;
        }

        dataFolder = root;
        return true;
    }

    /// <summary>
    /// A folder picker may return the Data subfolder. The active folder is the parent that
    /// contains Data, TaxData, RMDData, and MarketData.
    /// </summary>
    public static string NormalizeChosenFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("Folder is required.", nameof(folder));

        var full = Path.GetFullPath(folder);
        var name = Path.GetFileName(TrimTrailingSeparator(full));
        if (!name.Equals(DataFolderName, StringComparison.OrdinalIgnoreCase))
            return full;

        var looksLikeContentRoot =
            Directory.Exists(Path.Combine(full, TaxDataFolderName))
            || Directory.Exists(Path.Combine(full, RmdDataFolderName))
            || Directory.Exists(Path.Combine(full, MarketDataFolderName))
            || Directory.Exists(Path.Combine(full, DataFolderName));

        if (looksLikeContentRoot)
            return full;

        if (File.Exists(Path.Combine(full, AppStateFileName)))
        {
            var parent = Path.GetDirectoryName(TrimTrailingSeparator(full));
            if (!string.IsNullOrWhiteSpace(parent))
                return parent;
        }

        return full;
    }

    private static string TrimTrailingSeparator(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
