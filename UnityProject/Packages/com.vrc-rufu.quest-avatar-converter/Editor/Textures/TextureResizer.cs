using UnityEngine;

namespace VrcRufu.QuestAvatarConverter.Textures
{
    /// <summary>
    /// T023 (FR-009): pure resize-math logic (Constitution Principle VI). Input: a width/height
    /// plus the configured max size; output: the target width/height. The longest edge MUST NOT
    /// exceed the configured max (default 1024), aspect ratio MUST be preserved, and a texture
    /// already at or below the max MUST NOT be upscaled.
    /// </summary>
    public static class TextureResizer
    {
        public const int DefaultMaxSize = 1024;

        public static (int Width, int Height) ComputeTargetSize(int width, int height, int maxSize)
        {
            var longestEdge = Mathf.Max(width, height);
            if (longestEdge <= maxSize)
            {
                // Already at or below the max: never upscale.
                return (width, height);
            }

            var scale = maxSize / (float)longestEdge;
            var targetWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            var targetHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            return (targetWidth, targetHeight);
        }
    }
}
