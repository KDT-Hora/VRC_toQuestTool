using System.Collections.Generic;
using UnityEngine;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    /// <summary>
    /// T040 (FR-016): scans the Quest avatar against every loaded
    /// <see cref="QuestCompatibilityRules"/>.FlaggedComponents entry and populates one
    /// <see cref="CompatibilityFinding"/> per flagged object — never aggregate-only, so the caller
    /// can list each flagged object individually.
    /// </summary>
    public static class QuestCompatibilityChecker
    {
        public static List<CompatibilityFinding> Check(GameObject questAvatarRoot, IEnumerable<QuestCompatibilityRules> loadedRules)
        {
            var findings = new List<CompatibilityFinding>();
            if (questAvatarRoot == null || loadedRules == null)
            {
                return findings;
            }

            foreach (var ruleAsset in loadedRules)
            {
                if (ruleAsset == null)
                {
                    continue;
                }

                foreach (var flagged in ruleAsset.FlaggedComponents)
                {
                    var type = ComponentTypeResolver.Resolve(flagged.ComponentTypeName);
                    if (type == null)
                    {
                        // Already reported as a load-time warning by QuestCompatibilityRulesLoader
                        // (T010/T011) — nothing further to do here.
                        continue;
                    }

                    foreach (var component in questAvatarRoot.GetComponentsInChildren(type, true))
                    {
                        findings.Add(new CompatibilityFinding(component, flagged.ReasonMessage, CompatibilityFinding.FindingSeverity.Warning));
                    }
                }
            }

            return findings;
        }
    }
}
