using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    /// <summary>
    /// T013: builds a <see cref="PCAvatar"/> by traversing only each Renderer's static
    /// `sharedMaterial`(s) → Material → Texture at conversion time (FR-003). Animator/Animation-
    /// Clip-driven material/texture swaps are intentionally NOT discovered (out of scope per
    /// spec Assumptions) — only the Renderer's current `sharedMaterials` snapshot is read. The
    /// VRC Avatar Descriptor reference IS followed (captured on the returned PCAvatar) but does
    /// not itself widen which Renderers are discovered.
    /// </summary>
    public static class AssetResolver
    {
        /// <summary>Never modifies <paramref name="avatarRoot"/> or anything it references
        /// (Constitution I) — this is a pure read/traversal.</summary>
        public static PCAvatar Resolve(GameObject avatarRoot)
        {
            var descriptor = avatarRoot.GetComponent<VRCAvatarDescriptor>();
            var pcAvatar = new PCAvatar(avatarRoot, descriptor);

            // Reuse one PCMaterial instance per distinct source Material so that Renderers
            // sharing a Material end up sharing one PCMaterial instance (mirrors FR-011's
            // sharing-by-construction at the MaterialMap level, one step earlier in the pipeline).
            var materialCache = new Dictionary<Material, PCMaterial>();

            var renderers = avatarRoot.GetComponentsInChildren<Renderer>(true)
                .Where(r => r is SkinnedMeshRenderer || r is MeshRenderer);

            foreach (var renderer in renderers)
            {
                var transformPath = GetTransformPath(avatarRoot.transform, renderer.transform);
                var sourceMaterials = new List<PCMaterial>();

                foreach (var material in renderer.sharedMaterials)
                {
                    // An empty Renderer material slot is a real, if unusual, source state — kept
                    // as null to preserve index alignment with renderer.sharedMaterials, which
                    // RendererMaterialReplacer (T027) needs when writing the Quest side back.
                    if (material == null)
                    {
                        sourceMaterials.Add(null);
                        continue;
                    }

                    if (!materialCache.TryGetValue(material, out var pcMaterial))
                    {
                        pcMaterial = BuildPCMaterial(material);
                        materialCache[material] = pcMaterial;
                    }

                    sourceMaterials.Add(pcMaterial);
                }

                pcAvatar.Renderers.Add(new RendererRef(transformPath, renderer, sourceMaterials));
            }

            return pcAvatar;
        }

        /// <summary>Public entry point for building a standalone <see cref="PCMaterial"/> from a
        /// single Material asset outside of a full avatar traversal — used by the Preview panel
        /// (T045), which previews one Material at a time without resolving a whole avatar.</summary>
        public static PCMaterial BuildPCMaterialFromAsset(Material material) => BuildPCMaterial(material);

        private static PCMaterial BuildPCMaterial(Material material)
        {
            var properties = new List<MaterialProperty>();
            var shader = material.shader;
            var propertyCount = shader.GetPropertyCount();

            for (var i = 0; i < propertyCount; i++)
            {
                var propertyName = shader.GetPropertyName(i);
                switch (shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        properties.Add(MaterialProperty.ForTexture(
                            propertyName,
                            material.GetTexture(propertyName),
                            material.GetTextureScale(propertyName),
                            material.GetTextureOffset(propertyName)));
                        break;

                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        properties.Add(MaterialProperty.ForColor(propertyName, material.GetColor(propertyName)));
                        break;

                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        properties.Add(MaterialProperty.ForScalar(propertyName, material.GetFloat(propertyName)));
                        break;

                    // Vector/Int shader properties aren't modeled by MaterialProperty's
                    // Texture/Color/Scalar kinds (data-model.md) — not needed by any FR (shader
                    // conversion works off Texture/Color/Scalar-classified properties only).
                    default:
                        break;
                }
            }

            return new PCMaterial(material, properties);
        }

        /// <summary>Slash-separated path from <paramref name="root"/> to <paramref name="target"/>,
        /// empty string if they're the same Transform. Used to re-locate the matching Quest-side
        /// Renderer after duplication (RendererRef.TransformPath).</summary>
        private static string GetTransformPath(Transform root, Transform target)
        {
            if (target == root)
            {
                return string.Empty;
            }

            var segments = new List<string>();
            for (var current = target; current != null && current != root; current = current.parent)
            {
                segments.Add(current.name);
            }
            segments.Reverse();
            return string.Join("/", segments);
        }
    }
}
