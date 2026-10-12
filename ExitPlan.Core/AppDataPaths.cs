namespace ExitPlan.Core;

/// <summary>
/// Locations of files the user can edit (scenarios, tax years, RMD tables, market history).
/// Tests and unpackaged library calls leave <see cref="OverrideRoot"/> unset and keep reading
/// the copies next to the executable. The UI sets the override at startup so Velopack can
/// replace the install folder without deleting those files.
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
}
