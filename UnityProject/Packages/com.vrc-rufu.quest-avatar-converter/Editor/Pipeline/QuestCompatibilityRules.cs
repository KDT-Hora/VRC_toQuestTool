using System;
using System.Collections.Generic;
using UnityEngine;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    // T010: QuestCompatibilityRules ScriptableObject schema, enforcing
    // contracts/extension-data-contracts.md §2 invariants verbatim (Constitution Principle III:
    // externalized, rule-based conversion data). Consumed by QuestCompatibilityChecker (FR-016)
    // and PhysBoneValidator (FR-016a) — see Phase 5 (T040/T041).

    /// <summary>contracts §2 — FlaggedComponentRule: a generic unsupported/restricted component.</summary>
    [Serializable]
    public struct FlaggedComponentRule
    {
        /// <summary>REQUIRED. Fully-qualified type name, matched via reflection.</summary>
        public string ComponentTypeName;

        /// <summary>REQUIRED. Shown to the user per flagged object (FR-016).</summary>
        public string ReasonMessage;
    }

    /// <summary>contracts §2 — PhysBoneTierThresholds: one VRChat Quest Performance Rank tier's
    /// quantitative PhysBone limits.</summary>
    [Serializable]
    public struct PhysBoneTierThresholds
    {
        public int MaxPhysBoneComponents;
        public int MaxPhysBoneColliders;
        public int MaxPhysBoneAffectedTransforms;
        public int MaxPhysBoneCollisionCheckCount;
    }

    /// <summary>contracts §2 — PhysBoneThresholds: all four Quest Performance Rank tiers plus the
    /// hard per-component cap, since PhysBoneValidator (FR-016a) reports WHICH tier an avatar
    /// falls into, not just a single pass/fail line.</summary>
    [Serializable]
    public struct PhysBoneThresholds
    {
        public PhysBoneTierThresholds Excellent;
        public PhysBoneTierThresholds Good;
        public PhysBoneTierThresholds Medium;
        public PhysBoneTierThresholds Poor;

        /// <summary>REQUIRED. VRChat's hard per-component cap (research.md §5: 256) — exceeding
        /// this strips the component category at runtime regardless of rank, so it MUST be
        /// surfaced as a distinct, higher-severity finding from an ordinary rank downgrade
        /// (FR-016a).</summary>
        public int MaxAffectedTransformsPerComponentHardCap;

        /// <summary>REQUIRED, non-empty. Where these numbers came from (doc/version), for
        /// traceability (Constitution III).</summary>
        public string SourceCitation;
    }

    /// <summary>
    /// Two rule kinds sharing one contract file so both stay swappable as VRChat's published
    /// limits change (contracts §2): generic flagged components (FR-016) and PhysBone-specific
    /// quantitative thresholds (FR-016a). Updating VRChat's published numbers means editing one
    /// asset's values — no change to QuestCompatibilityChecker/PhysBoneValidator is permitted for
    /// that purpose (Constitution III).
    /// </summary>
    public sealed class QuestCompatibilityRules : ScriptableObject
    {
        [SerializeField]
        private FlaggedComponentRule[] flaggedComponents = Array.Empty<FlaggedComponentRule>();

        [SerializeField]
        private PhysBoneThresholds physBoneLimits;

        public IReadOnlyList<FlaggedComponentRule> FlaggedComponents => flaggedComponents;
        public PhysBoneThresholds PhysBoneLimits => physBoneLimits;

        /// <summary>Test-only construction helper (T011). Production callers author these assets
        /// via the Inspector; EditMode tests use this instead of SerializedObject/AssetDatabase
        /// gymnastics to build fixtures. Internal + <c>InternalsVisibleTo</c> the Editor.Tests
        /// assembly (see AssemblyInfo.cs).</summary>
        internal static QuestCompatibilityRules CreateForTests(FlaggedComponentRule[] flaggedComponents, PhysBoneThresholds physBoneLimits)
        {
            var instance = CreateInstance<QuestCompatibilityRules>();
            instance.flaggedComponents = flaggedComponents ?? Array.Empty<FlaggedComponentRule>();
            instance.physBoneLimits = physBoneLimits;
            return instance;
        }
    }

    /// <summary>The outcome of loading a set of <see cref="QuestCompatibilityRules"/> assets:
    /// which ones passed every contract invariant, plus every load-time error/warning produced
    /// along the way (contracts §2: "not silently skipped").</summary>
    public sealed class QuestCompatibilityRulesLoadResult
    {
        public IReadOnlyList<QuestCompatibilityRules> ValidRules { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }

        public QuestCompatibilityRulesLoadResult(
            IReadOnlyList<QuestCompatibilityRules> validRules,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings)
        {
            ValidRules = validRules ?? Array.Empty<QuestCompatibilityRules>();
            Errors = errors ?? Array.Empty<string>();
            Warnings = warnings ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Loads and validates a set of <see cref="QuestCompatibilityRules"/> assets against
    /// contracts/extension-data-contracts.md §2's invariants. Pure logic over the passed-in
    /// candidates (Constitution Principle VI).
    /// </summary>
    public static class QuestCompatibilityRulesLoader
    {
        public static QuestCompatibilityRulesLoadResult Load(IEnumerable<QuestCompatibilityRules> candidates)
        {
            var valid = new List<QuestCompatibilityRules>();
            var errors = new List<string>();
            var warnings = new List<string>();

            foreach (var rules in candidates ?? Array.Empty<QuestCompatibilityRules>())
            {
                if (rules == null)
                {
                    continue;
                }

                var label = string.IsNullOrEmpty(rules.name) ? "(unnamed QuestCompatibilityRules)" : rules.name;
                var rejected = false;

                foreach (var componentRule in rules.FlaggedComponents)
                {
                    if (!ResolvesToComponentType(componentRule.ComponentTypeName))
                    {
                        warnings.Add(
                            $"{label}: FlaggedComponentRule.ComponentTypeName '{componentRule.ComponentTypeName}' " +
                            "did not resolve to a real Component-derived type (contracts/extension-data-contracts.md §2).");
                    }
                }

                if (string.IsNullOrEmpty(rules.PhysBoneLimits.SourceCitation))
                {
                    errors.Add(
                        $"{label}: PhysBoneThresholds.SourceCitation is required and must be non-empty — " +
                        "treated as a rule-authoring error (contracts/extension-data-contracts.md §2).");
                    rejected = true;
                }

                if (!rejected)
                {
                    valid.Add(rules);
                }
            }

            return new QuestCompatibilityRulesLoadResult(valid, errors, warnings);
        }

        private static bool ResolvesToComponentType(string componentTypeName)
        {
            if (string.IsNullOrEmpty(componentTypeName))
            {
                return false;
            }

            var type = Type.GetType(componentTypeName);
            if (type == null)
            {
                // A bare/partial type name (no assembly qualification) won't resolve via
                // Type.GetType alone — fall back to scanning loaded assemblies.
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(componentTypeName);
                    if (type != null)
                    {
                        break;
                    }
                }
            }

            return type != null && typeof(Component).IsAssignableFrom(type);
        }
    }
}
