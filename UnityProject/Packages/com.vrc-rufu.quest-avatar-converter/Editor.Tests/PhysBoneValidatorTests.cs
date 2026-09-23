using NUnit.Framework;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter.Tests
{
    // T042: EditMode tests for PhysBoneValidator's threshold-evaluation logic (T041), against
    // research.md §5's published Quest PhysBone tier table:
    //   Metric \ Tier            Excellent  Good  Medium  Poor
    //   PhysBone Components         0        4      6      8
    //   PhysBone Affected Transforms 0       16     32     64
    //   PhysBone Colliders           0        4      8     16
    //   PhysBone Collision Checks    0       16     32     64
    // plus the 256-affected-transform-per-component hard cap.
    public class PhysBoneValidatorTests
    {
        private static readonly PhysBoneThresholds Limits = new PhysBoneThresholds
        {
            Excellent = new PhysBoneTierThresholds { MaxPhysBoneComponents = 0, MaxPhysBoneColliders = 0, MaxPhysBoneAffectedTransforms = 0, MaxPhysBoneCollisionCheckCount = 0 },
            Good = new PhysBoneTierThresholds { MaxPhysBoneComponents = 4, MaxPhysBoneColliders = 4, MaxPhysBoneAffectedTransforms = 16, MaxPhysBoneCollisionCheckCount = 16 },
            Medium = new PhysBoneTierThresholds { MaxPhysBoneComponents = 6, MaxPhysBoneColliders = 8, MaxPhysBoneAffectedTransforms = 32, MaxPhysBoneCollisionCheckCount = 32 },
            Poor = new PhysBoneTierThresholds { MaxPhysBoneComponents = 8, MaxPhysBoneColliders = 16, MaxPhysBoneAffectedTransforms = 64, MaxPhysBoneCollisionCheckCount = 64 },
            MaxAffectedTransformsPerComponentHardCap = 256,
            SourceCitation = "research.md §5 (test fixture)",
        };

        private static PhysBoneMetrics Metrics(int components, int colliders, int affectedTransforms, int collisionChecks) => new PhysBoneMetrics
        {
            PhysBoneComponentCount = components,
            PhysBoneColliderCount = colliders,
            PhysBoneAffectedTransformCount = affectedTransforms,
            PhysBoneCollisionCheckCount = collisionChecks,
        };

        [Test]
        public void AllZero_IsExcellent()
        {
            var rank = PhysBoneValidator.EvaluateRank(Metrics(0, 0, 0, 0), Limits);
            Assert.AreEqual(PhysBoneMetrics.PerformanceRank.Excellent, rank);
        }

        [Test]
        public void ExactlyAtGoodBoundary_IsGood()
        {
            var rank = PhysBoneValidator.EvaluateRank(Metrics(4, 4, 16, 16), Limits);
            Assert.AreEqual(PhysBoneMetrics.PerformanceRank.Good, rank);
        }

        [Test]
        public void OneOverGoodBoundary_IsMedium()
        {
            // Exceeding Good on just one metric (components) is enough to drop out of Good.
            var rank = PhysBoneValidator.EvaluateRank(Metrics(5, 4, 16, 16), Limits);
            Assert.AreEqual(PhysBoneMetrics.PerformanceRank.Medium, rank);
        }

        [Test]
        public void ExactlyAtMediumBoundary_IsMedium()
        {
            var rank = PhysBoneValidator.EvaluateRank(Metrics(6, 8, 32, 32), Limits);
            Assert.AreEqual(PhysBoneMetrics.PerformanceRank.Medium, rank);
        }

        [Test]
        public void ExactlyAtPoorBoundary_IsPoor()
        {
            var rank = PhysBoneValidator.EvaluateRank(Metrics(8, 16, 64, 64), Limits);
            Assert.AreEqual(PhysBoneMetrics.PerformanceRank.Poor, rank);
        }

        [Test]
        public void BeyondPoorBoundary_StillReportsPoor()
        {
            // No lower bucket than Poor exists to report.
            var rank = PhysBoneValidator.EvaluateRank(Metrics(20, 30, 200, 200), Limits);
            Assert.AreEqual(PhysBoneMetrics.PerformanceRank.Poor, rank);
        }

        [Test]
        public void SingleMetricExceedingATier_IsEnoughToDropOut()
        {
            // Only the collision-check count exceeds Medium; the other three still fit Excellent.
            var rank = PhysBoneValidator.EvaluateRank(Metrics(0, 0, 0, 33), Limits);
            Assert.AreEqual(PhysBoneMetrics.PerformanceRank.Poor, rank);
        }

        [Test]
        public void MaxAffectedTransformsAtHardCap_DoesNotExceed()
        {
            Assert.IsFalse(PhysBoneValidator.ComputeExceedsHardCap(256, 256));
        }

        [Test]
        public void MaxAffectedTransformsOverHardCap_Exceeds()
        {
            Assert.IsTrue(PhysBoneValidator.ComputeExceedsHardCap(257, 256));
        }

        [Test]
        public void MaxAffectedTransformsUnderHardCap_DoesNotExceed()
        {
            Assert.IsFalse(PhysBoneValidator.ComputeExceedsHardCap(64, 256));
        }
    }
}
