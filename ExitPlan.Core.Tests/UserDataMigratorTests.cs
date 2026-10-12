using ExitPlan.Core;

namespace ExitPlan.Core.Tests;

public class UserDataMigratorTests
{
    [Fact]
    public void Migrate_CopiesMissingFiles_AndDoesNotOverwrite()
    {
        using var source = new TempDir();
        using var newer = new TempDir();
        using var dest = new TempDir();

        Write(source, "Data", "AppState.json", "{\"from\":\"legacy\"}");
        Write(source, "TaxData", "Tax2026.json", "{\"year\":2026}");
        Write(source, "TaxData", "Tax2027.json", "{\"year\":2027}");
        Write(source, "RMDData", "RMD2025.json", "{\"rmd\":true}");
        Write(source, "MarketData", "MarketData.json", "{\"market\":true}");
        Write(source, "Other", "Ignore.txt", "no");

        Write(newer, "TaxData", "Tax2026.json", "{\"year\":2026,\"replaced\":true}");
        Write(newer, "TaxData", "Tax2028.json", "{\"year\":2028}");

        Write(dest, "Data", "AppState.json", "{\"from\":\"user\"}");

        var result = UserDataMigrator.Migrate(
            dest.Path,
            new[] { source.Path, newer.Path },
            applyOverride: false);

        Assert.Equal(Path.GetFullPath(dest.Path), result.UserDataRoot);
        Assert.Empty(result.Errors);
        Assert.Equal("{\"from\":\"user\"}", File.ReadAllText(Path.Combine(dest.Path, "Data", "AppState.json")));
        Assert.Equal("{\"year\":2026}", File.ReadAllText(Path.Combine(dest.Path, "TaxData", "Tax2026.json")));
        Assert.Equal("{\"year\":2027}", File.ReadAllText(Path.Combine(dest.Path, "TaxData", "Tax2027.json")));
        Assert.Equal("{\"year\":2028}", File.ReadAllText(Path.Combine(dest.Path, "TaxData", "Tax2028.json")));
        Assert.Equal("{\"rmd\":true}", File.ReadAllText(Path.Combine(dest.Path, "RMDData", "RMD2025.json")));
        Assert.Equal("{\"market\":true}", File.ReadAllText(Path.Combine(dest.Path, "MarketData", "MarketData.json")));
        Assert.False(Directory.Exists(Path.Combine(dest.Path, "Other")));
        Assert.DoesNotContain(result.CopiedFiles, path => path.EndsWith("AppState.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.CopiedFiles, path => path.EndsWith("Tax2028.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Migrate_SkipsMissingSources_AndTheDestinationItself()
    {
        using var dest = new TempDir();
        Write(dest, "Data", "AppState.json", "{\"keep\":true}");

        var result = UserDataMigrator.Migrate(
            dest.Path,
            new[] { Path.Combine(dest.Path, "does-not-exist"), dest.Path },
            applyOverride: false);

        Assert.Empty(result.Errors);
        Assert.Empty(result.CopiedFiles);
        Assert.Equal("{\"keep\":true}", File.ReadAllText(Path.Combine(dest.Path, "Data", "AppState.json")));
    }

    [Fact]
    public void ResolveUserDataRoot_UsesDocumentsExitPlan()
    {
        var previous = Environment.GetEnvironmentVariable("EXITPLAN_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("EXITPLAN_DATA_DIR", null);
            var root = UserDataMigrator.ResolveUserDataRoot();
            Assert.EndsWith($"{Path.DirectorySeparatorChar}ExitPlan", root);
        }
        finally
        {
            Environment.SetEnvironmentVariable("EXITPLAN_DATA_DIR", previous);
        }
    }

    [Fact]
    public void ResolveUserDataRoot_PrefersExitPlanDataDir()
    {
        var previous = Environment.GetEnvironmentVariable("EXITPLAN_DATA_DIR");
        try
        {
            Environment.SetEnvironmentVariable("EXITPLAN_DATA_DIR", "/tmp/exitplan-custom");
            Assert.Equal("/tmp/exitplan-custom", UserDataMigrator.ResolveUserDataRoot());
        }
        finally
        {
            Environment.SetEnvironmentVariable("EXITPLAN_DATA_DIR", previous);
        }
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
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "exitplan-userdata-" + Guid.NewGuid().ToString("N"));
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
