using System.Text.Json;

namespace ExitPlan.Core;

public static class DataService
{
    // Always AppState.json inside the one active data folder.
    private static string FilePath => AppDataPaths.AppStatePath;

    public static AppState Load()
    {
        if (!File.Exists(FilePath))
            return new AppState();

        var json = File.ReadAllText(FilePath);
        var state = JsonSerializer.Deserialize<AppState>(json) ?? new AppState();
        NormalizeClaimAges(state);
        NormalizeMonteCarloSettings(state);
        return state;
    }

    internal static void NormalizeClaimAges(AppState state)
    {
        foreach (var scenario in state.Scenarios)
        {
            if (scenario.PrimarySocialSecurity != null)
                scenario.PrimarySocialSecurity.ClaimAge =
                    SocialSecurityClaiming.Clamp(scenario.PrimarySocialSecurity.ClaimAge);

            if (scenario.SpouseSocialSecurity != null)
                scenario.SpouseSocialSecurity.ClaimAge =
                    SocialSecurityClaiming.Clamp(scenario.SpouseSocialSecurity.ClaimAge);
        }
    }

    internal static void NormalizeMonteCarloSettings(AppState state)
    {
        foreach (var scenario in state.Scenarios)
        {
            scenario.Scenario ??= new Scenario();
            if (scenario.Scenario.MonteCarloSimulations <= 0)
                scenario.Scenario.MonteCarloSimulations = 1000;
        }
    }

    public static void Save(AppState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(FilePath, json);
    }
}

