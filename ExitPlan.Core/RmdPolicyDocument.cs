using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExitPlan.Core
{
    /// <summary>
    /// Loads and saves an RMD{year}.json file while preserving unknown properties
    /// so future schema additions are not stripped by the settings editor.
    /// </summary>
    public sealed class RmdPolicyDocument
    {
        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true
        };

        private readonly JsonObject _root;

        private RmdPolicyDocument(string filePath, JsonObject root)
        {
            FilePath = filePath;
            _root = root;
        }

        public string FilePath { get; }

        public static RmdPolicyDocument Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException($"RMD policy file not found: {path}", path);

            var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new InvalidOperationException($"RMD file is not a JSON object: {path}");

            return new RmdPolicyDocument(path, node);
        }

        public static RmdPolicyDocument Create(string path) =>
            new(path, new JsonObject());

        public RmdPolicy ToPolicy()
        {
            var policy = new RmdPolicy();

            var startKey = FindKey(_root, "RmdStartAge");
            if (startKey != null &&
                _root[startKey] is JsonValue startValue &&
                startValue.TryGetValue<int>(out int startAge))
            {
                policy.RmdStartAge = startAge;
            }

            var bandsKey = FindKey(_root, "StartAgeBands");
            if (bandsKey != null && _root[bandsKey] is JsonArray bands)
            {
                foreach (var item in bands)
                {
                    if (item is not JsonObject obj)
                        continue;

                    var band = new RmdStartAgeBand();

                    var fromKey = FindKey(obj, "BornFrom");
                    if (fromKey != null &&
                        obj[fromKey] is JsonValue fromValue &&
                        fromValue.TryGetValue<int>(out int bornFrom))
                    {
                        band.BornFrom = bornFrom;
                    }

                    var throughKey = FindKey(obj, "BornThrough");
                    if (throughKey != null &&
                        obj[throughKey] is JsonValue throughValue &&
                        throughValue.TryGetValue<int>(out int bornThrough))
                    {
                        band.BornThrough = bornThrough;
                    }

                    var ageKey = FindKey(obj, "StartAge");
                    if (ageKey != null &&
                        obj[ageKey] is JsonValue ageValue &&
                        ageValue.TryGetValue<int>(out int bandStartAge))
                    {
                        band.StartAge = bandStartAge;
                    }

                    policy.StartAgeBands.Add(band);
                }
            }

            var divisorsKey = FindKey(_root, "LifeExpectancyDivisors");
            if (divisorsKey != null && _root[divisorsKey] is JsonObject divisors)
            {
                foreach (var pair in divisors)
                {
                    if (!int.TryParse(pair.Key, out int age))
                        continue;

                    if (pair.Value is JsonValue number &&
                        number.TryGetValue<decimal>(out decimal period))
                    {
                        policy.LifeExpectancyDivisors[age] = period;
                    }
                }
            }

            return policy;
        }

        public void Apply(RmdPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);

            SetNumber(_root, "RmdStartAge", policy.RmdStartAge);
            ApplyStartAgeBands(policy.StartAgeBands);
            ApplyDivisors(
                GetOrCreateObject(_root, "LifeExpectancyDivisors"),
                policy.LifeExpectancyDivisors ?? new Dictionary<int, decimal>());
        }

        public void Save()
        {
            var json = _root.ToJsonString(WriteOptions);
            if (!json.EndsWith('\n'))
                json += Environment.NewLine;

            File.WriteAllText(FilePath, json);
        }

        public RmdPolicyDocument CloneTo(string path)
        {
            var clone = _root.DeepClone().AsObject()
                ?? throw new InvalidOperationException("Failed to clone RMD document.");
            return new RmdPolicyDocument(path, clone);
        }

        public static void Save(string path, RmdPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);

            RmdPolicyDocument document = File.Exists(path)
                ? Load(path)
                : Create(path);

            document.Apply(policy);
            document.Save();
        }

        private void ApplyStartAgeBands(IReadOnlyList<RmdStartAgeBand>? bands)
        {
            var key = FindKey(_root, "StartAgeBands");
            var list = bands ?? Array.Empty<RmdStartAgeBand>();

            if (list.Count == 0)
            {
                if (key != null)
                    _root.Remove(key);
                return;
            }

            var array = new JsonArray();
            foreach (var band in list)
            {
                if (band == null)
                    continue;

                var obj = new JsonObject
                {
                    ["BornFrom"] = band.BornFrom,
                    ["BornThrough"] = band.BornThrough.HasValue
                        ? JsonValue.Create(band.BornThrough.Value)
                        : null,
                    ["StartAge"] = band.StartAge
                };
                array.Add(obj);
            }

            if (key != null)
                _root[key] = array;
            else
                _root["StartAgeBands"] = array;
        }

        private static void ApplyDivisors(JsonObject target, Dictionary<int, decimal> divisors)
        {
            foreach (var key in target.Select(pair => pair.Key).ToList())
            {
                if (int.TryParse(key, out int age) && !divisors.ContainsKey(age))
                    target.Remove(key);
            }

            foreach (var pair in divisors.OrderBy(p => p.Key))
            {
                var key = FindAgeKey(target, pair.Key) ?? pair.Key.ToString();
                target[key] = JsonValue.Create(pair.Value);
            }
        }

        private static JsonObject GetOrCreateObject(JsonObject parent, string name)
        {
            var key = FindKey(parent, name) ?? name;
            if (parent[key] is JsonObject existing)
                return existing;

            var created = new JsonObject();
            parent[key] = created;
            return created;
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

        private static string? FindAgeKey(JsonObject obj, int age)
        {
            return obj
                .Select(pair => pair.Key)
                .FirstOrDefault(key => int.TryParse(key, out int parsed) && parsed == age);
        }
    }
}
