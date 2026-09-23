using System.Collections.Generic;
using NUnit.Framework;
using VrcRufu.QuestAvatarConverter.Pipeline;
using Object = UnityEngine.Object;

namespace VrcRufu.QuestAvatarConverter.Tests
{
    // T011: EditMode tests for the T010 loader invariants
    // (contracts/extension-data-contracts.md §2).
    public class QuestCompatibilityRulesLoaderTests
    {
        private readonly List<Object> _createdInstances = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var instance in _createdInstances)
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }
            }
            _createdInstances.Clear();
        }

        private static PhysBoneThresholds ValidThresholds(string sourceCitation = "VRChat Creation docs, Performance Ranks page (fixture)")
        {
            return new PhysBoneThresholds
            {
                Excellent = default,
                Good = default,
                Medium = default,
                Poor = default,
                MaxAffectedTransformsPerComponentHardCap = 256,
                SourceCitation = sourceCitation,
            };
        }

        private QuestCompatibilityRules CreateRules(FlaggedComponentRule[] flaggedComponents, PhysBoneThresholds thresholds)
        {
            var rules = QuestCompatibilityRules.CreateForTests(flaggedComponents, thresholds);
            _createdInstances.Add(rules);
            return rules;
        }

        [Test]
        public void ValidRules_AreAccepted_WithNoErrorsOrWarnings()
        {
            var rules = CreateRules(
                new[]
                {
                    new FlaggedComponentRule { ComponentTypeName = "UnityEngine.Camera", ReasonMessage = "Camera component unsupported on Quest" },
                },
                ValidThresholds());

            var result = QuestCompatibilityRulesLoader.Load(new[] { rules });

            Assert.AreEqual(1, result.ValidRules.Count);
            Assert.AreSame(rules, result.ValidRules[0]);
            Assert.IsEmpty(result.Errors);
            Assert.IsEmpty(result.Warnings);
        }

        [Test]
        public void UnresolvedComponentTypeName_ProducesWarning_ButRuleStillLoads()
        {
            var rules = CreateRules(
                new[]
                {
                    new FlaggedComponentRule { ComponentTypeName = "Not.A.Real.ComponentType", ReasonMessage = "n/a" },
                },
                ValidThresholds());

            var result = QuestCompatibilityRulesLoader.Load(new[] { rules });

            Assert.AreEqual(1, result.ValidRules.Count, "An unresolved ComponentTypeName is a warning, not a load failure.");
            Assert.IsEmpty(result.Errors);
            Assert.IsNotEmpty(result.Warnings);
            StringAssert.Contains("Not.A.Real.ComponentType", result.Warnings[0]);
        }

        [Test]
        public void MissingSourceCitation_IsTreatedAsRuleAuthoringError()
        {
            var rules = CreateRules(System.Array.Empty<FlaggedComponentRule>(), ValidThresholds(sourceCitation: ""));

            var result = QuestCompatibilityRulesLoader.Load(new[] { rules });

            Assert.IsEmpty(result.ValidRules, "PhysBoneThresholds.SourceCitation must be non-empty (contracts §2).");
            Assert.IsNotEmpty(result.Errors);
            StringAssert.Contains("SourceCitation", result.Errors[0]);
        }

        [Test]
        public void NullSourceCitation_IsTreatedAsRuleAuthoringError()
        {
            var rules = CreateRules(System.Array.Empty<FlaggedComponentRule>(), ValidThresholds(sourceCitation: null));

            var result = QuestCompatibilityRulesLoader.Load(new[] { rules });

            Assert.IsEmpty(result.ValidRules);
            Assert.IsNotEmpty(result.Errors);
        }
    }
}
