using ExitPlan.Core.MarketData;
using ExitPlan.Core.Taxes;

namespace ExitPlan.Core.Tests;

public class DataLocationTests
{
    [Fact]
    public void DefaultRoot_MatchesDocumentsExitPlan_AndFollowsExitPlanDataDir()
    {
        var previous = Environment.GetEnvironmentVariable("EXITPLAN_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("EXITPLAN_DATA_DIR", null);
            Assert.Equal(UserDataMigrator.ResolveUserDataRoot(), AppDataPaths.DefaultRoot);
            Assert.EndsWith($"{Path.DirectorySeparatorChar}ExitPlan", AppDataPaths.DefaultRoot);

            Environment.SetEnvironmentVariable("EXITPLAN_DATA_DIR", "/tmp/exitplan-default-root");
            Assert.Equal("/tmp/exitplan-default-root", AppDataPaths.DefaultRoot);
            Assert.Equal(UserDataMigrator.ResolveUserDataRoot(), AppDataPaths.DefaultRoot);
        }
        finally
        {
            Environment.SetEnvironmentVariable("EXITPLAN_DATA_DIR", previous);
        }
    }

    [Fact]
    public void SettingsFile_IsUnderLocalAppDataExitPlan_NotTheInstallFolder()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.False(string.IsNullOrWhiteSpace(local));

        var expected = Path.Combine(local, "ExitPlan", AppDataPaths.SettingsFileName);
        Assert.Equal(expected, AppDataPaths.SettingsFilePath);
        Assert.Equal("data-location.json", Path.GetFileName(AppDataPaths.SettingsFilePath));

        var installCurrent = Path.Combine(local, "ExitPlan", "current");
        Assert.False(AppDataPaths.SettingsFilePath.StartsWith(
            installCurrent + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase));
        Assert.False(AppDataPaths.SettingsFilePath.StartsWith(
            AppContext.BaseDirectory,
            StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SaveAndRead_RoundTripsTheChosenFolder()
    {
        using var dir = new TempDir();
        var settings = Path.Combine(dir.Path, "ExitPlan", "data-location.json");
        var folder = Path.Combine(dir.Path, "household");
        Directory.CreateDirectory(folder);

        DataLocationSettings.Save(settings, folder);

        var saved = DataLocationSettings.TryRead(settings);
        Assert.Null(saved.Error);
        Assert.Equal(Path.GetFullPath(folder), saved.Folder);
        Assert.Contains("\"dataFolder\"", File.ReadAllText(settings));

        DataLocationSettings.Clear(settings);
        Assert.False(File.Exists(settings));
        Assert.Null(DataLocationSettings.TryRead(settings).Folder);
    }

    [Fact]
    public void Resolve_UsesRememberedFolder_WhenItExists()
    {
        using var fallback = new TempDir();
        using var chosen = new TempDir();

        var resolution = DataLocationSettings.Resolve(fallback.Path, chosen.Path);

        Assert.False(resolution.FellBackFromMissingFolder);
        Assert.False(resolution.ShouldTellUser);
        Assert.Equal(Path.GetFullPath(chosen.Path), resolution.ActiveFolder);
        Assert.Equal(Path.GetFullPath(fallback.Path), resolution.DefaultFolder);
    }

    [Fact]
    public void Resolve_FallsBack_WhenRememberedFolderIsMissing_AndDoesNotCreateIt()
    {
        using var fallback = new TempDir();
        var missing = Path.Combine(fallback.Path, "gone");

        var resolution = DataLocationSettings.Resolve(fallback.Path, missing);

        Assert.True(resolution.FellBackFromMissingFolder);
        Assert.True(resolution.ShouldTellUser);
        Assert.Equal(Path.GetFullPath(fallback.Path), resolution.ActiveFolder);
        Assert.Equal(Path.GetFullPath(missing), resolution.MissingFolder);
        Assert.Contains(Path.GetFullPath(missing), resolution.Notice);
        Assert.Contains(Path.GetFullPath(fallback.Path), resolution.Notice);
        Assert.Contains("missing", resolution.Notice, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public void Activate_UsesSavedFolder_WithoutChangingTheSettingsFile()
    {
        var previous = AppDataPaths.OverrideRoot;
        using var dir = new TempDir();
        try
        {
            var settings = Path.Combine(dir.Path, "data-location.json");
            var chosen = Path.Combine(dir.Path, "chosen");
            var fallback = Path.Combine(dir.Path, "default");
            Directory.CreateDirectory(chosen);
            Directory.CreateDirectory(fallback);
            DataLocationSettings.Save(settings, chosen);

            var resolution = DataLocationSettings.Activate(settings, environmentOverride: null, fallback);

            Assert.Equal(Path.GetFullPath(chosen), resolution.ActiveFolder);
            Assert.Equal(Path.GetFullPath(chosen), AppDataPaths.OverrideRoot);
            Assert.False(resolution.FellBackFromMissingFolder);
            Assert.Equal(Path.GetFullPath(chosen), DataLocationSettings.TryRead(settings).Folder);
        }
        finally
        {
            AppDataPaths.OverrideRoot = previous;
        }
    }

    [Fact]
    public void Activate_MissingSavedFolder_FallsBackAndKeepsTheSavedChoice()
    {
        var previous = AppDataPaths.OverrideRoot;
        using var dir = new TempDir();
        try
        {
            var settings = Path.Combine(dir.Path, "data-location.json");
            var missing = Path.Combine(dir.Path, "missing-drive");
            var fallback = Path.Combine(dir.Path, "default");
            Directory.CreateDirectory(fallback);
            DataLocationSettings.Save(settings, missing);

            var resolution = DataLocationSettings.Activate(settings, environmentOverride: null, fallback);

            Assert.True(resolution.FellBackFromMissingFolder);
            Assert.True(resolution.ShouldTellUser);
            Assert.Equal(Path.GetFullPath(fallback), AppDataPaths.OverrideRoot);
            Assert.Equal(Path.GetFullPath(missing), DataLocationSettings.TryRead(settings).Folder);
            Assert.False(Directory.Exists(missing));
        }
        finally
        {
            AppDataPaths.OverrideRoot = previous;
        }
    }

    [Fact]
    public void Activate_UnreadableSettings_UsesDefaultAndTellsTheUser()
    {
        var previous = AppDataPaths.OverrideRoot;
        using var dir = new TempDir();
        try
        {
            var settings = Path.Combine(dir.Path, "data-location.json");
            var fallback = Path.Combine(dir.Path, "default");
            Directory.CreateDirectory(fallback);
            File.WriteAllText(settings, "{ this is not json");

            var resolution = DataLocationSettings.Activate(settings, environmentOverride: null, fallback);

            Assert.True(resolution.CouldNotReadSettings);
            Assert.True(resolution.ShouldTellUser);
            Assert.Contains("could not read", resolution.Notice, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.GetFullPath(fallback), resolution.Notice);
            Assert.Equal(Path.GetFullPath(fallback), AppDataPaths.OverrideRoot);
            Assert.True(File.Exists(settings));
        }
        finally
        {
            AppDataPaths.OverrideRoot = previous;
        }
    }

    [Fact]
    public void Activate_EnvironmentOverride_WinsAndLeavesTheSavedChoice()
    {
        var previous = AppDataPaths.OverrideRoot;
        using var dir = new TempDir();
        try
        {
            var settings = Path.Combine(dir.Path, "data-location.json");
            var saved = Path.Combine(dir.Path, "saved");
            var fromEnv = Path.Combine(dir.Path, "from-env");
            Directory.CreateDirectory(saved);
            DataLocationSettings.Save(settings, saved);

            var resolution = DataLocationSettings.Activate(settings, fromEnv, Path.Combine(dir.Path, "default"));

            Assert.True(resolution.UsedEnvironmentOverride);
            Assert.Null(resolution.Notice);
            Assert.Equal(fromEnv, AppDataPaths.OverrideRoot);
            Assert.Equal(Path.GetFullPath(saved), DataLocationSettings.TryRead(settings).Folder);
        }
        finally
        {
            AppDataPaths.OverrideRoot = previous;
        }
    }

    [Fact]
    public void Activate_WithNoSavedChoice_UsesTheDefaultFolder()
    {
        var previous = AppDataPaths.OverrideRoot;
        using var dir = new TempDir();
        try
        {
            var settings = Path.Combine(dir.Path, "missing", "data-location.json");
            var fallback = Path.Combine(dir.Path, "default");

            var resolution = DataLocationSettings.Activate(settings, environmentOverride: null, fallback);

            Assert.False(resolution.FellBackFromMissingFolder);
            Assert.False(resolution.ShouldTellUser);
            Assert.Equal(Path.GetFullPath(fallback), resolution.ActiveFolder);
            Assert.Equal(Path.GetFullPath(fallback), AppDataPaths.OverrideRoot);
        }
        finally
        {
            AppDataPaths.OverrideRoot = previous;
        }
    }

    [Fact]
    public void TryResolveDataFolder_RequiresAppStateInsideDataFolder()
    {
        using var root = new TempDir();
        var file = Path.Combine(root.Path, "Data", "AppState.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "{}");

        Assert.True(AppDataPaths.TryResolveDataFolderFromAppStateFile(file, out var folder, out var error));
        Assert.Equal(Path.GetFullPath(root.Path), folder);
        Assert.Equal(string.Empty, error);

        var loose = Path.Combine(root.Path, "AppState.json");
        File.WriteAllText(loose, "{}");
        Assert.False(AppDataPaths.TryResolveDataFolderFromAppStateFile(loose, out _, out var looseError));
        Assert.False(string.IsNullOrWhiteSpace(looseError));

        Assert.False(AppDataPaths.TryResolveDataFolderFromAppStateFile(
            Path.Combine(root.Path, "Data", "missing.json"),
            out _,
            out _));
    }

    [Fact]
    public void NormalizeChosenFolder_LiftsADataSubfolder_AndKeepsAContentRoot()
    {
        using var root = new TempDir();
        var data = Path.Combine(root.Path, "Data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "AppState.json"), "{}");

        Assert.Equal(Path.GetFullPath(root.Path), AppDataPaths.NormalizeChosenFolder(data));
        Assert.Equal(Path.GetFullPath(root.Path), AppDataPaths.NormalizeChosenFolder(root.Path));
        Assert.True(AppDataPaths.PathsEqual(root.Path + Path.DirectorySeparatorChar, root.Path));

        using var namedData = new TempDir();
        var contentRoot = Path.Combine(namedData.Path, "Data");
        Directory.CreateDirectory(Path.Combine(contentRoot, "TaxData"));
        File.WriteAllText(Path.Combine(contentRoot, "AppState.json"), "{}");
        Assert.Equal(Path.GetFullPath(contentRoot), AppDataPaths.NormalizeChosenFolder(contentRoot));
    }

    [Fact]
    public void SeedSources_CustomFolder_IsBundleOnly_DefaultIncludesLegacyInstalls()
    {
        using var custom = new TempDir();
        using var fallback = new TempDir();

        var customSources = UserDataMigrator.SeedSourcesFor(custom.Path, fallback.Path);
        Assert.Equal(new[] { AppContext.BaseDirectory }, customSources);

        var defaultSources = UserDataMigrator.SeedSourcesFor(
            fallback.Path + Path.DirectorySeparatorChar,
            fallback.Path);
        Assert.Contains(AppContext.BaseDirectory, defaultSources);

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
        {
            Assert.Contains(
                defaultSources,
                path => path.Contains(Path.Combine("Programs", "ExitPlan"), StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void MissingFiles_CopyOnYes_DoesNotOverwrite_AndListingDoesNotWrite()
    {
        using var bundle = new TempDir();
        using var dest = new TempDir();

        Write(bundle, "Data", "AppState.json", "{\"from\":\"sample\"}");
        Write(bundle, "TaxData", "Tax2025.json", "{\"year\":2025}");
        Write(bundle, "TaxData", "Tax2026.json", "{\"year\":2026}");
        Write(bundle, "RMDData", "RMD2025.json", "{\"rmd\":true}");
        Write(bundle, "MarketData", "MarketData.json", "{\"market\":true}");
        Write(bundle, "Other", "Ignore.txt", "no");

        Write(dest, "TaxData", "Tax2026.json", "{\"year\":2026,\"user\":true}");

        var missing = BundledDataSeed.ListMissing(dest.Path, bundle.Path);

        Assert.Contains(missing, path => path.EndsWith("AppState.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(missing, path => path.EndsWith("Tax2025.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(missing, path => path.EndsWith("RMD2025.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(missing, path => path.EndsWith("MarketData.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(missing, path => path.EndsWith("Tax2026.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(missing, path => path.Contains("Ignore.txt", StringComparison.OrdinalIgnoreCase));
        Assert.False(File.Exists(Path.Combine(dest.Path, "Data", "AppState.json")));
        Assert.False(Directory.Exists(Path.Combine(dest.Path, "Other")));

        var copied = BundledDataSeed.CopyMissing(dest.Path, bundle.Path);

        Assert.Empty(copied.Errors);
        Assert.Equal("{\"year\":2026,\"user\":true}", File.ReadAllText(Path.Combine(dest.Path, "TaxData", "Tax2026.json")));
        Assert.Equal("{\"from\":\"sample\"}", File.ReadAllText(Path.Combine(dest.Path, "Data", "AppState.json")));
        Assert.Equal("{\"year\":2025}", File.ReadAllText(Path.Combine(dest.Path, "TaxData", "Tax2025.json")));
        Assert.Equal("{\"rmd\":true}", File.ReadAllText(Path.Combine(dest.Path, "RMDData", "RMD2025.json")));
        Assert.Equal("{\"market\":true}", File.ReadAllText(Path.Combine(dest.Path, "MarketData", "MarketData.json")));
        Assert.False(Directory.Exists(Path.Combine(dest.Path, "Other")));
        Assert.Contains(copied.CopiedFiles, path => path.EndsWith("AppState.json", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(copied.CopiedFiles, path => path.EndsWith("Tax2026.json", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(BundledDataSeed.ListMissing(dest.Path, bundle.Path));
    }

    [Fact]
    public void StartupLog_ListsEveryResolvedPath()
    {
        var previous = AppDataPaths.OverrideRoot;
        using var root = new TempDir();
        try
        {
            AppDataPaths.OverrideRoot = root.Path;
            var text = DataLocationSettings.FormatStartupLog(
                DataLocationResolution.ForFolder(root.Path, root.Path));

            Assert.Contains(AppDataPaths.ActiveRoot, text);
            Assert.Contains(AppDataPaths.AppStatePath, text);
            Assert.Contains(AppDataPaths.TaxDataDirectory, text);
            Assert.Contains(AppDataPaths.RmdDataDirectory, text);
            Assert.Contains(AppDataPaths.MarketDataPath, text);
            Assert.Contains(AppDataPaths.SettingsFilePath, text);
        }
        finally
        {
            AppDataPaths.OverrideRoot = previous;
        }
    }

    [Fact]
    public void Loaders_ReadAndWriteOnlyTheActiveFolder()
    {
        var previous = AppDataPaths.OverrideRoot;
        using var active = new TempDir();
        using var empty = new TempDir();
        try
        {
            AppDataPaths.OverrideRoot = empty.Path;

            Assert.Equal(Path.Combine(empty.Path, "TaxData"), TaxPolicyLoader.GetTaxDataFolder());
            Assert.Equal(Path.Combine(empty.Path, "RMDData"), RmdPolicyLoader.GetRmdDataFolder());
            Assert.Equal(Path.Combine(empty.Path, "MarketData", "MarketData.json"), MarketHistoryLoader.GetFilePath());
            Assert.Equal(Path.Combine(empty.Path, "Data", "AppState.json"), AppDataPaths.AppStatePath);

            Assert.ThrowsAny<Exception>(() => TaxPolicyLoader.LoadForYear(2026));
            Assert.Throws<DirectoryNotFoundException>(() => RmdPolicyLoader.LoadForYear(2026));
            var marketMiss = Assert.Throws<FileNotFoundException>(() => MarketHistoryLoader.Load());
            Assert.Contains(empty.Path, marketMiss.Message);
            Assert.Empty(DataService.Load().Scenarios);

            AppDataPaths.OverrideRoot = active.Path;
            CopyOutputFile("TaxData", "Tax2026.json", active.Path);
            CopyOutputFile("RMDData", "RMD2025.json", active.Path);
            CopyOutputFile("MarketData", "MarketData.json", active.Path);

            Assert.NotNull(TaxPolicyLoader.LoadForYear(2026));
            Assert.Equal(Path.Combine(active.Path, "TaxData"), TaxPolicyLoader.GetTaxDataFolder());
            Assert.NotNull(RmdPolicyLoader.LoadForYear(2026));
            Assert.NotEmpty(MarketHistoryLoader.Load().Years);
            Assert.Equal(
                Path.Combine(active.Path, "MarketData", "MarketData.json"),
                MarketHistoryLoader.GetFilePath());

            Directory.CreateDirectory(AppDataPaths.DataDirectory);
            File.WriteAllText(
                AppDataPaths.AppStatePath,
                """
                { "Scenarios": [ { "ScenarioId": "from-active", "Scenario": { "Name": "FromActiveFolder" } } ] }
                """);

            var state = DataService.Load();
            Assert.Equal("FromActiveFolder", state.Scenarios[0].Name);

            DataService.Save(state);
            var saved = File.ReadAllText(AppDataPaths.AppStatePath);
            Assert.Contains("FromActiveFolder", saved);
            Assert.StartsWith(Path.GetFullPath(active.Path), Path.GetFullPath(AppDataPaths.AppStatePath), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            AppDataPaths.OverrideRoot = previous;
        }
    }

    private static void CopyOutputFile(string folder, string name, string root)
    {
        var source = Path.Combine(AppContext.BaseDirectory, folder, name);
        Assert.True(File.Exists(source), source);
        var destDir = Path.Combine(root, folder);
        Directory.CreateDirectory(destDir);
        File.Copy(source, Path.Combine(destDir, name));
    }

    private static void Write(TempDir dir, string folder, string name, string contents)
    {
        var path = Path.Combine(dir.Path, folder);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, name), contents);
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "exitplan-location-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // temp cleanup is best-effort
            }
        }
    }
}
