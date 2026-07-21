using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using SLOverdrive.Core.Il2Cpp;

namespace SLOverdrive.TableDumper
{
    /// <summary>
    /// Writes one table's rows to a CSV file.
    ///
    /// Two sets of names are recorded. The row class exposes obfuscated field names
    /// (<c>NDHGAHKPBPB</c>), while the sheet knows the original column names from the
    /// game's data files. Both go into the header so the obfuscated fields can be
    /// matched up with something meaningful.
    /// </summary>
    internal sealed class TableWriter : IDisposable
    {
        /// <summary>
        /// Rows written between flushes. Flushing every row - which is what
        /// <c>AutoFlush</c> does - puts a synchronous disk write on the game's main
        /// thread for each row loaded. That is survivable while the main menu trickles
        /// tables in, but loading a save pulls thousands of rows at once and the game
        /// stalls long enough to be killed. Buffering makes that a rounding error.
        /// </summary>
        private const int FlushEvery = 512;

        private const int BufferBytes = 64 * 1024;

        private readonly StreamWriter _stream;
        private readonly List<PropertyInfo> _properties = new List<PropertyInfo>();
        private readonly int _maxRows;
        private readonly ManualLogSource _log;

        private int _rows;
        private int _sinceFlush;
        private bool _limitReported;

        public TableWriter(string directory, string typeName, string sheetName, int maxRows, ManualLogSource log)
        {
            _maxRows = maxRows;
            _log = log;

            string fileName = string.IsNullOrWhiteSpace(sheetName) ? typeName : $"{sheetName}_{typeName}";
            _stream = new StreamWriter(Path.Combine(directory, Sanitize(fileName) + ".csv"),
                                       false, Encoding.UTF8, BufferBytes)
            {
                AutoFlush = false
            };

            _log.LogInfo($"{typeName}: sheet '{sheetName ?? "?"}'");
        }

        /// <summary>Pushes anything buffered to disk. Cheap when there is nothing to write.</summary>
        public void Flush()
        {
            try { _stream.Flush(); }
            catch (Exception ex) { _log.LogWarning("Flush failed: " + ex.Message); }
        }

        public void Dispose()
        {
            try { _stream.Dispose(); }
            catch { }
        }

        /// <summary>
        /// Records the sheet's real column names, then writes the CSV header from the
        /// row class's own readable properties.
        /// </summary>
        public void WriteHeader(object row, object sheet)
        {
            var columns = ReadColumnNames(sheet);
            if (columns.Count > 0)
            {
                _stream.WriteLine("# sheet columns: " + string.Join(" | ", columns));
                _log.LogInfo("  columns: " + string.Join(", ", columns));
            }

            foreach (var property in row.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;

                // Skip the interop plumbing that every Il2Cpp object carries.
                if (property.Name == "Pointer" || property.Name == "ObjectClass") continue;

                _properties.Add(property);
            }

            _stream.WriteLine(string.Join(",", _properties.ConvertAll(p => p.Name)));
        }

        public void WriteRow(object row)
        {
            if (_maxRows > 0 && _rows >= _maxRows)
            {
                if (!_limitReported)
                {
                    _limitReported = true;
                    _stream.WriteLine("# row limit reached");
                }

                return;
            }

            var values = new List<string>(_properties.Count);

            foreach (var property in _properties)
            {
                string text;
                try { text = Format(property.GetValue(row)); }
                catch { text = "<error>"; }

                values.Add(Escape(text));
            }

            _stream.WriteLine(string.Join(",", values));
            _rows++;

            // A hard crash loses whatever is still buffered, so flush periodically
            // rather than only at exit. 512 rows caps the loss at a table fragment
            // while cutting disk writes by three orders of magnitude.
            if (++_sinceFlush >= FlushEvery)
            {
                _sinceFlush = 0;
                Flush();
            }
        }

        /// <summary>
        /// Pulls the original column names out of the sheet's <c>DicColums</c> dictionary.
        /// Best effort - the dictionary is an Il2Cpp type and may not enumerate cleanly.
        /// </summary>
        private static List<string> ReadColumnNames(object sheet)
        {
            var names = new List<string>();
            var dictionary = Il2CppReflect.GetMember(sheet, "DicColums");
            if (dictionary == null) return names;

            try
            {
                var keys = Il2CppReflect.GetMember(dictionary, "Keys");
                if (keys is IEnumerable enumerable)
                {
                    foreach (var key in enumerable)
                        names.Add(key?.ToString() ?? "");
                }
            }
            catch
            {
                // Not enumerable through interop; the obfuscated field names still get us there.
            }

            return names;
        }

        private static string Format(object value)
        {
            if (value == null) return "";

            int count = Il2CppReflect.ListCount(value);
            if (count > 0)
            {
                var parts = new List<string>(count);
                for (int i = 0; i < count; i++)
                    parts.Add(Describe(Il2CppReflect.ListItem(value, i)));

                return "[" + string.Join(";", parts) + "]";
            }

            return Describe(value);
        }

        /// <summary>
        /// Renders one value, expanding a nested object into its fields.
        ///
        /// Calling ToString on an Il2Cpp object returns its type name, so a column
        /// holding something like <c>List&lt;ItemStatInfo&gt;</c> used to dump as the
        /// literal text "NLib.ItemStatInfo" repeated - the row was recorded, the
        /// numbers inside it were not. Reading the properties instead keeps the
        /// values, which is the only way stat lines come out of the tables at all.
        ///
        /// One level deep on purpose: these objects are small records, and anything
        /// deeper is a reference graph that would bloat the file without adding data.
        /// </summary>
        private static string Describe(object value)
        {
            if (value == null) return "";

            var type = value.GetType();
            if (type.IsPrimitive || value is string || value is decimal || type.IsEnum)
                return value.ToString();

            PropertyInfo[] properties;
            try
            {
                properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            }
            catch
            {
                return value.ToString();
            }

            var fields = new List<string>();
            foreach (var property in properties)
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;
                if (property.Name == "Pointer" || property.Name == "ObjectClass") continue;

                object inner;
                try { inner = property.GetValue(value); }
                catch { continue; }
                if (inner == null) continue;

                var innerType = inner.GetType();
                string text = innerType.IsPrimitive || inner is string ||
                              inner is decimal || innerType.IsEnum
                    ? inner.ToString()
                    : null;

                if (text == null) continue;
                fields.Add(property.Name + "=" + text);
            }

            // Nothing readable means it is a reference to something else, and the
            // type name is the most honest thing to record.
            return fields.Count > 0 ? "{" + string.Join("|", fields) + "}" : value.ToString();
        }

        private static string Escape(string value)
        {
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
