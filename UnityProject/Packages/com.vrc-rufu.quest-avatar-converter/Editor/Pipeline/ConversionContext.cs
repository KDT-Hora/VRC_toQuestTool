using System.Collections.Generic;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Materials;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    // T005: ConversionContext, ConversionSettings, ConversionLogEntry, per data-model.md's "Core
    // Run-Scoped Entities" table. ConversionContext is the single object all pipeline stages read
    // from and write to (Constitution Principle IV: stages communicate only through this, never
    // through each other's internals).
    //
    // data-model.md's "Compatibility & Performance Entities" (CompatibilityFinding,
    // PhysBoneMetrics, PerformanceMetrics) are defined here too, since ConversionContext
    // references them directly — the logic that POPULATES them (QuestCompatibilityChecker,
    // PhysBoneValidator, PerformanceAnalyzer) is Phase 5 / User Story 3 scope (T039-T042), not
    // Phase 2, but the data shapes themselves are already fully specified and are needed for
    // ConversionContext to compile "exactly per data-model.md".

    public enum ConversionLogLevel
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>data-model.md "Core Run-Scoped Entities" → ConversionLogEntry (FR-019).</summary>
    public sealed class ConversionLogEntry
    {
        public ConversionLogLevel Level { get; }
        public string Message { get; }

        /// <summary>For "jump to object" style UX (source doc §13); nullable.</summary>
        public Object RelatedAsset { get; }

        public ConversionLogEntry(ConversionLogLevel level, string message, Object relatedAsset = null)
        {
            Level = level;
            Message = message;
            RelatedAsset = relatedAsset;
        }

        public static ConversionLogEntry Info(string message, Object relatedAsset = null) =>
            new ConversionLogEntry(ConversionLogLevel.Info, message, relatedAsset);

        public static ConversionLogEntry Warning(string message, Object relatedAsset = null) =>
            new ConversionLogEntry(ConversionLogLevel.Warning, message, relatedAsset);

        public static ConversionLogEntry Error(string message, Object relatedAsset = null) =>
            new ConversionLogEntry(ConversionLogLevel.Error, message, relatedAsset);
    }

    /// <summary>data-model.md "Core Run-Scoped Entities" → ConversionSettings: user-configured
    /// options from the Editor Window (US2).</summary>
    public sealed class ConversionSettings
    {
        /// <summary>FR-005.</summary>
        public ShaderConversionRuleSet TargetShaderRule { get; set; }

        /// <summary>FR-004.</summary>
        public Vector3 PlacementOffset { get; set; }

        /// <summary>Default 1024 (FR-009).</summary>
        public int MaxTextureSize { get; set; } = 1024;

        /// <summary>FR-018.</summary>
        public bool MergeTexturesEnabled { get; set; } = true;

        /// <summary>FR-018.</summary>
        public bool ResizeTexturesEnabled { get; set; } = true;

        /// <summary>FR-018.</summary>
        public bool AddAaoComponentEnabled { get; set; } = true;
    }

    /// <summary>data-model.md "Compatibility & Performance Entities" → CompatibilityFinding
    /// (FR-016: listed individually, not only aggregated).</summary>
    public sealed class CompatibilityFinding
    {
        public enum FindingSeverity
        {
            Warning,
            BlockingIfUnaddressed,
        }

        public enum RemovalDecision
        {
            /// <summary>FR-021 forbids any auto-remove; this MUST remain the default until the
            /// user explicitly chooses otherwise.</summary>
            Undecided,
            Remove,
            Keep,
        }

        public Object TargetObject { get; }
        public string Reason { get; }
        public FindingSeverity Severity { get; }
        public RemovalDecision UserDecision { get; set; } = RemovalDecision.Undecided;

        public CompatibilityFinding(Object targetObject, string reason, FindingSeverity severity)
        {
            TargetObject = targetObject;
            Reason = reason;
            Severity = severity;
        }
    }

    /// <summary>data-model.md "Compatibility & Performance Entities" → PhysBoneMetrics
    /// (specialization feeding into CompatibilityFinding, FR-016a).</summary>
    public sealed class PhysBoneMetrics
    {
        public enum PerformanceRank
        {
            Excellent,
            Good,
            Medium,
            Poor,
        }

        public int PhysBoneComponentCount { get; set; }
        public int PhysBoneColliderCount { get; set; }
        public int PhysBoneAffectedTransformCount { get; set; }

        /// <summary>4th metric in research.md §5's tier table.</summary>
        public int PhysBoneCollisionCheckCount { get; set; }

        /// <summary>Compared against the 256 hard cap (research.md §5), independently of rank.</summary>
        public int MaxAffectedTransformsOnAnySingleComponent { get; set; }

        /// <summary>Externalized data (Constitution III), not hard-coded; see
        /// contracts §2's PhysBoneThresholds (4 tiers + hard cap).</summary>
        public QuestCompatibilityRules ThresholdsSource { get; set; }

        /// <summary>The best tier whose thresholds are NOT exceeded by any of the 4 counts above.</summary>
        public PerformanceRank ResultingRank { get; set; }

        /// <summary>True if MaxAffectedTransformsOnAnySingleComponent exceeds the hard cap —
        /// reported as a distinct, higher-severity finding than a rank downgrade (FR-016a).</summary>
        public bool ExceedsHardCap { get; set; }
    }

    /// <summary>data-model.md "Compatibility & Performance Entities" → PerformanceMetrics (FR-015).</summary>
    public sealed class PerformanceMetrics
    {
        public int TriangleCount { get; set; }
        public int MaterialCount { get; set; }
        public int SkinnedMeshRendererCount { get; set; }
        public int BoneCount { get; set; }
        public int TextureCount { get; set; }
        public long EstimatedTextureMemoryBytes { get; set; }
    }

    /// <summary>
    /// data-model.md "Core Run-Scoped Entities" → ConversionContext: the single object all
    /// pipeline stages read from and write to (Constitution Principle IV).
    /// </summary>
    public sealed class ConversionContext
    {
        /// <summary>Read-only for the whole run (Constitution I).</summary>
        public PCAvatar SourceAvatar { get; }

        /// <summary>Populated once AvatarDuplicator (T014) runs.</summary>
        public QuestAvatar QuestAvatar { get; set; }

        /// <summary>`Assets/&lt;QuestConvertedRoot&gt;/&lt;AvatarName&gt;/` (FR-002).</summary>
        public string OutputRoot { get; }

        public ConversionSettings Settings { get; }

        /// <summary>FR-011/FR-014: one entry per distinct source Material, so sharing is enforced
        /// by construction (keyed by <see cref="PCMaterial"/>, whose equality is based on the
        /// wrapped Material asset reference).</summary>
        public Dictionary<PCMaterial, QuestMaterial> MaterialMap { get; } = new Dictionary<PCMaterial, QuestMaterial>();

        /// <summary>FR-007/FR-008: keyed per-Material *and* per classified type, since atlases are
        /// scoped per Material per texture-classification.</summary>
        public Dictionary<(PCMaterial Material, TextureClassification Classification), QuestTexture> TextureMap { get; } =
            new Dictionary<(PCMaterial, TextureClassification), QuestTexture>();

        /// <summary>FR-019; append-only during a run.</summary>
        public List<ConversionLogEntry> Log { get; } = new List<ConversionLogEntry>();

        /// <summary>FR-016/FR-016a/FR-021.</summary>
        public List<CompatibilityFinding> CompatibilityFindings { get; } = new List<CompatibilityFinding>();

        /// <summary>FR-015.</summary>
        public PerformanceMetrics PerformanceMetrics { get; set; } = new PerformanceMetrics();

        public ConversionContext(PCAvatar sourceAvatar, string outputRoot, ConversionSettings settings)
        {
            SourceAvatar = sourceAvatar;
            OutputRoot = outputRoot;
            Settings = settings;
        }
    }
}
