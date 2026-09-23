using System;
using System.Collections.Generic;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter.Textures
{
    /// <summary>
    /// T021: pure placement-math logic (Constitution Principle VI) — inputs are source texture
    /// dimensions for one (Material, TextureClassification) group (never mixing classifications
    /// into one layout: enforced structurally, since TextureTypeClassifier (T020) already split
    /// them and callers pass one classification's list at a time, FR-008), output is an
    /// <see cref="AtlasLayout"/> whose placements preserve every source image's aspect ratio.
    /// </summary>
    /// <remarks>
    /// Packing strategy: place every source texture at its own native pixel size (never
    /// stretched/scaled here — that trivially preserves aspect ratio) into cells of a roughly
    /// square grid sized to the largest source texture's width/height, so no two sources overlap
    /// regardless of size mismatch. Downscaling the finished merged image to
    /// ConversionSettings.MaxTextureSize is a separate, later concern (TextureResizer, T023) —
    /// this stage only decides where each source lands, not how big the final output is.
    /// </remarks>
    public static class TextureAtlasGenerator
    {
        public static AtlasLayout Generate(IReadOnlyList<PCTexture> sourceTextures)
        {
            if (sourceTextures == null || sourceTextures.Count == 0)
            {
                return new AtlasLayout(Array.Empty<AtlasPlacement>(), 0, 0);
            }

            if (sourceTextures.Count == 1)
            {
                // Single-texture case: no-op layout — canvas exactly matches the one source, no
                // merging needed.
                var only = sourceTextures[0];
                var soloPlacement = new AtlasPlacement(only, new RectInt(0, 0, only.Width, only.Height));
                return new AtlasLayout(new[] { soloPlacement }, only.Width, only.Height);
            }

            var cellWidth = 0;
            var cellHeight = 0;
            foreach (var texture in sourceTextures)
            {
                cellWidth = Mathf.Max(cellWidth, texture.Width);
                cellHeight = Mathf.Max(cellHeight, texture.Height);
            }

            var columns = Mathf.CeilToInt(Mathf.Sqrt(sourceTextures.Count));
            var rows = Mathf.CeilToInt(sourceTextures.Count / (float)columns);

            var placements = new List<AtlasPlacement>(sourceTextures.Count);
            for (var i = 0; i < sourceTextures.Count; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var texture = sourceTextures[i];
                var destRect = new RectInt(column * cellWidth, row * cellHeight, texture.Width, texture.Height);
                placements.Add(new AtlasPlacement(texture, destRect));
            }

            return new AtlasLayout(placements, columns * cellWidth, rows * cellHeight);
        }
    }
}
