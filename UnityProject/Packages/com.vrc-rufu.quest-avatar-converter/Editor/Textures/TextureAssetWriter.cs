using System.Linq;
using UnityEditor;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter.Textures
{
    /// <summary>
    /// T026 (FR-009): renders <see cref="TextureAtlasGenerator"/>/<see cref="TextureResizer"/>
    /// output to an actual <see cref="Texture2D"/> asset, always encoded as lossless PNG,
    /// preserving an alpha channel whenever any source Texture used one. Written under
    /// `Assets/&lt;QuestConvertedRoot&gt;/&lt;AvatarName&gt;/Textures/`.
    /// </summary>
    /// <remarks>
    /// Compositing goes through a temporary <see cref="RenderTexture"/> (GPU blit) rather than
    /// `Texture2D.GetPixels`, so it works regardless of whether the *source* PC Textures have
    /// "Read/Write Enabled" set in their import settings — requiring that on every source texture
    /// would be an invasive, easy-to-miss prerequisite on the user's PC avatar assets, and reading
    /// via the GPU never touches those source assets either way (Constitution I).
    /// </remarks>
    public static class TextureAssetWriter
    {
        /// <summary>Populates and returns <paramref name="questTexture"/>'s Asset/Layout/FinalSize
        /// fields. <paramref name="questTexture"/>.SourcePCTextures MUST already be set (the
        /// caller — TextureTypeClassifier's output, wrapped by ConversionPipeline — determines
        /// which sources feed this classification).</remarks>
        public static QuestTexture Write(QuestTexture questTexture, AtlasLayout layout, int maxSize, string texturesFolder, string baseFileName)
        {
            var sourceHasAlpha = questTexture.SourcePCTextures.Any(t => t.HasAlpha);
            var final = CompositeAndResize(layout, maxSize, out var targetWidth, out var targetHeight);

            var pngBytes = final.EncodeToPNG();
            Object.DestroyImmediate(final);

            QuestOutputPaths.EnsureFolder(texturesFolder);
            var assetPath = $"{texturesFolder}/{baseFileName}.png";
            System.IO.File.WriteAllBytes(assetPath, pngBytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.alphaSource = sourceHasAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = sourceHasAlpha;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            questTexture.Asset = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            // "Present only when SourcePCTextures.Count > 1" (data-model.md AtlasLayout on
            // QuestTexture) — a single-source, no-op layout carries no atlas information.
            questTexture.Layout = questTexture.SourcePCTextures.Count > 1 ? layout : null;
            questTexture.FinalSize = (targetWidth, targetHeight);
            return questTexture;
        }

        /// <summary>Composites <paramref name="layout"/>'s placements and resizes the result to
        /// <paramref name="maxSize"/> (<see cref="TextureResizer"/> math), entirely in memory —
        /// never writes to disk or touches AssetDatabase. Shared by <see cref="Write"/> (which
        /// encodes/persists the result) and the Preview panel (T045, FR-017: "without ... writing
        /// anything to disk"), so both use the exact same compositing code. Caller owns the
        /// returned Texture2D and must destroy it.</summary>
        internal static Texture2D CompositeAndResize(AtlasLayout layout, int maxSize, out int targetWidth, out int targetHeight)
        {
            (targetWidth, targetHeight) = TextureResizer.ComputeTargetSize(layout.Width, layout.Height, maxSize);

            var composed = Composite(layout);
            if (targetWidth == layout.Width && targetHeight == layout.Height)
            {
                return composed;
            }

            var resized = Resize(composed, targetWidth, targetHeight);
            Object.DestroyImmediate(composed);
            return resized;
        }

        private static Texture2D Composite(AtlasLayout layout)
        {
            var renderTexture = RenderTexture.GetTemporary(layout.Width, layout.Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;

            GL.Clear(true, true, new Color(0, 0, 0, 0));
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, layout.Width, 0, layout.Height);

            foreach (var placement in layout.Placements)
            {
                if (placement.SourceTexture?.Asset == null)
                {
                    continue;
                }
                var rect = placement.DestRect;
                Graphics.DrawTexture(new Rect(rect.x, rect.y, rect.width, rect.height), placement.SourceTexture.Asset);
            }

            GL.PopMatrix();

            var composed = new Texture2D(layout.Width, layout.Height, TextureFormat.RGBA32, false);
            composed.ReadPixels(new Rect(0, 0, layout.Width, layout.Height), 0, 0);
            composed.Apply();

            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
            return composed;
        }

        private static Texture2D Resize(Texture2D source, int targetWidth, int targetHeight)
        {
            var renderTexture = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var previousActive = RenderTexture.active;

            Graphics.Blit(source, renderTexture);
            RenderTexture.active = renderTexture;

            var resized = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
            resized.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            resized.Apply();

            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
            return resized;
        }
    }
}
