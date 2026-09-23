using UnityEditor;

namespace VrcRufu.QuestAvatarConverter.Pipeline
{
    /// <summary>
    /// T015: detects prior Quest output at the target path (FR-022). Detection only — the actual
    /// "no silent overwrite" gate is structural: <see cref="ConversionPipeline"/> (T029) calls
    /// <see cref="HasExistingOutput"/> before <see cref="AvatarDuplicator"/> and only proceeds
    /// past it when the caller-supplied confirmation callback explicitly returns true, so there is
    /// no code path that reaches AvatarDuplicator with pre-existing output present except through
    /// an explicit user-approved confirmation. The confirmation UI itself (an `EditorUtility`
    /// dialog) is wired in later, at T032 (Phase 3/US1) — Phase 2 has no end-user-facing UI yet.
    /// </summary>
    public static class ExistingOutputDetector
    {
        /// <summary>True if a previous generation run already produced output at
        /// <paramref name="outputRoot"/> (`Assets/&lt;QuestConvertedRoot&gt;/&lt;AvatarName&gt;/`).</summary>
        public static bool HasExistingOutput(string outputRoot)
        {
            return AssetDatabase.IsValidFolder(outputRoot);
        }
    }
}
