namespace ExitPlan.Core;

public class AppState
{
    public List<ScenarioState> Scenarios { get; set; } = new();
    public string? LastSelectedScenarioId { get; set; }
}

