namespace ExitPlan.Core;

/// <summary>
/// Copies shipped and previously saved JSON into the user-data folder the first time
/// each file is missing there. Existing files are never overwritten, so an update that
/// replaces the install directory cannot clobber scenarios or edited tax, RMD, or market files.
/// New default files shipped in a later version are copied only when the user does not already have them.
/// </summary>
public static class UserDataMigrator
{
    public static readonly string[] ContentFolders =
    {
        AppDataPaths.DataFolderName,
        AppDataPaths.TaxDataFolderName,
        AppDataPaths.RmdDataFolderName,
        AppDataPaths.MarketDataFolderName
    };

    /// <summary>
    /// Documents\ExitPlan, or EXITPLAN_DATA_DIR when that variable is set.
    /// Older installs under %LOCALAPPDATA%\Programs\ExitPlan are import sources, not the default folder.
    /// Velopack's install root is %LOCALAPPDATA%\ExitPlan and that whole folder is removed on uninstall,
    /// so user-editable files live under Documents instead.
    /// </summary>
    public static string ResolveUserDataRoot() => AppDataPaths.DefaultRoot;

    /// <summary>
    /// The default folder still imports from older install locations. A folder the user picked
    /// receives shipped defaults only, so a personal AppState.json from an old install is not
    /// copied into that folder.
    /// </summary>
    public static IReadOnlyList<string> SeedSourcesFor(string destination, string defaultRoot)
    {
        if (AppDataPaths.PathsEqual(destination, defaultRoot))
            return DefaultSourceRoots();

        return new[] { AppContext.BaseDirectory };
    }

    /// <summary>
    /// Relative paths under <paramref name="root"/> that <see cref="Migrate"/> would consider.
    /// </summary>
    public static IReadOnlyList<string> RelativeContentFiles(string root)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return list;

        string full;
        try
        {
            full = Path.GetFullPath(root);
        }
        catch (Exception)
        {
            return list;
        }

        foreach (var folder in ContentFolders)
        {
            var sourceFolder = Path.Combine(full, folder);
            if (!Directory.Exists(sourceFolder))
                continue;

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(sourceFolder);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var file in files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var name = Path.GetFileName(file);
                if (string.IsNullOrEmpty(name))
                    continue;

                list.Add(Path.Combine(folder, name));
            }
        }

        return list;
    }

    /// <summary>
    /// Files present in <paramref name="sourceRoots"/> and absent from <paramref name="userDataRoot"/>.
    /// Does not create or overwrite anything.
    /// </summary>
    public static IReadOnlyList<string> ListMissing(string userDataRoot, IEnumerable<string> sourceRoots)
    {
        if (string.IsNullOrWhiteSpace(userDataRoot))
            throw new ArgumentException("User data root is required.", nameof(userDataRoot));

        var destination = Path.GetFullPath(userDataRoot);
        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var sourceRoot in sourceRoots)
        {
            foreach (var relative in RelativeContentFiles(sourceRoot))
            {
                if (!seen.Add(relative))
                    continue;

                if (!File.Exists(Path.Combine(destination, relative)))
                    missing.Add(relative);
            }
        }

        return missing;
    }

    /// <summary>
    /// Older installer builds wrote AppState.json next to the exe. Per-user default was
    /// %LOCALAPPDATA%\Programs\ExitPlan; all-users was Program Files\ExitPlan.
    /// The directory that contains the running exe is last so shipped defaults fill gaps only.
    /// </summary>
    public static IReadOnlyList<string> DefaultSourceRoots()
    {
        var roots = new List<string>();

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(local))
            roots.Add(Path.Combine(local, "Programs", "ExitPlan"));

        AddProgramFiles(roots, Environment.SpecialFolder.ProgramFiles);
        AddProgramFiles(roots, Environment.SpecialFolder.ProgramFilesX86);

        roots.Add(AppContext.BaseDirectory);
        return roots;
    }

    public static UserDataMigrationResult MigrateToDefault()
    {
        var destination = string.IsNullOrWhiteSpace(AppDataPaths.OverrideRoot)
            ? ResolveUserDataRoot()
            : AppDataPaths.OverrideRoot!;
        return Migrate(destination, SeedSourcesFor(destination, ResolveUserDataRoot()), applyOverride: true);
    }

    public static UserDataMigrationResult Migrate(
        string userDataRoot,
        IEnumerable<string> sourceRoots,
        bool applyOverride)
    {
        if (string.IsNullOrWhiteSpace(userDataRoot))
            throw new ArgumentException("User data root is required.", nameof(userDataRoot));

        var destination = Path.GetFullPath(userDataRoot);
        Directory.CreateDirectory(destination);

        if (applyOverride)
            AppDataPaths.OverrideRoot = destination;

        var copied = new List<string>();
        var errors = new List<string>();
        var destinationKey = Normalize(destination);

        foreach (var sourceRoot in sourceRoots)
        {
            if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
                continue;

            string sourceFull;
            try
            {
                sourceFull = Path.GetFullPath(sourceRoot);
            }
            catch (Exception ex)
            {
                errors.Add($"{sourceRoot}: {ex.Message}");
                continue;
            }

            if (Normalize(sourceFull) == destinationKey)
                continue;

            foreach (var folder in ContentFolders)
            {
                var sourceFolder = Path.Combine(sourceFull, folder);
                if (!Directory.Exists(sourceFolder))
                    continue;

                var destFolder = Path.Combine(destination, folder);
                try
                {
                    Directory.CreateDirectory(destFolder);
                }
                catch (Exception ex)
                {
                    errors.Add($"{destFolder}: {ex.Message}");
                    continue;
                }

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(sourceFolder);
                }
                catch (Exception ex)
                {
                    errors.Add($"{sourceFolder}: {ex.Message}");
                    continue;
                }

                foreach (var file in files)
                {
                    var name = Path.GetFileName(file);
                    if (string.IsNullOrEmpty(name))
                        continue;

                    var destFile = Path.Combine(destFolder, name);
                    if (File.Exists(destFile))
                        continue;

                    try
                    {
                        File.Copy(file, destFile);
                        copied.Add(destFile);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{destFile}: {ex.Message}");
                    }
                }
            }
        }

        return new UserDataMigrationResult(destination, copied, errors);
    }

    private static void AddProgramFiles(List<string> roots, Environment.SpecialFolder folder)
    {
        var path = Environment.GetFolderPath(folder);
        if (!string.IsNullOrWhiteSpace(path))
            roots.Add(Path.Combine(path, "ExitPlan"));
    }

    private static string Normalize(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

public sealed class UserDataMigrationResult
{
    public UserDataMigrationResult(
        string userDataRoot,
        IReadOnlyList<string> copiedFiles,
        IReadOnlyList<string> errors)
    {
        UserDataRoot = userDataRoot;
        CopiedFiles = copiedFiles;
        Errors = errors;
    }

    public string UserDataRoot { get; }

    public IReadOnlyList<string> CopiedFiles { get; }

    public IReadOnlyList<string> Errors { get; }
}
