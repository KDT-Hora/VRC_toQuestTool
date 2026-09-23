using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using VrcRufu.QuestAvatarConverter.Materials;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter.Textures
{
    /// <summary>
    /// T020 (FR-008): groups one Material's texture-kind mapped properties by their resolved
    /// <see cref="TextureClassification"/> (Color/Normal/Mask/Emission), driven by
    /// <c>PropertyMapping.TargetClassification</c> as already resolved by ShaderPropertyMapper
    /// (T016). This grouping is exactly TextureAtlasGenerator's (T021) input: one atlas per
    /// (Material, Classification), never mixing classifications into one merged image.
    /// </summary>
    public static class TextureTypeClassifier
    {
        public static Dictionary<TextureClassification, List<PCTexture>> Classify(ShaderPropertyMappingResult mappingResult)
        {
            var result = new Dictionary<TextureClassification, List<PCTexture>>();

            foreach (var mapped in mappingResult.MappedProperties)
            {
                if (mapped.SourceProperty.Kind != MaterialPropertyKind.Texture)
                {
                    continue;
                }

                // An empty texture slot (flat-color Material Edge Case) contributes nothing to
                // merge/atlas. A texture-kind property whose value isn't a Texture2D (e.g. a
                // Cubemap/RenderTexture — not expected in practice for avatar Materials, since
                // the atlas/resize pipeline operates on 2D source images) is likewise skipped
                // here rather than merged.
                if (!(mapped.SourceProperty.TextureValue is Texture2D texture))
                {
                    continue;
                }

                if (!result.TryGetValue(mapped.TargetClassification, out var list))
                {
                    list = new List<PCTexture>();
                    result[mapped.TargetClassification] = list;
                }

                list.Add(new PCTexture(texture, texture.width, texture.height, HasAlphaChannel(texture)));
            }

            return result;
        }

        private static bool HasAlphaChannel(Texture2D texture)
        {
            return GraphicsFormatUtility.HasAlphaChannel(texture.graphicsFormat);
        }
    }
}
