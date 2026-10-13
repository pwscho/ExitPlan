using System.Text.RegularExpressions;

namespace ExitPlan.Core.Tests;

public class ScenarioDeleteConfirmationTests
{
    [Fact]
    public void Message_QuotesTheScenarioName()
    {
        Assert.Equal(
            "Are you sure you want to delete the scenario \"Early Retirement\"? This cannot be undone.",
            ScenarioDeleteConfirmation.Message("Early Retirement"));
        Assert.True(ScenarioDeleteConfirmation.DefaultsToNo);
        Assert.Equal("Delete Scenario", ScenarioDeleteConfirmation.Title);
    }

    [Fact]
    public void TryDelete_No_LeavesTheScenarioInPlace()
    {
        var keep = Scenario("Only one");
        var scenarios = new List<ScenarioState> { keep };
        string? prompted = null;

        var deleted = ScenarioDeleteConfirmation.TryDelete(
            scenarios,
            keep,
            message =>
            {
                prompted = message;
                return false;
            },
            out var selection);

        Assert.False(deleted);
        Assert.Same(keep, selection);
        Assert.Same(keep, Assert.Single(scenarios));
        Assert.Equal(ScenarioDeleteConfirmation.Message("Only one"), prompted);
    }

    [Fact]
    public void TryDelete_Yes_RemovesTheSelectedScenario_AndSelectsTheItemThatSlidesIntoItsPlace()
    {
        var first = Scenario("First");
        var middle = Scenario("Middle");
        var last = Scenario("Last");
        var scenarios = new List<ScenarioState> { first, middle, last };

        var deleted = ScenarioDeleteConfirmation.TryDelete(
            scenarios,
            middle,
            _ => true,
            out var selection);

        Assert.True(deleted);
        Assert.Equal(new[] { "First", "Last" }, scenarios.Select(s => s.Name));
        Assert.Same(last, selection);
        Assert.DoesNotContain(middle, scenarios);
    }

    [Fact]
    public void TryDelete_Yes_OnTheLastScenario_SelectsThePreviousOne()
    {
        var first = Scenario("First");
        var last = Scenario("Last");
        var scenarios = new List<ScenarioState> { first, last };

        var deleted = ScenarioDeleteConfirmation.TryDelete(
            scenarios,
            last,
            _ => true,
            out var selection);

        Assert.True(deleted);
        Assert.Same(first, Assert.Single(scenarios));
        Assert.Same(first, selection);
    }

    [Fact]
    public void TryDelete_Yes_OnTheOnlyScenario_ReplacesItSoTheListIsNotEmpty()
    {
        var only = Scenario("Only one");
        var scenarios = new List<ScenarioState> { only };

        var deleted = ScenarioDeleteConfirmation.TryDelete(
            scenarios,
            only,
            _ => true,
            out var selection);

        Assert.True(deleted);
        var replacement = Assert.Single(scenarios);
        Assert.NotSame(only, replacement);
        Assert.Equal("New Scenario", replacement.Name);
        Assert.Same(replacement, selection);
    }

    [Fact]
    public void TryDelete_Yes_RemovesTheSelectedInstance_WhenAnotherScenarioHasTheSameName()
    {
        var first = Scenario("Same");
        var second = Scenario("Same");
        var scenarios = new List<ScenarioState> { first, second };

        var deleted = ScenarioDeleteConfirmation.TryDelete(
            scenarios,
            second,
            _ => true,
            out var selection);

        Assert.True(deleted);
        Assert.Same(first, Assert.Single(scenarios));
        Assert.Same(first, selection);
    }

    [Fact]
    public void DeleteScenario_MenuAndSidebar_ShareTheCommand_AndTheDialogDefaultsToNo()
    {
        var root = RepoRoot();
        var viewModel = File.ReadAllText(Path.Combine(root, "ExitPlan.UI", "ViewModels", "ScenarioManagerViewModel.cs"));
        var xaml = File.ReadAllText(Path.Combine(root, "ExitPlan.UI", "MainWindow.xaml"));

        Assert.Contains("Func<string, bool>? confirmDelete", viewModel);
        Assert.Contains("ScenarioDeleteConfirmation.TryDelete", viewModel);
        Assert.Contains("MessageBoxButton.YesNo", viewModel);
        Assert.Contains("ScenarioDeleteConfirmation.DefaultsToNo", viewModel);
        Assert.Contains("ScenarioDeleteConfirmation.Title", viewModel);
        Assert.Contains("MessageBoxResult.No", viewModel);
        Assert.Contains("MessageBoxResult.Yes", viewModel);

        var command = "Command=\"{Binding ScenarioManager.DeleteScenarioCommand}\"";
        Assert.Equal(2, Regex.Matches(xaml, Regex.Escape(command)).Count);
        Assert.Contains("Header=\"Delete Scenario...\"", xaml);
        Assert.Contains("Content=\"Delete\"", xaml);
    }

    private static ScenarioState Scenario(string name) => new() { Name = name };

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
