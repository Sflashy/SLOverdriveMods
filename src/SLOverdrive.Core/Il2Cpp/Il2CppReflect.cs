using System;
using System.Collections.Generic;

namespace SLOverdrive.Core.Il2Cpp
{
    /// <summary>
    /// Reflection helpers for reading and writing Il2Cpp objects and collections.
    ///
    /// A note on why this exists. Il2CppInterop generates C# properties over the game's
    /// raw fields. Those properties are useless as Harmony hook points, because the game's
    /// native code writes the underlying memory directly and never calls them. They work
    /// perfectly well in the other direction: when called from managed code they read and
    /// write the real object. That is exactly what these helpers do.
    /// </summary>
    public static class Il2CppReflect
    {
        /// <summary>Reads a property or field by name. Returns null if absent.</summary>
        public static object GetMember(object target, string name)
        {
            if (target == null) return null;

            var type = target.GetType();

            var property = type.GetProperty(name);
            if (property != null) return property.GetValue(target);

            return type.GetField(name)?.GetValue(target);
        }

        /// <summary>Writes a property or field by name. Returns false if it could not be set.</summary>
        public static bool SetMember(object target, string name, object value)
        {
            if (target == null) return false;

            var type = target.GetType();

            var property = type.GetProperty(name);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
                return true;
            }

            var field = type.GetField(name);
            if (field != null)
            {
                field.SetValue(target, value);
                return true;
            }

            return false;
        }

        /// <summary>Reads an int-like member, falling back to <paramref name="fallback"/>.</summary>
        public static int GetInt(object target, string name, int fallback = 0)
        {
            var value = GetMember(target, name);
            if (value == null) return fallback;

            try { return Convert.ToInt32(value); }
            catch { return fallback; }
        }

        /// <summary>Element count of an Il2Cpp List. Returns 0 for null.</summary>
        public static int ListCount(object list)
        {
            if (list == null) return 0;

            var property = list.GetType().GetProperty("Count");
            if (property == null) return 0;

            try { return (int)property.GetValue(list); }
            catch { return 0; }
        }

        /// <summary>Element at <paramref name="index"/> of an Il2Cpp List.</summary>
        public static object ListItem(object list, int index)
        {
            var method = list?.GetType().GetMethod("get_Item", new[] { typeof(int) });

            try { return method?.Invoke(list, new object[] { index }); }
            catch { return null; }
        }

        /// <summary>
        /// Walks an Il2Cpp collection.
        ///
        /// Il2Cpp dictionaries and sets do not implement the managed IEnumerable that
        /// foreach expects, so they have to be driven by hand through their own
        /// GetEnumerator / MoveNext / Current triple.
        /// </summary>
        public static IEnumerable<object> Enumerate(object collection)
        {
            if (collection == null) yield break;

            // A managed enumerable, if we are lucky.
            if (collection is System.Collections.IEnumerable managed)
            {
                foreach (var item in managed) yield return item;
                yield break;
            }

            var getEnumerator = collection.GetType().GetMethod("GetEnumerator", Type.EmptyTypes);
            if (getEnumerator == null) yield break;

            object enumerator;
            try { enumerator = getEnumerator.Invoke(collection, null); }
            catch { yield break; }

            if (enumerator == null) yield break;

            var type = enumerator.GetType();
            var moveNext = type.GetMethod("MoveNext", Type.EmptyTypes);
            var current = type.GetProperty("Current") ?? type.GetProperty("get_Current");

            if (moveNext == null || current == null) yield break;

            while (true)
            {
                object more;
                try { more = moveNext.Invoke(enumerator, null); }
                catch { yield break; }

                if (!(more is bool advanced) || !advanced) yield break;

                object value;
                try { value = current.GetValue(enumerator); }
                catch { yield break; }

                yield return value;
            }
        }

        /// <summary>Keys of an Il2Cpp dictionary, as strings.</summary>
        public static IEnumerable<string> DictionaryKeys(object dictionary)
        {
            var keys = GetMember(dictionary, "Keys");
            if (keys == null) yield break;

            foreach (var key in Enumerate(keys))
                if (key != null)
                    yield return key.ToString();
        }
    }
}
