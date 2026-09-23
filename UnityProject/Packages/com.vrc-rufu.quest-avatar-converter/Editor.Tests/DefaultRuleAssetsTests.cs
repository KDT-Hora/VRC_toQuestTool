using NUnit.Framework;
using UnityEditor;
using VrcRufu.QuestAvatarConverter.Materials;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter.Tests
{
    // Regression coverage for the shipped default rule assets (T009/T012): loads each asset from
    // AssetDatabase and confirms its serialized data survived import intact, and that it passes
    // its own loader (ShaderConversionRuleLoader / QuestCompatibilityRulesLoader) with no errors.
    public class DefaultRuleAssetsTests
    {
        private const string StandardToToonLitPath =
            "Packages/com.vrc-rufu.quest-avatar-converter/Data/ShaderConversionRules/Standard_To_ToonLit.asset";

        private const string StandardToToonStandardPath =
            "Packages/com.vrc-rufu.quest-avatar-converter/Data/ShaderConversionRules/Standard_To_ToonStandard.asset";

        private const string DefaultQuestCompatibilityRulesPath =
            "Packages/com.vrc-rufu.quest-avatar-converter/Data/QuestCompatibilityRules/DefaultQuestCompatibilityRules.asset";

        [Test]
        public void StandardToToonLit_LoadsWithExpectedShadersAndMapping()
        {
            var rule = AssetDatabase.LoadAssetAtPath<ShaderConversionRuleSet>(StandardToToonLitPath);
            Assert.NotNull(rule, $"Expected asset at {StandardToToonLitPath}");
            Assert.AreEqual("Standard", rule.SourceShader.name);
            Assert.AreEqual("VRChat/Mobile/Toon Lit", rule.TargetShader.name);
            Assert.AreEqual(1, rule.PropertyMappings.Count);
            Assert.AreEqual("_MainTex", rule.PropertyMappings[0].SourcePropertyName);
            Assert.AreEqual("_MainTex", rule.PropertyMappings[0].TargetPropertyName);
            Assert.AreEqual(TextureClassification.Color, rule.PropertyMappings[0].TargetClassification);

            var result = ShaderConversionRuleLoader.Load(new[] { rule });
            Assert.AreEqual(1, result.ValidRules.Count);
            Assert.IsEmpty(result.Errors);
            Assert.IsEmpty(result.Warnings);
        }

        [Test]
        public void StandardToToonStandard_LoadsWithExpectedShadersAndMappings()
        {
            var rule = AssetDatabase.LoadAssetAtPath<ShaderConversionRuleSet>(StandardToToonStandardPath);
            Assert.NotNull(rule, $"Expected asset at {StandardToToonStandardPath}");
            Assert.AreEqual("Standard", rule.SourceShader.name);
            Assert.AreEqual("VRChat/Mobile/Toon Standard", rule.TargetShader.name);
            Assert.AreEqual(5, rule.PropertyMappings.Count);

            var result = ShaderConversionRuleLoader.Load(new[] { rule });
            Assert.AreEqual(1, result.ValidRules.Count);
            Assert.IsEmpty(result.Errors);
            Assert.IsEmpty(result.Warnings, string.Join("; ", result.Warnings));
        }

        [Test]
        public void TwoDefaultShaderRules_DoNotCollideAsADuplicatePair()
        {
            var toonLit = AssetDatabase.LoadAssetAtPath<ShaderConversionRuleSet>(StandardToToonLitPath);
            var toonStandard = AssetDatabase.LoadAssetAtPath<ShaderConversionRuleSet>(StandardToToonStandardPath);

            var result = ShaderConversionRuleLoader.Load(new[] { toonLit, toonStandard });

            Assert.AreEqual(2, result.ValidRules.Count);
            Assert.IsEmpty(result.Errors);
        }

        [Test]
        public void DefaultQuestCompatibilityRules_LoadsWithCitedPhysBoneThresholds()
        {
            var rules = AssetDatabase.LoadAssetAtPath<QuestCompatibilityRules>(DefaultQuestCompatibilityRulesPath);
            Assert.NotNull(rules, $"Expected asset at {DefaultQuestCompatibilityRulesPath}");

            var limits = rules.PhysBoneLimits;
            Assert.IsNotEmpty(limits.SourceCitation);
            Assert.AreEqual(256, limits.MaxAffectedTransformsPerComponentHardCap);

            Assert.AreEqual(0, limits.Excellent.MaxPhysBoneComponents);
            Assert.AreEqual(4, limits.Good.MaxPhysBoneComponents);
            Assert.AreEqual(6, limits.Medium.MaxPhysBoneComponents);
            Assert.AreEqual(8, limits.Poor.MaxPhysBoneComponents);

            Assert.AreEqual(0, limits.Excellent.MaxPhysBoneAffectedTransforms);
            Assert.AreEqual(16, limits.Good.MaxPhysBoneAffectedTransforms);
            Assert.AreEqual(32, limits.Medium.MaxPhysBoneAffectedTransforms);
            Assert.AreEqual(64, limits.Poor.MaxPhysBoneAffectedTransforms);

            Assert.AreEqual(0, limits.Excellent.MaxPhysBoneColliders);
            Assert.AreEqual(4, limits.Good.MaxPhysBoneColliders);
            Assert.AreEqual(8, limits.Medium.MaxPhysBoneColliders);
            Assert.AreEqual(16, limits.Poor.MaxPhysBoneColliders);

            Assert.AreEqual(0, limits.Excellent.MaxPhysBoneCollisionCheckCount);
            Assert.AreEqual(16, limits.Good.MaxPhysBoneCollisionCheckCount);
            Assert.AreEqual(32, limits.Medium.MaxPhysBoneCollisionCheckCount);
            Assert.AreEqual(64, limits.Poor.MaxPhysBoneCollisionCheckCount);

            var result = QuestCompatibilityRulesLoader.Load(new[] { rules });
            Assert.AreEqual(1, result.ValidRules.Count);
            Assert.IsEmpty(result.Errors);
        }
    }
}
