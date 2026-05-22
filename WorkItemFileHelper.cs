using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AzureDevOpsWorkItemReader
{
    public static class WorkItemFileHelper
    {
        private static readonly string[] ReadOnlyFields = 
        { 
            "System.Id", "System.Rev", "System.AreaId", "System.IterationId", 
            "System.BoardColumn", "System.BoardColumnDone", "System.NodeName", "WEF_" 
        };

        // 1. Change return type to List<Dictionary<string, object?>>
        public static List<Dictionary<string, object?>> LoadMultipleFieldsFromJson(string filePath, string projectName)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"File not found: {filePath}");

            string jsonContent = File.ReadAllText(filePath);
            
            // 2. Add '?' to handle possible null from Deserialize
            var rawDataList = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonContent);

            if (rawDataList == null)
            {
                Console.WriteLine("Warning: JSON deserialization returned null. Check the file format.");
                Environment.Exit(1);
                return new List<Dictionary<string, object?>>(); // unreachable but satisfies compiler
            }

            // 3. Ensure resultList uses object?
            var resultList = new List<Dictionary<string, object?>>();

            foreach (var rawItem in rawDataList)
            {
                // 4. cleanFields must be Dictionary<string, object?>
                var cleanFields = new Dictionary<string, object?>();

                foreach (var kvp in rawItem)
                {
                    // Skip read-only fields
                    if (ReadOnlyFields.Any(rf => kvp.Key.StartsWith(rf))) continue;

                    // 5. Explicitly use object? for the return from ExtractValue
                    object? value = ExtractValue(kvp.Value);
                    cleanFields.Add(kvp.Key, value);
                }

                // 6. This now matches the signature of FixPath(Dictionary<string, object?> ...)
                FixPath(cleanFields, "System.AreaPath", projectName);
                FixPath(cleanFields, "System.IterationPath", projectName);

                resultList.Add(cleanFields);
            }

            return resultList;
        }

        private static object? ExtractValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String: return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int i)) return i;
                    return element.GetDouble();
                case JsonValueKind.True: return true;
                case JsonValueKind.False: return false;
                case JsonValueKind.Null: return null;
                default: return element.GetRawText();
            }
        }

        private static void FixPath(Dictionary<string, object?> fields, string key, string projectName)
        {
            if (fields.TryGetValue(key, out object? value) && value is string strValue)
            {
                if (string.IsNullOrWhiteSpace(strValue))
                {
                    Console.WriteLine($"Warning: {key} is empty or whitespace. Skipping path fix.");
                    Environment.Exit(3);
                    return;
                }

                string currentPath = strValue.Trim('\\', ' ');

                if (!currentPath.StartsWith(projectName, StringComparison.OrdinalIgnoreCase))
                {
                    fields[key] = $"{projectName}\\{currentPath}";
                }
                else
                {
                    fields[key] = currentPath;
                }
            }
        }
    }
}