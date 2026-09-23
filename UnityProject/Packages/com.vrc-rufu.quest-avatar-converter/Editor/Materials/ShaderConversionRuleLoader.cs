using System;
using System.Collections.Generic;

namespace VrcRufu.QuestAvatarConverter.Materials
{
    // T007 (continued): loading/validation logic for ShaderConversionRuleSet (see that file for
    // the schema types themselves).

    /// <summary>The outcome of loading a set of <see cref="ShaderConversionRuleSet"/> assets:
    /// which ones passed every contract invariant, plus every load-time error/warning produced
    /// along the way (contracts §1: "not silently ignored").</summary>
    public sealed class ShaderConversionRuleLoadResult
    {
        public IReadOnlyList<ShaderConversionRuleSet> ValidRules { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }

        public ShaderConversionRuleLoadResult(
            IReadOnlyList<ShaderConversionRuleSet> validRules,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings)
        {
            ValidRules = validRules ?? Array.Empty<ShaderConversionRuleSet>();
            Errors = errors ?? Array.Empty<string>();
            Warnings = warnings ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Loads and validates a set of <see cref="ShaderConversionRuleSet"/> assets against
    /// contracts/extension-data-contracts.md §1's invariants. Pure logic over the passed-in
    /// candidates (Constitution Principle VI) — does not itself query AssetDatabase for which
    /// assets exist; callers (e.g. ConversionPipeline) pass in the assets to validate.
    /// </summary>
    public static class ShaderConversionRuleLoader
    {
        /// <summary>VRChat hard-restricts avatar shaders on Quest/Android to its own bundled
        /// family (research.md §4) — no third-party shader, however "Quest-compatible" branded,
        /// may be registered as a TargetShader.</summary>
        public const string AllowedTargetShaderPrefix = "VRChat/Mobile/";

        public static ShaderConversionRuleLoadResult Load(IEnumerable<ShaderConversionRuleSet> candidates)
        {
            var valid = new List<ShaderConversionRuleSet>();
            var errors = new List<string>();
            var warnings = new List<string>();

            // Rules whose SourceShader+TargetShader pair collides with another candidate: MUST
            // raise a load-time error "rather than silently picking one" (contracts §1) — so
            // BOTH/ALL colliding rules are excluded from ValidRules, not just the second seen.
            // Any Material whose shader is left unmapped as a result is then reported as an
            // explicit conversion failure by MaterialConverter (T018/FR-006), which is the
            // correct, non-silent fallback.
            var pairOwners = new Dictionary<(UnityEngine.Shader Source, UnityEngine.Shader Target), List<ShaderConversionRuleSet>>();

            foreach (var rule in candidates ?? Array.Empty<ShaderConversionRuleSet>())
            {
                if (rule == null)
                {
                    continue;
                }

                var ruleLabel = string.IsNullOrEmpty(rule.name) ? "(unnamed ShaderConversionRuleSet)" : rule.name;
                var rejected = false;

                if (rule.SourceShader == null)
                {
                    errors.Add($"{ruleLabel}: SourceShader is not set — rule rejected.");
                    rejected = true;
                }

                if (rule.TargetShader == null || !rule.TargetShader.name.StartsWith(AllowedTargetShaderPrefix, StringComparison.Ordinal))
                {
                    var targetName = rule.TargetShader != null ? rule.TargetShader.name : "(none)";
                    errors.Add(
                        $"{ruleLabel}: TargetShader '{targetName}' is not one of VRChat's VRChat/Mobile/* " +
                        "shaders — rule rejected (contracts/extension-data-contracts.md §1).");
                    rejected = true;
                }

                if (rejected)
                {
                    continue;
                }

                var pairKey = (rule.SourceShader, rule.TargetShader);
                if (!pairOwners.TryGetValue(pairKey, out var owners))
                {
                    owners = new List<ShaderConversionRuleSet>();
                    pairOwners[pairKey] = owners;
                }
                owners.Add(rule);
            }

            foreach (var entry in pairOwners)
            {
                var (source, target) = entry.Key;
                var owners = entry.Value;

                if (owners.Count > 1)
                {
                    var ownerNames = string.Join(", ", owners.ConvertAll(r => r.name));
                    errors.Add(
                        $"Duplicate SourceShader+TargetShader pair ('{source.name}' -> '{target.name}') " +
                        $"registered by multiple rule assets: {ownerNames} — all rejected " +
                        "(contracts/extension-data-contracts.md §1).");
                    continue;
                }

                var rule = owners[0];
                foreach (var mapping in rule.PropertyMappings)
                {
                    if (!ShaderHasProperty(rule.SourceShader, mapping.SourcePropertyName))
                    {
                        warnings.Add(
                            $"{rule.name}: SourcePropertyName '{mapping.SourcePropertyName}' was not found " +
                            $"on SourceShader '{rule.SourceShader.name}' (contracts/extension-data-contracts.md §1).");
                    }
                }

                valid.Add(rule);
            }

            return new ShaderConversionRuleLoadResult(valid, errors, warnings);
        }

        private static bool ShaderHasProperty(UnityEngine.Shader shader, string propertyName)
        {
            var count = shader.GetPropertyCount();
            for (var i = 0; i < count; i++)
            {
                if (shader.GetPropertyName(i) == propertyName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
