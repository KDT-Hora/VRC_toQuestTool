using UnityEngine;
using VRC.SDKBase.Validation.Performance;
using VRC.SDKBase.Validation.Performance.Stats;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    /// <summary>
    /// T039 (FR-015): computes performance-relevant metrics for the generated Quest avatar.
    /// Delegates the actual measurement to VRChat SDK's own official
    /// <see cref="AvatarPerformance.CalculatePerformanceStats"/> — the same calculator the VRChat
    /// SDK Control Panel itself uses — rather than re-deriving triangle/bone/Material counts by
    /// hand, since that's both more accurate (it already knows every VRChat-specific counting
    /// rule) and avoids duplicating logic VRChat maintains for us.
    /// </summary>
    public static class PerformanceAnalyzer
    {
        public static PerformanceMetrics Analyze(QuestAvatar questAvatar)
        {
            var metrics = new PerformanceMetrics();
            if (questAvatar?.RootPrefab == null)
            {
                return metrics;
            }

            var stats = new AvatarPerformanceStats(true);
            AvatarPerformance.CalculatePerformanceStats(questAvatar.RootPrefab.name, questAvatar.RootPrefab, stats, true);

            metrics.TriangleCount = stats.polyCount ?? 0;
            metrics.MaterialCount = stats.materialCount ?? 0;
            metrics.SkinnedMeshRendererCount = stats.skinnedMeshCount ?? 0;
            metrics.BoneCount = stats.boneCount ?? 0;
            metrics.TextureCount = CountDistinctTextures(questAvatar.RootPrefab);
            metrics.EstimatedTextureMemoryBytes = (long)((stats.textureMegabytes ?? 0) * 1024L * 1024L);

            return metrics;
        }

        private static int CountDistinctTextures(GameObject avatarRoot)
        {
            var textures = new System.Collections.Generic.HashSet<Texture>();
            foreach (var renderer in avatarRoot.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null)
                    {
                        continue;
                    }

                    var shader = material.shader;
                    var propertyCount = shader.GetPropertyCount();
                    for (var i = 0; i < propertyCount; i++)
                    {
                        if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture)
                        {
                            continue;
                        }
                        var texture = material.GetTexture(shader.GetPropertyName(i));
                        if (texture != null)
                        {
                            textures.Add(texture);
                        }
                    }
                }
            }
            return textures.Count;
        }
    }
}
