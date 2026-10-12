using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExitPlan.Core.MarketData
{
    /// <summary>
    /// Loads and saves MarketData.json while preserving unknown properties
    /// so future schema additions are not stripped by the settings editor.
    /// </summary>
    public sealed class MarketHistoryDocument
    {
        private static readonly JsonSerializerOptions DeserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true
        };

        private readonly JsonObject _root;

        private MarketHistoryDocument(string filePath, JsonObject root)
        {
            FilePath = filePath;
            _root = root;
        }

        public string FilePath { get; }

        public static MarketHistoryDocument Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException($"Market data file not found: {path}", path);

            var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new InvalidOperationException($"Market data file is not a JSON object: {path}");

            return new MarketHistoryDocument(path, node);
        }

        public MarketHistory ToHistory()
        {
            return _root.Deserialize<MarketHistory>(DeserializeOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize market data: {FilePath}");
        }

        public void Apply(MarketHistory history)
        {
            ArgumentNullException.ThrowIfNull(history);
            ApplyYears(_root, history.Years ?? new List<MarketYear>());
        }

        public void Save()
        {
            var json = _root.ToJsonString(WriteOptions);
            if (!json.EndsWith('\n'))
                json += Environment.NewLine;

            File.WriteAllText(FilePath, json);
        }

        public static void Save(string path, MarketHistory history)
        {
            ArgumentNullException.ThrowIfNull(history);

            MarketHistoryDocument document = File.Exists(path)
                ? Load(path)
                : new MarketHistoryDocument(path, new JsonObject());

            document.Apply(history);
            document.Save();
        }

        private static void ApplyYears(JsonObject root, IList<MarketYear> years)
        {
            var key = FindKey(root, "Years") ?? "Years";
            var existingByYear = new Dictionary<int, JsonObject>();

            if (root[key] is JsonArray existing)
            {
                foreach (var item in existing)
                {
                    if (item is not JsonObject obj)
                        continue;

                    var yearKey = FindKey(obj, "Year");
                    if (yearKey == null)
                        continue;

                    if (obj[yearKey] is JsonValue value &&
                        value.TryGetValue<int>(out int year) &&
                        !existingByYear.ContainsKey(year))
                    {
                        existingByYear[year] = obj.DeepClone().AsObject() ?? new JsonObject();
                    }
                }
            }

            var array = new JsonArray();
            foreach (var year in years)
            {
                var obj = existingByYear.TryGetValue(year.Year, out var found)
                    ? found
                    : new JsonObject();

                SetNumber(obj, "Year", year.Year);
                SetNumber(obj, "EquityReturn", year.EquityReturn);
                SetNumber(obj, "BondReturn", year.BondReturn);
                SetNumber(obj, "InflationRate", year.InflationRate);
                array.Add(obj);
            }

            root[key] = array;
        }

        private static void SetNumber(JsonObject obj, string name, decimal value)
        {
            var key = FindKey(obj, name) ?? name;
            obj[key] = JsonValue.Create(value);
        }

        private static void SetNumber(JsonObject obj, string name, int value)
        {
            var key = FindKey(obj, name) ?? name;
            obj[key] = JsonValue.Create(value);
        }

        private static string? FindKey(JsonObject obj, string name)
        {
            return obj
                .Select(pair => pair.Key)
                .FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));
        }
    }
}
