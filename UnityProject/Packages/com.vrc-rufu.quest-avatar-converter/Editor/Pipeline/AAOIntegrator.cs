using Anatawa12.AvatarOptimizer;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    /// <summary>
    /// T028 (FR-012/FR-013): detects whether AAO (Avatar Optimizer) is installed and halts
    /// generation with a clear, actionable message before any output is produced if absent;
    /// otherwise adds the AAO `Trace And Optimize` Avatar Global Component to the Quest avatar
    /// root. The exact class name (<see cref="TraceAndOptimize"/>, sealed, extends
    /// `AvatarGlobalComponent`, `internal` constructor — added via `AddComponent&lt;T&gt;()`, never
    /// `new T()`) was confirmed against the installed AAO package source per research.md §3.
    /// </summary>
    /// <remarks>
    /// This package's own `package.json` already declares a hard UPM dependency on
    /// `com.anatawa12.avatar-optimizer` (T004), and this file compiles against AAO's runtime
    /// assembly directly — so within a project resolved normally through UPM, AAO being "absent at
    /// runtime" after a successful compile is not reachable. The check still exists because a
    /// precompiled distribution of this package dropped into a project without AAO (bypassing
    /// normal UPM dependency resolution) is a real scenario for VRChat creators, and FR-013
    /// requires a clear halt rather than an unhandled error in that case.
    /// </remarks>
    public static class AAOIntegrator
    {
        private const string TraceAndOptimizeAssemblyQualifiedName =
            "Anatawa12.AvatarOptimizer.TraceAndOptimize, com.anatawa12.avatar-optimizer.runtime";

        public static bool IsAaoInstalled()
        {
            return ResolveTraceAndOptimizeType() != null;
        }

        /// <summary>Call once AAO's presence has already been confirmed (<see cref="IsAaoInstalled"/>)
        /// and <c>context.QuestAvatar.RootPrefab</c> exists (after AvatarDuplicator, T014).</summary>
        public static void AddTraceAndOptimizeComponent(ConversionContext context)
        {
            var component = context.QuestAvatar.RootPrefab.AddComponent<TraceAndOptimize>();
            context.QuestAvatar.AaoTraceAndOptimize = component;
        }

        private static System.Type ResolveTraceAndOptimizeType()
        {
            var type = System.Type.GetType(TraceAndOptimizeAssemblyQualifiedName);
            if (type != null)
            {
                return type;
            }

            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType("Anatawa12.AvatarOptimizer.TraceAndOptimize");
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
