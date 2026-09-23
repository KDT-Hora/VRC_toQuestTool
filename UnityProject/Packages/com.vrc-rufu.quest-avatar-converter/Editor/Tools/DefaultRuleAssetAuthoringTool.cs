using System.IO;
using UnityEditor;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter.Materials
{
    /// <summary>
    /// One-off headless authoring utility for the shipped default rule assets (T009/T012). Not
    /// part of the runtime pipeline — invoked once via `unity run -executeMethod` to (re)generate
    /// Data/ assets through Unity's own ScriptableObject serializer, which is the reliable path
    /// versus hand-authoring asset YAML. Safe to re-run: it overwrites the existing assets at the
    /// same paths (preserving their GUIDs) rather than creating duplicates.
    /// </summary>
    internal static class DefaultRuleAssetAuthoringTool
    {
        private const string ShaderRulesDir = "Packages/com.vrc-rufu.quest-avatar-converter/Data/ShaderConversionRules";
        private const string CompatRulesDir = "Packages/com.vrc-rufu.quest-avatar-converter/Data/QuestCompatibilityRules";

        public static void Run()
        {
            Directory.CreateDirectory(ShaderRulesDir);
            Directory.CreateDirectory(CompatRulesDir);

            AuthorStandardToToonLit();
            AuthorStandardToToonStandard();
            AuthorDefaultQuestCompatibilityRules();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("DefaultRuleAssetAuthoringTool: done.");
        }

        private static void AuthorStandardToToonLit()
        {
            var path = ShaderRulesDir + "/Standard_To_ToonLit.asset";
            var rule = ShaderConversionRuleSet.CreateForTests(
                Shader.Find("Standard"),
                Shader.Find("VRChat/Mobile/Toon Lit"),
                new PropertyMapping
                {
                    SourcePropertyName = "_MainTex",
                    TargetPropertyName = "_MainTex",
                    TargetClassification = TextureClassification.Color,
                });
            CreateOrReplaceAsset(rule, path);
        }

        private static void AuthorStandardToToonStandard()
        {
            var path = ShaderRulesDir + "/Standard_To_ToonStandard.asset";
            var rule = ShaderConversionRuleSet.CreateForTests(
                Shader.Find("Standard"),
                Shader.Find("VRChat/Mobile/Toon Standard"),
                new PropertyMapping { SourcePropertyName = "_MainTex", TargetPropertyName = "_MainTex", TargetClassification = TextureClassification.Color },
                new PropertyMapping { SourcePropertyName = "_BumpMap", TargetPropertyName = "_BumpMap", TargetClassification = TextureClassification.Normal },
                new PropertyMapping { SourcePropertyName = "_MetallicGlossMap", TargetPropertyName = "_MetallicMap", TargetClassification = TextureClassification.Mask },
                new PropertyMapping { SourcePropertyName = "_OcclusionMap", TargetPropertyName = "_OcclusionMap", TargetClassification = TextureClassification.Mask },
                new PropertyMapping { SourcePropertyName = "_EmissionMap", TargetPropertyName = "_EmissionMap", TargetClassification = TextureClassification.Emission });
            CreateOrReplaceAsset(rule, path);
        }

        private static void AuthorDefaultQuestCompatibilityRules()
        {
            var path = CompatRulesDir + "/DefaultQuestCompatibilityRules.asset";
            var thresholds = new PhysBoneThresholds
            {
                Excellent = new PhysBoneTierThresholds { MaxPhysBoneComponents = 0, MaxPhysBoneColliders = 0, MaxPhysBoneAffectedTransforms = 0, MaxPhysBoneCollisionCheckCount = 0 },
                Good = new PhysBoneTierThresholds { MaxPhysBoneComponents = 4, MaxPhysBoneColliders = 4, MaxPhysBoneAffectedTransforms = 16, MaxPhysBoneCollisionCheckCount = 16 },
                Medium = new PhysBoneTierThresholds { MaxPhysBoneComponents = 6, MaxPhysBoneColliders = 8, MaxPhysBoneAffectedTransforms = 32, MaxPhysBoneCollisionCheckCount = 32 },
                Poor = new PhysBoneTierThresholds { MaxPhysBoneComponents = 8, MaxPhysBoneColliders = 16, MaxPhysBoneAffectedTransforms = 64, MaxPhysBoneCollisionCheckCount = 64 },
                MaxAffectedTransformsPerComponentHardCap = 256,
                SourceCitation = "VRChat Creation docs -- \"Performance Ranks\" page and \"PhysBones\" page " +
                                  "(Quest/mobile-specific thresholds); see research.md Phase 0 Research section 5, " +
                                  "verified against VRChat's published tables on 2026-09-19.",
            };
            var rules = QuestCompatibilityRules.CreateForTests(System.Array.Empty<FlaggedComponentRule>(), thresholds);
            CreateOrReplaceAsset(rules, path);
        }

        private static void CreateOrReplaceAsset(Object asset, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(asset, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(asset);
            }
            else
            {
                AssetDatabase.CreateAsset(asset, path);
            }
        }
    }
}
