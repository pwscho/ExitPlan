using System.Text.RegularExpressions;

namespace ExitPlan.Core.Tests;

/// <summary>
/// Run.Text bindings default to TwoWay. Binding one to a get-only property
/// throws when the window loads and previously crashed startup.
/// </summary>
public class XamlBindingModeTests
{
    [Fact]
    public void RunTextBindings_SpecifyOneWayOrOneTime()
    {
        var root = RepoRoot();
        var failures = new List<string>();
        foreach (var file in XamlFiles(root))
        {
            var relative = Path.GetRelativePath(root, file);
            failures.AddRange(XamlRunBindingScanner.Find(File.ReadAllText(file), relative));
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Theory]
    [InlineData("<Run Text=\"{Binding Name}\"/>", 1)]
    [InlineData("<Run Text=\"{Binding Name, Mode=TwoWay}\"/>", 1)]
    [InlineData("<Run Text=\"{Binding Name, Mode=OneWayToSource}\"/>", 1)]
    [InlineData("<Run Text=\"{Binding Name, Mode=OneWay}\"/>", 0)]
    [InlineData("<Run Text=\"{Binding Name, Mode = OneTime}\"/>", 0)]
    [InlineData("<Run Text=\"Data folder: \"/>", 0)]
    [InlineData("<Run.Text><Binding Path=\"Name\" /></Run.Text>", 1)]
    [InlineData("<Run.Text><Binding Path=\"Name\" Mode=\"OneWay\" /></Run.Text>", 0)]
    public void Scanner_FlagsRunBindingsWithoutAOneWayMode(string xaml, int expected)
    {
        Assert.Equal(expected, XamlRunBindingScanner.Find(xaml, "sample.xaml").Count);
    }

    [Fact]
    public void AboutWindow_ReadOnlyPathBoxes_BindOneWay()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "ExitPlan.UI", "Views", "AboutWindow.xaml"));
        var boxes = Regex.Matches(xaml, @"<TextBox\b[\s\S]*?/>");
        Assert.Equal(2, boxes.Count);
        foreach (Match box in boxes)
        {
            Assert.Contains("IsReadOnly=\"True\"", box.Value);
            Assert.Contains("Mode=OneWay", box.Value);
            Assert.DoesNotContain("Mode=TwoWay", box.Value);
        }

        Assert.Contains("https://polyformproject.org/licenses/noncommercial/1.0.0/", xaml);
        Assert.Contains("https://github.com/pwscho/ExitPlan/releases", xaml);
        Assert.Contains("https://github.com/pwscho/ExitPlan\"", xaml);
        Assert.Contains("Show LICENSE.md", xaml);
    }

    [Fact]
    public void MainWindow_HelpMenu_DoesNotShowDataPaths()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "ExitPlan.UI", "MainWindow.xaml"));
        Assert.Contains("Header=\"_Help\"", xaml);
        Assert.Contains("Header=\"Check for updates\"", xaml);
        Assert.Contains("About ExitPlan...", xaml);
        Assert.DoesNotContain("Content=\"Check for updates\"", xaml);
        Assert.DoesNotContain("Data folder:", xaml);
        Assert.DoesNotContain("AppState.json:", xaml);
    }

    [Fact]
    public void AboutSources_UseInstalledVersionAndSplashDisclaimer()
    {
        var root = RepoRoot();
        var viewModel = File.ReadAllText(Path.Combine(root, "ExitPlan.UI", "ViewModels", "AboutViewModel.cs"));
        var installed = File.ReadAllText(Path.Combine(root, "ExitPlan.UI", "Updates", "InstalledAppInfo.cs"));
        var uiCsproj = File.ReadAllText(Path.Combine(root, "ExitPlan.UI", "ExitPlan.UI.csproj"));

        Assert.Contains("ProductInfo.SplashLead", viewModel);
        Assert.Contains("Paul Schofield", viewModel);
        Assert.Contains("Copyright (c) 2026 Paul Schofield", viewModel);
        Assert.Contains("CurrentVersion", installed);
        Assert.Contains("IsInstalled", installed);
        Assert.Contains("ChooseDisplayVersion", installed);
        Assert.Contains("LICENSE.md", uiCsproj);
    }

    private static IEnumerable<string> XamlFiles(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.xaml", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                continue;
            yield return file;
        }
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

    private static class XamlRunBindingScanner
    {
        private static readonly Regex Comment = new("<!--[\\s\\S]*?-->", RegexOptions.Compiled);
        private static readonly Regex RunTag = new("<Run\\b([^>]*)/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TextAttribute = new(
            "\\bText\\s*=\\s*(?:\"([^\"]*)\"|'([^']*)')",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RunTextElement = new(
            "<Run\\.Text\\s*>\\s*<Binding\\b([^>]*)/?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex SafeMode = new(
            "\\bMode\\s*=\\s*['\"]?(OneWay|OneTime)\\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static IReadOnlyList<string> Find(string xaml, string fileName)
        {
            var text = Comment.Replace(xaml, "");
            var failures = new List<string>();

            foreach (Match tag in RunTag.Matches(text))
            {
                var attribute = TextAttribute.Match(tag.Groups[1].Value);
                if (!attribute.Success)
                    continue;

                var value = attribute.Groups[1].Success ? attribute.Groups[1].Value : attribute.Groups[2].Value;
                if (value.Contains("{Binding", StringComparison.OrdinalIgnoreCase) && !SafeMode.IsMatch(value))
                    failures.Add(fileName + ": Run Text binding needs Mode=OneWay: " + value.Trim());
            }

            foreach (Match element in RunTextElement.Matches(text))
            {
                if (!SafeMode.IsMatch(element.Groups[1].Value))
                    failures.Add(fileName + ": Run.Text binding needs Mode=OneWay: " + element.Value.Trim());
            }

            return failures;
        }
    }
}
