using System.Reflection;
using System.Text.RegularExpressions;

namespace ExitPlan.Core.Tests;

public class VersionMetadataTests
{
    [Fact]
    public void AssemblyVersions_FollowCsprojVersion()
    {
        var root = RepoRoot();
        var uiCsproj = File.ReadAllText(Path.Combine(root, "ExitPlan.UI", "ExitPlan.UI.csproj"));
        var coreCsproj = File.ReadAllText(Path.Combine(root, "ExitPlan.Core", "ExitPlan.Core.csproj"));
        var version = ReadProjectVersion(uiCsproj, "ExitPlan.UI.csproj");
        Assert.Equal(version, ReadProjectVersion(coreCsproj, "ExitPlan.Core.csproj"));

        AssertVersionPropertiesFollow(uiCsproj, "ExitPlan.UI.csproj", version);
        AssertVersionPropertiesFollow(coreCsproj, "ExitPlan.Core.csproj", version);

        foreach (var path in Directory.EnumerateFiles(root, "Directory.Build.*", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(path))
                continue;
            AssertVersionPropertiesFollow(File.ReadAllText(path), Path.GetRelativePath(root, path), version);
        }

        foreach (var relative in new[] { "publish.ps1", Path.Combine(".github", "workflows", "windows-installer.yml") })
        {
            var text = File.ReadAllText(Path.Combine(root, relative));
            foreach (var token in new[]
                     {
                         "-p:Version", "-p:AssemblyVersion", "-p:FileVersion", "-p:InformationalVersion",
                         "/p:Version", "/p:AssemblyVersion", "/p:FileVersion", "/p:InformationalVersion"
                     })
            {
                Assert.DoesNotContain(token, text, StringComparison.OrdinalIgnoreCase);
            }
        }

        var assembly = typeof(ProductInfo).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        var assemblyVersion = assembly.GetName().Version;

        Assert.False(string.IsNullOrWhiteSpace(informational));
        Assert.Equal(version, ProductInfo.WithoutGitHash(informational));
        Assert.True(
            informational == version || informational!.StartsWith(version + "+", StringComparison.Ordinal),
            "Informational version was '" + informational + "', expected " + version + " or " + version + "+<git>.");
        Assert.Equal(version, ProductInfo.Version);
        Assert.DoesNotContain("+", ProductInfo.Version);
        Assert.Equal("ExitPlan " + version, ProductInfo.Title);
        Assert.Equal(ToFourPart(version), fileVersion);
        Assert.Equal(Version.Parse(ToFourPart(version)), assemblyVersion);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("2.4.2", "2.4.2")]
    [InlineData("2.3+abc123", "2.3")]
    [InlineData("2.4.2+e9a33d2", "2.4.2")]
    [InlineData("2.4.2-beta+e9a33d2", "2.4.2-beta")]
    public void WithoutGitHash_StripsSourceRevisionOnly(string? version, string expected)
    {
        Assert.Equal(expected, ProductInfo.WithoutGitHash(version));
    }

    [Fact]
    public void ChooseDisplayVersion_PrefersInstalledVersion()
    {
        Assert.Equal(ProductInfo.Version, ProductInfo.ChooseDisplayVersion(null));
        Assert.Equal(ProductInfo.Version, ProductInfo.ChooseDisplayVersion("  "));
        Assert.Equal("9.9.9", ProductInfo.ChooseDisplayVersion("9.9.9+feed"));
    }

    private static void AssertVersionPropertiesFollow(string text, string label, string version)
    {
        foreach (var property in new[] { "AssemblyVersion", "FileVersion", "InformationalVersion" })
        {
            foreach (Match match in Regex.Matches(text, $"<{property}\\s*>([^<]*)</{property}>"))
            {
                var value = match.Groups[1].Value.Trim();
                var follows = value == version
                    || value == version + ".0"
                    || value == "$(Version)"
                    || value == "$(Version).0";
                Assert.True(follows, $"{label} <{property}> is '{value}', which does not follow <Version> {version}.");
            }
        }
    }

    private static string ReadProjectVersion(string csproj, string label)
    {
        var match = Regex.Match(csproj, "<Version>([^<]+)</Version>");
        Assert.True(match.Success, label + " has no <Version>.");
        return match.Groups[1].Value.Trim();
    }

    private static string ToFourPart(string version)
    {
        var core = version.Split('+')[0].Split('-')[0];
        var parts = core.Split('.');
        var numbers = new int[4];
        for (var i = 0; i < parts.Length && i < 4; i++)
            numbers[i] = int.Parse(parts[i]);
        return string.Join('.', numbers);
    }

    private static bool IsBuildOutput(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ExitPlan.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repo root from " + AppContext.BaseDirectory);
    }
}
