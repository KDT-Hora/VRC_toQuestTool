using System;
using System.Collections.Generic;
using Anatawa12.AvatarOptimizer;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    // T006: Avatar/Renderer/Material/Texture domain types, per data-model.md's "Avatar & Renderer
    // Entities" / "Material & Shader Entities" / "Texture Entities" tables.

    /// <summary>
    /// The four texture-property routing categories used throughout the pipeline (FR-008): atlas
    /// scoping (one atlas per Material per classification), <c>ConversionContext.TextureMap</c>
    /// keying, <see cref="QuestTexture.Classification"/>, and
    /// <c>PropertyMapping.TargetClassification</c> (contracts/extension-data-contracts.md §1).
    /// </summary>
    public enum TextureClassification
    {
        Color,
        Normal,
        Mask,
        Emission,
    }

    /// <summary>
    /// What kind of value a <see cref="MaterialProperty"/> holds. data-model.md describes
    /// <c>MaterialProperty.Classification</c> as a union of "TextureClassification | Color |
    /// Scalar"; this enum is that union's discriminator. A non-texture "Color" property (e.g. a
    /// `_Color` tint) is a distinct concept from a texture classified as
    /// <see cref="TextureClassification.Color"/> (e.g. `_MainTex`) — both exist, so they are kept
    /// as separate enums rather than merged into one.
    /// </summary>
    public enum MaterialPropertyKind
    {
        Texture,
        Color,
        Scalar,
    }

    /// <summary>
    /// A single Texture/Color/Float property actually set on a source Material (data-model.md
    /// "Material & Shader Entities" → MaterialProperty). <see cref="TextureClassification"/> is
    /// populated only once a <c>PropertyMapping</c> resolves this property's routing (see
    /// ShaderPropertyMapper/TextureTypeClassifier, T016/T020) — it is null when the AssetResolver
    /// (T013) first captures this property from the source Material.
    /// </summary>
    public sealed class MaterialProperty
    {
        public string Name { get; }
        public MaterialPropertyKind Kind { get; }

        /// <summary>Set iff <see cref="Kind"/> is <see cref="MaterialPropertyKind.Texture"/>.</summary>
        public Texture TextureValue { get; }

        /// <summary>Meaningful only when <see cref="Kind"/> is Texture (Edge Case: non-default UV
        /// tiling detection, T025) — not used for UV remapping (out of scope for v1).</summary>
        public Vector2 UvScale { get; }
        public Vector2 UvOffset { get; }

        /// <summary>Set iff <see cref="Kind"/> is <see cref="MaterialPropertyKind.Color"/>.</summary>
        public Color ColorValue { get; }

        /// <summary>Set iff <see cref="Kind"/> is <see cref="MaterialPropertyKind.Scalar"/>.</summary>
        public float ScalarValue { get; }

        /// <summary>Resolved routing for a Texture-kind property; null until a PropertyMapping
        /// assigns it (see remarks above). Not applicable to Color/Scalar-kind properties.</summary>
        public TextureClassification? TextureClassification { get; set; }

        public static MaterialProperty ForTexture(string name, Texture textureValue, Vector2 uvScale, Vector2 uvOffset)
        {
            return new MaterialProperty(name, MaterialPropertyKind.Texture, textureValue, uvScale, uvOffset, default, default);
        }

        public static MaterialProperty ForColor(string name, Color colorValue)
        {
            return new MaterialProperty(name, MaterialPropertyKind.Color, null, default, default, colorValue, default);
        }

        public static MaterialProperty ForScalar(string name, float scalarValue)
        {
            return new MaterialProperty(name, MaterialPropertyKind.Scalar, null, default, default, default, scalarValue);
        }

        private MaterialProperty(string name, MaterialPropertyKind kind, Texture textureValue, Vector2 uvScale, Vector2 uvOffset, Color colorValue, float scalarValue)
        {
            Name = name;
            Kind = kind;
            TextureValue = textureValue;
            UvScale = uvScale;
            UvOffset = uvOffset;
            ColorValue = colorValue;
            ScalarValue = scalarValue;
        }
    }

    /// <summary>
    /// Wraps a source-avatar Material asset (data-model.md "Material & Shader Entities" →
    /// PCMaterial). Equality is based on the wrapped Unity asset reference rather than this
    /// wrapper instance, so <c>ConversionContext.MaterialMap</c> lookups behave correctly
    /// regardless of how many PCMaterial instances get constructed for the same underlying
    /// Material (Constitution Principle IV; FR-011 sharing-by-construction).
    /// </summary>
    public sealed class PCMaterial : IEquatable<PCMaterial>
    {
        /// <summary>Never written to (Constitution I: Source Immutability).</summary>
        public Material Asset { get; }
        public Shader Shader { get; }
        public IReadOnlyList<MaterialProperty> Properties { get; }

        public PCMaterial(Material asset, IReadOnlyList<MaterialProperty> properties)
        {
            Asset = asset;
            Shader = asset != null ? asset.shader : null;
            Properties = properties ?? Array.Empty<MaterialProperty>();
        }

        public bool Equals(PCMaterial other) => other != null && ReferenceEquals(Asset, other.Asset);
        public override bool Equals(object obj) => Equals(obj as PCMaterial);
        public override int GetHashCode() => Asset != null ? Asset.GetInstanceID() : 0;
    }

    /// <summary>data-model.md "Material & Shader Entities" → QuestMaterial.</summary>
    public sealed class QuestMaterial
    {
        /// <summary>Newly created under OutputRoot/Materials; null until T018 (MaterialConverter)
        /// creates the asset.</summary>
        public Material Asset { get; set; }

        public Shader Shader => Asset != null ? Asset.shader : null;

        /// <summary>For MaterialMap traceability (FR-014).</summary>
        public PCMaterial SourcePCMaterial { get; }

        /// <summary>Always true post-generation (FR-020a); set by T019 (GpuInstancingApplier).</summary>
        public bool GpuInstancingEnabled { get; set; }

        public QuestMaterial(PCMaterial sourcePCMaterial)
        {
            SourcePCMaterial = sourcePCMaterial;
        }
    }

    /// <summary>
    /// Wraps a source-avatar Texture2D asset (data-model.md "Texture Entities" → PCTexture).
    /// Equality is based on the wrapped Unity asset reference, mirroring <see cref="PCMaterial"/>.
    /// </summary>
    public sealed class PCTexture : IEquatable<PCTexture>
    {
        /// <summary>Never written to (Constitution I: Source Immutability).</summary>
        public Texture2D Asset { get; }
        public int Width { get; }
        public int Height { get; }

        /// <summary>Drives FR-009's alpha-preservation rule.</summary>
        public bool HasAlpha { get; }

        public PCTexture(Texture2D asset, int width, int height, bool hasAlpha)
        {
            Asset = asset;
            Width = width;
            Height = height;
            HasAlpha = hasAlpha;
        }

        public bool Equals(PCTexture other) => other != null && ReferenceEquals(Asset, other.Asset);
        public override bool Equals(object obj) => Equals(obj as PCTexture);
        public override int GetHashCode() => Asset != null ? Asset.GetInstanceID() : 0;
    }

    /// <summary>
    /// One source texture's aspect-ratio-preserving placement within a merged atlas image
    /// (data-model.md "Texture Entities" → AtlasPlacement, FR-007).
    /// </summary>
    public readonly struct AtlasPlacement
    {
        public PCTexture SourceTexture { get; }
        public RectInt DestRect { get; }

        public AtlasPlacement(PCTexture sourceTexture, RectInt destRect)
        {
            SourceTexture = sourceTexture;
            DestRect = destRect;
        }
    }

    /// <summary>
    /// data-model.md "Texture Entities" → AtlasLayout. <see cref="Width"/>/<see cref="Height"/>
    /// (the overall merged-canvas size) are not listed as separate data-model fields but are
    /// required by <c>TextureAssetWriter</c> (T026) to actually render the atlas, so they are
    /// added here alongside <see cref="Placements"/>.
    /// </summary>
    public sealed class AtlasLayout
    {
        public IReadOnlyList<AtlasPlacement> Placements { get; }
        public int Width { get; }
        public int Height { get; }

        public AtlasLayout(IReadOnlyList<AtlasPlacement> placements, int width, int height)
        {
            Placements = placements ?? Array.Empty<AtlasPlacement>();
            Width = width;
            Height = height;
        }
    }

    /// <summary>data-model.md "Texture Entities" → QuestTexture.</summary>
    public sealed class QuestTexture
    {
        /// <summary>Newly created under OutputRoot/Textures, always lossless PNG (FR-009); null
        /// until T026 (TextureAssetWriter) creates the asset.</summary>
        public Texture2D Asset { get; set; }

        /// <summary>1 entry if not merged, N entries if this is an atlas (FR-007).</summary>
        public IReadOnlyList<PCTexture> SourcePCTextures { get; }

        /// <summary>One QuestTexture per (Material, Classification) pair (FR-008).</summary>
        public TextureClassification Classification { get; }

        /// <summary>Present only when <see cref="SourcePCTextures"/>.Count > 1.</summary>
        public AtlasLayout Layout { get; set; }

        /// <summary>&lt;= ConversionSettings.MaxTextureSize on the longest edge (FR-009).</summary>
        public (int Width, int Height) FinalSize { get; set; }

        public QuestTexture(IReadOnlyList<PCTexture> sourcePCTextures, TextureClassification classification)
        {
            SourcePCTextures = sourcePCTextures ?? Array.Empty<PCTexture>();
            Classification = classification;
        }
    }

    /// <summary>
    /// One Renderer's static <c>sharedMaterials</c> snapshot (data-model.md "Avatar & Renderer
    /// Entities" → RendererRef). Reused for both <see cref="PCAvatar.Renderers"/> (where
    /// <see cref="RendererComponent"/> is the PC-side Renderer) and
    /// <see cref="QuestAvatar.Renderers"/> (where it is the duplicated Quest-side Renderer,
    /// matched to its PC counterpart via <see cref="TransformPath"/> — data-model.md: "Mirrors
    /// PCAvatar.Renderers 1:1").
    /// </summary>
    public sealed class RendererRef
    {
        /// <summary>Used to match a PC renderer to its Quest counterpart after duplication.</summary>
        public string TransformPath { get; }

        public Renderer RendererComponent { get; }

        /// <summary>Renderer's `sharedMaterials` at conversion time (FR-003).</summary>
        public IReadOnlyList<PCMaterial> SourceMaterials { get; }

        public RendererRef(string transformPath, Renderer rendererComponent, IReadOnlyList<PCMaterial> sourceMaterials)
        {
            TransformPath = transformPath;
            RendererComponent = rendererComponent;
            SourceMaterials = sourceMaterials ?? Array.Empty<PCMaterial>();
        }
    }

    /// <summary>data-model.md "Avatar & Renderer Entities" → PCAvatar.</summary>
    public sealed class PCAvatar
    {
        /// <summary>Never written to (Constitution I: Source Immutability).</summary>
        public GameObject RootPrefab { get; }

        /// <summary>SkinnedMeshRenderer/MeshRenderer discovered via static traversal (FR-003);
        /// populated by AssetResolver (T013).</summary>
        public List<RendererRef> Renderers { get; } = new List<RendererRef>();

        /// <summary>Followed for avatar-level config, not treated as a "runtime asset swap"
        /// source (FR-003).</summary>
        public VRCAvatarDescriptor VrcAvatarDescriptor { get; }

        public PCAvatar(GameObject rootPrefab, VRCAvatarDescriptor vrcAvatarDescriptor)
        {
            RootPrefab = rootPrefab;
            VrcAvatarDescriptor = vrcAvatarDescriptor;
        }
    }

    /// <summary>data-model.md "Avatar & Renderer Entities" → QuestAvatar.</summary>
    public sealed class QuestAvatar
    {
        /// <summary>Independent copy, never a Prefab Variant (FR-023); set by AvatarDuplicator (T014).</summary>
        public GameObject RootPrefab { get; set; }

        /// <summary>Mirrors PCAvatar.Renderers 1:1; each renderer's material list is replaced in
        /// place by RendererMaterialReplacer (T027, FR-010).</summary>
        public List<RendererRef> Renderers { get; } = new List<RendererRef>();

        /// <summary>Added to root by AAOIntegrator (T028, FR-012).</summary>
        public TraceAndOptimize AaoTraceAndOptimize { get; set; }
    }
}
