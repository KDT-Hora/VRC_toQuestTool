using UnityEditor;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter.Materials
{
    /// <summary>
    /// T019 (FR-020a): enables GPU Instancing on every generated <see cref="QuestMaterial"/> by
    /// default. `Material.enableInstancing` is the mechanism every VRChat `VRChat/Mobile/*` target
    /// shader (and Unity shaders generally) exposes for this — no per-shader special-casing is
    /// needed, so this stays a single, shader-agnostic unit per Constitution Principle III/IV.
    /// </summary>
    public static class GpuInstancingApplier
    {
        public static void Apply(QuestMaterial questMaterial)
        {
            if (questMaterial?.Asset == null)
            {
                return;
            }

            questMaterial.Asset.enableInstancing = true;
            questMaterial.GpuInstancingEnabled = true;
            EditorUtility.SetDirty(questMaterial.Asset);
        }

        /// <summary>Convenience: applies to every Material generated so far in this run.</summary>
        public static void ApplyAll(ConversionContext context)
        {
            foreach (var questMaterial in context.MaterialMap.Values)
            {
                Apply(questMaterial);
            }
        }
    }
}
