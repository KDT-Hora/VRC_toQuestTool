using System;
using UnityEngine;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    /// <summary>
    /// Shared reflection helper for resolving a `FlaggedComponentRule.ComponentTypeName` (a plain
    /// or assembly-qualified type name) to an actual `Component`-derived <see cref="Type"/>. Used
    /// by <see cref="QuestCompatibilityRulesLoader"/> (load-time validation, T010/T011) and
    /// <see cref="QuestCompatibilityChecker"/> (scanning, T040).
    /// </summary>
    public static class ComponentTypeResolver
    {
        public static Type Resolve(string componentTypeName)
        {
            if (string.IsNullOrEmpty(componentTypeName))
            {
                return null;
            }

            var type = Type.GetType(componentTypeName);
            if (type == null)
            {
                // A bare/partial type name (no assembly qualification) won't resolve via
                // Type.GetType alone — fall back to scanning loaded assemblies.
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(componentTypeName);
                    if (type != null)
                    {
                        break;
                    }
                }
            }

            return type != null && typeof(Component).IsAssignableFrom(type) ? type : null;
        }
    }
}
