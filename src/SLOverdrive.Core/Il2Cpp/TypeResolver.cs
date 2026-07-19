using System;
using System.Collections.Generic;
using System.Reflection;

namespace SLOverdrive.Core.Il2Cpp
{
    /// <summary>
    /// Locates types and methods in the Il2CppInterop-generated assemblies.
    ///
    /// Most of this game's gameplay classes are obfuscated, so their names change with
    /// every build. Type signatures are not obfuscated, which makes searching by
    /// parameter or return type far more durable than searching by name.
    /// </summary>
    public static class TypeResolver
    {
        /// <summary>
        /// Finds a type by full name. Il2CppInterop may prefix generated namespaces
        /// with "Il2Cpp", so both forms are tried.
        /// </summary>
        public static Type Find(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;

            var candidates = new[] { fullName, "Il2Cpp" + fullName };

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var candidate in candidates)
                {
                    try
                    {
                        var type = assembly.GetType(candidate, false);
                        if (type != null) return type;
                    }
                    catch
                    {
                        // Assemblies that cannot be inspected are not interesting.
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the first instance method on <paramref name="type"/> whose parameter
        /// types match the given type-name fragments, ignoring the method's own name.
        ///
        /// Use this instead of looking a method up by name: the name is obfuscated,
        /// the signature is not.
        /// </summary>
        public static MethodInfo FindMethodBySignature(Type type, params string[] parameterTypeNames)
        {
            if (type == null) return null;

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var method in type.GetMethods(flags))
            {
                var parameters = method.GetParameters();
                if (parameters.Length != parameterTypeNames.Length) continue;

                bool match = true;
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (!parameters[i].ParameterType.Name.Contains(parameterTypeNames[i]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match) return method;
            }

            return null;
        }

        /// <summary>
        /// Scans every loaded interop assembly for a type that declares a method
        /// accepting the given parameter type. Slower than <see cref="Find"/>, but it
        /// survives game updates because it never relies on an obfuscated name.
        ///
        /// Useful for recovering a renamed class after a game patch.
        /// </summary>
        public static IEnumerable<Type> FindTypesDeclaringParameter(string parameterTypeName)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (var type in types)
                {
                    MethodInfo[] methods;
                    try { methods = type.GetMethods(flags); }
                    catch { continue; }

                    foreach (var method in methods)
                    {
                        foreach (var parameter in method.GetParameters())
                        {
                            if (parameter.ParameterType.Name.Contains(parameterTypeName))
                            {
                                yield return type;
                                goto nextType;
                            }
                        }
                    }

                    nextType: ;
                }
            }
        }
    }
}
