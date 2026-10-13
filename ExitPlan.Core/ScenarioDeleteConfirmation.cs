namespace ExitPlan.Core;

/// <summary>
/// Prompt and list update for deleting the selected scenario.
/// The caller supplies the Yes/No answer so tests can skip the dialog.
/// </summary>
public static class ScenarioDeleteConfirmation
{
    public const string Title = "Delete Scenario";

    /// <summary>The dialog opens with No selected, so Enter does not delete.</summary>
    public static bool DefaultsToNo => true;

    public static string Message(string scenarioName) =>
        $"Are you sure you want to delete the scenario \"{scenarioName}\"? This cannot be undone.";

    /// <summary>
    /// Asks <paramref name="confirm"/> with <see cref="Message"/>. Removes
    /// <paramref name="selected"/> only when that callback returns true.
    /// </summary>
    public static bool TryDelete(
        IList<ScenarioState> scenarios,
        ScenarioState selected,
        Func<string, bool> confirm,
        out ScenarioState selection)
    {
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(confirm);

        selection = selected;
        if (!confirm(Message(selected.Name)))
            return false;

        int index = scenarios.IndexOf(selected);
        scenarios.Remove(selected);

        if (scenarios.Count == 0)
        {
            var created = new ScenarioState { Name = "New Scenario" };
            scenarios.Add(created);
            selection = created;
            return true;
        }

        if (index >= scenarios.Count)
            index = scenarios.Count - 1;

        selection = scenarios[index];
        return true;
    }
}
