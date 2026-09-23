using System.Collections.Generic;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    /// <summary>
    /// T043 (FR-019): runs the post-generation analysis stages (PerformanceAnalyzer T039,
    /// QuestCompatibilityChecker T040, PhysBoneValidator T041) against the already-generated Quest
    /// avatar, writes their results back into <see cref="ConversionContext"/>'s canonical fields
    /// (PerformanceMetrics, CompatibilityFindings — data-model.md's "Core Run-Scoped Entities"),
    /// and returns a single aggregated, displayable report — sufficient to understand the outcome
    /// without manually inspecting each generated asset.
    /// </summary>
    public static class ConversionReport
    {
        public sealed class Report
        {
            public IReadOnlyList<ConversionLogEntry> Log { get; }
            public PerformanceMetrics PerformanceMetrics { get; }

            /// <summary>Null if no QuestCompatibilityRules asset was available to validate
            /// against.</summary>
            public PhysBoneMetrics PhysBoneMetrics { get; }

            public IReadOnlyList<CompatibilityFinding> CompatibilityFindings { get; }

            public Report(
                IReadOnlyList<ConversionLogEntry> log,
                PerformanceMetrics performanceMetrics,
                PhysBoneMetrics physBoneMetrics,
                IReadOnlyList<CompatibilityFinding> compatibilityFindings)
            {
                Log = log;
                PerformanceMetrics = performanceMetrics;
                PhysBoneMetrics = physBoneMetrics;
                CompatibilityFindings = compatibilityFindings;
            }
        }

        public static Report Analyze(ConversionContext context, IReadOnlyList<QuestCompatibilityRules> loadedCompatibilityRules)
        {
            if (context.QuestAvatar?.RootPrefab == null)
            {
                return new Report(context.Log, context.PerformanceMetrics, null, context.CompatibilityFindings);
            }

            context.PerformanceMetrics = PerformanceAnalyzer.Analyze(context.QuestAvatar);

            var findings = QuestCompatibilityChecker.Check(context.QuestAvatar.RootPrefab, loadedCompatibilityRules);

            PhysBoneMetrics physBoneMetrics = null;
            var primaryRules = loadedCompatibilityRules != null && loadedCompatibilityRules.Count > 0 ? loadedCompatibilityRules[0] : null;
            if (primaryRules != null)
            {
                physBoneMetrics = PhysBoneValidator.Validate(context.QuestAvatar.RootPrefab, primaryRules);

                // FR-016a: the hard-cap violation is surfaced as its own, higher-severity
                // CompatibilityFinding — distinct from an ordinary rank downgrade — so it goes
                // through the same individually-listed, per-item Remove/Keep review as any other
                // flagged object (FR-021), not just a number in the report.
                if (physBoneMetrics.ExceedsHardCap)
                {
                    findings.Add(new CompatibilityFinding(
                        context.QuestAvatar.RootPrefab,
                        $"A PhysBone component affects {physBoneMetrics.MaxAffectedTransformsOnAnySingleComponent} transforms, " +
                        $"exceeding VRChat's hard cap of {primaryRules.PhysBoneLimits.MaxAffectedTransformsPerComponentHardCap} " +
                        "per component — VRChat strips this component category at runtime regardless of performance rank (FR-016a).",
                        CompatibilityFinding.FindingSeverity.BlockingIfUnaddressed));
                }
            }

            context.CompatibilityFindings.Clear();
            context.CompatibilityFindings.AddRange(findings);

            return new Report(context.Log, context.PerformanceMetrics, physBoneMetrics, context.CompatibilityFindings);
        }
    }
}
