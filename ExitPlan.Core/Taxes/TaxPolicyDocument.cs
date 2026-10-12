using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ExitPlan.Core.Taxes
{
    /// <summary>
    /// Loads and saves a Tax{year}.json file while preserving unknown properties
    /// so future schema additions are not stripped by the settings editor.
    /// </summary>
    public sealed class TaxPolicyDocument
    {
        private static readonly JsonSerializerOptions DeserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true
        };

        private static readonly JsonSerializerOptions WriteOptions = new()
        {
            WriteIndented = true
        };

        private readonly JsonObject _root;

        private TaxPolicyDocument(string filePath, JsonObject root)
        {
            FilePath = filePath;
            _root = root;
        }

        public string FilePath { get; }

        public static TaxPolicyDocument Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", nameof(path));

            if (!File.Exists(path))
                throw new FileNotFoundException($"Tax policy file not found: {path}", path);

            var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                ?? throw new InvalidOperationException($"Tax file is not a JSON object: {path}");

            return new TaxPolicyDocument(path, node);
        }

        public TaxPolicy ToPolicy()
        {
            return _root.Deserialize<TaxPolicy>(DeserializeOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize tax policy: {FilePath}");
        }

        public void Apply(TaxPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);

            SetNumber(_root, "Year", policy.Year);
            SetNumber(_root, "AdditionalStandardDeductionAge", policy.AdditionalStandardDeductionAge);
            ApplyFilingStatus(GetOrCreateObject(_root, "Single"), policy.Single);
            ApplyFilingStatus(GetOrCreateObject(_root, "MarriedFilingJointly"), policy.MarriedFilingJointly);
            SetNumber(_root, "SingleSsBaseThreshold", policy.SingleSsBaseThreshold);
            SetNumber(_root, "SingleSsSecondThreshold", policy.SingleSsSecondThreshold);
            SetNumber(_root, "MarriedSsBaseThreshold", policy.MarriedSsBaseThreshold);
            SetNumber(_root, "MarriedSsSecondThreshold", policy.MarriedSsSecondThreshold);
        }

        public void Save() => SaveTo(FilePath);

        public void SaveTo(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", nameof(path));

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = _root.ToJsonString(WriteOptions);
            if (!json.EndsWith('\n'))
                json += Environment.NewLine;

            File.WriteAllText(path, json);
        }

        public static void Save(string path, TaxPolicy policy)
        {
            ArgumentNullException.ThrowIfNull(policy);

            TaxPolicyDocument document = File.Exists(path)
                ? Load(path)
                : new TaxPolicyDocument(path, new JsonObject());

            document.Apply(policy);
            document.Save();
        }

        private static void ApplyFilingStatus(JsonObject target, FilingStatusTaxTable table)
        {
            SetNumber(target, "StandardDeduction", table.StandardDeduction);
            SetNumber(target, "AdditionalStandardDeduction", table.AdditionalStandardDeduction);
            MergeArray(target, "OrdinaryBrackets", table.OrdinaryBrackets, WriteTaxBracket);
            MergeArray(target, "CapitalGainsBrackets", table.CapitalGainsBrackets, WriteTaxBracket);
            MergeArray(target, "IrmaaBrackets", table.IrmaaBrackets, WriteIrmaaBracket);
        }

        private static void WriteTaxBracket(JsonObject obj, TaxBracket bracket)
        {
            SetNumber(obj, "Threshold", bracket.Threshold);
            SetNumber(obj, "Rate", bracket.Rate);
        }

        private static void WriteIrmaaBracket(JsonObject obj, IrmaaBracket bracket)
        {
            SetNumber(obj, "Threshold", bracket.Threshold);
            SetNumber(obj, "MonthlySurcharge", bracket.MonthlySurcharge);
        }

        private static void MergeArray<T>(
            JsonObject parent,
            string propertyName,
            IList<T> items,
            Action<JsonObject, T> apply)
        {
            var key = FindKey(parent, propertyName) ?? propertyName;
            var array = parent[key] as JsonArray;
            if (array == null)
            {
                array = new JsonArray();
                parent[key] = array;
            }

            while (array.Count > items.Count)
                array.RemoveAt(array.Count - 1);

            for (int i = 0; i < items.Count; i++)
            {
                JsonObject obj;
                if (i < array.Count && array[i] is JsonObject existing)
                {
                    obj = existing;
                }
                else
                {
                    obj = new JsonObject();
                    if (i < array.Count)
                        array[i] = obj;
                    else
                        array.Add(obj);
                }

                apply(obj, items[i]);
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
