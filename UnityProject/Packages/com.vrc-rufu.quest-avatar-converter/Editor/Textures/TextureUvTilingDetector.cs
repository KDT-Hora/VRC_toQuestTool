using System.Collections.Generic;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Materials;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter.Textures
{
    /// <summary>
    /// T025 (spec.md Edge Case): detects non-default UV Scale/Offset on a Material whose textures
    /// are being merged, and appends a <see cref="ConversionLogEntry"/> warning — the merged
    /// result may not sample correctly since the merge/atlas pipeline assumes a standard 0-1 UV
    /// range (see Assumptions). This detector never attempts UV remapping — that stays out of
    /// scope for v1.
    /// </summary>
    public static class TextureUvTilingDetector
    {
        private static readonly Vector2 DefaultScale = Vector2.one;
        private static readonly Vector2 DefaultOffset = Vector2.zero;

        /// <summary>Call once per Material after TextureTypeClassifier (T020) has grouped its
        /// texture-kind properties — <paramref name="classifiedTextures"/> is that grouping, used
        /// only to determine whether this Material is actually undergoing a merge (any
        /// classification group with more than one source texture).</summary>
        public static void DetectAndLog(
            ConversionContext context,
            PCMaterial sourceMaterial,
            IReadOnlyDictionary<TextureClassification, List<PCTexture>> classifiedTextures)
        {
            if (!IsBeingMerged(classifiedTextures))
            {
                return;
            }

            foreach (var property in sourceMaterial.Properties)
            {
                if (property.Kind != MaterialPropertyKind.Texture || property.TextureValue == null)
                {
                    continue;
                }

                if (property.UvScale != DefaultScale || property.UvOffset != DefaultOffset)
                {
                    context.Log.Add(ConversionLogEntry.Warning(
                        $"Material '{sourceMaterial.Asset?.name}': Property '{property.Name}' has a non-default " +
                        $"UV Scale/Offset (Scale={property.UvScale}, Offset={property.UvOffset}) and this Material's " +
                        "textures are being merged — the merged result may not be visually accurate (UV remapping " +
                        "is out of scope for v1).",
                        sourceMaterial.Asset));
                }
            }
        }

        private static bool IsBeingMerged(IReadOnlyDictionary<TextureClassification, List<PCTexture>> classifiedTextures)
        {
            if (classifiedTextures == null)
            {
                return false;
            }

            foreach (var group in classifiedTextures.Values)
            {
                if (group.Count > 1)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
