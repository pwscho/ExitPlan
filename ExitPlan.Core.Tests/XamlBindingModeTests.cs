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
    public void MainWindow_Menus_FileConfigHelp()
    {
        var xaml = File.ReadAllText(Path.Combine(RepoRoot(), "ExitPlan.UI", "MainWindow.xaml"));
        var file = xaml.IndexOf("Header=\"_File\"", StringComparison.Ordinal);
        var config = xaml.IndexOf("Header=\"_Config\"", StringComparison.Ordinal);
        var help = xaml.IndexOf("Header=\"_Help\"", StringComparison.Ordinal);
        Assert.True(file >= 0 && config > file && help > config);

        var fileMenu = xaml[file..config];
        Assert.Contains("Header=\"Add Scenario...\"", fileMenu);
        Assert.Contains("Command=\"{Binding ScenarioManager.AddScenarioCommand}\"", fileMenu);
        Assert.Contains("Header=\"Copy Scenario...\"", fileMenu);
        Assert.Contains("Command=\"{Binding ScenarioManager.DuplicateScenarioCommand}\"", fileMenu);
        Assert.Contains("Header=\"Delete Scenario...\"", fileMenu);
        Assert.Contains("Command=\"{Binding ScenarioManager.DeleteScenarioCommand}\"", fileMenu);
        Assert.Contains("Header=\"Close\"", fileMenu);
        Assert.Contains("InputGestureText=\"Alt+F4\"", fileMenu);
        Assert.Contains("Click=\"Close_Click\"", fileMenu);
        Assert.DoesNotContain("OpenAppState_Click", fileMenu);
        Assert.DoesNotContain("OpenTaxSettings_Click", fileMenu);

        var configMenu = xaml[config..help];
        Assert.Contains("Header=\"Tax Settings...\"", configMenu);
        Assert.Contains("Click=\"OpenTaxSettings_Click\"", configMenu);
        Assert.Contains("Header=\"RMD Settings...\"", configMenu);
        Assert.Contains("Click=\"OpenRmdSettings_Click\"", configMenu);
        Assert.Contains("Header=\"Market Data...\"", configMenu);
        Assert.Contains("Click=\"OpenMarketData_Click\"", configMenu);
        Assert.Contains("Header=\"Open AppState.json...\"", configMenu);
        Assert.Contains("Click=\"OpenAppState_Click\"", configMenu);
        Assert.Contains("Header=\"Choose Data Folder...\"", configMenu);
        Assert.Contains("Click=\"ChooseDataFolder_Click\"", configMenu);
        Assert.Contains("Header=\"Open Data Folder\"", configMenu);
        Assert.Contains("Click=\"OpenDataFolder_Click\"", configMenu);
        Assert.Contains("Header=\"Reset to Default Folder\"", configMenu);
        Assert.Contains("Click=\"ResetDataFolder_Click\"", configMenu);

        var helpMenu = xaml[help..];
        Assert.Contains("Header=\"Check for updates\"", helpMenu);
        Assert.Contains("About ExitPlan...", helpMenu);

        Assert.DoesNotContain("Content=\"Tax Settings\"", xaml);
        Assert.DoesNotContain("Content=\"RMD Settings\"", xaml);
        Assert.DoesNotContain("Content=\"Market Data\"", xaml);
        Assert.Contains("Content=\"Add\"", xaml);
        Assert.Contains("Content=\"Copy\"", xaml);
        Assert.Contains("Content=\"Delete\"", xaml);
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
