using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase.Validation.Performance;
using VRC.SDKBase.Validation.Performance.Stats;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    /// <summary>
    /// T041 (FR-016a): counts PhysBone components / PhysBone Colliders / PhysBone-affected
    /// transforms / PhysBone collision checks on the Quest avatar, compares them against
    /// <see cref="QuestCompatibilityRules.PhysBoneLimits"/> (externalized data, research.md §5),
    /// and reports the resulting Quest Performance Rank tier — plus surfaces the hard
    /// 256-affected-transform-per-component cap as a distinct, higher-severity signal
    /// (<see cref="PhysBoneMetrics.ExceedsHardCap"/>) from an ordinary rank downgrade.
    /// </summary>
    /// <remarks>
    /// The four avatar-wide aggregate counts come from VRChat SDK's own official
    /// <see cref="AvatarPerformance.CalculatePerformanceStats"/> (same source PerformanceAnalyzer,
    /// T039, uses) — accurate and already VRChat-semantics-aware. The ONE count that calculator
    /// does not provide is a *per-component* breakdown (needed for the hard cap, which applies to
    /// "a single PhysBone component," not the avatar total) — <see cref="CountAffectedTransforms"/>
    /// approximates it by walking each PhysBone's own transform hierarchy (its `rootTransform`,
    /// excluding `ignoreTransforms` subtrees), which covers PhysBone's common configurations but
    /// does not fully replicate every nuance of its internal chain-building (e.g. `multiChildType`
    /// branching behavior) — acceptable for a hard-cap warning, not represented as exact.
    /// </remarks>
    public static class PhysBoneValidator
    {
        public static PhysBoneMetrics Validate(GameObject questAvatarRoot, QuestCompatibilityRules thresholdsSource)
        {
            var metrics = new PhysBoneMetrics { ThresholdsSource = thresholdsSource };
            if (questAvatarRoot == null || thresholdsSource == null)
            {
                return metrics;
            }

            var stats = new AvatarPerformanceStats(true);
            AvatarPerformance.CalculatePerformanceStats(questAvatarRoot.name, questAvatarRoot, stats, true);

            var physBoneStats = stats.physBone;
            metrics.PhysBoneComponentCount = physBoneStats?.componentCount ?? 0;
            metrics.PhysBoneColliderCount = physBoneStats?.colliderCount ?? 0;
            metrics.PhysBoneAffectedTransformCount = physBoneStats?.transformCount ?? 0;
            metrics.PhysBoneCollisionCheckCount = physBoneStats?.collisionCheckCount ?? 0;

            var limits = thresholdsSource.PhysBoneLimits;
            metrics.MaxAffectedTransformsOnAnySingleComponent = ComputeMaxAffectedTransformsPerComponent(questAvatarRoot);
            metrics.ExceedsHardCap = ComputeExceedsHardCap(metrics.MaxAffectedTransformsOnAnySingleComponent, limits.MaxAffectedTransformsPerComponentHardCap);
            metrics.ResultingRank = EvaluateRank(metrics, limits);

            return metrics;
        }

        /// <summary>The best (strictest) tier whose thresholds are NOT exceeded by any of the 4
        /// counts, per data-model.md's PhysBoneMetrics.ResultingRank — falls back to Poor (the
        /// worst officially ranked tier) when even Poor's thresholds are exceeded, since there is
        /// no lower bucket to report. Internal + testable directly (T042) against hand-built
        /// fixtures, without needing real VRCPhysBone components/AvatarPerformance calls.</summary>
        internal static PhysBoneMetrics.PerformanceRank EvaluateRank(PhysBoneMetrics metrics, PhysBoneThresholds limits)
        {
            if (FitsTier(metrics, limits.Excellent))
            {
                return PhysBoneMetrics.PerformanceRank.Excellent;
            }
            if (FitsTier(metrics, limits.Good))
            {
                return PhysBoneMetrics.PerformanceRank.Good;
            }
            if (FitsTier(metrics, limits.Medium))
            {
                return PhysBoneMetrics.PerformanceRank.Medium;
            }
            return PhysBoneMetrics.PerformanceRank.Poor;
        }

        /// <summary>FR-016a: exceeding the hard per-component cap is reported as a distinct,
        /// higher-severity finding from an ordinary rank downgrade. Internal + testable (T042).</summary>
        internal static bool ComputeExceedsHardCap(int maxAffectedTransformsOnAnySingleComponent, int hardCap)
        {
            return maxAffectedTransformsOnAnySingleComponent > hardCap;
        }

        private static bool FitsTier(PhysBoneMetrics metrics, PhysBoneTierThresholds tier)
        {
            return metrics.PhysBoneComponentCount <= tier.MaxPhysBoneComponents
                && metrics.PhysBoneColliderCount <= tier.MaxPhysBoneColliders
                && metrics.PhysBoneAffectedTransformCount <= tier.MaxPhysBoneAffectedTransforms
                && metrics.PhysBoneCollisionCheckCount <= tier.MaxPhysBoneCollisionCheckCount;
        }

        private static int ComputeMaxAffectedTransformsPerComponent(GameObject root)
        {
            var max = 0;
            foreach (var physBone in root.GetComponentsInChildren<VRCPhysBone>(true))
            {
                max = Mathf.Max(max, CountAffectedTransforms(physBone));
            }
            return max;
        }

        private static int CountAffectedTransforms(VRCPhysBone physBone)
        {
            var chainRoot = physBone.rootTransform != null ? physBone.rootTransform : physBone.transform;

            var ignore = new HashSet<Transform>();
            if (physBone.ignoreTransforms != null)
            {
                foreach (var transform in physBone.ignoreTransforms)
                {
                    if (transform != null)
                    {
                        ignore.Add(transform);
                    }
                }
            }

            return CountDescendants(chainRoot, ignore);
        }

        private static int CountDescendants(Transform node, HashSet<Transform> ignore)
        {
            if (node == null || ignore.Contains(node))
            {
                return 0;
            }

            var count = 1;
            for (var i = 0; i < node.childCount; i++)
            {
                count += CountDescendants(node.GetChild(i), ignore);
            }
            return count;
        }
    }
}
