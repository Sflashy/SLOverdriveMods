using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using BepInEx.Logging;

namespace SLOverdrive.Core.Data
{
    /// <summary>
    /// Maps item IDs to display names, for readable logs.
    ///
    /// Backed by an optional <c>item_names.json</c> placed next to the plugin DLL:
    /// <code>{ "11100044": "Massive Tentacle", "16000002": "??? Key" }</code>
    ///
    /// Purely cosmetic. Everything degrades to raw IDs when the file is missing.
    /// </summary>
    public static class ItemDatabase
    {
        private static readonly Dictionary<int, string> Names = new Dictionary<int, string>();

        public static int Count => Names.Count;
        public static bool IsLoaded => Names.Count > 0;

        /// <summary>
        /// Loads the item name table from the directory containing the calling assembly.
        /// Safe to call more than once; later calls merge into the existing table.
        /// </summary>
        public static void Load(ManualLogSource log, string fileName = "item_names.json")
        {
            try
            {
                string directory = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
                string path = Path.Combine(directory, fileName);

                if (!File.Exists(path))
                {
                    log?.LogInfo($"{fileName} not found - items will be logged by ID.");
                    return;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(path));

                foreach (var property in document.RootElement.EnumerateObject())
                    if (int.TryParse(property.Name, out int id))
                        Names[id] = property.Value.GetString();

                log?.LogInfo($"Loaded {Names.Count} item names.");
            }
            catch (Exception ex)
            {
                log?.LogWarning($"Could not read {fileName}: {ex.Message}");
            }
        }

        /// <summary>Display name for an item ID, or <c>#id</c> when unknown.</summary>
        public static string NameOf(int id)
            => Names.TryGetValue(id, out var name) ? name : $"#{id}";

        /// <summary>Display name for a boxed item ID of unknown numeric type.</summary>
        public static string NameOf(object id)
        {
            if (id == null) return "<unknown>";

            try { return NameOf(Convert.ToInt32(id)); }
            catch { return id.ToString(); }
        }
    }
}
