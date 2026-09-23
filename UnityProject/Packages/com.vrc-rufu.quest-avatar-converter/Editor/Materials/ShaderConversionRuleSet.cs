using System;
using System.Collections.Generic;
using UnityEngine;
using VrcRufu.QuestAvatarConverter.Pipeline;

namespace VrcRufu.QuestAvatarConverter.Materials
{
    // T007: ShaderConversionRuleSet / PropertyMapping ScriptableObject schema, enforcing
    // contracts/extension-data-contracts.md §1 invariants verbatim (Constitution Principle III:
    // externalized, rule-based conversion data). Loading/validation logic lives in
    // ShaderConversionRuleLoader.cs (kept in a separate file so each file's name matches its main
    // type — see that file's header comment for why this matters for ScriptableObject assets).

    /// <summary>contracts/extension-data-contracts.md §1 — PropertyMapping.</summary>
    [Serializable]
    public struct PropertyMapping
    {
        /// <summary>REQUIRED. Must exist on the owning rule's SourceShader (validated at load
        /// time by <see cref="ShaderConversionRuleLoader"/>; unresolvable entries produce a
        /// warning, not a load failure).</summary>
        public string SourcePropertyName;

        /// <summary>REQUIRED. Must exist on the owning rule's TargetShader.</summary>
        public string TargetPropertyName;

        /// <summary>REQUIRED for texture properties (FR-008 routing).</summary>
        public TextureClassification TargetClassification;
    }

    /// <summary>
    /// One Source Shader → Target Shader conversion rule (contracts/extension-data-contracts.md
    /// §1), consumed by MaterialConverter/ShaderPropertyMapper (FR-006, FR-020). Adding support
    /// for a new shader pair means authoring one new asset of this type — no change to
    /// MaterialConverter/ShaderPropertyMapper/ConversionPipeline is permitted for that purpose
    /// (Constitution III).
    /// </summary>
    public sealed class ShaderConversionRuleSet : ScriptableObject
    {
        [SerializeField]
        [Tooltip("REQUIRED. Exact shader asset this rule applies to. Any PC-side shader may be a valid source.")]
        private Shader sourceShader;

        [SerializeField]
        [Tooltip("REQUIRED. Must be one of VRChat's own VRChat/Mobile/* shaders (research.md §4).")]
        private Shader targetShader;

        [SerializeField]
        private PropertyMapping[] propertyMappings = Array.Empty<PropertyMapping>();

        public Shader SourceShader => sourceShader;
        public Shader TargetShader => targetShader;
        public IReadOnlyList<PropertyMapping> PropertyMappings => propertyMappings;

        /// <summary>Test-only construction helper (T008). Production callers author these assets
        /// via the Inspector; EditMode tests use this instead of SerializedObject/AssetDatabase
        /// gymnastics to build fixtures. Internal + <c>InternalsVisibleTo</c> the Editor.Tests
        /// assembly (see AssemblyInfo.cs).</summary>
        internal static ShaderConversionRuleSet CreateForTests(Shader sourceShader, Shader targetShader, params PropertyMapping[] propertyMappings)
        {
            var instance = CreateInstance<ShaderConversionRuleSet>();
            instance.sourceShader = sourceShader;
            instance.targetShader = targetShader;
            instance.propertyMappings = propertyMappings ?? Array.Empty<PropertyMapping>();
            return instance;
        }
    }
}
