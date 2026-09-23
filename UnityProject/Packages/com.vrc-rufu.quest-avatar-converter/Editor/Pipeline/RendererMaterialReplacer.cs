using UnityEngine;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    /// <summary>
    /// T027 (FR-010/FR-011): points each Quest-side Renderer at its <see cref="QuestMaterial"/>
    /// via <c>ConversionContext.MaterialMap</c>, so Renderers sharing a source
    /// <see cref="PCMaterial"/> end up sharing one <see cref="QuestMaterial"/> by construction
    /// (the same map lookup returns the same instance for every Renderer referencing it).
    /// </summary>
    public static class RendererMaterialReplacer
    {
        public static void ReplaceAll(ConversionContext context)
        {
            if (context.QuestAvatar == null)
            {
                return;
            }

            foreach (var rendererRef in context.QuestAvatar.Renderers)
            {
                var newMaterials = new Material[rendererRef.SourceMaterials.Count];

                for (var i = 0; i < rendererRef.SourceMaterials.Count; i++)
                {
                    var sourceMaterial = rendererRef.SourceMaterials[i];
                    if (sourceMaterial != null &&
                        context.MaterialMap.TryGetValue(sourceMaterial, out var questMaterial) &&
                        questMaterial.Asset != null)
                    {
                        newMaterials[i] = questMaterial.Asset;
                    }
                    else
                    {
                        // No successful conversion for this slot (either an empty PC slot, or
                        // MaterialConverter (T018) already recorded an explicit failure in
                        // context.Log) — FR-010 requires Quest Renderers reference only newly
                        // generated Quest Materials, never the original PC ones, so the slot is
                        // left null (a visibly "missing material") rather than silently kept
                        // pointing at the PC Material or guessed.
                        newMaterials[i] = null;
                    }
                }

                rendererRef.RendererComponent.sharedMaterials = newMaterials;
            }
        }
    }
}
