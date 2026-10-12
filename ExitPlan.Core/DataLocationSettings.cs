using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExitPlan.Core;

/// <summary>
/// Remembers the active data folder in %LocalAppData%\ExitPlan\data-location.json
/// so a Velopack update (which replaces the install's current folder) keeps the choice.
/// </summary>
public static class DataLocationSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static DataLocationResolution ActivateFromSavedChoice()
        => Activate(
            AppDataPaths.SettingsFilePath,
            Environment.GetEnvironmentVariable("EXITPLAN_DATA_DIR"),
            AppDataPaths.DefaultRoot);

    public static DataLocationResolution Activate(
        string settingsPath,
        string? environmentOverride,
        string defaultFolder)
    {
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            var folder = environmentOverride.Trim();
            AppDataPaths.OverrideRoot = folder;
            return DataLocationResolution.ForEnvironment(folder, SafeFullPath(defaultFolder));
        }

        var saved = TryRead(settingsPath);
        if (!string.IsNullOrWhiteSpace(saved.Error))
        {
            var fallback = SafeFullPath(defaultFolder);
            AppDataPaths.OverrideRoot = fallback;
            return DataLocationResolution.ForUnreadableSettings(fallback, saved.Error!);
        }

        var resolution = Resolve(defaultFolder, saved.Folder);
        AppDataPaths.OverrideRoot = resolution.ActiveFolder;
        return resolution;
    }

    public static DataLocationResolution Resolve(string defaultFolder, string? rememberedFolder)
    {
        var fallback = SafeFullPath(defaultFolder);
        if (string.IsNullOrWhiteSpace(rememberedFolder))
            return DataLocationResolution.ForFolder(fallback, fallback);

        string full;
        try
        {
            full = Path.GetFullPath(rememberedFolder);
        }
        catch (Exception ex)
        {
            return DataLocationResolution.ForMissing(
                fallback,
                rememberedFolder,
                InvalidPathNotice(rememberedFolder, fallback, ex.Message));
        }

        if (!Directory.Exists(full))
        {
            return DataLocationResolution.ForMissing(
                fallback,
                full,
                MissingFolderNotice(full, fallback));
        }

        return DataLocationResolution.ForFolder(full, fallback);
    }

    public static SavedDataFolder TryRead(string settingsPath)
    {
        if (string.IsNullOrWhiteSpace(settingsPath) || !File.Exists(settingsPath))
            return new SavedDataFolder(null, null);

        try
        {
            var json = File.ReadAllText(settingsPath);
            var parsed = JsonSerializer.Deserialize<DataLocationFile>(json, SerializerOptions);
            var folder = parsed?.DataFolder;
            if (string.IsNullOrWhiteSpace(folder))
                return new SavedDataFolder(null, null);

            return new SavedDataFolder(folder.Trim(), null);
        }
        catch (Exception ex)
        {
            return new SavedDataFolder(null, ex.Message);
        }
    }

    public static void Save(string settingsPath, string dataFolder)
    {
        if (string.IsNullOrWhiteSpace(settingsPath))
            throw new ArgumentException("Settings path is required.", nameof(settingsPath));
        if (string.IsNullOrWhiteSpace(dataFolder))
            throw new ArgumentException("Data folder is required.", nameof(dataFolder));

        var fullSettings = Path.GetFullPath(settingsPath);
        var directory = Path.GetDirectoryName(fullSettings);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("Settings path has no directory.");

        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(
            new DataLocationFile { DataFolder = Path.GetFullPath(dataFolder) },
            SerializerOptions);
        File.WriteAllText(fullSettings, json);
    }

    public static void Clear(string settingsPath)
    {
        if (string.IsNullOrWhiteSpace(settingsPath))
            return;

        if (File.Exists(settingsPath))
            File.Delete(settingsPath);
    }

    public static string FormatStartupLog(DataLocationResolution resolution)
    {
        var lines = new List<string>
        {
            "Resolved data paths:",
            "  Active data folder: " + AppDataPaths.ActiveRoot,
            "  AppState.json: " + AppDataPaths.AppStatePath,
            "  Tax data: " + AppDataPaths.TaxDataDirectory,
            "  RMD data: " + AppDataPaths.RmdDataDirectory,
            "  Market data: " + AppDataPaths.MarketDataPath,
            "  Settings file: " + AppDataPaths.SettingsFilePath,
            "  Default folder: " + resolution.DefaultFolder
        };

        if (resolution.UsedEnvironmentOverride)
            lines.Add("  EXITPLAN_DATA_DIR is set and overrides the saved folder for this run.");

        if (!string.IsNullOrWhiteSpace(resolution.Notice))
            lines.Add(resolution.Notice);

        return string.Join(Environment.NewLine, lines);
    }

    private static string SafeFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path;
        }
    }

    private static string MissingFolderNotice(string missing, string defaultFolder)
        => "The data folder ExitPlan used last time is missing:" + Environment.NewLine
           + missing + Environment.NewLine + Environment.NewLine
           + "ExitPlan is using the default folder instead:" + Environment.NewLine
           + defaultFolder + Environment.NewLine + Environment.NewLine
           + "That saved choice is still remembered. It will be used again if the folder comes back. Reset to Default Folder forgets it.";

    private static string InvalidPathNotice(string missing, string defaultFolder, string detail)
        => "The saved data folder path is not valid (" + detail + "):" + Environment.NewLine
           + missing + Environment.NewLine + Environment.NewLine
           + "ExitPlan is using the default folder instead:" + Environment.NewLine
           + defaultFolder;

    private sealed class DataLocationFile
    {
        [JsonPropertyName("dataFolder")]
        public string? DataFolder { get; set; }
    }
}

public sealed class SavedDataFolder
{
    public SavedDataFolder(string? folder, string? error)
    {
        Folder = folder;
        Error = error;
    }

    public string? Folder { get; }

    public string? Error { get; }
}

public sealed class DataLocationResolution
{
    private DataLocationResolution(
        string activeFolder,
        string defaultFolder,
        bool fellBackFromMissingFolder,
        bool usedEnvironmentOverride,
        bool couldNotReadSettings,
        string? missingFolder,
        string? notice)
    {
        ActiveFolder = activeFolder;
        DefaultFolder = defaultFolder;
        FellBackFromMissingFolder = fellBackFromMissingFolder;
        UsedEnvironmentOverride = usedEnvironmentOverride;
        CouldNotReadSettings = couldNotReadSettings;
        MissingFolder = missingFolder;
        Notice = notice;
    }

    public string ActiveFolder { get; }

    public string DefaultFolder { get; }

    public bool FellBackFromMissingFolder { get; }

    public bool UsedEnvironmentOverride { get; }

    public bool CouldNotReadSettings { get; }

    public string? MissingFolder { get; }

    public string? Notice { get; }

    public bool ShouldTellUser => !string.IsNullOrWhiteSpace(Notice);

    public static DataLocationResolution ForFolder(string activeFolder, string defaultFolder)
        => new(activeFolder, defaultFolder, false, false, false, null, null);

    public static DataLocationResolution ForMissing(string defaultFolder, string missingFolder, string notice)
        => new(defaultFolder, defaultFolder, true, false, false, missingFolder, notice);

    public static DataLocationResolution ForEnvironment(string activeFolder, string defaultFolder)
        => new(activeFolder, defaultFolder, false, true, false, null, null);

    public static DataLocationResolution ForUnreadableSettings(string defaultFolder, string error)
        => new(
            defaultFolder,
            defaultFolder,
            false,
            false,
            true,
            null,
            "ExitPlan could not read the saved data folder (" + error + ")." + Environment.NewLine
            + "ExitPlan is using the default folder instead:" + Environment.NewLine
            + defaultFolder);
}
