using System.Text.Json;
using System.IO;

namespace ExitPlan.Core.MarketData
{
    public static class MarketHistoryLoader
    {
        private static string FilePath => AppDataPaths.MarketDataPath;

        public static string GetFilePath() => FilePath;

        public static string GetMarketDataFolder() => AppDataPaths.MarketDataDirectory;

        public static MarketHistory LoadFromPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException($"Market data file not found: {path}");

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<MarketHistory>(json)
                   ?? new MarketHistory();
        }

        public static void Save(string path, MarketHistory history)
        {
            var errors = MarketHistoryValidator.Validate(history);
            if (errors.Count > 0)
                throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

            MarketHistoryDocument.Save(path, history);
        }

        public static MarketHistory Load()
        {
            if (!File.Exists(FilePath))
                throw new FileNotFoundException($"Market data file not found: {FilePath}");

            var json = File.ReadAllText(FilePath);

            return JsonSerializer.Deserialize<MarketHistory>(json)
                   ?? new MarketHistory();
        }
    }
}

