using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using SLOverdrive.Core.Il2Cpp;

namespace SLOverdrive.TableDumper
{
    /// <summary>
    /// Dumps whole data sheets rather than individual typed rows.
    ///
    /// The row-class dumper can only see tables that have a <c>GLDataBase</c> subclass,
    /// which leaves out anything the game loads into a plain structure - the localisation
    /// table being the important one. Hooking the sheet loader instead catches everything,
    /// and gives the sheet's real column names instead of obfuscated field names:
    ///
    /// <code>
    /// GLDataSheet
    ///  ├─ SheetName
    ///  └─ DicColums : name -> GLDataColum
    ///                          └─ DataList : List&lt;object&gt;   one entry per row
    /// </code>
    ///
    /// Columns are index aligned, so row N is the Nth element of every column.
    /// </summary>
    internal static class SheetDumper
    {
        private static string _outputPath;
        private static ManualLogSource _log;
        private static int _maxRows;

        private static readonly HashSet<string> Dumped = new HashSet<string>();

        public static void Initialize(string outputPath, int maxRows, ManualLogSource log)
        {
            _outputPath = outputPath;
            _maxRows = maxRows;
            _log = log;
        }

        /// <summary>Harmony postfix on <c>GLDataSheet.LoadBin</c>.</summary>
        public static void Postfix(object __instance)
        {
            if (__instance == null) return;

            try
            {
                string sheetName = Il2CppReflect.GetMember(__instance, "SheetName") as string;
                if (string.IsNullOrWhiteSpace(sheetName)) sheetName = "unnamed";

                // A sheet can be loaded more than once; the first copy is enough.
                if (!Dumped.Add(sheetName)) return;

                Dump(sheetName, __instance);
            }
            catch (Exception ex)
            {
                _log.LogError("Failed to dump sheet: " + ex.Message);
            }
        }

        private static void Dump(string sheetName, object sheet)
        {
            var columns = Il2CppReflect.GetMember(sheet, "DicColums");
            var names = Il2CppReflect.DictionaryKeys(columns).ToList();

            if (names.Count == 0)
            {
                _log.LogWarning($"{sheetName}: no columns could be read.");
                return;
            }

            // Pull each column's values out once, then write across them row by row.
            var data = new List<List<object>>(names.Count);
            int rowCount = 0;

            foreach (var name in names)
            {
                var column = Invoke(sheet, "GetColum", name);
                var list = Il2CppReflect.GetMember(column, "DataList");

                int count = Il2CppReflect.ListCount(list);
                var values = new List<object>(count);

                for (int i = 0; i < count; i++)
                    values.Add(Il2CppReflect.ListItem(list, i));

                data.Add(values);
                rowCount = Math.Max(rowCount, count);
            }

            if (_maxRows > 0) rowCount = Math.Min(rowCount, _maxRows);

            string file = Path.Combine(_outputPath, Sanitize(sheetName) + ".csv");

            using (var writer = new StreamWriter(file, false, Encoding.UTF8))
            {
                writer.WriteLine(string.Join(",", names.Select(Escape)));

                for (int row = 0; row < rowCount; row++)
                {
                    var cells = new string[names.Count];

                    for (int col = 0; col < data.Count; col++)
                        cells[col] = Escape(row < data[col].Count ? data[col][row]?.ToString() ?? "" : "");

                    writer.WriteLine(string.Join(",", cells));
                }
            }

            _log.LogInfo($"  {sheetName}  ({rowCount} rows, {names.Count} columns)");
        }

        private static object Invoke(object target, string method, string argument)
        {
            var info = target.GetType().GetMethod(method, new[] { typeof(string) });

            try { return info?.Invoke(target, new object[] { argument }); }
            catch { return null; }
        }

        private static string Escape(string value)
        {
            if (value == null) return "";
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Sanitize(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            return name;
        }
    }
}
