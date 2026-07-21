using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using SLOverdrive.Core.Il2Cpp;

namespace SLOverdrive.TableDumper
{
    /// <summary>
    /// Dumps the game's GameData tables to CSV at runtime.
    ///
    /// The tables in <c>GameData/*.byte</c> are encrypted on disk, but the game decrypts
    /// them into <c>NLib.GLDataSheet</c> before handing each row to
    /// <c>GLDataBase.LoadDataSheet</c>. Hooking that gives us the plaintext, complete
    /// with real column names - no need to break the encryption.
    ///
    /// This is a diagnostic tool. It changes nothing in the game.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BasePlugin
    {
        public const string Guid = "sflashy.sloverdrive.tabledumper";
        public const string Name = "SL Overdrive - Table Dumper";
        public const string Version = "1.0.0";

        internal static Plugin Instance;

        private ConfigEntry<bool> _enabled;
        private ConfigEntry<string> _typeNames;
        private ConfigEntry<string> _outputDirectory;
        private ConfigEntry<int> _maxRowsPerTable;

        private readonly Dictionary<string, TableWriter> _writers = new Dictionary<string, TableWriter>();
        private string _outputPath;

        public override void Load()
        {
            Instance = this;

            _enabled = Config.Bind("General", "Enabled", false,
                "Dump tables on the next launch. This is a diagnostic tool, not something " +
                "to leave running - it patches every table row loader and slows startup. " +
                "Switch it on when you need a dump, then switch it back off.");

            var sheetMode = Config.Bind("General", "SheetMode", true,
                "Dump whole data sheets instead of typed rows. This catches every table, " +
                "including ones with no row class such as the localisation text, and gives " +
                "the sheet's real column names rather than obfuscated field names.");

            _typeNames = Config.Bind("General", "Types", "AJIHKOOLMFK",
                "Comma separated row class names to dump. These are obfuscated and change " +
                "between game builds. Use '*' to dump every table - slow, and produces a lot of files.");

            _outputDirectory = Config.Bind("General", "OutputDirectory", "",
                "Where to write the CSV files. Empty means a 'TableDump' folder next to the plugin.");

            _maxRowsPerTable = Config.Bind("General", "MaxRowsPerTable", 0,
                "Stop after this many rows per table. 0 = no limit.");

            if (!_enabled.Value)
            {
                Log.LogInfo("Disabled. Set Enabled = true in the config to dump tables.");
                return;
            }

            _outputPath = string.IsNullOrWhiteSpace(_outputDirectory.Value)
                ? Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "TableDump")
                : _outputDirectory.Value;

            Directory.CreateDirectory(_outputPath);
            Log.LogInfo($"Writing table dumps to {_outputPath}");

            // Rows are buffered, so a clean exit has to push the tail of every table
            // to disk. A crash still keeps everything up to the last checkpoint.
            AppDomain.CurrentDomain.ProcessExit += (_, __) => CloseAll();

            if (sheetMode.Value)
            {
                SheetDumper.Initialize(_outputPath, _maxRowsPerTable.Value, Log);
                PatchSheetLoader();
                return;
            }

            PatchLoaders();
        }

        /// <summary>
        /// Patches <c>LoadDataSheet</c> on each requested row class.
        ///
        /// The method is declared on the shared <c>GLDataBase</c> base and overridden per
        /// table, so patching the override catches exactly that table's rows.
        /// </summary>
        private void PatchLoaders()
        {
            var harmony = new Harmony(Guid);
            var postfix = AccessTools.Method(typeof(Plugin), nameof(LoadDataSheetPostfix));

            foreach (var raw in _typeNames.Value.Split(','))
            {
                string name = raw.Trim();
                if (name.Length == 0) continue;

                if (name == "*")
                {
                    PatchAllTables(harmony, postfix);
                    continue;
                }

                var type = TypeResolver.Find(name);
                if (type == null)
                {
                    Log.LogWarning($"Row class not found: {name}");
                    continue;
                }

                TryPatch(harmony, postfix, type);
            }
        }

        /// <summary>
        /// Patches <c>GLDataSheet.LoadBin</c>, which every table passes through
        /// regardless of whether it has a typed row class.
        /// </summary>
        private void PatchSheetLoader()
        {
            var type = TypeResolver.Find("NLib.GLDataSheet");
            if (type == null)
            {
                Log.LogError("NLib.GLDataSheet not found - cannot dump in sheet mode.");
                return;
            }

            var target = AccessTools.Method(type, "LoadBin");
            if (target == null)
            {
                Log.LogError("GLDataSheet.LoadBin not found.");
                return;
            }

            try
            {
                var postfix = AccessTools.Method(typeof(SheetDumper), nameof(SheetDumper.Postfix));
                new Harmony(Guid).Patch(target, postfix: new HarmonyMethod(postfix));
                Log.LogInfo("Sheet mode: dumping every table as it loads.");
            }
            catch (Exception ex)
            {
                Log.LogError("Could not patch GLDataSheet.LoadBin: " + ex.Message);
            }
        }

        private void PatchAllTables(Harmony harmony, MethodInfo postfix)
        {
            var baseType = TypeResolver.Find("NLib.GLDataBase") ?? TypeResolver.Find("GLDataBase");
            if (baseType == null)
            {
                Log.LogWarning("GLDataBase not found - cannot dump every table.");
                return;
            }

            int count = 0;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (var type in types)
                {
                    if (type == baseType || !baseType.IsAssignableFrom(type)) continue;
                    if (TryPatch(harmony, postfix, type)) count++;
                }
            }

            Log.LogInfo($"Patched {count} table row classes.");
        }

        private bool TryPatch(Harmony harmony, MethodInfo postfix, Type type)
        {
            var target = AccessTools.Method(type, "LoadDataSheet");
            if (target == null) return false;

            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                Log.LogInfo($"Dumping rows of {type.Name}");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Could not patch {type.Name}.LoadDataSheet - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Runs once per row. <c>__instance</c> is the row, <c>__0</c> the sheet it came from.
        /// </summary>
        public static void LoadDataSheetPostfix(object __instance, object __0)
        {
            Instance?.WriteRow(__instance, __0);
        }

        private void WriteRow(object row, object sheet)
        {
            try
            {
                if (row == null) return;

                string typeName = row.GetType().Name;

                if (!_writers.TryGetValue(typeName, out var writer))
                {
                    // A new table starting means the previous ones are finished, which
                    // makes this the natural checkpoint: it bounds what a crash can
                    // lose to the table currently being read.
                    FlushAll();

                    string sheetName = Il2CppReflect.GetMember(sheet, "SheetName") as string;
                    writer = new TableWriter(_outputPath, typeName, sheetName, _maxRowsPerTable.Value, Log);
                    _writers[typeName] = writer;

                    writer.WriteHeader(row, sheet);
                }

                writer.WriteRow(row);
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to dump row: " + ex.Message);
            }
        }

        private void FlushAll()
        {
            foreach (var writer in _writers.Values)
                writer.Flush();
        }

        private void CloseAll()
        {
            foreach (var writer in _writers.Values)
                writer.Dispose();

            _writers.Clear();
        }
    }
}
