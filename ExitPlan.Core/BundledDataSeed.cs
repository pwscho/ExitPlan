namespace ExitPlan.Core;

/// <summary>
/// Shipped defaults next to the executable: Tax*.json, RMD*.json, MarketData.json,
/// and the sample AppState.json. Copying never overwrites a file the user already has.
/// </summary>
public static class BundledDataSeed
{
    public static string BundleRoot => AppContext.BaseDirectory;

    public static IReadOnlyList<string> ListMissing(string destinationRoot, string bundleRoot)
        => UserDataMigrator.ListMissing(destinationRoot, new[] { bundleRoot });

    public static UserDataMigrationResult CopyMissing(string destinationRoot, string bundleRoot)
        => UserDataMigrator.Migrate(destinationRoot, new[] { bundleRoot }, applyOverride: false);
}
